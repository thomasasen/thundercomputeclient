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
    private readonly GitHubWorkflowSource _github = new();
    private WorkflowSnapshot? _snapshot;
    private readonly ThunderCli _thunder;
    private readonly HttpClient _thunderPricingHttp = new(new HttpClientHandler { AllowAutoRedirect = false });
    private readonly ThunderPublicPricing _livePricing;
    private readonly SshService _ssh;
    private readonly SystemReadinessService _readiness;
    private readonly ClientSettings _settings;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly string _lockDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThunderClient");
    private bool _busy;
    private bool _autoDeploymentRunning;
    private bool _autoComfyReady;
    private bool _autoDeploymentVerified;
    private bool _autoVerificationStarted;
    private DeploymentReadinessGate? _deploymentGate;
    private SshConnection? _autoDeploymentSsh;
    private string _autoDeploymentWorkflows = "";
    private string _autoDeploymentCommit = "";
    private CancellationTokenSource? _autoDeploymentCancel;
    private string _autoDeploymentStatus = "Noch nicht gestartet · ein Knopfdruck erstellt den Pod und startet den bestehenden parallelen Bootstrap.";
    private string _message = "Noch nicht verbunden";
    private string _readinessSummary = "Noch keine Systemprüfung durchgeführt. Der Test wird nur auf Klick gestartet.";
    private bool _readinessRunning;
    private string _commit = "unbekannt";
    private string _gitSyncStatus = "Noch nicht synchronisiert";
    private string _selectionSummary = "Keine Workflows ausgewählt";
    private string _hardwareGuide = "Zuerst Workflows auswählen und Empfehlungen ansehen.";
    private string _storageGuide = "Die Speicherplanung wird aus den ausgewählten Manifesten ermittelt.";
    private string _wizardNotice = "1 · Katalog laden  →  2 · Workflows wählen  →  3 · Hardware prüfen  →  4 · Pod verwalten";
    private string _preview = "Konfiguration wird nach Synchronisierung angezeigt.";
    private string _hardware = "Hardwarestatus nicht geladen";
    private string _downloadStatus = "Bootstrap noch nicht gestartet";
    private string _downloadSummary = "Keine Downloadereignisse empfangen";
    private string _versionStatus = "Nicht geprüft";
    private string _lastRefreshed = "Instanzstatus nicht geladen";
    private Task? _monitorTask;
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
    // The GUI renders only a small sliding window. Keep a separate, sanitized,
    // much larger session history for manual export and incident diagnosis.
    private readonly List<string> _redactedLogHistory = [];
    private bool _logHistoryTruncated;
    private const int MaximumExportLogLines = 100_000;
    public ObservableCollection<DownloadItem> Downloads { get; } = [];
    public ObservableCollection<SetupCheck> SystemChecks { get; } = SystemReadinessService.CreateChecks();

    public event Action? PodCreated;
    public event Action? DeploymentLogsRequested;
    public RelayCommand ApplyHardwareGuidanceCommand { get; }
    public AsyncCommand SyncCommand { get; }
    public AsyncCommand RunSystemChecksCommand { get; }
    public AsyncCommand RefreshInstancesCommand { get; }
    public AsyncCommand CreateCommand { get; }
    public AsyncCommand AutoDeployCommand { get; }
    public RelayCommand CancelAutoDeploymentCommand { get; }
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
        _thunder = new ThunderCli(_runner);
        _livePricing = new ThunderPublicPricing(_thunderPricingHttp);
        _ssh = new SshService(_runner);
        _readiness = new SystemReadinessService(_runner, _vault.Read,
            (token, ct) => _github.FetchAsync(token, ct), () => SelectedInstance, _ssh);
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
        RunSystemChecksCommand = New(RunSystemChecksAsync);
        RefreshInstancesCommand = New(() => RefreshInstancesAsync());
        CreateCommand = New(CreateAsync);
        AutoDeployCommand = New(AutoDeployAsync);
        CancelAutoDeploymentCommand = new RelayCommand(CancelAutoDeployment);
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
        ApplyHardwareGuidanceCommand = new RelayCommand(ApplyHardwareGuidance);
        ExportLogsCommand = new RelayCommand(() =>
            _dialogs.ExportText("thunderclient-diagnose.txt",
                (_logHistoryTruncated
                    ? "[THUNDER-CLIENT] Hinweis: Die lokale Export-Historie ist auf 100.000 Zeilen begrenzt; das vollständige Remote-Bootstrap-Log bleibt auf dem Pod.\n"
                    : "") +
                string.Join(Environment.NewLine, _redactedLogHistory)));
        ThunderLoginCommand = new RelayCommand(() =>
            _dialogs.Notify("Thunder-Anmeldung", "Bitte im Windows Terminal 'tnr login' ausführen. Der Client verwendet anschließend dieselbe Thunder-CLI-Anmeldung; keine Tokens in diese Oberfläche kopieren."));
        UpdateSecretStatuses();
        RefreshPreview();
    }

    private AsyncCommand New(Func<Task> handler) => new(handler, Error);

    public bool Busy
    {
        get => _busy;
        private set
        {
            if (Set(ref _busy, value))
            {
                OnPropertyChanged(nameof(CanCreatePod));
                OnPropertyChanged(nameof(CanAutoDeploy));
            }
        }
    }
    public bool AutoDeploymentRunning
    {
        get => _autoDeploymentRunning;
        private set
        {
            if (Set(ref _autoDeploymentRunning, value))
            {
                OnPropertyChanged(nameof(CanAutoDeploy));
                OnPropertyChanged(nameof(CanCreatePod));
            }
        }
    }
    public string AutoDeploymentStatus
    {
        get => _autoDeploymentStatus;
        private set => Set(ref _autoDeploymentStatus, value);
    }
    public bool CanAutoDeploy => CanContinueToHardware && !Busy && !AutoDeploymentRunning;
    public string Message { get => _message; private set => Set(ref _message, value); }
    public string ReadinessSummary { get => _readinessSummary; private set => Set(ref _readinessSummary, value); }
    public bool ReadinessRunning { get => _readinessRunning; private set => Set(ref _readinessRunning, value); }

    public async Task RunSystemChecksAsync()
    {
        if (ReadinessRunning) return;
        if (Busy)
        {
            Message = "Andere Aktion läuft. Systemprüfung danach erneut starten.";
            return;
        }
        ReadinessRunning = true;
        ReadinessSummary = "Lesende Tests laufen: GitHub, Thunder, OpenSSH und gespeicherte Provider-Tokens.";
        try
        {
            await _readiness.CheckAllAsync(SystemChecks,
                _ => UpdateReadinessSummary(), _shutdown.Token);
            UpdateReadinessSummary();
            Message = "Systemstatus geprüft. Details und Einrichtungslinks im Status-Bereich.";
        }
        finally { ReadinessRunning = false; }
    }

    private void UpdateReadinessSummary()
    {
        var required = SystemChecks.Where(x => !x.Optional).ToArray();
        var verified = required.Count(x => x.Status is "Erfolgreich getestet" or "Vorhanden");
        var optional = SystemChecks.Count(x => x.Optional &&
            x.Status == "Erfolgreich getestet");
        ReadinessSummary = verified + "/" + required.Length +
            " erforderliche Komponenten vorhanden bzw. erfolgreich geprüft; " +
            optional + " optionale API(s) erfolgreich getestet. " +
            "„Vorhanden“ ist kein erfolgreicher Verbindungstest.";
    }

    private void ResetSecretCheck(string service)
    {
        var name = service switch
        {
            "github" => "github",
            "huggingface" => "huggingface",
            "civitai" => "civitai",
            _ => ""
        };
        var entry = SystemChecks.FirstOrDefault(x => x.Id == name);
        entry?.Update("Nicht geprüft", "Token geändert. Bitte erneut testen.");
        UpdateReadinessSummary();
    }
    public string Commit { get => _commit; private set => Set(ref _commit, value); }
    public string GitSyncStatus { get => _gitSyncStatus; private set => Set(ref _gitSyncStatus, value); }
    public string SelectionSummary { get => _selectionSummary; private set => Set(ref _selectionSummary, value); }
    public string HardwareGuide { get => _hardwareGuide; private set => Set(ref _hardwareGuide, value); }
    public string StorageGuide { get => _storageGuide; private set => Set(ref _storageGuide, value); }
    public string WizardNotice { get => _wizardNotice; private set => Set(ref _wizardNotice, value); }
    public bool CanContinueToHardware => _snapshot is not null &&
        Workflows.Any(x => x.IsSelected) &&
        WorkflowCatalog.Estimate(Workflows.Where(x => x.IsSelected)).Collisions == 0;
    public bool CanCreatePod => CanContinueToHardware && !Busy && !AutoDeploymentRunning;
    public Task ReloadCatalogAsync() => SyncAsync();
    public async Task PrepareHardwareStepAsync()
    {
        if (_snapshot is null)
            throw new InvalidOperationException("Zuerst den privaten Workflow-Katalog laden.");
        if (!Workflows.Any(x => x.IsSelected))
            throw new InvalidOperationException("Mindestens einen Workflow über die Auswahlspalte aktivieren.");
        if (WorkflowCatalog.Estimate(Workflows.Where(x => x.IsSelected)).Collisions > 0)
            throw new InvalidOperationException("Ausgewählte Workflows haben widersprüchliche Modelldateien. Auswahl korrigieren.");
        await SaveSettingsAsync();
        Message = "Workflow-Auswahl gespeichert. Hardwareempfehlungen prüfen und nach Bedarf ändern.";
    }

    public string Preview { get => _preview; private set => Set(ref _preview, value); }
    public string Hardware { get => _hardware; private set => Set(ref _hardware, value); }
    public string DownloadStatus { get => _downloadStatus; private set => Set(ref _downloadStatus, value); }
    public string DownloadSummary { get => _downloadSummary; private set => Set(ref _downloadSummary, value); }
    public string LogText => string.Join(Environment.NewLine, LogLines);
    public string VersionStatus { get => _versionStatus; private set => Set(ref _versionStatus, value); }
    public string LastRefreshed { get => _lastRefreshed; private set => Set(ref _lastRefreshed, value); }
    public string GitTokenStatus { get => _gitTokenStatus; private set => Set(ref _gitTokenStatus, value); }
    public string HfTokenStatus { get => _hfTokenStatus; private set => Set(ref _hfTokenStatus, value); }
    public string CivitaiTokenStatus { get => _civitaiTokenStatus; private set => Set(ref _civitaiTokenStatus, value); }
    public string SnapshotName { get => _snapshotName; set => Set(ref _snapshotName, value); }

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
        set { if (Set(ref _selectedInstance, value)) { OnPropertyChanged(nameof(InstanceSummary)); OnPropertyChanged(nameof(ConnectionSummary)); OnPropertyChanged(nameof(CostSummary)); UpdateWizardNotice(); } }
    }
    public WorkflowItem? SelectedWorkflow { get => _selectedWorkflow; set => Set(ref _selectedWorkflow, value); }
    public GpuChoice? SelectedGpu { get => _selectedGpu; set => Set(ref _selectedGpu, value); }
    public SnapshotItem? SelectedSnapshot { get => _selectedSnapshot; set => Set(ref _selectedSnapshot, value); }
    public string InstanceSummary => SelectedInstance is null ? "Keine Instanz ausgewählt" : SelectedInstance.Display;
    public string ConnectionSummary
    {
        get
        {
            var pod = SelectedInstance;
            if (pod is null) return "SSH: Bitte Pod auswählen. ComfyUI läuft privat über den SSH-Tunnel auf 127.0.0.1:8188.";
            var port = pod.Port is >= 1 and <= 65535
                ? pod.Port.ToString() : "noch nicht bereit";
            return "SSH: " + pod.Ip + ":" + port +
                " (Port von Thunder). ComfyUI: lokal 127.0.0.1:8188 per SSH-Tunnel; " +
                "eine öffentliche HTTP-Portfreigabe wird dafür nicht benötigt.";
        }
    }
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
            if (SelectedInstance.GpuCount <= 0 || SelectedInstance.StorageGb <= 0)
                return "Instanzdisk/GPU-Anzahl vom Anbieter nicht verfügbar; keine belastbare Kostenschätzung.";
            var rate = Estimates.Hourly(gpu, SelectedInstance.GpuCount, SelectedInstance.StorageGb);
            return $"Schätzung: {rate:N3} USD/h · Laufzeit {elapsed.Value.TotalHours:N1} h · ca. {rate * (decimal)Math.Max(0, elapsed.Value.TotalHours):N2} USD. Kein Abrechnungsstopp, keine Live-Preis-/Guthabenabfrage, ohne Snapshotkosten.";
        }
    }

    public string RedactForCopy(string text) => RedactKnownSecrets(text);

    public void ReportCopyStatus(string text) => Message = text;

    public void ReportStartupFailure(Exception exception)
    {
        var safe = RedactKnownSecrets(exception.Message);
        GitSyncStatus = "Privater GitHub-Zugriff fehlgeschlagen";
        Message = "GitHub-API: " + safe +
            " · Lesetoken unter Zugangsdaten prüfen und Katalog erneut aktualisieren. " +
            "Diagnose: " + StartupDiagnostics.LogPath;
    }

    public async Task InitializeAsync()
    {
        // Independent subsystems initialize even if the authenticated private GitHub API fails.
        try { await SyncAsync(); }
        catch (Exception ex)
        {
            StartupDiagnostics.Record("GitHub-Katalog", ex);
            ReportStartupFailure(ex);
        }
        try { await RefreshInstancesAsync(); }
        catch (Exception ex)
        {
            StartupDiagnostics.Record("Thunder-Instanzen", ex);
            Log("Thunder-Status beim Start nicht verfügbar: " + RedactKnownSecrets(ex.Message));
        }
        try { await RefreshSnapshotsAsync(); }
        catch (Exception ex)
        {
            StartupDiagnostics.Record("Thunder-Snapshots", ex);
            Log("Snapshot-Liste beim Start nicht verfügbar: " + RedactKnownSecrets(ex.Message));
        }
        _monitorTask = MonitorLoopAsync(_shutdown.Token);
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (Busy) continue;
                try { await RefreshInstancesAsync(ct); }
                catch (Exception ex) { Log("Status-Aktualisierung fehlgeschlagen: " + RedactKnownSecrets(ex.Message)); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void UpdateSecretStatuses()
    {
        GitTokenStatus = _vault.Exists("github") ? "Lesetoken gespeichert: ••••••••" : "Lesetoken fehlt: privater GitHub-Katalog nicht abrufbar";
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
            ResetSecretCheck(service);
            Message = $"{service}: sicher im Windows Credential Manager gespeichert.";
        }
        catch (Exception ex) { Error(ex); }
    }

    public void DeleteSecret(string service)
    {
        try { _vault.Delete(service); UpdateSecretStatuses(); ResetSecretCheck(service); Message = $"{service}: Eintrag gelöscht."; }
        catch (Exception ex) { Error(ex); }
    }

    private async Task SyncAsync()
    {
        Busy = true;
        try
        {
            var token = _vault.Read("github");
            var next = await _github.FetchAsync(token ?? "", _shutdown.Token);
            // Replace only after all manifests and the bootstrap passed validation.
            foreach (var previous in Workflows) previous.PropertyChanged -= WorkflowChanged;
            _snapshot = next;
            Commit = next.Commit;
            GitSyncStatus = "API-Lesezugriff · " + next.Commit[..12];
            LoadWorkflows();
            Message = next.Workflows.Count + " Workflows aus privater GitHub-Revision geladen.";
            UpdateWizardNotice();
            Log("GitHub-Katalog aktualisiert: " + next.Commit);
        }
        catch (Exception ex)
        {
            Log("GitHub-Katalog fehlgeschlagen: " + RedactKnownSecrets(ex.Message));
            if (_snapshot is not null &&
                _dialogs.Confirm("Bisheriger Stand (nur Arbeitsspeicher)",
                    "GitHub ist nicht erreichbar. Den zuletzt erfolgreich gelesenen Katalog dieser Sitzung verwenden? " +
                    "Keine neuen Inhalte werden geladen."))
            {
                Commit = _snapshot.Commit;
                GitSyncStatus = "Offline im Arbeitsspeicher · " + Commit[..12];
                Message = "OFFLINE: bisheriger Katalog aus dieser Sitzung, Revision " + Commit;
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
        foreach (var workflow in _snapshot?.Workflows ?? [])
        {
            workflow.IsSelected = chosen.Contains(workflow.Name);
            workflow.PropertyChanged += WorkflowChanged;
            Workflows.Add(workflow);
        }
        RefreshPreview();
        UpdateWizardNotice();
    }

    private void WorkflowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowItem.IsSelected))
        {
            RefreshPreview();
            UpdateWizardNotice();
        }
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
        var plan = HardwareRecommendations.Plan(selected);
        HardwareGuide = plan.GpuExplanation;
        StorageGuide = plan.StorageExplanation;
        OnPropertyChanged(nameof(CanContinueToHardware));
        OnPropertyChanged(nameof(CanCreatePod));
        OnPropertyChanged(nameof(CanAutoDeploy));
        var size = WorkflowCatalog.Estimate(selected);
        var sizeLabel = size.TotalGb.HasValue ? $"{size.TotalGb:N2} GB" : "unvollständig";
        SelectionSummary = $"{selected.Length} Workflows · {size.TotalFiles} eindeutige Modelldateien · {sizeLabel}" +
            (size.FilesWithoutSize > 0 ? $" · {size.FilesWithoutSize} ohne Größe" : "") +
            (size.Collisions > 0 ? $" · WARNUNG: {size.Collisions} Modell-Zielkonflikte" : "");
        var sb = new StringBuilder();
        sb.AppendLine("WORKFLOWS=" + (WorkflowNames.Length > 0 ? WorkflowNames : "(keine Auswahl)"));
        sb.AppendLine("REPO_REF=" + (_snapshot?.Commit ?? "unbekannt") +
            " (Remote-Bootstrap vor Ausführung SHA-gepinnt)");
        if (plan.ReferenceVramGb.HasValue)
            sb.AppendLine("RunPod-README-VRAM-Orientierung: " + plan.ReferenceVramGb +
                " GB. Keine getestete Thunder-Mindestanforderung.");
        if (plan.SuggestedDiskGb.HasValue && DiskGb < plan.SuggestedDiskGb.Value)
            sb.AppendLine("ACHTUNG: gewählte Disk unter konservativem Planungsansatz " +
                plan.SuggestedDiskGb + " GB; Mindestwert nicht vom Anbieter bestätigt.");
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

    private void UpdateWizardNotice()
    {
        var workflows = Workflows.Count;
        var selected = Workflows.Count(x => x.IsSelected);
        var pods = Instances.Count;
        WizardNotice = "Katalog: " + (workflows == 0 ? "noch nicht geladen" : workflows + " Workflows") +
            "  ·  Auswahl: " + selected + "  ·  Pods: " + pods;
        OnPropertyChanged(nameof(CanContinueToHardware));
        OnPropertyChanged(nameof(CanCreatePod));
    }

    private void ApplyHardwareGuidance()
    {
        var selected = Workflows.Where(x => x.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            Message = "Zuerst mindestens einen Workflow auswählen.";
            return;
        }
        var plan = HardwareRecommendations.Plan(selected);
        if (plan.SuggestedDiskGb.HasValue)
        {
            if (plan.SuggestedDiskGb.Value > 2000)
            {
                Message = "Speicherplanung über 2000 GB: Konfiguration manuell prüfen.";
                return;
            }
            // Do not silently reduce disk that the user has already chosen.
            DiskGb = Math.Max(DiskGb, plan.SuggestedDiskGb.Value);
        }
        var available = plan.ReferenceVramGb.HasValue && plan.CompleteVramReference
            ? GpuChoices.Where(x => x.VramGb >= plan.ReferenceVramGb.Value).ToArray()
            : Array.Empty<GpuChoice>();
        if (available.Length > 0)
        {
            foreach (var gpu in GpuChoices) gpu.Enabled = available.Contains(gpu);
            // Prefer the smallest candidate meeting the reference, not a price-based
            // inference. Actual Thunder pricing and compatibility are unverified.
            var ordered = available.OrderBy(x => x.VramGb).ToArray();
            for (var i = 0; i < ordered.Length; i++)
                GpuChoices.Move(GpuChoices.IndexOf(ordered[i]), i);
            Message = "Planungswerte übernommen. GPU-/Treiber-Kompatibilität, Preisgrenze " +
                "und tatsächliche Thunder-Verfügbarkeit manuell kontrollieren.";
        }
        else
            Message = "Speicherplanung soweit verfügbar übernommen. " +
                "Keine vollständige passende Thunder-VRAM-Auswahl ableitbar; GPU manuell prüfen.";
        RefreshPreview();
    }

    public async Task RefreshInstancesAsync(CancellationToken ct = default)
    {
        var items = await _thunder.ListAsync(ct);
        var selectedId = SelectedInstance?.Id;
        Instances.Clear();
        foreach (var item in items) Instances.Add(item);
        SelectedInstance = Instances.FirstOrDefault(x => x.Id == selectedId) ?? Instances.FirstOrDefault();
        LastRefreshed = $"Letzte Statusprüfung: {DateTime.Now:HH:mm:ss}";
        Message = $"{Instances.Count} Thunder-Instanzen gefunden";
        UpdateWizardNotice();
    }

    private async Task RefreshSnapshotsAsync()
    {
        var snapshots = await _thunder.ListSnapshotsAsync(_shutdown.Token);
        Snapshots.Clear();
        foreach (var snapshot in snapshots) Snapshots.Add(snapshot);
        SelectedSnapshot = Snapshots.FirstOrDefault();
    }

    private void SetAutoDeploymentStage(string stage)
    {
        AutoDeploymentStatus = stage;
        Log("[AUTO-DEPLOY] " + stage);
    }

    private void CancelAutoDeployment()
    {
        if (!AutoDeploymentRunning || _autoDeploymentCancel is null) return;
        if (!_dialogs.Confirm("Automatische Bereitstellung unterbrechen",
            "Nur die lokale Log-/SSH-Verbindung wird getrennt. " +
            "Der eigenständig gestartete Remote-Bootstrap läuft auf dem Pod weiter und " +
            "kann weiter GPU-Kosten verursachen. Später mit derselben Workflow-Auswahl " +
            "wieder verbinden; kein neuer Pod wird erstellt. Jetzt Verbindung trennen?"))
            return;
        _autoDeploymentCancel.Cancel();
    }

    private void ObserveAutomaticBootstrapLine(string line)
    {
        // ProcessRunner invokes callbacks from a reader thread. WPF state must
        // always be modified on the owning UI dispatcher.
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        void Apply()
        {
            Log(line);
            var gate = _deploymentGate;
            gate?.Observe(line);
            if (gate?.HasBlocker == true)
            {
                AutoDeploymentStatus = "Installation nicht vollständig: " + gate.Blocker +
                    " Pod kann weiter Kosten verursachen; Logs prüfen.";
                return;
            }
            if (line.Contains("[DOWNLOAD-STATUS] START:", StringComparison.Ordinal) ||
                line.Contains("[DOWNLOAD-STATUS] LAEUFT", StringComparison.Ordinal) ||
                line.Contains("[DOWNLOAD-STATUS] FORTSCHRITT", StringComparison.Ordinal))
                AutoDeploymentStatus = "Modelle und Nodes werden parallel eingerichtet (bis zu 4 Datei-Jobs, adaptive Providerlimits).";
            if (line.Contains("ComfyUI startet auf Port 8188", StringComparison.Ordinal))
                AutoDeploymentStatus = "Modelldownload-Phase durchlaufen · ComfyUI startet, HTTP-Bereitschaft wird geprüft.";
            if (line.Contains("phase=comfyui_http_ready", StringComparison.Ordinal))
            {
                if (line.Contains("status=ok", StringComparison.Ordinal))
                {
                    _autoComfyReady = true;
                    AutoDeploymentStatus = "ComfyUI antwortet auf HTTP. " +
                        "Prüfe jetzt den Abschluss aller Modelle, Nodes und Installationsphasen.";
                    Message = AutoDeploymentStatus;
                }
                else if (line.Contains("status=timeout", StringComparison.Ordinal))
                    AutoDeploymentStatus = "ComfyUI ist noch nicht per HTTP bestätigt. Pod-Logs prüfen.";
            }
            if (gate?.ReadyForAudit == true && !_autoVerificationStarted &&
                !_autoDeploymentVerified && _autoDeploymentSsh is not null)
            {
                _autoVerificationStarted = true;
                AutoDeploymentStatus = "ComfyUI ist gestartet. Prüfe jetzt alle " +
                    gate.ExpectedModels + " Modelle und die benötigten Node-Versionen " +
                    "direkt auf dem Pod.";
                _ = ConfirmFullReadinessAsync(_autoDeploymentSsh, gate.ExpectedModels,
                    _autoDeploymentCommit, _autoDeploymentWorkflows,
                    _autoDeploymentCancel?.Token ?? _shutdown.Token);
            }
        }
        if (dispatcher.CheckAccess()) Apply();
        else dispatcher.BeginInvoke(Apply);
    }

    private async Task ConfirmFullReadinessAsync(SshConnection endpoint,
        int expectedModels, string expectedCommit, string workflows,
        CancellationToken ct)
    {
        try
        {
            var audited = await _ssh.VerifyDeploymentReadyAsync(
                endpoint, expectedCommit, workflows, ct);
            if (ct.IsCancellationRequested || !AutoDeploymentRunning) return;
            if (!audited.Ready)
            {
                AutoDeploymentStatus =
                    "ComfyUI antwortet, aber die vollständige Installation ist nicht bestätigt: " +
                    audited.Reason + ". Unter Downloads & Logs prüfen.";
                Message = AutoDeploymentStatus;
                Log("[THUNDER-READINESS] Nicht vollständig: " + audited.Reason);
                return;
            }
            if (audited.Models != expectedModels)
            {
                AutoDeploymentStatus = "Vollständigkeit nicht bestätigt: Die installierte " +
                    "Modellanzahl stimmt nicht mit der ausgewählten GitHub-Revision überein.";
                Message = AutoDeploymentStatus;
                Log("[THUNDER-READINESS] Modellanzahl weicht von der Auswahl ab.");
                return;
            }
            _autoDeploymentVerified = true;
            AutoDeploymentStatus = "ComfyUI vollständig einsatzbereit: " +
                audited.Models + " Modelle und " + audited.Nodes +
                " Custom-Node-Pakete geprüft. Unter Instanzen ComfyUI öffnen.";
            Message = AutoDeploymentStatus;
            Log("[THUNDER-READINESS] Vollständig geprüft: " +
                audited.Models + " Modelle, " + audited.Nodes +
                " Custom-Node-Pakete, ComfyUI-HTTP und Node-Registrierung.");
            _dialogs.Notify("ComfyUI ist einsatzbereit",
                "Alle ausgewählten Modelle und benötigten Node-Pakete wurden " +
                "gegen die geprüfte GitHub-Revision kontrolliert. " +
                "ComfyUI antwortet und die Node-Klassen sind registriert.\n\n" +
                "Modelle: " + audited.Models + "\nCustom-Node-Pakete: " + audited.Nodes +
                "\n\nDu kannst ComfyUI jetzt unter Instanzen über den " +
                "privaten SSH-Tunnel öffnen. Eine echte Modell-Inferenz wurde " +
                "dadurch noch nicht durchgeführt.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!AutoDeploymentRunning || ct.IsCancellationRequested) return;
            AutoDeploymentStatus = "ComfyUI antwortet, aber der abschließende " +
                "Remote-Vollständigkeitscheck ist fehlgeschlagen. Keine Freigabe als vollständig bereit.";
            Message = AutoDeploymentStatus;
            Log("[THUNDER-READINESS] Prüfaufruf fehlgeschlagen: " + ex.GetType().Name);
        }
    }

    private async Task AutoDeployAsync()
    {
        if (AutoDeploymentRunning || Busy)
            throw new InvalidOperationException("Eine andere Bereitstellung läuft bereits.");
        if (_snapshot is null)
            throw new InvalidOperationException("Privaten Workflow-Katalog unter Übersicht laden.");
        var chosen = Workflows.Where(x => x.IsSelected).ToArray();
        var plan = AutoProvisioning.Plan(chosen, _snapshot.Commit, GpuChoices.ToArray(),
            GpuCount, DiskGb, Template, MaximumHourlyUsd);
        var githubToken = _vault.Read("github");
        if (string.IsNullOrWhiteSpace(githubToken))
            throw new InvalidOperationException(
                "GitHub-Lesetoken unter Zugangsdaten hinterlegen (privates Repository).");
        var hfToken = _vault.Read("huggingface");
        var civitaiToken = _vault.Read("civitai");
        if (chosen.SelectMany(x => x.Models).Any(x => x.Source == "civitai") &&
            string.IsNullOrWhiteSpace(civitaiToken))
            throw new InvalidOperationException(
                "Die ausgewählten Workflows enthalten Civitai-Modelle, aber kein " +
                "Civitai-Token ist hinterlegt. Für die automatische Vollinstallation " +
                "erst unter Zugangsdaten einen Civitai-API-Key speichern und im Systemstatus prüfen.");
        var adapter = _snapshot.BootstrapScript;
        // Validate the reviewed adapter's SHA-pinning contract BEFORE any
        // billable create, not after SSH access has been charged.
        _ = SshService.PinBootstrapAdapter(adapter, plan.Revision);
        // A selected set and its adapter are frozen before the first awaited
        // provider call; editing checkboxes during a running deploy cannot change it.
        var workflowsCsv = string.Join(",", plan.WorkflowNames);

        AutoDeploymentRunning = true;
        _autoComfyReady = false;
        _autoDeploymentVerified = false;
        _autoVerificationStarted = false;
        _autoDeploymentSsh = null;
        _deploymentGate = new DeploymentReadinessGate(chosen
            .SelectMany(x => x.Models)
            .GroupBy(x => x.TargetKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()), plan.Revision);
        _autoDeploymentWorkflows = workflowsCsv;
        _autoDeploymentCommit = plan.Revision;
        _autoDeploymentCancel = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        var ct = _autoDeploymentCancel.Token;
        string? podId = null;
        try
        {
            SetAutoDeploymentStage("Prüfe vorhandene Thunder-Pods und die geplante Hardware.");
            var existing = await _thunder.ListAsync(ct);
            if (existing.Count > 1)
                throw new InvalidOperationException(
                    "Mehrere Pods vorhanden: automatische Zuordnung nicht eindeutig. " +
                    "Pod in der Instanzverwaltung manuell auswählen. Es wird kein weiterer erstellt.");
            var reuse = existing.Count == 1;
            if (reuse)
            {
                var item = existing[0];
                if (item.StorageGb > 0 && item.StorageGb < plan.DiskGb)
                    throw new InvalidOperationException(
                        "Bestehender Pod hat nur " + item.StorageGb +
                        " GB Disk; für die gewählten Workflows sind " + plan.DiskGb +
                        " GB geplant. Keine Installation auf zu kleiner Disk.");
                if (item.Status is "STOPPED" or "TERMINATED" or "FAILED" or "ERROR")
                    throw new InvalidOperationException(
                        "Pod existiert, ist aber nicht nutzbar (" + item.Status +
                        "). Kein automatischer neuer Create-Versuch.");
                podId = item.Id;
            }

            // Confirm provider rates and hardware options before the first paid
            // create. A price API outage is NOT permission to use stale estimates.
            decimal cost;
            if (!reuse)
            {
                SetAutoDeploymentStage("Prüfe aktuelle Thunder-Preise und GPU-/vCPU-/Disk-Spezifikationen.");
                var quotes = await _livePricing.QuoteAsync(
                    plan.Candidates, plan.GpuCount, plan.DiskGb, ct);
                var approved = plan.Candidates.Where(x =>
                    quotes[x.Key].HourlyUsd <= plan.MaxEstimatedHourlyUsd).ToArray();
                if (approved.Length == 0)
                    throw new InvalidOperationException(
                        "Keine GPU erfüllt die bestätigte aktuelle Thunder-Preisgrenze. " +
                        "Kein kostenpflichtiger Pod wurde erstellt.");
                plan = plan with { Candidates = approved };
                cost = quotes[approved[0].Key].HourlyUsd;
                Log("[THUNDER-PRICING] Live-Tarife und Spezifikationen geprüft: " +
                    string.Join(", ", approved.Select(x =>
                        x.Key + " " + quotes[x.Key].HourlyUsd.ToString("N3") + " USD/h")));
            }
            else
                cost = plan.FirstHourlyEstimate;

            var offer =
                "Workflows: " + workflowsCsv + "\n" +
                "Quelle: private GitHub-Revision " + plan.Revision[..12] + "\n" +
                "Modelle (dedupliziert): " + plan.ModelGb.ToString("N2") + " GB\n" +
                "Hardware: " + string.Join(" → ", plan.Candidates.Select(x =>
                    x.Display + ", " + x.Vcpus + " vCPUs")) + "\n" +
                "GPU-Anzahl: " + plan.GpuCount + "\n" +
                "Gesamtdisk: " + plan.DiskGb + " GB · Template: " + plan.Template + "\n" +
                (reuse ? "Bestehender Pod, nicht live quotiert: " :
                    "Live-Quote (max. aus Anbieterpreis und konservativem Listenwert): ") +
                cost.ToString("N3") + " USD/h\n" +
                "Rechnerisch für 1 / 4 / 12 Stunden: " +
                    cost.ToString("N2") + " / " + (4 * cost).ToString("N2") +
                    " / " + (12 * cost).ToString("N2") + " USD\n" +
                "Maximale Schätzpreisgrenze: " + plan.MaxEstimatedHourlyUsd.ToString("N2") +
                " USD/h (kein serverseitiges Kostenlimit)\n\n" +
                plan.Warning + "\n\n" +
                "Danach startet der Client automatisch den bestehenden Thunder-Bootstrap " +
                "(bis zu 4 parallele Downloads), sobald SSH verifiziert ist. " +
                "Der Remote-Bootstrap läuft nach Schließen der GUI weiter; " +
                "bei erneutem Start wird sein Log wieder eingelesen. " +
                "Ein unbekannter SSH-Fingerprint braucht deine separate Bestätigung.\n\n" +
                (reuse
                    ? "Ein Pod existiert bereits (ID " + podId + "). KEIN neuer Pod wird erstellt. " +
                      "Falls der letzte SSH-Schlüssel-API-Aufruf in ein Zeitlimit lief, " +
                      "könnte er serverseitig dennoch verarbeitet worden sein. " +
                      "Ein erneuter Verbindungsversuch kann einen weiteren SSH-Schlüssel " +
                      "registrieren. Auf diesem kostenpflichtigen Pod fortfahren?"
                    : "Jetzt EINEN kostenpflichtigen Thunder-Pod erstellen und anschließend installieren?");
            if (!_dialogs.Confirm(reuse ? "Bestehenden Pod automatisch einrichten"
                    : "Pod automatisch erstellen und Modelle laden", offer))
            {
                AutoDeploymentStatus = "Abgebrochen · keine neue Aktion ausgeführt.";
                return;
            }

            await SaveSettingsAsync();
            Busy = true;
            InstanceItem target;
            if (reuse)
            {
                target = existing[0];
                SetAutoDeploymentStage("Vorhandenen Pod " + target.Id +
                    " verwenden. Keine zweite Instanz.");
            }
            else
            {
                SetAutoDeploymentStage("Erstelle genau einen Pod via Thunder-CLI · GPU-Fallback nur bei eindeutigem Kapazitätsfehler.");
                target = await _thunder.CreateWithFallbackAsync(
                    plan.Candidates, plan.GpuCount, plan.DiskGb, plan.Template,
                    plan.MaxEstimatedHourlyUsd, _lockDirectory, Log, ct);
                podId = target.Id;
                SetAutoDeploymentStage("Pod " + podId + " wurde angelegt. Warte auf SSH-IP und Port.");
            }

            target = await AutoProvisioning.WaitForRunningSshAsync(target.Id,
                c => _thunder.ListAsync(c),
                (delay, c) => Task.Delay(delay, c),
                line => Log("[AUTO-DEPLOY] " + line),
                tolerateMissing: !reuse, ct: ct);

            // Keep visible pod selection consistent with the status service.
            await RefreshInstancesAsync(ct);
            SelectedInstance = Instances.FirstOrDefault(x => x.Id == target.Id);
            if (SelectedInstance is null)
                throw new InvalidOperationException("Pod ist nicht mehr im Instanzstatus. Keine Neu-Erstellung.");
            PodCreated?.Invoke();
            DeploymentLogsRequested?.Invoke();
            SetAutoDeploymentStage("SSH-Endpunkt bereit: authentifiziere Schlüssel und prüfe den Hostfingerprint.");
            var ssh = await TrustedSshAsync(ct, SetAutoDeploymentStage);
            var probe = await _ssh.ProbeAuthenticationAsync(ssh, ct);
            if (!probe.Success)
                throw new InvalidOperationException(
                    "SSH-Anmeldung fehlgeschlagen (Exitcode " + probe.ExitCode +
                    "). Im Systemstatus SSH-Key, Netzwerk und bestätigten Hostfingerprint prüfen.");

            _autoDeploymentSsh = ssh;
            if (await _ssh.IsComfyUiReadyAsync(ssh, ct))
            {
                SetAutoDeploymentStage(
                    "Bereits laufendes ComfyUI erkannt. Prüfe jetzt die ausgewählten Modelle, " +
                    "Nodes und Workflows read-only; kein zweiter Bootstrap.");
                _autoVerificationStarted = true;
                await ConfirmFullReadinessAsync(ssh, _deploymentGate!.ExpectedModels,
                    plan.Revision, workflowsCsv, ct);
                return;
            }

            SetAutoDeploymentStage("Prüfe tatsächliche GPU-Anzahl, VRAM und freien Speicher auf dem Pod.");
            var capacity = await _ssh.CheckGpuAndFreeDiskAsync(ssh,
                plan.GpuCount, plan.ReferenceVramGb ?? 0, plan.ModelGb, ct);
            Log(capacity);

            Downloads.Clear();
            DownloadSummary = "Warte auf die ersten Downloadereignisse des bestehenden Bootstrap.";
            DownloadStatus = "Bootstrap startet · parallele HF-/Civitai-Downloads bis 4 Dateien.";
            SetAutoDeploymentStage("Starte privaten Thunder-Bootstrap mit ausgewählten Workflows. " +
                "Downloads laufen bereits während der Installation an.");
            // Re-enable normal status refresh and management while the long-running
            // ComfyUI process keeps this authenticated SSH stream open.
            Busy = false;
            var outcome = await _ssh.BootstrapAsync(ssh, adapter, workflowsCsv,
                plan.Revision, githubToken, hfToken, civitaiToken,
                ObserveAutomaticBootstrapLine, ct);
            if (!outcome.Success)
                throw new InvalidOperationException(
                    "Bootstrap-/ComfyUI-Verbindung endete mit Exitcode " + outcome.ExitCode +
                    ". Remote-Zustand und Downloadfehler prüfen; kein automatischer Neustart.");
            AutoDeploymentStatus = _autoComfyReady
                ? (_autoDeploymentVerified
                    ? "ComfyUI war vollständig geprüft, aber der Remote-Prozess ist inzwischen beendet. Pod-Status prüfen."
                    : "ComfyUI war HTTP-bereit, die vollständige Installation ist aber nicht bestätigt. Pod-Status und Logs prüfen.")
                : "SSH-Bootstrap ist beendet, aber HTTP-Bereitschaft nicht bestätigt. Logs prüfen.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            AutoDeploymentStatus = "Lokale SSH-/Logverbindung unterbrochen. " +
                (podId is null ? "Es wurde noch kein Pod bestätigt."
                    : "Pod " + podId + " läuft möglicherweise weiter und verursacht Kosten. " +
                      "Remote-Job beim nächsten Verbindungsaufbau prüfen und wieder anhängen.");
            Log("[AUTO-DEPLOY] " + AutoDeploymentStatus);
        }
        catch (OperationCanceledException ex)
        {
            // Some platform/CLI implementations throw TaskCanceledException
            // for their own internal deadline without cancelling our deploy
            // token. It is not a user-initiated stop. Preserve the stage.
            var lastStep = AutoDeploymentStatus;
            var timeoutMessage = "Ein interner Befehl hat während '" +
                lastStep + "' das Zeitlimit überschritten. " +
                "Es wurde kein weiterer Pod erstellt.";
            AutoDeploymentStatus = timeoutMessage +
                (podId is null ? "" :
                    " Pod " + podId + " kann weiter Kosten verursachen. " +
                    "Unter Instanzen vorhandenen Pod prüfen; nicht neu erstellen.");
            Log("[AUTO-DEPLOY] " + AutoDeploymentStatus);
            throw new TimeoutException(timeoutMessage, ex);
        }
        catch (ThunderSshKeyApiTimeoutException ex)
        {
            AutoDeploymentStatus =
                "Thunder hat die Registrierung des SSH-Schlüssels nicht beantwortet. " +
                (podId is null ? "" : "Pod " + podId + " besteht weiter und kann Kosten verursachen. ") +
                "Die Installation hat nicht begonnen. Keine zweite Instanz und " +
                "kein automatischer Registrierungsversuch. Unter Instanzen den " +
                "vorhandenen Pod prüfen und bei Bedarf eine erneute Verbindung bewusst starten.";
            Log("[AUTO-DEPLOY] " + AutoDeploymentStatus);
            // Error(ex) emits the sanitized root cause once, after this catch.
            throw;
        }
        catch (Exception ex)
        {
            var reason = ex is TimeoutException timeout
                ? RedactKnownSecrets(timeout.Message)
                : RedactKnownSecrets(ex.Message);
            var isHostKeyIssue =
                reason.Contains("SSH-Hostschlüssel", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("ssh-keyscan", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("Programm 'ssh'", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("Programm 'ssh-keygen'", StringComparison.OrdinalIgnoreCase);
            AutoDeploymentStatus = isHostKeyIssue
                ? "SSH-Hostschlüssel noch nicht bestätigt. " +
                  (podId is null ? "Keine Pod-ID bestätigt. " :
                      "Pod " + podId + " besteht weiterhin und kann Kosten verursachen. ") +
                  "Es wird kein weiterer Pod erstellt. Unter Instanzen den Status prüfen; " +
                  "technische Details stehen im kopierbaren Protokoll."
                : "Bereitstellung gestoppt: " + reason +
                  (podId is null ? "" :
                      " · Pod " + podId + " kann weiter Kosten verursachen; kein automatischer zweiter Create-Versuch.");
            Log("[AUTO-DEPLOY] " + AutoDeploymentStatus);
            if (isHostKeyIssue) Log("[SSH-DIAGNOSE] " + reason);
            throw;
        }
        finally
        {
            Busy = false;
            AutoDeploymentRunning = false;
            _autoDeploymentSsh = null;
            _autoDeploymentCancel?.Dispose();
            _autoDeploymentCancel = null;
        }
    }

    private async Task CreateAsync()
    {
        if (AutoDeploymentRunning)
            throw new InvalidOperationException("Automatische Bereitstellung läuft. Keine zweite Instanz erstellen.");
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
        // Quote before asking for the irreversible paid-create confirmation.
        var providerEligible = GpuChoices.Where(gpu => gpu.Enabled &&
            Estimates.Hourly(gpu, GpuCount, DiskGb) <= MaximumHourlyUsd).ToArray();
        var rates = await _livePricing.QuoteAsync(providerEligible, GpuCount, DiskGb, _shutdown.Token);
        var approvedChoices = providerEligible.Where(gpu =>
            rates[gpu.Key].HourlyUsd <= MaximumHourlyUsd).ToArray();
        if (approvedChoices.Length == 0)
            throw new InvalidOperationException(
                "Keine GPU erfüllt die aktuelle Thunder-Preisgrenze. Kein Pod erstellt.");
        var approvedFirst = rates[approvedChoices[0].Key].HourlyUsd;
        if (!_dialogs.Confirm("Kostenpflichtige Instanz erstellen",
            Preview + "\nLive-Tarife (GPU + zusätzliche vCPUs + Disk): " +
            string.Join(", ", approvedChoices.Select(gpu =>
                gpu.Key + " " + rates[gpu.Key].HourlyUsd.ToString("N3") + " USD/h")) +
            "\nKostenschätzung für 1/4/12 Stunden: " +
            string.Join(" / ", new[] { approvedFirst, 4 * approvedFirst, 12 * approvedFirst }
                .Select(x => x.ToString("N2"))) + " USD (erste GPU).\n" +
            "Kostenpflichtig ab Create; dies ist keine serverseitige Abrechnungsbremse. Jetzt erstellen?"))
            return;
        Busy = true;
        try
        {
            var instance = await _thunder.CreateWithFallbackAsync(approvedChoices,
                GpuCount, DiskGb, Template, MaximumHourlyUsd, _lockDirectory, Log, _shutdown.Token);
            await RefreshInstancesAsync();
            SelectedInstance = Instances.FirstOrDefault(x => x.Id == instance.Id);
            Message = "Instanz erstellt: " + instance.Id;
            PodCreated?.Invoke();
            Log(Message);
        }
        finally { Busy = false; }
    }

    private async Task<SshConnection> TrustedSshAsync(CancellationToken ct = default,
        Action<string>? progress = null)
    {
        var item = SelectedInstance ?? throw new InvalidOperationException("Zuerst eine Instanz auswählen.");
        var token = ct == default ? _shutdown.Token : ct;
        progress?.Invoke("Prüfe vorhandene lokale SSH-Schlüssel ohne Thunder-API-Aufruf.");
        var existing = _thunder.GetExistingSsh(item);
        progress?.Invoke(existing is null
            ? "Kein lokaler Schlüssel für diesen Pod vorhanden; tnr connect kann eine " +
              "neue Schlüsselregistrierung beim Anbieter auslösen."
            : "Vorhandener privater SSH-Schlüssel gefunden: Thunder /add_key wird nicht aufgerufen.");
        var endpoint = existing ?? await _thunder.GetSshAsync(item, token);
        progress?.Invoke("Prüfe gespeichertes SSH-Hostvertrauen (maximal 10 Sekunden).");
        if (!await _ssh.HasKnownHostAsync(endpoint, token))
        {
            var scan = await _ssh.ScanHostKeyAsync(endpoint, token, progress);
            progress?.Invoke("SSH-Fingerprint muss jetzt unabhängig geprüft und bestätigt werden.");
            if (!_dialogs.ApproveFingerprint(endpoint.Host, scan.Fingerprint))
                throw new OperationCanceledException("SSH-Fingerprint nicht bestätigt.");
            _ssh.TrustHostKey(scan.KeyLine);
        }
        progress?.Invoke("SSH-Hostvertrauen geprüft. Starte sicheren SSH-Anmeldetest.");
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
        if (AutoDeploymentRunning)
            throw new InvalidOperationException("Automatische Bereitstellung läuft; Bootstrap nicht ein zweites Mal starten.");
        var selected = Workflows.Where(x => x.IsSelected).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("Workflows auswählen.");
        if (WorkflowCatalog.Estimate(selected).Collisions > 0)
            throw new InvalidOperationException("Modelldatei-Konflikt. Installation blockiert.");
        var token = _vault.Read("github");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("GitHub-Token für privaten Remote-Bootstrap im Zugangsdaten-Tab hinterlegen.");
        if (_snapshot is null)
            throw new InvalidOperationException("Zuerst den privaten GitHub-Katalog erfolgreich laden.");
        var script = _snapshot.BootstrapScript;
        var ssh = await TrustedSshAsync();
        if (!_dialogs.Confirm("Bootstrap starten",
            "Der Thunder-Adapter aus der geprüften privaten GitHub-Revision wird einmalig auf der ausgewählten Instanz ausgeführt. GPU-Kosten laufen. Vorhandene Installationsvorgänge oder einen Verbindungsabbruch vorher prüfen. Jetzt starten?"))
            return;
        Downloads.Clear();
        DownloadSummary = "Warte auf Downloadereignisse aus dem Bootstrap";
        DownloadStatus = "Installing / Downloading (Log prüfen)";
        Busy = true;
        try
        {
            var result = await _ssh.BootstrapAsync(ssh, script, WorkflowNames,
                _snapshot.Commit, token, _vault.Read("huggingface"),
                _vault.Read("civitai"), Log, _shutdown.Token);
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
        if (AutoDeploymentRunning)
            throw new InvalidOperationException("Bereitstellung läuft. Erst ausdrücklich abbrechen und den Remote-Zustand prüfen.");
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
            var dated = DateTime.Now.ToString("HH:mm:ss") + "  " + safe;
            _redactedLogHistory.Add(dated);
            if (_redactedLogHistory.Count > MaximumExportLogLines)
            {
                _redactedLogHistory.RemoveRange(0, 1000);
                _logHistoryTruncated = true;
            }
            LogLines.Add(dated);
            while (LogLines.Count > 600) LogLines.RemoveAt(0);
            OnPropertyChanged(nameof(LogText));
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
        if (exception is ThunderSshKeyApiTimeoutException)
        {
            var pod = SelectedInstance?.Id;
            Message = "Thunder konnte den SSH-Schlüssel nicht rechtzeitig einrichten. " +
                (string.IsNullOrWhiteSpace(pod) ? "Die vorhandene Instanz " :
                    "Pod " + pod + " ") +
                "kann weiter Kosten verursachen. Modelle wurden noch nicht heruntergeladen.";
            Log("[THUNDER-API] " + safe);
            _dialogs.Notify("Thunder-API: SSH-Zugang noch nicht bereit",
                Message + "\n\n" +
                "Die Thunder-API hat bei der Schlüsselregistrierung 30 Sekunden " +
                "lang nicht geantwortet. Es ist unbekannt, ob die Registrierung " +
                "serverseitig bereits erfolgte.\n\n" +
                "1. Unter Instanzen den bereits vorhandenen Pod prüfen.\n" +
                "2. Bevor ein weiterer API-Registrierungsversuch erfolgt: " +
                "einen bereits autorisierten privaten SSH-Schlüssel direkt nutzen. " +
                "Dessen absoluten Dateipfad als Windows-Benutzervariable " +
                "THUNDERCLIENT_SSH_KEY eintragen und die App neu starten. " +
                "Der öffentliche Schlüssel muss auf genau diesem Pod hinterlegt sein.\n" +
                "3. Ist kein passender Schlüssel vorhanden, die dokumentierte " +
                "Thunder-API mit ausdrücklich angegebenem public_key bzw. den " +
                "Thunder-Support prüfen. Ein weiterer normaler tnr-connect-Versuch " +
                "kann sonst erneut einen Schlüssel erzeugen.\n\n" +
                "Falls der Pod nicht gebraucht wird, seine Löschung unter " +
                "Instanzen separat bestätigen, damit keine weiteren Kosten entstehen.");
            return;
        }
        if (safe.Contains("SSH-Hostschlüssel", StringComparison.OrdinalIgnoreCase) ||
            safe.Contains("ssh-keyscan", StringComparison.OrdinalIgnoreCase) ||
            safe.Contains("Programm 'ssh'", StringComparison.OrdinalIgnoreCase) ||
            safe.Contains("Programm 'ssh-keygen'", StringComparison.OrdinalIgnoreCase))
        {
            var pod = SelectedInstance?.Id;
            Message = "SSH-Verbindung noch nicht möglich. " +
                (string.IsNullOrWhiteSpace(pod) ? "Eine bestehende Instanz " :
                    "Pod " + pod + " ") +
                "kann weiterhin Kosten verursachen. Kein neuer Pod wurde durch den " +
                "fehlgeschlagenen Verbindungsversuch erstellt.";
            Log("[SSH-DIAGNOSE] " + safe);
            _dialogs.Notify("SSH-Hostschlüssel nicht bestätigt",
                Message + "\n\n" +
                "Die Einrichtung wurde vor den Modelldownloads gestoppt. " +
                "Die SSH-Hostprüfung bleibt aktiv.\n\n" +
                "1. Unter Instanzen den Pod-Status prüfen.\n" +
                "2. Unter Systemstatus Windows OpenSSH prüfen.\n" +
                "3. Bei anhaltendem Fehler unter Downloads & Logs das Protokoll kopieren.\n\n" +
                "Falls der Pod nicht weiter benötigt wird, unter Instanzen " +
                "die kostenpflichtige Instanz nach Prüfung bewusst löschen.");
            return;
        }
        Message = "Fehler: " + safe;
        Log(Message);
        _dialogs.Notify("Thunder Client", safe);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _autoDeploymentCancel?.Cancel();
        _ssh.StopTunnel();
        _readiness.Dispose();
        _github.Dispose();
        _thunderPricingHttp.Dispose();
        _shutdown.Dispose();
    }
}

