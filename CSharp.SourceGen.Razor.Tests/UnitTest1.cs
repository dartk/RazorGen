using Xunit.Abstractions;


namespace CSharp.SourceGen.Razor.Tests;


public class UnitTest1
{
    public UnitTest1(ITestOutputHelper output)
    {
        this._output = output;
    }


    [Fact]
    public void Test1()
    {
        this._output.WriteLine(Test.Directory);
    }


    private readonly ITestOutputHelper _output;
}