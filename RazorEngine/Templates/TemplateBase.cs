using System.Text;


namespace RazorEngine.Templates;


public abstract class TemplateBase : MarshalByRefObject
{
    private readonly StringBuilder _stringBuilder = new StringBuilder();
    private string? _attributeSuffix = null;

    public dynamic? Model { get; set; }


    public void WriteLiteral(string? literal = null)
    {
        this._stringBuilder.Append(literal);
    }


    public void Write(object? obj = null)
    {
        this._stringBuilder.Append(obj);
    }


    public void BeginWriteAttribute(string name, string prefix, int prefixOffset, string suffix,
        int suffixOffset, int attributeValuesCount)
    {
        this._attributeSuffix = suffix;
        this._stringBuilder.Append(prefix);
    }


    public void WriteAttributeValue(string prefix, int prefixOffset, object value, int valueOffset,
        int valueLength,
        bool isLiteral)
    {
        this._stringBuilder.Append(prefix);
        this._stringBuilder.Append(value);
    }


    public void EndWriteAttribute()
    {
        this._stringBuilder.Append(this._attributeSuffix);
        this._attributeSuffix = null;
    }


    public void Execute()
    {
        this.ExecuteAsync().GetAwaiter().GetResult();
    }


    public string Result()
    {
        this.Execute();
        return this._stringBuilder.ToString();
    }


    public abstract Task ExecuteAsync();
}