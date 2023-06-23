using System.Reflection;


namespace RazorEngine.Templates;


public static class TemplateRenderer
{
    public static List<string> RenderAll(Assembly assembly)
    {
        var types = assembly.GetExportedTypes();
        var results = new List<string>(types.Length);
        foreach (var type in types)
        {
            if (!type.IsSubclassOf(typeof(TemplateBase))) continue;

            var template = (TemplateBase)Activator.CreateInstance(type);
            var result = template.Result();
            results.Add(result);
        }

        return results;
    }
}