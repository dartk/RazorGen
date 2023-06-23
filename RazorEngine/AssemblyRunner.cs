using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;


namespace RazorEngine;


public record AssemblyRunnerInput(string AssemblyPath, ImmutableArray<string> References);
public record AssemblyRunnerOutputItem(string ClassName, string RenderedText);
public record AssemblyRunnerOutput(ImmutableArray<AssemblyRunnerOutputItem> Items);


public class AssemblyRunner
{
    public AssemblyRunner()
    {
        this._serializer = JsonSerializer.CreateDefault();
    }


    public AssemblyRunnerOutput Run(string assemblyPath, ImmutableArray<string> references)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        return this.Run(new AssemblyRunnerInput(assemblyPath, references));
    }


    public AssemblyRunnerOutput Run(AssemblyRunnerInput input)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments =
                $@"R:\dartk\csharp-sourcegen-razor\RazorEngine.Service\bin\Debug\net7.0\RazorEngine.Service.dll",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = new Process
        {
            StartInfo = startInfo
        };

        var outputWriter = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data == null) return;
            outputWriter.AppendLine(args.Data);
        };

        var errorWriter = new StringBuilder();
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data == null) return;
            errorWriter.AppendLine(args.Data);
        };

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        this._serializer.Serialize(process.StandardInput, input);
        process.StandardInput.WriteLine();
        process.StandardInput.Flush();

        process.WaitForExit();

        if (errorWriter.Length > 0)
        {
            throw new Exception(errorWriter.ToString());
        }

        using var reader = new StringReader(outputWriter.ToString());
        using var jsonReader = new JsonTextReader(reader);
        var output = this._serializer.Deserialize<AssemblyRunnerOutput>(jsonReader)
            ?? throw new NullReferenceException(
                $"Deserialized value is null '{nameof(AssemblyRunnerOutput)}'");

        return output;
    }


    private readonly JsonSerializer _serializer;
}