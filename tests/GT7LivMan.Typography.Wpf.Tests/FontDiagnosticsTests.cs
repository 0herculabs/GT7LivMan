using System.IO;
using System.Windows.Media;
using Xunit.Abstractions;

namespace GT7LivMan.Typography.Wpf.Tests;

/// <summary>
/// Not a correctness test — prints the real MESPREG font's own natural advance widths so a sane
/// FieldDef.Tracking (the template's fixed per-character pitch) can be chosen empirically instead
/// of guessed. Kept around as a diagnostic tool for onboarding future countries' fonts too.
/// </summary>
public class FontDiagnosticsTests(ITestOutputHelper output)
{
    [Fact]
    public void Print_NaturalAdvanceWidths_ForMespreg()
    {
        var resolver = new FontResolver(Path.Combine(RepoPaths.Root, "fonts"));
        FontResolution resolution = resolver.Resolve("MESPREG", []);
        GlyphTypeface gt = resolution.Typeface;

        double capsHeight = gt.CapsHeight > 0 ? gt.CapsHeight : 0.7;
        double scale = 190.0 / capsHeight;
        output.WriteLine($"Source={resolution.Source}, CapsHeight={gt.CapsHeight:F4}, scale(charHeight=190)={scale:F2}");

        foreach (char ch in "0123456789 BCDFGHJKLMNPRSTVWXYZ")
        {
            if (gt.CharacterToGlyphMap.TryGetValue(ch, out ushort gi))
            {
                double naturalAdvance = gt.AdvanceWidths[gi] * scale;
                output.WriteLine($"'{ch}': naturalAdvance={naturalAdvance:F1}");
            }
            else
            {
                output.WriteLine($"'{ch}': MISSING from font's cmap");
            }
        }
    }
}
