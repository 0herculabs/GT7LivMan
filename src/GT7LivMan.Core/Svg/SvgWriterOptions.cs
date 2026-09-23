namespace GT7LivMan.Core.Svg;

/// <summary>
/// Every knob here exists because GT7's SVG parser behavior for that specific feature is
/// undocumented. <see cref="Default"/> is the size-optimized guess; <see cref="MaxCompatibility"/>
/// turns off every guess so there's a documented fallback to retry with if an export gets rejected
/// or renders wrong in-game.
/// </summary>
public sealed record SvgWriterOptions
{
    /// <summary>
    /// Decimal places for coordinates. The primary size lever once byte budget gets tight. 1 decimal
    /// place is sub-0.1mm precision at every template's scale so far (the coarsest, UK's, is
    /// ~1 unit/mm) — imperceptible on a physical decal — and California's complex cursive wordmark
    /// plus a detailed plate font need the headroom it buys back (~2 KB on that template alone).
    /// </summary>
    public int Decimals { get; init; } = 1;

    /// <summary>Omit the command letter when a path command repeats consecutively (e.g. "L1,2 3,4" instead of "L1,2 L3,4").</summary>
    public bool ImplicitRepeatedCommands { get; init; } = true;

    /// <summary>Group sibling elements that share a fill color under one &lt;g fill="#hex"&gt;, both for bytes and as a defensive guess at how GT7 groups recolorable layers.</summary>
    public bool GroupByFill { get; init; } = true;

    public static SvgWriterOptions Default { get; } = new();

    /// <summary>Explicit commands, no grouping, full decimal precision — the fallback profile to retry an export with if the default guesses turn out to not render correctly in-game.</summary>
    public static SvgWriterOptions MaxCompatibility { get; } = new()
    {
        Decimals = 4,
        ImplicitRepeatedCommands = false,
        GroupByFill = false,
    };
}
