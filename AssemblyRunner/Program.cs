using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using AssemblyRunnerShared;


namespace AssemblyRunner;


internal static class Program
{
    public static void Main()
    {
        var line = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(line)) return;
        if (!TryReadInput(line, out var input)) return;
        VerifyInputOrThrow(input);

        var output = GenerateOutput(input);
        var json = JsonSerializer.Serialize(output);
        Console.WriteLine(json);
    }


    private static void VerifyInputOrThrow(AssemblyRunnerInput input)
    {
        AssertFileExists(input.AssemblyPath);
        foreach (var path in input.References)
        {
            AssertFileExists(path);
        }
    }


    private static AssemblyRunnerOutput GenerateOutput(AssemblyRunnerInput input)
    {
        var assembly = Assembly.LoadFrom(input.AssemblyPath);
        foreach (var reference in input.References)
        {
            Assembly.LoadFrom(reference);
        }

        var templateBaseType = assembly.GetType("Templates.TemplateBase")
            ?? throw new NullReferenceException("TemplateBase class not found.");

        var resultMethod = templateBaseType.GetMethod("Result")
            ?? throw new NullReferenceException("Result method not found in TemplateBase.");

        var types = assembly.GetExportedTypes();
        var items = ImmutableArray.CreateBuilder<AssemblyRunnerOutputItem>(types.Length);

        foreach (var type in types)
        {
            if (type.IsAbstract || !type.IsSubclassOf(templateBaseType))
            {
                continue;
            }

            var template = Activator.CreateInstance(type)
                ?? throw new ArgumentException($"Cannot create instance of '{type}'.");

            var className = type.Name;
            var renderedText = ((string?)resultMethod.Invoke(template, null)) ??
                throw new NullReferenceException($"Result is null '{type}'.");

            var item = new AssemblyRunnerOutputItem(className, renderedText);
            items.Add(item);
        }

        return new AssemblyRunnerOutput(items.ToImmutable());
    }


    private static bool TryReadInput(string message,
        [MaybeNullWhen(false)] out AssemblyRunnerInput input)
    {
        try
        {
            input = JsonSerializer.Deserialize<AssemblyRunnerInput>(message)
                ?? throw new SerializationException("Deserialized input is null.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.Write(ex.ToString());
            input = null;
            return false;
        }
    }


    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }


    private static void AssertFileExists(string path)
    {
        Assert(File.Exists(path), $"File '{path}' does not exist.");
    }
}