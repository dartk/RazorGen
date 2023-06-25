using System.Collections.Immutable;
using System.Diagnostics;
using System.Resources;
using System.Text;
using AssemblyRunnerShared;
using Newtonsoft.Json;


namespace CSharp.SourceGen.Razor.RazorAssembly;


public class AssemblyRunner
{
    public AssemblyRunner()
    {
        this._serializer = JsonSerializer.CreateDefault();
        try
        {
            if (File.Exists(AssemblyFilePath))
            {
                File.Delete(AssemblyFilePath);
            }

            if (File.Exists(ConfigFilePath))
            {
                File.Delete(AssemblyFilePath);
            }
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch
        {
        }
    }


    public AssemblyRunnerOutput Run(string assemblyPath, ImmutableArray<string> references)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        return this.Run(new AssemblyRunnerInput(assemblyPath, references));
    }


    public AssemblyRunnerOutput Run(AssemblyRunnerInput input)
    {
        var runnerFilePath = ExtractRunner();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{runnerFilePath}\"",
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


    private static string ExtractRunner()
    {
        const string prefix = AssemblyUtil.ResourceNamePrefix + "RazorAssembly.Runner.";
        CopyToFile(prefix + AssemblyName, AssemblyFilePath);
        CopyToFile(prefix + ConfigName, ConfigFilePath);

        return AssemblyFilePath;


        static void CopyToFile(string resourceName, string fileName)
        {
            if (File.Exists(fileName)) return;
            System.IO.Directory.CreateDirectory(Directory);

            using var assemblyInput =
                AssemblyUtil.CurrentAssembly.GetManifestResourceStream(resourceName)
                ?? throw new MissingManifestResourceException(
                    $"Resource not found '{resourceName}'.");
            using var assemblyOutput = File.Create(fileName);
            assemblyInput.CopyTo(assemblyOutput);
        }
    }


    private static readonly string Directory =
        Path.GetDirectoryName(typeof(AssemblyRunner).Assembly.Location)!;


    private const string AssemblyName = "AssemblyRunner.dll";
    private const string ConfigName = "AssemblyRunner.runtimeconfig.json";


    private static readonly string AssemblyFilePath = Path.Combine(Directory, AssemblyName);
    private static readonly string ConfigFilePath = Path.Combine(Directory, ConfigName);
}