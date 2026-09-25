using System.IO;
using System.Windows;
using System.Windows.Controls;
using GT7LivMan.App.ViewModels;
using Microsoft.Win32;

namespace GT7LivMan.App;

public partial class MainWindow : Window
{
    private readonly PlateEditorViewModel _viewModel;
    private readonly VinylGeneratorViewModel _vinylViewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new PlateEditorViewModel();
        DataContext = _viewModel;

        _vinylViewModel = new VinylGeneratorViewModel();
        VinylPanel.DataContext = _vinylViewModel;

        LanguageSelector.ItemsSource = LocalizationService.Languages;
        LanguageSelector.DisplayMemberPath = nameof(LanguageOption.DisplayName);
        LanguageSelector.SelectedIndex = 0;
        ApplyLanguage();
    }

    private const string WebsiteUrl = "https://gt7livman.com";
    private const string KofiUrl = "https://ko-fi.com/herculabs";

    private void WebsiteButton_Click(object sender, RoutedEventArgs e) => OpenInBrowser(WebsiteUrl);

    private void KofiButton_Click(object sender, RoutedEventArgs e) => OpenInBrowser(KofiUrl);

    // UseShellExecute hands the URL to Windows, which opens it in the default browser.
    private static void OpenInBrowser(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    private void RandomizeButton_Click(object sender, RoutedEventArgs e) => _viewModel.RandomizeAllFields();

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ExportSvg is not { } svg)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = LocalizationService.Text("SvgFiles"),
            FileName = LocalizationService.IsSpanish ? "matricula.svg" : "license-plate.svg",
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, svg);
            MessageBox.Show(
                this,
                LocalizationService.PlateExported(dialog.FileName),
                LocalizationService.Text("ExportComplete"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void LoadImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = LocalizationService.Text("ImageFiles"),
        };

        if (dialog.ShowDialog(this) == true)
        {
            _vinylViewModel.LoadFile(dialog.FileName);
        }
    }

    private void ExportVinylButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vinylViewModel.ExportSvg is not { } svg)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = LocalizationService.Text("SvgFiles"),
            FileName = LocalizationService.IsSpanish ? "vinilo.svg" : "vinyl.svg",
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, svg);
            MessageBox.Show(
                this,
                LocalizationService.VinylExported(dialog.FileName),
                LocalizationService.Text("ExportComplete"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageSelector.SelectedItem is not LanguageOption language)
        {
            return;
        }

        LocalizationService.SetLanguage(language.Code);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Title = LocalizationService.Text("WindowTitle");
        LanguageLabel.Text = LocalizationService.Text("Language");
        WebsiteButton.Content = LocalizationService.Text("Website");
        KofiButton.Content = LocalizationService.Text("SupportKofi");
        VinylTab.Header = LocalizationService.Text("VinylTab");
        PlateTab.Header = LocalizationService.Text("PlateTab");
        LoadImageButton.Content = LocalizationService.Text("LoadImage");
        CountryTemplateLabel.Text = LocalizationService.Text("CountryTemplate");
        RandomizeButton.Content = LocalizationService.Text("RandomPlate");
        ColorsLabelRun.Text = LocalizationService.Text("Colors");
        SimplificationLabel.Text = LocalizationService.Text("Simplification");
        VinylSizeLabel.Text = LocalizationService.Text("SvgSize");
        PlateSizeLabel.Text = LocalizationService.Text("SvgSize");
        ExportVinylButton.Content = LocalizationService.Text("ExportSvg");
        ExportButton.Content = LocalizationService.Text("ExportSvg");

        _viewModel.RefreshLanguage();
        _vinylViewModel.RefreshLanguage();
    }
}
