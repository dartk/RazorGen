namespace RazorGen;


public readonly record struct AdditionalFile(string FilePath, string Text)
{
    public string FileName => Path.GetFileName(this.FilePath);
    public bool IsNotEmpty => !string.IsNullOrWhiteSpace(this.Text);
}