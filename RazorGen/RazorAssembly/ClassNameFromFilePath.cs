using System.Text;


namespace RazorGen.RazorAssembly;


public static class ClassNameFromFilePath
{
    public static string GetClassName(string filePath)
    {
        const string prefix = "T_";
        var classNameBuilder = new StringBuilder(prefix.Length + filePath.Length);
        foreach (var c in filePath)
        {
            var safeChar = char.IsLetterOrDigit(c) ? c : '_';
            classNameBuilder.Append(safeChar);
        }

        return classNameBuilder.ToString();
    }
}