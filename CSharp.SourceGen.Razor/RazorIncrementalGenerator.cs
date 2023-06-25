using System.Collections.Immutable;
using CSharp.SourceGen.Razor.RazorAssembly;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


namespace CSharp.SourceGen.Razor;


[Generator(LanguageNames.CSharp)]
public class RazorIncrementalGenerator : IIncrementalGenerator
{
    private readonly RazorEngine _razorEngine;


    public RazorIncrementalGenerator()
    {
        this._razorEngine = new RazorEngine();
        this._compiler = new CSharpCompiler();
        this._assemblyRunner = new AssemblyRunner();
    }


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = RazorTemplatesProvider().Combine(ProjectReferencesProvider());
        context.RegisterImplementationSourceOutput(provider, this.GenerateSources);
        return;

        // Non-empty additional files with '.razor' extension except those that start
        // with an underscore '_'
        IncrementalValueProvider<ImmutableArray<RazorTemplateSyntaxTree>>
            RazorTemplatesProvider()
        {
            return context.AdditionalTextsProvider
                .Select(static (file, token) =>
                {
                    var source = file.GetText(token)?.ToString() ?? string.Empty;
                    return new AdditionalFile(file.Path, source);
                })
                .Where(static file => file.IsNotEmpty
                    && file.FilePath.EndsWith(".razor",
                        StringComparison.InvariantCultureIgnoreCase)
                    && file.FileName[0] != '_'
                    && file.IsNotEmpty)
                .Select((file, _) =>
                {
                    var randomStr = Guid.NewGuid().ToString("N").Substring(0, 8);
                    var className = "Template_" + randomStr;
                    var source = this._razorEngine.GenerateClassForTemplate(className, file.Text);
                    var syntaxTree = CSharpSyntaxTree.ParseText(source);
                    return new RazorTemplateSyntaxTree(file.FilePath, className, syntaxTree);
                })
                .Collect();
        }


        // Provides project's references if the the project targets netstandard2.0
        IncrementalValueProvider<ImmutableArray<PortableExecutableReference>>
            ProjectReferencesProvider()
        {
            return IsTargetingNetstandardProvider()
                .Combine(context.CompilationProvider)
                .Select((arg, _) =>
                {
                    var (isNetstandard, compilation) = arg;
                    var references = isNetstandard
                        ? compilation.References
                            .OfType<PortableExecutableReference>()
                            .Where(reference => File.Exists(reference.FilePath))
                            .ToImmutableArray()
                        : ImmutableArray<PortableExecutableReference>.Empty;

                    return references;
                });
        }


        IncrementalValueProvider<bool> IsTargetingNetstandardProvider()
        {
            return context.ParseOptionsProvider.Select((options, _) =>
                options.PreprocessorSymbolNames.Contains("NETSTANDARD2_0"));
        }
    }


    private void GenerateSources(SourceProductionContext context,
        (ImmutableArray<RazorTemplateSyntaxTree>, ImmutableArray<PortableExecutableReference>) arg)
    {
        var (templates, projectReferences) = arg;

        try
        {
            var syntaxTrees = templates.Select(x => x.SyntaxTree);

            // ReSharper disable once PossibleMultipleEnumeration
            var assemblyBytes = this._compiler.EmitAssemblyNetstandard2_0(syntaxTrees,
                projectReferences, context.ReportDiagnostic);

            if (assemblyBytes == null)
            {
                return;
            }

            using var assemblyFile = new DisposableFile(Path.GetTempFileName());
            File.WriteAllBytes(assemblyFile.Path, assemblyBytes);

            // ReSharper disable once PossibleMultipleEnumeration
            var referencePaths = projectReferences
                .Select(reference => reference.FilePath)
                .ToImmutableArray();

            var output = this._assemblyRunner.Run(assemblyFile.Path, referencePaths!);
            var renderedTextByClassName =
                output.Items.ToDictionary(x => x.ClassName, x => x.RenderedText);

            foreach (var template in templates)
            {
                if (!renderedTextByClassName.TryGetValue(template.ClassName, out var renderedText))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Error, Location.None,
                        $"'{template.ClassName}' was not rendered"));
                    continue;
                }

                context.AddSource(template.SuggestedGeneratedFileName(), renderedText);
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


    private readonly CSharpCompiler _compiler;
    private readonly AssemblyRunner _assemblyRunner;
    private const string DiagnosticIdPrefix = "SourceGen.Razor";
    private const string DiagnosticCategory = "CSharp.SourceGen.Razor";
}


public record RazorTemplateSyntaxTree(string FileName, string ClassName, SyntaxTree SyntaxTree)
{
    public string SuggestedGeneratedFileName() =>
        $"{Path.GetFileNameWithoutExtension(this.FileName)}.razor.cs_{(uint)this.FileName.GetHashCode()}";
}