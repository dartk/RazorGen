using Microsoft.AspNetCore.Razor.Language;


namespace RazorGen.RazorAssembly;


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
                builder.SetBaseType("TemplateBase");
                builder.ConfigureClass((document, node) =>
                {
                    var className = ClassNameFromFilePath.GetClassName(document.Source.FilePath);
                    node.ClassName = className;
                });
            });
    }


    public string GenerateClassForTemplate(string fileName, string template)
    {
        var document = RazorSourceDocument.Create(template, fileName);

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