using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using GT7LivMan.App.Rendering;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Svg;
using GT7LivMan.Vectorization;

namespace GT7LivMan.App.ViewModels;

/// <summary>
/// The SVG Generator tab: load a JPG/PNG (vectorized via <see cref="RasterVectorizer"/>) or an
/// SVG file directly, both funneled through the same permissive <see cref="GenericSvgImporter"/>,
/// into a <see cref="PlateDocument"/> with no <see cref="FieldDef"/>s — a free-form decal is just a
/// template with nothing to fill in, so from there on this mirrors <see cref="PlateEditorViewModel"/>'s
/// own compile/render/export pipeline exactly.
/// </summary>
public sealed class VinylGeneratorViewModel : ObservableObject
{
    private const int DefaultColorCount = 6;
    private const double DefaultSimplification = 1.0;

    private string? _loadedFilePath;
    private PlateDocument? _document;
    private int _colorCount = DefaultColorCount;
    private double _simplification = DefaultSimplification;
    private bool _isRasterLoaded;
    private ImageSource? _previewImage;
    private string _sizeLabel = string.Empty;
    private bool _isOverBudget;
    private string? _exportSvg;
    private StatusKind _statusKind = StatusKind.Ready;
    private string? _statusDetail;
    private int _importedShapeCount;
    private readonly List<string> _sourceWarnings = [];

    /// <summary>One line per category of thing the last import couldn't represent faithfully (gradients flattened, text skipped, etc.) — see <see cref="GenericSvgImporter"/>.</summary>
    public ObservableCollection<string> Warnings { get; } = [];

    /// <summary>How many flat colors a raster upload is quantized down to. Only meaningful (and only re-traces on change) for a raster load — an uploaded SVG already has its own colors.</summary>
    public int ColorCount
    {
        get => _colorCount;
        set
        {
            int clamped = Math.Clamp(value, 2, 16);
            if (SetField(ref _colorCount, clamped) && IsRasterLoaded)
            {
                ReloadRaster();
            }
        }
    }

    /// <summary>How hard to simplify a raster trace — higher smooths curves and drops small speckle regions more aggressively. See <see cref="RasterVectorizer.Trace(string, int, double)"/>.</summary>
    public double Simplification
    {
        get => _simplification;
        set
        {
            if (SetField(ref _simplification, value) && IsRasterLoaded)
            {
                ReloadRaster();
            }
        }
    }

    /// <summary>Whether the color/simplification controls should be enabled — they only apply to a raster source, not a directly-uploaded SVG.</summary>
    public bool IsRasterLoaded
    {
        get => _isRasterLoaded;
        private set => SetField(ref _isRasterLoaded, value);
    }

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        private set => SetField(ref _previewImage, value);
    }

    public string SizeLabel
    {
        get => _sizeLabel;
        private set => SetField(ref _sizeLabel, value);
    }

    public bool IsOverBudget
    {
        get => _isOverBudget;
        private set => SetField(ref _isOverBudget, value);
    }

    /// <summary>The most recently exported SVG text, ready to write to disk; null until something has been loaded successfully.</summary>
    public string? ExportSvg
    {
        get => _exportSvg;
        private set
        {
            if (SetField(ref _exportSvg, value))
            {
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    /// <summary>Drives the export button's IsEnabled — a plain bool binding, since WPF doesn't null-check a bound nullable string on its own.</summary>
    public bool CanExport => ExportSvg is not null;

    public string StatusMessage
    {
        get => _statusKind switch
        {
            StatusKind.Ready => LocalizationService.Text("Ready"),
            StatusKind.UnsupportedFormat => LocalizationService.UnsupportedFormat(_statusDetail ?? string.Empty),
            StatusKind.ImportFailed => LocalizationService.ImportFailed(_statusDetail ?? string.Empty),
            StatusKind.NoShapes => LocalizationService.Text("NoShapes"),
            StatusKind.ShapesImported => LocalizationService.ShapesImported(_importedShapeCount),
            _ => string.Empty,
        };
    }

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(StatusMessage));
        RefreshWarnings();
    }

    public void LoadFile(string path)
    {
        _loadedFilePath = path;
        string extension = Path.GetExtension(path).ToUpperInvariant();

        switch (extension)
        {
            case ".JPG" or ".JPEG" or ".PNG":
                IsRasterLoaded = true;
                ReloadRaster();
                break;

            case ".SVG":
                IsRasterLoaded = false;
                ImportAndRender(File.ReadAllText(path), cutTransparentHoles: false);
                break;

            default:
                SetStatus(StatusKind.UnsupportedFormat, extension);
                break;
        }
    }

    private void ReloadRaster()
    {
        if (_loadedFilePath is null)
        {
            return;
        }

        string tracedSvg = RasterVectorizer.Trace(_loadedFilePath, ColorCount, Simplification);
        ImportAndRender(tracedSvg, cutTransparentHoles: true);
    }

    /// <param name="cutTransparentHoles">
    /// Only for a traced raster, where a transparent region genuinely means "cut this out of the
    /// shape underneath" (see <see cref="HoleCutter"/>). In a hand-authored SVG a transparent shape
    /// just doesn't render — punching a hole through the artwork under it would be wrong.
    /// </param>
    private void ImportAndRender(string svgContent, bool cutTransparentHoles)
    {
        GenericSvgImporter.Result result;
        try
        {
            result = GenericSvgImporter.Import(svgContent);
        }
        catch (FormatException ex)
        {
            SetStatus(StatusKind.ImportFailed, ex.Message);
            ExportSvg = null;
            PreviewImage = null;
            _sourceWarnings.Clear();
            Warnings.Clear();
            return;
        }

        _sourceWarnings.Clear();
        _sourceWarnings.AddRange(result.Warnings);
        RefreshWarnings();

        IReadOnlyList<Element> elements = cutTransparentHoles
            ? HoleCutter.CutHoles(result.Elements, result.TransparentRegions)
            : result.Elements;

        _document = new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "VINYL",
            DisplayName: Path.GetFileName(_loadedFilePath) ?? "Vinilo",
            Size: result.Size,
            Elements: elements,
            Fields: []);

        Recompute();
    }

    private void Recompute()
    {
        if (_document is not { } document)
        {
            return;
        }

        Core.Rendering.Scene scene = SceneCompiler.Compile(document, new Dictionary<string, string>(), ThrowingTextOutliner.Instance);
        PreviewImage = WpfSceneRenderer.Render(scene);

        string svg = ExportEscalation.Write(scene);
        ExportSvg = svg;

        int bytes = SizeBudget.MeasureUtf8Bytes(svg);
        SizeLabel = $"{bytes / 1024.0:F1} KB / {SizeBudget.MaxBytes / 1024.0:F1} KB";
        IsOverBudget = !SizeBudget.IsWithinGt7Limit(svg);

        if (document.Elements.Count == 0)
        {
            SetStatus(StatusKind.NoShapes);
        }
        else
        {
            _importedShapeCount = document.Elements.Count;
            SetStatus(StatusKind.ShapesImported);
        }
    }

    private void SetStatus(StatusKind kind, string? detail = null)
    {
        _statusKind = kind;
        _statusDetail = detail;
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void RefreshWarnings()
    {
        Warnings.Clear();
        foreach (string warning in _sourceWarnings)
        {
            Warnings.Add(LocalizationService.TranslateWarning(warning));
        }
    }

    private enum StatusKind
    {
        Ready,
        UnsupportedFormat,
        ImportFailed,
        NoShapes,
        ShapesImported,
    }

    /// <summary>A vinyl document never has <see cref="PlateDocument.Fields"/>, so <see cref="SceneCompiler"/> should never need to outline any text — a throwing stub makes that invariant loud instead of silently resolving fonts nobody asked for.</summary>
    private sealed class ThrowingTextOutliner : ITextOutliner
    {
        public static readonly ThrowingTextOutliner Instance = new();

        public IReadOnlyList<PathData> Outline(
            string text, string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies, double charHeight, double tracking, double spaceTracking) =>
            throw new InvalidOperationException("A vinyl document has no text fields — nothing should call the outliner.");
    }
}
