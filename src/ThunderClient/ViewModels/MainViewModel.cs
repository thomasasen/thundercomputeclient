using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ThunderClient.Core;

namespace ThunderClient.ViewModels;

public interface IUserDialogs
{
    bool Confirm(string title, string message);
    bool ApproveFingerprint(string host, string fingerprint);
    void Notify(string title, string message);
    void ExportText(string suggestedName, string text);
}

public sealed class MainViewModel : Bindable, IDisposable
{
    private readonly IUserDialogs _dialogs;
    private readonly ICommandRunner _runner = new ProcessRunner();
    private readonly CredentialVault _vault = new();
    private readonly SettingsStore _store = new();
    private readonly GitWorkspace _git;
    private readonly ThunderCli _thunder;
    private readonly SshService _ssh;
    private readonly ClientSettings _settings;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly string _lockDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThunderClient");
    private bool _busy;
    private string _message = "Noch nicht verbunden";
    private string _commit = "unbekannt";
    private string _selectionSummary = "Keine Workflows ausgewählt";
    private string _preview = "Konfiguration wird nach Synchronisierung angezeigt.";
    private string _hardware = "Hardwarestatus nicht geladen";
    private string _downloadStatus = "Bootstrap noch nicht gestartet";
    private string _downloadSummary = "Keine Downloadereignisse empfangen";
    private string _versionStatus = "Nicht geprüft";
    private string _snapshotName = "comfyui-backup";
    private string _gitTokenStatus = "";
    private string _hfTokenStatus = "";
    private string _civitaiTokenStatus = "";
    private InstanceItem? _selectedInstance;
    private WorkflowItem? _selectedWorkflow;
    private GpuChoice? _selectedGpu;
    private SnapshotItem? _selectedSnapshot;

    public ObservableCollection<WorkflowItem> Workflows { get; } = [];
    public ObservableCollection<GpuChoice> GpuChoices { get; } = [];
    public ObservableCollection<InstanceItem> Instances { get; } = [];
    public ObservableCollection<SnapshotItem> Snapshots { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];
    public ObservableCollection<DownloadItem> Downloads { get; } = [];

    public AsyncCommand SyncCommand { get; }
    public AsyncCommand RefreshInstancesCommand { get; }
    public AsyncCommand CreateCommand { get; }
    public AsyncCommand BootstrapCommand { get; }
    public AsyncCommand HardwareCommand { get; }
    public AsyncCommand SshTerminalCommand { get; }
    public AsyncCommand TunnelCommand { get; }
    public AsyncCommand SnapshotCommand { get; }
    public AsyncCommand RefreshSnapshotsCommand { get; }
    public AsyncCommand RestoreSnapshotCommand { get; }
    public AsyncCommand DeleteInstanceCommand { get; }
    public AsyncCommand SaveSettingsCommand { get; }
    public AsyncCommand UpdateCheckCommand { get; }
    public RelayCommand GpuUpCommand { get; }
    public RelayCommand GpuDownCommand { get; }
    public RelayCommand OpenReadmeCommand { get; }
    public RelayCommand ExportLogsCommand { get; }
    public RelayCommand ThunderLoginCommand { get; }

    public MainViewModel(IUserDialogs dialogs)
    {
        _dialogs = dialogs;
        _settings = _store.Load();
        _git = new GitWorkspace(_runner);
        _thunder = new ThunderCli(_runner);
        _ssh = new SshService(_runner);
        var available = new[]
        {
            new GpuChoice("a6000", "RTX A6000", 48, 6, 0.35m),
            new GpuChoice("l40", "L40", 48, 6, 0.79m),
            new GpuChoice("a100", "A100", 80, 8, 1.09m) { Enabled = false }
        };
        foreach (var gpu in available.OrderBy(x =>
            { var pos = _settings.GpuOrder.IndexOf(x.Key); return pos < 0 ? 999 : pos; }))
        {
            GpuChoices.Add(gpu);
            gpu.PropertyChanged += (_, _) => RefreshPreview();
        }
        SyncCommand = New(SyncAsync);
        RefreshInstancesCommand = New(() => RefreshInstancesAsync());
        CreateCommand = New(CreateAsync);
        BootstrapCommand = New(BootstrapAsync);
        HardwareCommand = New(HardwareAsync);
        SshTerminalCommand = New(OpenTerminalAsync);
        TunnelCommand = New(OpenComfyAsync);
        SnapshotCommand = New(CreateSnapshotAsync);
        RefreshSnapshotsCommand = New(RefreshSnapshotsAsync);
        RestoreSnapshotCommand = New(RestoreSnapshotAsync);
        DeleteInstanceCommand = New(DeleteInstanceAsync);
        SaveSettingsCommand = New(SaveSettingsAsync);
        UpdateCheckCommand = New(CheckForUpdatesAsync);
        GpuUpCommand = new RelayCommand(() => MoveSelectedGpu(-1));
        GpuDownCommand = new RelayCommand(() => MoveSelectedGpu(+1));
        OpenReadmeCommand = new RelayCommand(OpenReadme);
        ExportLogsCommand = new RelayCommand(() =>
            _dialogs.ExportText("thunderclient-diagnose.txt", string.Join(Environment.NewLine, LogLines)));
        ThunderLoginCommand = new RelayCommand(() =>
            _dialogs.Notify("Thunder-Anmeldung", "Bitte im Windows Terminal 'tnr login' ausführen. Der Client verwendet anschließend dieselbe Thunder-CLI-Anmeldung; keine Tokens in diese Oberfläche kopieren."));
        UpdateSecretStatuses();
        RefreshPreview();
    }

    private AsyncCommand New(Func<Task> handler) => new(handler, Error);

    public bool Busy { get => _busy; private set => Set(ref _busy, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public string Commit { get => _commit; private set => Set(ref _commit, value); }
    public string SelectionSummary { get => _selectionSummary; private set => Set(ref _selectionSummary, value); }
    public string Preview { get => _preview; private set => Set(ref _preview, value); }
    public string Hardware { get => _hardware; private set => Set(ref _hardware, value); }
    public string DownloadStatus { get => _downloadStatus; private set => Set(ref _downloadStatus, value); }
    public string DownloadSummary { get => _downloadSummary; private set => Set(ref _downloadSummary, value); }
    public string VersionStatus { get => _versionStatus; private set => Set(ref _versionStatus, value); }
    public string GitTokenStatus { get => _gitTokenStatus; private set => Set(ref _gitTokenStatus, value); }
    public string HfTokenStatus { get => _hfTokenStatus; private set => Set(ref _hfTokenStatus, value); }
    public string CivitaiTokenStatus { get => _civitaiTokenStatus; private set => Set(ref _civitaiTokenStatus, value); }
    public string SnapshotName { get => _snapshotName; set => Set(ref _snapshotName, value); }

    public string WorkflowsDirectory
    {
        get => _settings.WorkflowsDirectory;
        set { _settings.WorkflowsDirectory = value; OnPropertyChanged(); }
    }
    public int DiskGb
    {
        get => _settings.DiskGb;
        set { _settings.DiskGb = value; OnPropertyChanged(); RefreshPreview(); }
    }
    public int GpuCount
    {
        get => _settings.GpuCount;
        set { _settings.GpuCount = value; OnPropertyChanged(); RefreshPreview(); }
    }
    public decimal MaximumHourlyUsd
    {
        get => _settings.MaximumHourlyUsd;
        set { _settings.MaximumHourlyUsd = value; OnPropertyChanged(); RefreshPreview(); }
    }
    public string Template
    {
        get => _settings.Template;
        set { _settings.Template = value; OnPropertyChanged(); RefreshPreview(); }
    }
    public string Theme
    {
        get => _settings.Theme;
        set { _settings.Theme = value; OnPropertyChanged(); }
    }
    public InstanceItem? SelectedInstance
    {
        get => _selectedInstance;
        set { if (Set(ref _selectedInstance, value)) { OnPropertyChanged(nameof(InstanceSummary)); OnPropertyChanged(nameof(CostSummary)); } }
    }
    public WorkflowItem? SelectedWorkflow { get => _selectedWorkflow; set => Set(ref _selectedWorkflow, value); }
    public GpuChoice? SelectedGpu { get => _selectedGpu; set => Set(ref _selectedGpu, value); }
    public SnapshotItem? SelectedSnapshot { get => _selectedSnapshot; set => Set(ref _selectedSnapshot, value); }
    public string InstanceSummary => SelectedInstance is null ? "Keine Instanz ausgewählt" : SelectedInstance.Display;
    public string CostSummary
    {
        get
        {
            if (SelectedInstance is null) return "Keine laufende Instanz ausgewählt.";
            var gpu = GpuChoices.FirstOrDefault(x =>
                SelectedInstance.Gpu.Contains(x.Key, StringComparison.OrdinalIgnoreCase));
            var elapsed = SelectedInstance.Created is { } created
                ? DateTimeOffset.Now - created : (TimeSpan?)null;
            if (gpu is null || !elapsed.HasValue)
                return "Kosten nicht belastbar berechenbar (Tarif oder Startzeit unbekannt). Guthaben und Snapshotkosten nur in Thunder Console prüfen.";
            var rate = Estimates.Hourly(gpu, GpuCount, DiskGb);
            return $"Schätzung: {rate:N3} USD/h · Laufzeit {elapsed.Value.TotalHours:N1} h · ca. {rate * (decimal)Math.Max(0, elapsed.Value.TotalHours):N2} USD. Kein Abrechnungsstopp, keine Live-Preis-/Guthabenabfrage, ohne Snapshotkosten.";
        }
    }

    public async Task InitializeAsync()
    {
        await SyncAsync();
        try { await RefreshInstancesAsync(); }
        catch (Exception ex) { Error(ex); }
        try { await RefreshSnapshotsAsync(); }
        catch (Exception ex) { Error(ex); }
    }

    private void UpdateSecretStatuses()
    {
        GitTokenStatus = _vault.Exists("github") ? "Gespeichert: ••••••••" : "Nicht hinterlegt";
        HfTokenStatus = _vault.Exists("huggingface") ? "Gespeichert: ••••••••" : "Nicht hinterlegt";
        CivitaiTokenStatus = _vault.Exists("civitai") ? "Gespeichert: ••••••••" : "Nicht hinterlegt";
    }

    public void SaveSecret(string service, string value)
    {
        try
        {
            if (service is not ("github" or "huggingface" or "civitai"))
                throw new ArgumentOutOfRangeException(nameof(service));
            _vault.Save(service, value);
            UpdateSecretStatuses();
            Message = $"{service}: sicher im Windows Credential Manager gespeichert.";
        }
        catch (Exception ex) { Error(ex); }
    }

    public void DeleteSecret(string service)
    {
        try { _vault.Delete(service); UpdateSecretStatuses(); Message = $"{service}: Eintrag gelöscht."; }
        catch (Exception ex) { Error(ex); }
    }

    private async Task SyncAsync()
    {
        Busy = true;
        try
        {
            var sync = await _git.SynchronizeAsync(WorkflowsDirectory, _shutdown.Token);
            Commit = sync.Commit;
            LoadWorkflows();
            Message = sync.Detail;
            Log("Workflow-Repository: " + sync.Commit);
        }
        catch (Exception ex)
        {
            Log("GitHub-Synchronisierung fehlgeschlagen: " + RedactKnownSecrets(ex.Message));
            if (Directory.Exists(Path.Combine(WorkflowsDirectory, ".git")) &&
                _dialogs.Confirm("Offline-Modus", "GitHub-Synchronisierung fehlgeschlagen. Den letzten lokalen Workflow-Stand ausdrücklich im Offline-Modus verwenden?"))
            {
                Commit = await _git.CurrentCommitAsync(WorkflowsDirectory, _shutdown.Token);
                LoadWorkflows();
                Message = "OFFLINE: lokaler Stand " + Commit;
            }
            else throw;
        }
        finally { Busy = false; }
    }

    private void LoadWorkflows()
    {
        foreach (var previous in Workflows) previous.PropertyChanged -= WorkflowChanged;
        Workflows.Clear();
        var chosen = _settings.SelectedWorkflows.ToHashSet(StringComparer.Ordinal);
        foreach (var workflow in WorkflowCatalog.Load(WorkflowsDirectory))
        {
            workflow.IsSelected = chosen.Contains(workflow.Name);
            workflow.PropertyChanged += WorkflowChanged;
            Workflows.Add(workflow);
        }
        RefreshPreview();
    }

    private void WorkflowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowItem.IsSelected)) RefreshPreview();
    }

    public void MoveGpu(int from, int to)
    {
        if (from < 0 || to < 0 || from >= GpuChoices.Count || to >= GpuChoices.Count || from == to) return;
        GpuChoices.Move(from, to);
        RefreshPreview();
    }
    private void MoveSelectedGpu(int offset)
    {
        if (SelectedGpu is null) return;
        var index = GpuChoices.IndexOf(SelectedGpu);
        MoveGpu(index, index + offset);
    }

    private string WorkflowNames => string.Join(",", Workflows.Where(x => x.IsSelected).Select(x => x.Name));

    private void RefreshPreview()
    {
        var selected = Workflows.Where(x => x.IsSelected).ToArray();
        var size = WorkflowCatalog.Estimate(selected);
        var sizeLabel = size.TotalGb.HasValue ? $"{size.TotalGb:N2} GB" : "unvollständig";
        SelectionSummary = $"{selected.Length} Workflows · {size.TotalFiles} eindeutige Modelldateien · {sizeLabel}" +
            (size.FilesWithoutSize > 0 ? $" · {size.FilesWithoutSize} ohne Größe" : "") +
            (size.Collisions > 0 ? $" · WARNUNG: {size.Collisions} Modell-Zielkonflikte" : "");
        var sb = new StringBuilder();
        sb.AppendLine("WORKFLOWS=" + (WorkflowNames.Length > 0 ? WorkflowNames : "(keine Auswahl)"));
        sb.AppendLine("REPO_REF=main");
        sb.AppendLine($"Template: {Template} · {GpuCount} GPU(s) · {DiskGb} GB gesamte Instanzdisk");
        sb.AppendLine(Estimates.CapacityWarning(size.TotalGb, DiskGb));
        sb.AppendLine("Reihenfolge (Listenpreis-Schätzung vom 15.11.2026, keine Live-Abfrage):");
        foreach (var gpu in GpuChoices.Where(x => x.Enabled))
        {
            var rate = Estimates.Hourly(gpu, GpuCount, DiskGb);
            sb.AppendLine($"  {gpu.Label}, {gpu.Vcpus} vCPUs: ca. {rate:N3} USD/h " +
                (rate <= MaximumHourlyUsd ? "innerhalb" : "AUSSERHALB") + " der Schätzpreisgrenze");
        }
        sb.AppendLine($"Freigegebene Schätzpreisgrenze: {MaximumHourlyUsd:N2} USD/h. Kein serverseitiges Kostenlimit.");
        if (size.Collisions != 0) sb.AppendLine("ACHTUNG: widersprüchliche Dateiziele; Bootstrap nicht starten.");
        Preview = sb.ToString();
        OnPropertyChanged(nameof(CostSummary));
    }

    public async Task RefreshInstancesAsync(CancellationToken ct = default)
    {
        var items = await _thunder.ListAsync(ct);
        var selectedId = SelectedInstance?.Id;
        Instances.Clear();
        foreach (var item in items) Instances.Add(item);
        SelectedInstance = Instances.FirstOrDefault(x => x.Id == selectedId) ?? Instances.FirstOrDefault();
        Message = $"{Instances.Count} Thunder-Instanzen gefunden";
    }

    private async Task RefreshSnapshotsAsync()
    {
        var snapshots = await _thunder.ListSnapshotsAsync(_shutdown.Token);
        Snapshots.Clear();
        foreach (var snapshot in snapshots) Snapshots.Add(snapshot);
        SelectedSnapshot = Snapshots.FirstOrDefault();
    }

    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkflowNames))
            throw new InvalidOperationException("Mindestens einen Workflow auswählen.");
        var chosen = Workflows.Where(x => x.IsSelected).ToArray();
        var estimate = WorkflowCatalog.Estimate(chosen);
        if (estimate.Collisions > 0)
            throw new InvalidOperationException("Widersprüchliche Modell-Zieldateien erkannt. Start blockiert.");
        if (GpuCount is < 1 or > 8 || DiskGb is < 50 or > 2000 || MaximumHourlyUsd <= 0)
            throw new InvalidOperationException("GPU-Anzahl, Disk oder Schätzpreisgrenze ungültig.");
        if (!GpuChoices.Any(x => x.Enabled && Estimates.Hourly(x, GpuCount, DiskGb) <= MaximumHourlyUsd))
            throw new InvalidOperationException("Keine GPU innerhalb der freigegebenen Schätzpreisgrenze aktiviert.");
        if (!_dialogs.Confirm("Kostenpflichtige Instanz erstellen",
            Preview + "\nKostenschätzung für 1/4/12 Stunden: " +
            string.Join(" / ", GpuChoices.Where(x => x.Enabled).Take(1).SelectMany(gpu =>
            {
                var h = Estimates.Hourly(gpu, GpuCount, DiskGb);
                return new[] { (h * 1).ToString("N2"), (h * 4).ToString("N2"), (h * 12).ToString("N2") };
            })) + " USD (nur erste GPU).\n" +
            "Die Instanz kostet ab Erstellung Geld. Preisgrenze ist KEINE Abrechnungsbremse. Verbindlich jetzt kostenpflichtig erstellen?"))
            return;
        Busy = true;
        try
        {
            var instance = await _thunder.CreateWithFallbackAsync(GpuChoices.ToList(),
                GpuCount, DiskGb, Template, MaximumHourlyUsd, _lockDirectory, Log, _shutdown.Token);
            await RefreshInstancesAsync();
            SelectedInstance = Instances.FirstOrDefault(x => x.Id == instance.Id);
            Message = "Instanz erstellt: " + instance.Id;
            Log(Message);
        }
        finally { Busy = false; }
    }

    private async Task<SshConnection> TrustedSshAsync()
    {
        var item = SelectedInstance ?? throw new InvalidOperationException("Zuerst eine Instanz auswählen.");
        var endpoint = await _thunder.GetSshAsync(item, _shutdown.Token);
        if (!await _ssh.HasKnownHostAsync(endpoint, _shutdown.Token))
        {
            var scan = await _ssh.ScanHostKeyAsync(endpoint, _shutdown.Token);
            if (!_dialogs.ApproveFingerprint(endpoint.Host, scan.Fingerprint))
                throw new OperationCanceledException("SSH-Fingerprint nicht bestätigt.");
            _ssh.TrustHostKey(scan.KeyLine);
        }
        return endpoint;
    }

    private async Task HardwareAsync()
    {
        var ssh = await TrustedSshAsync();
        Hardware = await _ssh.CollectHardwareAsync(ssh, _shutdown.Token);
        Message = "Hardwarestatus über SSH abgerufen.";
    }

    private async Task BootstrapAsync()
    {
        var selected = Workflows.Where(x => x.IsSelected).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("Workflows auswählen.");
        if (WorkflowCatalog.Estimate(selected).Collisions > 0)
            throw new InvalidOperationException("Modelldatei-Konflikt. Installation blockiert.");
        var token = _vault.Read("github");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("GitHub-Token für privaten Remote-Bootstrap im Zugangsdaten-Tab hinterlegen.");
        var adapterPath = Path.Combine(WorkflowsDirectory, "thunder", "bootstrap.sh");
        if (!File.Exists(adapterPath)) throw new FileNotFoundException("Thunder-Bootstrap fehlt im Workflow-Checkout.", adapterPath);
        var ssh = await TrustedSshAsync();
        if (!_dialogs.Confirm("Bootstrap starten",
            "Der bestehende Thunder-Adapter aus dem synchronisierten Git-Checkout wird einmalig auf der ausgewählten Instanz ausgeführt. GPU-Kosten laufen. Vorhandene Installationsvorgänge oder einen Verbindungsabbruch vorher prüfen. Jetzt starten?"))
            return;
        Downloads.Clear();
        DownloadSummary = "Warte auf Downloadereignisse aus dem Bootstrap";
        DownloadStatus = "Installing / Downloading (Log prüfen)";
        Busy = true;
        try
        {
            var script = await File.ReadAllTextAsync(adapterPath, _shutdown.Token);
            var result = await _ssh.BootstrapAsync(ssh, script, WorkflowNames,
                token, _vault.Read("huggingface"), _vault.Read("civitai"),
                Log, _shutdown.Token);
            if (!result.Success)
            {
                DownloadStatus = "Failed / Unterbrochen: Remote-Zustand vor erneutem Start prüfen";
                throw new ExternalCommandException("Thunder-Bootstrap", result);
            }
            DownloadStatus = "Bootstrap-Prozess erfolgreich beendet (ComfyUI-Status separat prüfen)";
        }
        finally { Busy = false; }
    }

    private async Task OpenTerminalAsync()
    {
        var ssh = await TrustedSshAsync();
        _ssh.OpenTerminal(ssh);
        Message = "SSH-Terminal geöffnet.";
    }

    private async Task OpenComfyAsync()
    {
        var ssh = await TrustedSshAsync();
        _ssh.StartTunnel(ssh);
        await Task.Delay(600, _shutdown.Token);
        if (!_ssh.TunnelIsRunning) throw new InvalidOperationException("SSH-Tunnel ist vorzeitig beendet worden. ComfyUI wurde nicht geöffnet.");
        Process.Start(new ProcessStartInfo("http://127.0.0.1:8188/") { UseShellExecute = true });
        Message = "Privater SSH-Tunnel auf 127.0.0.1:8188 geöffnet.";
    }

    private async Task CreateSnapshotAsync()
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("Instanz auswählen.");
        if (!_dialogs.Confirm("Snapshot erstellen", $"Snapshot '{SnapshotName}' von Instanz {instance.Id} starten? Snapshot-Speicher kann Kosten verursachen."))
            return;
        await _thunder.CreateSnapshotAsync(instance, SnapshotName, _shutdown.Token);
        Message = "Snapshot angefordert. Erst nach Status READY verwenden.";
        await RefreshSnapshotsAsync();
    }

    private async Task RestoreSnapshotAsync()
    {
        var snapshot = SelectedSnapshot ?? throw new InvalidOperationException("Snapshot auswählen.");
        if (!snapshot.Status.Equals("READY", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Nur READY-Snapshots sind wiederherstellbar.");
        if (!_dialogs.Confirm("Snapshot als Template", $"Snapshot {snapshot.Name} als Quelle für die nächste Instanz setzen? Es wird noch KEINE Instanz erstellt.")) return;
        Template = snapshot.Name;
        if (DiskGb < snapshot.MinimumDiskGb) DiskGb = snapshot.MinimumDiskGb;
        await SaveSettingsAsync();
        Message = "Snapshot als Template übernommen. Kostenpflichtigen Start separat über Neue Instanz bestätigen.";
    }

    private async Task DeleteInstanceAsync()
    {
        var instance = SelectedInstance ?? throw new InvalidOperationException("Instanz auswählen.");
        if (!_dialogs.Confirm("Instanz endgültig löschen",
            $"Instanz {instance.Id} löschen? Ohne fertigen Snapshot gehen Modelle und Outputs auf der Instanzdisk verloren. Zuerst Backup prüfen. Fortfahren?")) return;
        if (!_dialogs.Confirm("Löschung bestätigen", $"Instanz {instance.Id} wirklich unwiderruflich löschen?")) return;
        await _thunder.DeleteAsync(instance, _shutdown.Token);
        _ssh.StopTunnel();
        await RefreshInstancesAsync();
        Message = $"Löschung von {instance.Id} angefordert.";
    }

    private async Task SaveSettingsAsync()
    {
        _settings.SelectedWorkflows = Workflows.Where(x => x.IsSelected).Select(x => x.Name).ToList();
        _settings.GpuOrder = GpuChoices.Select(x => x.Key).ToList();
        await _store.SaveAsync(_settings, _shutdown.Token);
        Message = "Einstellungen gespeichert (ohne Zugangsdaten).";
    }

    private async Task CheckForUpdatesAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/repos/thomasasen/thundercomputeclient/releases/latest");
        request.Headers.UserAgent.ParseAdd("ThunderClient/0.1.0");
        using var response = await client.SendAsync(request, _shutdown.Token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            VersionStatus = "Noch kein veröffentlichtes Release verfügbar.";
            return;
        }
        response.EnsureSuccessStatusCode();
        using var json = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(_shutdown.Token));
        var version = json.RootElement.GetProperty("tag_name").GetString() ?? "?";
        VersionStatus = $"Letztes veröffentlichtes Release: {version}. Prüfung nur; keine automatische Installation.";
    }

    private void OpenReadme()
    {
        if (SelectedWorkflow is null) return;
        Process.Start(new ProcessStartInfo(SelectedWorkflow.ReadmeUrl) { UseShellExecute = true });
    }

    private static string Sanitize(string text)
    {
        return Regex.Replace(text,
            @"(?i)(gh[pousr]_[a-zA-Z0-9_]{12,}|github_pat_[a-zA-Z0-9_]{12,}|hf_[a-zA-Z0-9]{12,}|Bearer\s+\S+)",
            "[GESCHWÄRZT]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    private string RedactKnownSecrets(string line)
    {
        var safe = Sanitize(line);
        foreach (var service in new[] { "github", "huggingface", "civitai" })
        {
            var secret = _vault.Read(service);
            if (!string.IsNullOrEmpty(secret)) safe = safe.Replace(secret, "[GESCHWÄRZT]", StringComparison.Ordinal);
        }
        return safe;
    }

    private void Log(string line)
    {
        var safe = RedactKnownSecrets(line);
        void Apply()
        {
            LogLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + safe);
            while (LogLines.Count > 600) LogLines.RemoveAt(0);
            if (safe.Contains("[DOWNLOAD-STATUS]", StringComparison.Ordinal)) DownloadStatus = safe;
            if (DownloadTracker.TryParse(safe, out var update) && update is not null)
            {
                var entry = Downloads.FirstOrDefault(x => x.Model == update.Model);
                if (entry is null)
                {
                    entry = new DownloadItem(update.Model, update.State, update.Progress);
                    Downloads.Add(entry);
                }
                else
                {
                    entry.State = update.State;
                    entry.Progress = update.Progress;
                }
                DownloadSummary = $"{Downloads.Count} Modelle protokolliert · " +
                    $"{Downloads.Count(x => x.State is "GELADEN" or "VORHANDEN")} abgeschlossen · " +
                    $"{Downloads.Count(x => x.State == "FEHLER")} fehlgeschlagen";
            }
        }
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        if (dispatcher.CheckAccess()) Apply();
        else dispatcher.BeginInvoke(Apply);
    }

    private void Error(Exception exception)
    {
        var safe = RedactKnownSecrets(exception.Message);
        Message = "Fehler: " + safe;
        Log(Message);
        _dialogs.Notify("Thunder Client", safe);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _ssh.StopTunnel();
        _shutdown.Dispose();
    }
}

