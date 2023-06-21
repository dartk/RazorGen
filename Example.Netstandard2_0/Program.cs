using static System.Console;


namespace Example.Netstandard2_0;


public static class Program
{
    public static void Main()
    {
        WriteLine(HelloWorld.SayIt());  // Hello, World!
        WriteLine();
        WriteLine("Why use Scriban inside a Razor template?");
        WriteLine(RazorScribanMadness.OhMyGoodnessWhy());  // Because you can!
    }
}