using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Rendering;

/// <summary>One flattened, already-positioned shape ready to render or export — no transforms, no fields left.</summary>
public sealed record SceneElement(PathData Path, RgbColor Fill);
