namespace CSharp.SourceGen.Razor.RazorAssembly;


public readonly record struct DisposableFile(string Path) : IDisposable
{
    public void Dispose()
    {
        if (File.Exists(this.Path))
        {
            File.Delete(this.Path);
        }
    }
}