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
    }


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var referencesProvider = context.CompilationProvider.Select((compilation, _) =>
            compilation.References.ToImmutableArray());

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

        context.RegisterSourceOutput(provider, GenerateSources);
    }


    public static void GenerateSources(SourceProductionContext context,
        (ImmutableArray<RazorTemplateSyntaxTree>, ImmutableArray<MetadataReference>) arg)
    {
        try
        {
            var (templates, references) = arg;

            var assemblyBytes = RazorEngine.EmitAssembly(templates.Select(x => x.SyntaxTree),
                references, context.ReportDiagnostic);

            var assembly = Assembly.Load(assemblyBytes);
            var resultMethod = GetResultMethod(assembly)
                ?? throw new ArgumentException("Result method not found");

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
                var type = assembly.GetType("TemplateBase");
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


    private const string DiagnosticIdPrefix = "SourceGen.Razor";
    private const string DiagnosticCategory = "CSharp.SourceGen.Razor";
}