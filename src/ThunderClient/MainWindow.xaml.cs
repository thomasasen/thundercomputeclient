using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ThunderClient.Core;
using ThunderClient.ViewModels;

namespace ThunderClient;

public partial class MainWindow : Window, IUserDialogs
{
    private readonly MainViewModel _model;
    private Point _dragStart;
    private readonly bool _uiSmokeTest;

    public MainWindow(bool uiSmokeTest = false)
    {
        _uiSmokeTest = uiSmokeTest;
        StartupDiagnostics.Trace("MainWindow: vor InitializeComponent");
        InitializeComponent();
        StartupDiagnostics.Trace("MainWindow: XAML geladen");
        _model = new MainViewModel(this);
        StartupDiagnostics.Trace("MainWindow: ViewModel geladen");
        DataContext = _model;
        ApplyTheme(_model.Theme);
        StartupDiagnostics.Trace("MainWindow: Theme gesetzt");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_uiSmokeTest) return; // UI-Test darf weder Netzwerk noch kostenpflichtige Aktionen ausführen.
        try { await _model.InitializeAsync(); }
        catch (Exception ex)
        {
            StartupDiagnostics.Record("Initialisierung", ex);
            Notify("Initialisierung fehlgeschlagen", ex.Message +
                "\n\nDie Oberfläche bleibt geöffnet. Diagnose: " + StartupDiagnostics.LogPath);
        }
    }

    private void Window_Closed(object? sender, EventArgs e) => _model.Dispose();

    public bool Confirm(string title, string message) =>
        MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;

    public bool ApproveFingerprint(string host, string fingerprint) =>
        Confirm("SSH-Hostschlüssel verifizieren",
            "Host: " + host + "\nED25519: " + fingerprint +
            "\n\nUnbedingt mit dem Fingerprint aus der unabhängigen Thunder-Konsole oder einer vertrauenswürdigen Quelle vergleichen. Stimmt er exakt überein? Sonst Nein wählen.");

    public void Notify(string title, string message) =>
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ExportText(string suggestedName, string text)
    {
        var picker = new SaveFileDialog
        {
            FileName = suggestedName,
            DefaultExt = ".txt",
            Filter = "Textdatei (*.txt)|*.txt"
        };
        if (picker.ShowDialog(this) == true) File.WriteAllText(picker.FileName, text);
    }

    private void SaveGithub_Click(object sender, RoutedEventArgs e)
    { _model.SaveSecret("github", GithubPassword.Password); GithubPassword.Clear(); }
    private void DeleteGithub_Click(object sender, RoutedEventArgs e)
    { _model.DeleteSecret("github"); GithubPassword.Clear(); }
    private void SaveHf_Click(object sender, RoutedEventArgs e)
    { _model.SaveSecret("huggingface", HfPassword.Password); HfPassword.Clear(); }
    private void DeleteHf_Click(object sender, RoutedEventArgs e)
    { _model.DeleteSecret("huggingface"); HfPassword.Clear(); }
    private void SaveCivitai_Click(object sender, RoutedEventArgs e)
    { _model.SaveSecret("civitai", CivitaiPassword.Password); CivitaiPassword.Clear(); }
    private void DeleteCivitai_Click(object sender, RoutedEventArgs e)
    { _model.DeleteSecret("civitai"); CivitaiPassword.Clear(); }

    private void GpuList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _dragStart = e.GetPosition(GpuList);

    private void GpuList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _model.SelectedGpu is null) return;
        var now = e.GetPosition(GpuList);
        if (Math.Abs(now.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(now.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        DragDrop.DoDragDrop(GpuList, _model.SelectedGpu, DragDropEffects.Move);
    }

    private void GpuList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(GpuChoice)) is not GpuChoice source) return;
        var visual = e.OriginalSource as DependencyObject;
        while (visual is not null && visual is not ListBoxItem)
            visual = VisualTreeHelper.GetParent(visual);
        if (visual is ListBoxItem { DataContext: GpuChoice target })
            _model.MoveGpu(_model.GpuChoices.IndexOf(source), _model.GpuChoices.IndexOf(target));
    }

    private void ThemeSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_model is not null) ApplyTheme(_model.Theme);
    }

    private void ApplyTheme(string theme)
    {
        var light = theme == "Hell";
        void SetBrush(string key, byte r, byte g, byte b) =>
            Application.Current.Resources[key] = new SolidColorBrush(Color.FromRgb(r, g, b));
        if (light)
        {
            SetBrush("WindowBrush", 244, 248, 253);
            SetBrush("SurfaceBrush", 255, 255, 255);
            SetBrush("FieldBrush", 231, 238, 246);
            SetBrush("TextBrush", 23, 37, 56);
            SetBrush("SubtleBrush", 74, 92, 112);
            SetBrush("AccentBrush", 0, 99, 166);
        }
        else
        {
            SetBrush("WindowBrush", 16, 24, 39);
            SetBrush("SurfaceBrush", 25, 38, 56);
            SetBrush("FieldBrush", 35, 52, 73);
            SetBrush("TextBrush", 243, 246, 250);
            SetBrush("SubtleBrush", 170, 192, 214);
            SetBrush("AccentBrush", 56, 189, 248);
        }
    }
}

