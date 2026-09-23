namespace GT7LivMan.Core.Model;

/// <summary>
/// The one place that defines how wide a run of fixed-pitch plate text is. Both the scene
/// compiler (to resolve <see cref="TextAnchor"/>) and the glyph outliner (to advance its pen) go
/// through here, so their idea of the layout can't drift apart.
/// </summary>
public static class TextMetrics
{
    /// <summary>The pen advance for one character: a literal whitespace in the mask gets its own, narrower pitch.</summary>
    public static double Advance(char c, double tracking, double spaceTracking) =>
        char.IsWhiteSpace(c) ? spaceTracking : tracking;

    /// <summary>Total width of the character cells <paramref name="text"/> occupies.</summary>
    public static double AdvanceWidth(string text, double tracking, double spaceTracking)
    {
        double total = 0;
        foreach (char c in text)
        {
            total += Advance(c, tracking, spaceTracking);
        }

        return total;
    }

    /// <summary>How far to shift a block of text so <paramref name="anchor"/> lands on the element's transform origin.</summary>
    public static double AnchorOffset(TextAnchor anchor, double advanceWidth) => anchor switch
    {
        TextAnchor.Center => -advanceWidth / 2,
        TextAnchor.Right => -advanceWidth,
        _ => 0,
    };
}
