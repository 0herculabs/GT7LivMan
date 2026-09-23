using System.Windows.Media;
using GT7LivMan.Core.Rendering;

namespace GT7LivMan.App.Rendering;

/// <summary>
/// Renders a <see cref="Scene"/> — the exact same model <see cref="Core.Svg.SvgWriter"/> exports —
/// as a WPF <see cref="DrawingImage"/>. Sharing one model between the live preview and the export
/// path means a broken glyph or a bad transform shows up on screen instantly, not just at export.
/// </summary>
public static class WpfSceneRenderer
{
    public static DrawingImage Render(Scene scene)
    {
        var group = new DrawingGroup();
        using (DrawingContext dc = group.Open())
        {
            foreach (SceneElement element in scene.Elements)
            {
                Geometry geometry = PathDataToWpfGeometryConverter.Convert(element.Path);
                var brush = new SolidColorBrush(Color.FromRgb(element.Fill.R, element.Fill.G, element.Fill.B));
                brush.Freeze();
                dc.DrawGeometry(brush, null, geometry);
            }
        }

        group.Freeze();

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
