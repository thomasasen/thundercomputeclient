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
        _model.PodCreated += () => MainTabs.SelectedIndex = 3;
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
            _model.ReportStartupFailure(ex);
        }
    }

    private void Window_Closed(object? sender, EventArgs e) => _model.Dispose();

    // CI exercise: every real page is constructed, displayed and bound without external calls.
    // The app-level ContentRendered callback invokes this only with --ui-smoke-test.
    public void VerifyPagesForSmokeTest()
    {
        if (Navigation.Items.Count != 8 || MainTabs.Items.Count != 8)
            throw new InvalidOperationException("Navigation und Inhalte sind nicht vollständig.");
        if (ReadinessItems.Items.Count != 7 ||
            RunSystemChecksButton.Content is not "Jetzt alles sicher prüfen")
            throw new InvalidOperationException("Systemstatus-Ansicht ist nicht vollständig.");
        if (!LogTextBox.IsReadOnly || CopyLogButton.Content is not "Log kopieren" ||
            CopyStartupLogButton.Content is not "Startdiagnose kopieren")
            throw new InvalidOperationException("Die Protokollansicht muss markierbar und kopierbar sein.");
        if (LoadCatalogButton.Content is not "1  Katalog laden und Workflows wählen" ||
            ContinueToHardwareButton.Content is not "2  Auswahl übernehmen → Hardware prüfen" ||
            PodSetupButton.Content is not "4  Installation & Logs")
            throw new InvalidOperationException("Geführter Ablauf ist unvollständig.");
        var initialWidth = Width;
        try
        {
            // Verify the main interactive view also fits the supported minimum width.
            // No network request or billable action is executed by this check.
            foreach (var testWidth in new[] { 1320d, MinWidth })
            {
                Width = testWidth;
                UpdateLayout();
                for (var index = 0; index < MainTabs.Items.Count; index++)
                {
                    MainTabs.SelectedIndex = index;
                    UpdateLayout();
                    if (Navigation.SelectedIndex != index)
                        throw new InvalidOperationException("Sidebar und Inhalt sind nicht synchron: " + index);
                    if (index == 2)
                    {
                        if (GpuList.ActualWidth < 450 ||
                            InstanceCreateButton.ActualWidth < 150 ||
                            !InstanceCreateButton.IsVisible)
                            throw new InvalidOperationException(
                                "Instanzformular oder fester Kosten-Button ist bei " +
                                testWidth + "px nicht richtig sichtbar.");
                    }
                }
            }
        }
        finally
        {
            Width = initialWidth;
            MainTabs.SelectedIndex = 0;
            UpdateLayout();
        }
    }

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

    private async void LoadCatalog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _model.ReloadCatalogAsync();
            MainTabs.SelectedIndex = 1;
        }
        catch (Exception ex)
        {
            Notify("Privaten Katalog laden", ex.Message);
        }
    }

    private async void ContinueToHardware_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _model.PrepareHardwareStepAsync();
            MainTabs.SelectedIndex = 2;
        }
        catch (Exception ex)
        {
            Notify("Workflow-Auswahl", ex.Message);
        }
    }

    private void BackToWorkflows_Click(object sender, RoutedEventArgs e) =>
        MainTabs.SelectedIndex = 1;

    private void OpenPodSetup_Click(object sender, RoutedEventArgs e) =>
        MainTabs.SelectedIndex = 4;

    private void OpenSystemStatus_Click(object sender, RoutedEventArgs e) =>
        MainTabs.SelectedIndex = 7;

    private void GoToCredentials_Click(object sender, RoutedEventArgs e) =>
        MainTabs.SelectedIndex = 5;

    private void GoToPods_Click(object sender, RoutedEventArgs e) =>
        MainTabs.SelectedIndex = 3;

    private void GoToCheckSetup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SetupCheck setup } &&
            setup.SetupTabIndex is >= 0 and <= 7)
            MainTabs.SelectedIndex = setup.SetupTabIndex;
    }

    private void OpenCheckHelp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SetupCheck setup }) return;
        // Allowlisted official setup destinations only, never a URL from a remote manifest.
        if (!Uri.TryCreate(setup.HelpUrl, UriKind.Absolute, out var target) ||
            target.Scheme != Uri.UriSchemeHttps ||
            target.Host is not ("github.com" or "huggingface.co" or "learn.microsoft.com" or "developer.civitai.com" or "civitai.com"))
        {
            Notify("Einrichtungshilfe", "Der externe Hilfelink ist nicht vertrauenswürdig.");
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Notify("Einrichtungshilfe", "Der Browser konnte nicht geöffnet werden.");
        }
    }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        CopyTextToClipboard(_model.LogText, "Das Protokoll ist noch leer.", "Log in die Zwischenablage kopiert.");
    }

    private void CopyStartupLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(StartupDiagnostics.LogPath))
            {
                _model.ReportCopyStatus("Noch kein Startprotokoll vorhanden.");
                return;
            }
            // Never put the original on disk log directly onto the clipboard.
            var safeContent = _model.RedactForCopy(File.ReadAllText(StartupDiagnostics.LogPath));
            CopyTextToClipboard(safeContent, "Das Startprotokoll ist leer.",
                "Startdiagnose in die Zwischenablage kopiert.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _model.ReportCopyStatus("Startdiagnose konnte nicht gelesen werden.");
        }
    }

    private void CopyTextToClipboard(string content, string emptyMessage, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _model.ReportCopyStatus(emptyMessage);
            return;
        }
        try
        {
            Clipboard.SetText(content);
            _model.ReportCopyStatus(successMessage + " Vor dem Teilen auf persönliche Daten prüfen.");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _model.ReportCopyStatus("Zwischenablage ist gerade belegt. Bitte erneut versuchen.");
        }
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
            SetBrush("WindowBrush", 246, 247, 249);
            SetBrush("SidebarBrush", 238, 241, 245);
            SetBrush("SurfaceBrush", 255, 255, 255);
            SetBrush("FieldBrush", 244, 246, 249);
            SetBrush("TextBrush", 24, 34, 48);
            SetBrush("SubtleBrush", 89, 103, 121);
            SetBrush("AccentBrush", 33, 101, 202);
            SetBrush("OnAccentBrush", 255, 255, 255);
            SetBrush("BorderBrush", 221, 227, 235);
            SetBrush("HoverBrush", 229, 235, 243);
            SetBrush("SelectedBrush", 220, 233, 252);
            SetBrush("DangerBrush", 184, 74, 89);
        }
        else
        {
            SetBrush("WindowBrush", 15, 21, 30);
            SetBrush("SidebarBrush", 22, 31, 43);
            SetBrush("SurfaceBrush", 28, 39, 53);
            SetBrush("FieldBrush", 36, 49, 65);
            SetBrush("TextBrush", 240, 244, 249);
            SetBrush("SubtleBrush", 171, 185, 202);
            SetBrush("AccentBrush", 82, 157, 255);
            SetBrush("OnAccentBrush", 14, 32, 52);
            SetBrush("BorderBrush", 56, 70, 88);
            SetBrush("HoverBrush", 42, 57, 75);
            SetBrush("SelectedBrush", 44, 77, 112);
            SetBrush("DangerBrush", 156, 57, 72);
        }
    }
}
