using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;


namespace CSharp.SourceGen.Razor;


[Generator(LanguageNames.CSharp)]
public class RazorIncrementalGenerator : IIncrementalGenerator
{
    private readonly RazorEngine _razorEngine;


    public RazorIncrementalGenerator()
    {
        this._razorEngine = new RazorEngine();

        var netstandardAssembly = Assembly.Load(
            "netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var mscorlibAssembly = typeof(object).Assembly;

        this._netstandardAssemblies = netstandardAssembly.GetReferencedAssemblies()
            .Select(Assembly.Load)
            .Append(netstandardAssembly)
            .Append(mscorlibAssembly)
            .ToImmutableArray();

        this._netstandardAssemblyReferences =
            this._netstandardAssemblies.Select(GetReferenceFromAssembly).ToImmutableArray();
    }


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var referencesProvider = context.CompilationProvider
            .SelectMany((compilation, _) => compilation.References.ToImmutableArray())
            .Select((reference, _) =>
            {
                if (reference.Display == null) return null;

                try
                {
                    var assembly = Assembly.LoadFile(reference.Display);
                    var fullName = assembly.FullName;
                    return new ReferenceInfo(fullName, reference);
                }
                catch
                {
                    return null;
                }
            })
            .Where(x => x != null)
            .Collect();

        var additionalFiles = context.AdditionalTextsProvider
            .Select(static (file, token) =>
            {
                var source = file.GetText(token)?.ToString() ?? string.Empty;
                return new AdditionalFile(file.Path, source);
            })
            .Where(static x => x.IsNotEmpty);

        var razorTemplates = additionalFiles
            .Where(file =>
                file.FilePath.EndsWith(".razor", StringComparison.InvariantCultureIgnoreCase)
                && file.FileName[0] != '_'
                && file.IsNotEmpty)
            .Select((file, _) => this._razorEngine.GenerateRazorTemplateSource(file).ToSyntaxTree())
            .Collect();

        var provider = razorTemplates.Combine(referencesProvider);

        context.RegisterSourceOutput(provider, this.GenerateSources!);
    }


    private void GenerateSources(SourceProductionContext context,
        (ImmutableArray<RazorTemplateSyntaxTree>, ImmutableArray<ReferenceInfo>) arg)
    {
        var (templates, projectReferences) = arg;
        var referenceByName =
            projectReferences.ToDictionary(x => x.AssemblyName, x => x.Reference);

        Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            if (referenceByName.TryGetValue(args.Name, out var reference))
            {
                var assembly = Assembly.LoadFrom(reference.Display!);
                return assembly;
            }
            else
            {
                return null;
            }
        }

        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

        try
        {
            var references = this._netstandardAssemblyReferences.Concat(
                projectReferences.Select(x => x.Reference));

            var assemblyBytes = RazorEngine.EmitAssembly(templates.Select(x => x.SyntaxTree),
                references!, context.ReportDiagnostic);
            if (assemblyBytes == null)
            {
                return;
            }

            var assembly = Assembly.Load(assemblyBytes)
                ?? throw new NullReferenceException("Assembly is not loaded");

            var resultMethod = GetResultMethod(assembly)
                ?? throw new NullReferenceException("Result method not found");

            foreach (var template in templates)
            {
                var type = assembly.GetType(template.TypeFullName);
                var obj = Activator.CreateInstance(type);
                Environment.CurrentDirectory = Path.GetDirectoryName(template.FileName);
                var renderedText = (string)resultMethod.Invoke(obj, Array.Empty<object>());
                context.AddSource(template.TypeFullName, renderedText);
            }


            static MethodInfo? GetResultMethod(Assembly assembly)
            {
                const string typeName = "TemplateBase";
                var type = assembly.GetType(typeName)
                    ?? throw new NullReferenceException($"Type '{typeName}' was not found");
                return type.GetMethod("Result")!;
            }
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(Error, Location.None, ex.ToString()));
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
        }
    }


    private static readonly DiagnosticDescriptor Error = new(
        id: $"{DiagnosticIdPrefix}001",
        title: "Razor Source Generator Error",
        messageFormat: "{0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private readonly ImmutableArray<Assembly> _netstandardAssemblies;
    private readonly ImmutableArray<MetadataReference> _netstandardAssemblyReferences;


    private const string DiagnosticIdPrefix = "SourceGen.Razor";
    private const string DiagnosticCategory = "CSharp.SourceGen.Razor";


    private static MetadataReference GetReferenceFromAssembly(Assembly assembly) =>
        MetadataReference.CreateFromFile(assembly.Location);


    private record ReferenceInfo(string AssemblyName, MetadataReference Reference);
}