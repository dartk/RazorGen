using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


namespace RazorEngine;


public class CSharpCompiler
{
    public CSharpCompiler()
    {
        this._netstandardReferences = new NetstandardReferences();
        this._usingsTree = CSharpSyntaxTree.ParseText(Usings);
    }


    public byte[]? EmitAssemblyNetstandard2_0(IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> references, Action<Diagnostic> reportDiagnostic)
    {
        return EmitAssembly(syntaxTrees.Append(this._usingsTree),
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


    const string Usings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Reflection;
        global using System.Threading;
        global using System.Threading.Tasks;
        global using RazorEngine.Templates;
        """;
}