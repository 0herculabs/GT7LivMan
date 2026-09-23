using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Rendering;

/// <summary>
/// The single source of truth consumed by both <see cref="Svg.SvgWriter"/> and the WPF preview
/// renderer, in paint order (first element painted first, later elements on top).
/// </summary>
public sealed record Scene(SizeMm Size, IReadOnlyList<SceneElement> Elements);
