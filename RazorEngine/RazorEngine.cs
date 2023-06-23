using Microsoft.AspNetCore.Razor.Language;
using RazorEngine.Templates;


namespace RazorEngine;


public class RazorEngine
{
    public RazorEngine()
    {
        this._engine = RazorProjectEngine.Create(
            RazorConfiguration.Default,
            RazorProjectFileSystem.Create(Environment.CurrentDirectory),
            builder =>
            {
                builder.SetNamespace("Templates");
                builder.SetBaseType(nameof(TemplateBase));
                builder.ConfigureClass((document, node) =>
                {
                    var className = document.Source.FilePath;
                    node.ClassName = className;
                });
            });
    }


    public string GenerateClassForTemplate(string className, string template)
    {
        var document = RazorSourceDocument.Create(template, fileName: className);

        var codeDocument = this._engine.Process(
            document,
            null,
            Array.Empty<RazorSourceDocument>(),
            Array.Empty<TagHelperDescriptor>());

        var razorCSharpDocument = codeDocument.GetCSharpDocument();
        return razorCSharpDocument.GeneratedCode;
    }


    private readonly RazorProjectEngine _engine;
}