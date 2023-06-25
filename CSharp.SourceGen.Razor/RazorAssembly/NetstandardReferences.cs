using System.Collections.Immutable;
using Microsoft.CodeAnalysis;


namespace CSharp.SourceGen.Razor.RazorAssembly;


public readonly struct NetstandardReferences
{
    public NetstandardReferences()
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>(100);
        foreach (var name in AssemblyUtil.GetNetstandardNames())
        {
            using var stream = AssemblyUtil.CurrentAssembly.GetManifestResourceStream(name)!;
            var reference = MetadataReference.CreateFromStream(stream);
            builder.Add(reference);
        }

        this.References = builder.ToImmutable();
    }


    public readonly ImmutableArray<MetadataReference> References;
}