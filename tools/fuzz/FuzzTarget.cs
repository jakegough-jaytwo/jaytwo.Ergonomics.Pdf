using System;

namespace jaytwo.Ergonomics.Pdf.Fuzz;

/// <summary>
/// One fuzz entry point. <see cref="Run"/> catches only the exceptions the contract documents for that entry point.
/// Anything else escapes and is a finding.
/// </summary>
internal sealed class FuzzTarget
{
    public FuzzTarget(string name, int maxLength, Action<byte[]> run)
    {
        Name = name;
        MaxLength = maxLength;
        Run = run;
    }

    public string Name { get; }

    public int MaxLength { get; }

    public Action<byte[]> Run { get; }
}
