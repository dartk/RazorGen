using System.Reflection;


namespace CSharp.SourceGen.Razor.RazorEngine;


public static class AssemblyUtil
{
    public const string ResourceNamePrefix = "CSharp.SourceGen.Razor.";
    public static readonly Assembly CurrentAssembly = typeof(AssemblyUtil).Assembly;


    public static IEnumerable<string> GetNetstandardNames()
    {
        var resourceNames = CurrentAssembly.GetManifestResourceNames();
        return resourceNames.Where(static name =>
            name.StartsWith(ResourceNamePrefix + "netstandard"));
    }


    public static IEnumerable<string> GetRunnerNames()
    {
        var resourceNames = CurrentAssembly.GetManifestResourceNames();
        return resourceNames.Where(static name =>
            name.StartsWith("CSharp.SourceGen.Razor.Runner"));
    }
}