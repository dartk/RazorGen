using System.Reflection;


namespace CSharp.SourceGen.Razor.RazorAssembly;


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
}