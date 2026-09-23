namespace GT7LivMan.Core.Model;

/// <summary>How an <see cref="Element.TextField"/>'s block of characters sits relative to its own transform origin.</summary>
public enum TextAnchor
{
    /// <summary>The origin is the left edge of the first character's cell.</summary>
    Left,

    /// <summary>The origin is the horizontal center of the whole block — what plate text normally wants, so it stays put regardless of which characters are typed.</summary>
    Center,

    /// <summary>The origin is the right edge of the last character's cell.</summary>
    Right,
}
