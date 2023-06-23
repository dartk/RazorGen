using System.Collections.Immutable;
using System.Reflection;
using CSharp.SourceGen.Razor.RazorEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using Xunit.Abstractions;


namespace RazorEngine.Tests;


[UsesVerify]
public class UnitTests : VerifyBase
{
    public UnitTests(ITestOutputHelper output) : base()
    {
        this._output = output;
    }


    [Fact]
    public void CSharpCompilerTest()
    {
        var compiler = new CSharpCompiler();

        var tree = CSharpSyntaxTree.ParseText("""
            public class HelloWorld {
                public string SayIt() => "Hello, World!";
            }
            """);

        var assemblyBytes = compiler.EmitAssemblyNetstandard2_0(
            new[] { tree },
            Array.Empty<MetadataReference>(),
            diagnostic => this._output.WriteLine(diagnostic.ToString()))!;

        var assembly = Assembly.Load(assemblyBytes);
        var type = assembly.GetType("HelloWorld")!;
        dynamic helloWorld = Activator.CreateInstance(type)!;
        Assert.Equal("Hello, World!", helloWorld.SayIt());
    }


    [Fact]
    public Task RazorProjectEngineExTest()
    {
        var engine = new RazorEngine("TemplateBase");
        var source = engine.GenerateClassForTemplate("MyTemplate", """
public static class GeneratedClass {
    @for (var i = 0; i < 10; ++i) {
        @:public const int Field@(i) = @(i);
    }
}
""");

        return this.Verify(source);
    }


    [Fact]
    public async Task Foo()
    {
        var templateClassName = "MyTemplate";
        var templateSource = """
public static class GeneratedClass {
    @for (var i = 0; i < 10; ++i) {
        @:public const int Field@(i) = @(i);
    }
}
""";

        var engine = new RazorEngine("TemplateBase");
        var compiler = new CSharpCompiler();

        var usingsSyntaxTree = CSharpSyntaxTree.ParseText("""
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Reflection;
global using System.Threading;
global using System.Threading.Tasks;
global using RazorEngine.Templates;
""");

        var reference = MetadataReference.CreateFromFile(typeof(TemplateBase).Assembly.Location);

        var generatedClass = engine.GenerateClassForTemplate(templateClassName, templateSource);
        var syntaxTree = CSharpSyntaxTree.ParseText(generatedClass);
        var trees = new[] { syntaxTree, usingsSyntaxTree };
        var references = new[] { reference };
        var runner = new AssemblyRunner();

        var assemblyBytes = compiler.EmitAssemblyNetstandard2_0(trees, references,
            diagnostic => this._output.WriteLine(diagnostic.ToString()));

        var file = new DisposableFile("output.dll");
        File.WriteAllBytes(file.Path, assemblyBytes!);

        var output = runner.Run(file.Path, ImmutableArray<string>.Empty);
        this._output.WriteLine(JsonConvert.SerializeObject(output, Formatting.Indented));
    }


    private readonly ITestOutputHelper _output;
}