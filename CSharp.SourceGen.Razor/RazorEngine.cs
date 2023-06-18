using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


namespace CSharp.SourceGen.Razor;


public record RazorTemplateSource(string FileName, string TypeFullName, string GeneratedSource)
{
    public RazorTemplateSyntaxTree ToSyntaxTree() =>
        new(this.FileName, this.TypeFullName, CSharpSyntaxTree.ParseText(this.GeneratedSource));
}


public record RazorTemplateSyntaxTree(string FileName, string TypeFullName, SyntaxTree SyntaxTree);


public class RazorEngine
{
    private const string Namespace = "Templates";


    public RazorEngine()
    {
        this._typeNameByPath = new Dictionary<string, string>();
        this._engine = RazorProjectEngine.Create(
            RazorConfiguration.Default,
            RazorProjectFileSystem.Create(Environment.CurrentDirectory),
            builder =>
            {
                builder.SetNamespace(Namespace);
                builder.SetBaseType("TemplateBase");
                builder.ConfigureClass((document, node) =>
                {
                    var className = "Template_" + Guid.NewGuid().ToString("N");
                    this._typeNameByPath[document.Source.FilePath] = className;
                    node.ClassName = className;
                });
            });
    }


    public RazorTemplateSource GenerateRazorTemplateSource(AdditionalFile file)
    {
        var document = RazorSourceDocument.Create(file.Text, file.FilePath);

        var codeDocument = this._engine.Process(
            document,
            null,
            Array.Empty<RazorSourceDocument>(),
            Array.Empty<TagHelperDescriptor>());

        var razorCSharpDocument = codeDocument.GetCSharpDocument();

        var className = this._typeNameByPath[document.FilePath];
        return new RazorTemplateSource(document.FilePath, $"{Namespace}.{className}",
            razorCSharpDocument.GeneratedCode);
    }


    public static SyntaxTree GetSyntaxTree(RazorCSharpDocument document)
    {
        return CSharpSyntaxTree.ParseText(document.GeneratedCode);
    }


    public static byte[]? EmitAssembly(IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> references, Action<Diagnostic> reportDiagnostic)
    {
        var templateBaseTree = CSharpSyntaxTree.ParseText(TemplateBaseSource);
        var globalUsingsTree = CSharpSyntaxTree.ParseText(GlobalUsings);

        var fileName = Path.GetRandomFileName();
        var compilation = CSharpCompilation.Create(
            fileName,
            syntaxTrees.Append(templateBaseTree).Append(globalUsingsTree),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var memoryStream = new MemoryStream();
        var emitResult = compilation.Emit(memoryStream);

        if (!emitResult.Success)
        {
            foreach (var diagnostic in emitResult.Diagnostics)
            {
                reportDiagnostic(diagnostic);
            }

            return null;
        }

        return memoryStream.ToArray();
    }


    private const string GlobalUsings = """
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
""";


    private const string TemplateBaseSource = """
using System.Text;


public abstract class TemplateBase
{
    private readonly StringBuilder _stringBuilder = new StringBuilder();
    private string? _attributeSuffix = null;

    public dynamic? Model { get; set; }


    public void WriteLiteral(string? literal = null)
    {
        this._stringBuilder.Append(literal);
    }


    public void Write(object? obj = null)
    {
        this._stringBuilder.Append(obj);
    }


    public void BeginWriteAttribute(string name, string prefix, int prefixOffset, string suffix,
        int suffixOffset, int attributeValuesCount)
    {
        this._attributeSuffix = suffix;
        this._stringBuilder.Append(prefix);
    }


    public void WriteAttributeValue(string prefix, int prefixOffset, object value, int valueOffset,
        int valueLength,
        bool isLiteral)
    {
        this._stringBuilder.Append(prefix);
        this._stringBuilder.Append(value);
    }


    public void EndWriteAttribute()
    {
        this._stringBuilder.Append(this._attributeSuffix);
        this._attributeSuffix = null;
    }


    public void Execute()
    {
        this.ExecuteAsync().GetAwaiter().GetResult();
    }


    public string Result()
    {
        this.Execute();
        return this._stringBuilder.ToString();
    }

    public abstract global::System.Threading.Tasks.Task ExecuteAsync();
}
""";


    private readonly RazorProjectEngine _engine;
    private readonly Dictionary<string, string> _typeNameByPath;
}