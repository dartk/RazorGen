# RazorGen

A C# source generator that renders Razor templates.

> **Warning**: The source generator requires .NET 7 runtime installed and `dotnet`
> command available. See [Source generation](#source-generation) section for more information about
> the source generation process.

> **Info**: If the project targets `.NET Standard 2.0`, then the project's references can be used in
> Razor templates.

- [Installation](#installation)
- [Source generation](#source-generation)
- [Saving generated files](#saving-generated-files)
- [Example](#example)

## Installation

```text
dotnet add package Dartk.RazorGen
```

To avoid propagating dependency on the package set the option `PrivateAssets="all"` in the project
file:

```xml

<ItemGroup>
    <PackageReference Include="Dartk.RazorGen" Version="0.2.2" PrivateAssets="All" />
</ItemGroup>
```

Include razor template files with *.razor* extension to the project as `AdditionalFiles`. For
example, to render all razor templates in the project add this to the project file:

```xml

<ItemGroup>
    <AdditionalFiles Include="**/*.razor" />
</ItemGroup>
```

A [complete example](#example) is presented below.

## Source generation

Razor engine does not render a template directly, instead it generates a C# class that has a
method that returns a rendered output. In order to render a template, an intermediate library
with Razor generated classes needs to be compiled.

The source generator passes all found `.razor` (included to the project as `AdditionalFiles`)
templates to the Razor engine, which generates C# classes that render templates. Those classes are
compiled into a temporary intermediate library.

Source generators target `.NET Standard 2.0` which does not support assembly unloading. In order to
prevent memory leak by repeated assembly loading, the intermediate library is called in
a separate process by an external .NET 6 executable.

If the project that uses the source generator targets `.NET Standard 2.0`, then the project's
references will be referenced by the intermediate library.

## Saving generated files

To save the generated source files set properties `EmitCompilerGeneratedFiles`
and `CompilerGeneratedFilesOutputPath` in the project file:

```xml

<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <!--Generated files will be saved to 'obj\GeneratedFiles' folder-->
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

## Example

Create a new console C# project:

```text
dotnet new Example.Netstandard2_0
```

Install the package `Dartk.RazorGen` and set the property `PrivateAssets="All"` by
editing the project file *Example.csproj*:

```xml

<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>latest</LangVersion>
    </PropertyGroup>

    <ItemGroup>
        <!--PrivateAssets="All" prevents propagation of dependency to other projects-->
        <PackageReference Include="Dartk.RazorGen" Version="0.2.2"
                          PrivateAssets="All" />

        <!--Scriban can be used within Razor templates, since the target platform is netstandard2.0-->
        <PackageReference Include="Scriban" Version="5.7.0" />
    </ItemGroup>

    <!--Includes all .razor files as AdditionalFiles-->
    <ItemGroup>
        <AdditionalFiles Include="**\*.razor" />
    </ItemGroup>
</Project>
```

Create a file `RazorScribanMadness.razor`:

```c#
namespace Example.Netstandard2_0;

// 'partial' is used for JetBrains Rider, it's linter thinks that a '.razor' file declares
// a class, and will treat the generated code as second declaration highlighting errors
// in places where the class is being used.
public static partial class RazorScribanMadness
{
    @using global::Scriban
    @{
        @:public static string Why() => "@(RenderScriban())";

        string RenderScriban()
        {
            var template = Template.Parse("Because {{ reason }}!");
            return template.Render(new { reason = "you can" });
        }
    }
}
```

The template above will generate following code:

```c#
namespace Example.Netstandard2_0;

// 'partial' is used for JetBrains Rider, it's linter thinks that a '.razor' file declares
// a class, and will treat the generated code as second declaration highlighting errors
// in places where the class is being used.
public static partial class RazorScribanMadness
{
        public static string Why() => "Because you can!";
}
```