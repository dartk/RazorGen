using System.Collections.Immutable;


namespace AssemblyRunnerShared;


public record AssemblyRunnerInput(string AssemblyPath, ImmutableArray<string> References);
public record AssemblyRunnerOutputItem(string ClassName, string RenderedText);
public record AssemblyRunnerOutput(ImmutableArray<AssemblyRunnerOutputItem> Items);