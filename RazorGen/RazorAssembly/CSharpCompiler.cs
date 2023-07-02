using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


namespace RazorGen.RazorAssembly;


public class CSharpCompiler
{
    public CSharpCompiler()
    {
        this._netstandardReferences = new NetstandardReferences();
        this._usingsTree = CSharpSyntaxTree.ParseText(Usings);
        this._templateBaseTree = CSharpSyntaxTree.ParseText(TemplateBase);
    }


    public byte[]? EmitAssemblyNetstandard2_0(IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> references, Action<Diagnostic> reportDiagnostic)
    {
        return EmitAssembly(
            syntaxTrees.Append(this._usingsTree).Append(this._templateBaseTree),
            this._netstandardReferences.References.Concat(references),
            reportDiagnostic);
    }


    private static byte[]? EmitAssembly(IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> references, Action<Diagnostic> reportDiagnostic)
    {
        var fileName = Path.GetRandomFileName();
        var compilation = CSharpCompilation.Create(
            fileName,
            syntaxTrees,
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


    private readonly NetstandardReferences _netstandardReferences;
    private readonly SyntaxTree _usingsTree;
    private readonly SyntaxTree _templateBaseTree;


    const string Usings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Reflection;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;


    private const string TemplateBase = """
using System.Text;


public abstract class __TemplateBase
{
    private readonly StringBuilder _stringBuilder = new();
    private string? _attributeSuffix;

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


    public abstract Task ExecuteAsync();
}
""";
}