using System.Collections.Immutable;
using System.Runtime.CompilerServices;


namespace RazorGen.RazorAssembly;


public static class PathUtil
{
    public static string GetFullClassNameFromFilePath(string filePath) =>
        string.Join(".", GetNames(filePath));


    public static (string Namespace, string ClassName) GetNamespaceAndClassNameFromFilePath(
        string filePath)
    {
        var names = GetNames(filePath);
        var className = names.Last();
        var @namespace = string.Join(".", names.Take(names.Length - 1));
        return (@namespace, className);
    }


    public static ImmutableArray<string> GetNames(string filePath)
    {
        var parts = filePath.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        ref var fileName = ref parts[parts.Length - 1];
        fileName = Path.GetFileNameWithoutExtension(fileName);

        foreach (ref var part in parts.AsSpan())
        {
            part = SafeName(part);
        }

        return Unsafe.As<string[], ImmutableArray<string>>(ref parts);


        static string SafeName(string name)
        {
            var firstChar = name[0];
            if (!IsValidFirstChar(firstChar))
            {
                name = '_' + name;
            }

            if (name.All(IsValidChar))
            {
                return name;
            }

            var chars = name.ToCharArray();
            foreach (ref var c in chars.AsSpan())
            {
                if (!IsValidChar(c))
                {
                    c = '_';
                }
            }

            return new string(chars);


            static bool IsValidFirstChar(char c) => char.IsLetter(c) || c == '_';
            static bool IsValidChar(char c) => char.IsLetterOrDigit(c) || c == '_';
        }
    }


    private static readonly char[] Separators = new[]
    {
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar,
        Path.VolumeSeparatorChar
    };
}