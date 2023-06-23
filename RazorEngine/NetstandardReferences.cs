using System.Collections.Immutable;
using Microsoft.CodeAnalysis;


namespace RazorEngine;


public readonly struct NetstandardReferences
{
    public NetstandardReferences()
    {
        var assembly = typeof(NetstandardReferences).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();
        var builder = ImmutableArray.CreateBuilder<MetadataReference>(resourceNames.Length);
        foreach (var name in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var reference = MetadataReference.CreateFromStream(stream);
            builder.Add(reference);
        }

        this.References = builder.MoveToImmutable();
    }


    public readonly ImmutableArray<MetadataReference> References;
}