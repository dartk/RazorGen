using System.Collections.Immutable;
using System.Diagnostics;
using System.Resources;
using System.Runtime.ExceptionServices;
using System.Text;
using AssemblyRunnerShared;
using Newtonsoft.Json;


namespace RazorGen.RazorAssembly;


public enum AssemblyRunResult
{
    Success,
    RuntimeMissing
}


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


    public AssemblyRunResult Run(string assemblyPath, ImmutableArray<string> references,
        out AssemblyRunnerOutput output)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        return this.Run(new AssemblyRunnerInput(assemblyPath, references), out output);
    }


    public AssemblyRunResult Run(AssemblyRunnerInput input, out AssemblyRunnerOutput output)
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

        try
        {
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

            output = this._serializer.Deserialize<AssemblyRunnerOutput>(jsonReader)
                ?? throw new NullReferenceException(
                    $"Deserialized value is null '{nameof(AssemblyRunnerOutput)}'");

            return AssemblyRunResult.Success;
        }
        catch (Exception ex)
        {
            if (process is { HasExited: true, ExitCode: 150 })
            {
                output = null!;
                return AssemblyRunResult.RuntimeMissing;
            }

            ExceptionDispatchInfo.Capture(ex).Throw();
            throw;
        }
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