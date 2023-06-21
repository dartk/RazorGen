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
        this._netstandardReferences = GetNetstandardReferences();
        this._razorEngine = new RazorEngine();
    }


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = RazorTemplatesProvider().Combine(ProjectReferencesProvider());
        context.RegisterSourceOutput(provider, this.GenerateSources);
        return;


        // If the the project targets netstandard2.0, then loads and provides project's references
        IncrementalValueProvider<ImmutableArray<MetadataReference>> ProjectReferencesProvider()
        {
            return IsNetstandardProvider()
                .Combine(context.CompilationProvider)
                .Select((arg, _) =>
                {
                    var (isNetstandard, compilation) = arg;
                    var references = isNetstandard
                        ? compilation.References.ToImmutableArray()
                        : ImmutableArray<MetadataReference>.Empty;

                    foreach (var reference in references)
                    {
                        if (reference is not PortableExecutableReference { FilePath: { } filePath })
                        {
                            continue;
                        }

                        try
                        {
                            Assembly.LoadFrom(filePath);
                        }
                        // ReSharper disable once EmptyGeneralCatchClause
                        catch
                        {
                        }
                    }

                    return references;
                });


            IncrementalValueProvider<bool> IsNetstandardProvider()
            {
                return context.ParseOptionsProvider.Select((options, _) =>
                    options.PreprocessorSymbolNames.Contains("NETSTANDARD2_0"));
            }
        }

        IncrementalValueProvider<ImmutableArray<RazorTemplateSyntaxTree>> RazorTemplatesProvider()
        {
            return context.AdditionalTextsProvider
                .Select(static (file, token) =>
                {
                    var source = file.GetText(token)?.ToString() ?? string.Empty;
                    return new AdditionalFile(file.Path, source);
                })
                .Where(static file => file.IsNotEmpty
                    && file.FilePath.EndsWith(".razor", StringComparison.InvariantCultureIgnoreCase)
                    && file.FileName[0] != '_'
                    && file.IsNotEmpty)
                .Select((file, _) =>
                    this._razorEngine.GenerateRazorTemplateSource(file).ToSyntaxTree())
                .Collect();
        }
    }


    private void GenerateSources(SourceProductionContext context,
        (ImmutableArray<RazorTemplateSyntaxTree>, ImmutableArray<MetadataReference>) arg)
    {
        var (templates, projectReferences) = arg;
        var references = projectReferences.Concat(this._netstandardReferences);

        try
        {
            var assemblyBytes = RazorEngine.EmitAssembly(templates.Select(x => x.SyntaxTree),
                references, context.ReportDiagnostic);
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
                context.AddSource(template.SuggestedGeneratedFileName(), renderedText);
            }


            static MethodInfo GetResultMethod(Assembly assembly)
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
    }


    private static readonly DiagnosticDescriptor Error = new(
        id: $"{DiagnosticIdPrefix}001",
        title: "Razor Source Generator Error",
        messageFormat: "{0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private readonly ImmutableArray<MetadataReference> _netstandardReferences;


    private const string DiagnosticIdPrefix = "SourceGen.Razor";
    private const string DiagnosticCategory = "CSharp.SourceGen.Razor";


    // The source generator compiles a library with classes generated by razor and invokes each class.
    // Since the source generator targets netstandard2.0, the compiled library needs to reference
    // netstandard2.0 assemblies. In order to reliably reference netstandard2.0 assemblies, they are
    // included as embedded resources to the source generator assembly.
    private static ImmutableArray<MetadataReference> GetNetstandardReferences()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var builder = ImmutableArray.CreateBuilder<MetadataReference>(resourceNames.Length);
        foreach (var name in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var reference = MetadataReference.CreateFromStream(stream);
            builder.Add(reference);
        }

        return builder.MoveToImmutable();
    }
}