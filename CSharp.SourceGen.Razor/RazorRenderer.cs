using System.Collections.Immutable;
using Microsoft.CodeAnalysis;


namespace CSharp.SourceGen.Razor;


public class RazorRenderer
{
    public RazorRenderer()
    {
    }


    public static string Render(IEnumerable<string> templateFiles,
        IEnumerable<MetadataReference> references, Action<Diagnostic> reportDiagnostic,
        CancellationToken token)
    {
        try
        {
            // RazorEngine.Process(templateFiles, references, reportDiagnostic);
            // var razorEngine = new RazorEngine();
            // var initialTemplate = razorEngine.Compile("Hello @Model.Name");
            // // return initialTemplate.Run();
            // initialTemplate.SaveToFile("testTemplate.dll");
            //
            // // var loadedTemplate = RazorEngineCompiledTemplate.LoadFromFile("testTemplate.dll");
            // // var result = loadedTemplate.Run(new { Name = "World" });
            // // File.AppendAllText(@"C:\Users\user\Desktop\tmp\out.txt", Environment.NewLine + Environment.NewLine + result);
            //
            // var bytes = File.ReadAllBytes("testTemplate.dll");
            // var assembly = Assembly.Load(bytes);
            // foreach (var type in assembly.GetTypes())
            // {
            //     Log(type.ToString());
            // }
            //
            // {
            //     var type = assembly.GetType("TemplateNamespace.Template");
            //     if (type == null) throw new TypeLoadException();
            //
            //
            //     var instance = (IRazorEngineTemplate)Activator.CreateInstance(type);
            //     instance.Model = new { Name = "World!" };
            //     instance.Execute();
            //     var result = instance.Result();
            //     Log(result);
            //     return result;
            // }

            return "";

            // var text = File.ReadAllText(filePath);
            // token.ThrowIfCancellationRequested();
            // var engine = new RazorEngine();
            // var template = engine.Compile(text);
            // token.ThrowIfCancellationRequested();
            // return template.Run();
        }
        catch (Exception ex)
        {
            File.AppendAllText(@"C:\Users\user\Desktop\tmp\out.txt",
                Environment.NewLine + Environment.NewLine + ex.ToString());
            var diagnostic = Diagnostic.Create(ScriptError, Location.None, ex);
            reportDiagnostic(diagnostic);
            return "";
        }
    }


    private static readonly DiagnosticDescriptor ScriptError = new(
        id: $"{DiagnosticIdPrefix}001",
        title: "C# script threw exception",
        messageFormat: "{0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private static readonly DiagnosticDescriptor ScriptWarning = new(
        id: $"{DiagnosticIdPrefix}002",
        title: "C# script produced warning",
        messageFormat: "{0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);


    private const string DiagnosticIdPrefix = "SourceGen.Razor";
    private const string DiagnosticCategory = "CSharp.SourceGen.Razor";
}