namespace IBEBarcode.Scanner.Core;

/// <summary>One Application Identifier element from a GS1 element string.</summary>
/// <param name="Ai">The 2-4 digit Application Identifier, e.g. <c>01</c>.</param>
/// <param name="Description">Human-readable AI name, or null when the AI is not in the known table.</param>
/// <param name="Value">The element value, with separators removed.</param>
public sealed record Gs1Element(string Ai, string? Description, string Value)
{
    /// <summary>Element string in the conventional bracketed notation, e.g. <c>(01)09501101530003</c>.</summary>
    public string ToBracketedString() => $"({Ai}){Value}";

    public override string ToString() => ToBracketedString();
}
