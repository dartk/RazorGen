using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RazorGen.RazorAssembly;


namespace RazorGen;


[Generator(LanguageNames.CSharp)]
public class RazorGen : IIncrementalGenerator
{
    private readonly RazorEngine _razorEngine;


    public RazorGen()
    {
        this._razorEngine = new RazorEngine();
        this._compiler = new CSharpCompiler();
        this._assemblyRunner = new AssemblyRunner();
    }


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var formatCodeProvider = context.AnalyzerConfigOptionsProvider.Select(
            (optionsProvider, _) => optionsProvider.GlobalOptions
                    .TryGetValue("build_property.RazorGen_FormatCode", out var value)
                && value.Equals("true", StringComparison.InvariantCultureIgnoreCase));

        var provider = RazorTemplatesProvider().Combine(ProjectReferencesProvider());
        context.RegisterSourceOutput(provider.Combine(formatCodeProvider), this.GenerateSources);
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
                    var source =
                        this._razorEngine.GenerateClassForTemplate(file.FilePath, file.Text);
                    var syntaxTree = CSharpSyntaxTree.ParseText(source, path: file.FilePath);
                    return new RazorTemplateSyntaxTree(file.FilePath, syntaxTree);
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
        ((ImmutableArray<RazorTemplateSyntaxTree>, ImmutableArray<PortableExecutableReference>),
            bool FormatCode) arg)
    {
        var ((templates, projectReferences), formatCode) = arg;

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

            var result =
                this._assemblyRunner.Run(assemblyFile.Path, referencePaths!, out var output);

            switch (result)
            {
                case AssemblyRunResult.RuntimeMissing:
                    context.ReportDiagnostic(Diagnostic.Create(RuntimeIsMissingError, Location.None,
                        DiagnosticSeverity.Error));
                    return;

                case AssemblyRunResult.Success:
                    var renderedTextByClassName =
                        output.Items.ToDictionary(x => x.ClassName, x => x.RenderedText);

                    var reservedFileNames = new HashSet<string>();

                    string GetUniqueFileName(string path)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(path);
                        for (var i = 1; reservedFileNames.Contains(fileName); ++i)
                        {
                            fileName += i;
                        }

                        reservedFileNames.Add(fileName);

                        return fileName;
                    }

                    foreach (var template in templates)
                    {
                        if (!renderedTextByClassName.TryGetValue(template.ClassName,
                            out var renderedText))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(UnexpectedError,
                                Location.None,
                                $"'{template.FileName}' was not rendered"));
                            continue;
                        }

                        renderedText = renderedText
                            .Replace("<code>", "")
                            .Replace("</code>", "");

                        if (formatCode)
                        {
                            renderedText = CSharpSyntaxTree.ParseText(renderedText)
                                .GetRoot()
                                .NormalizeWhitespace()
                                .ToString();
                        }

                        var outputFileName = GetUniqueFileName(template.FileName) + ".razor.cs";
                        context.AddSource(outputFileName, renderedText);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(UnexpectedError, Location.None,
                ex.Message));
        }
    }


    private static readonly DiagnosticDescriptor RuntimeIsMissingError = new(
        id: $"{DiagnosticIdPrefix}01",
        title: ".NET 7 runtime is missing",
        messageFormat: "RazorGen requires .NET 7 runtime installed.",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private static readonly DiagnosticDescriptor UnexpectedError = new(
        id: $"{DiagnosticIdPrefix}02",
        title: "RazorGen unexpected error",
        messageFormat: "RazorGen unexpected error. {0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private readonly CSharpCompiler _compiler;
    private readonly AssemblyRunner _assemblyRunner;
    private const string DiagnosticIdPrefix = "RAZORGEN";
    private const string DiagnosticCategory = "RazorGen";
}


public record RazorTemplateSyntaxTree(string FileName, SyntaxTree SyntaxTree)
{
    public string ClassName => PathUtil.GetFullClassNameFromFilePath(this.FileName);
}