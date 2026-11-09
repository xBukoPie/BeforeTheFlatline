using Godot;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HttpClient = System.Net.Http.HttpClient;

using DiagnosticsProcess = System.Diagnostics.Process;

public partial class UpdateManager : Node
{
    public static UpdateManager Instance { get; private set; }

    private const string ReleasesApiUrl =
        "https://api.github.com/repos/xBukoPie/BeforeTheFlatline/releases?per_page=30";
    private const string ExecutableAssetName = "BeforeTheFlatline.exe";
    private const string DefaultVersion = "windows-preview-8";
    private const string DownloadRelativePath = "user://Updates/BeforeTheFlatline.download.exe";
    private const string InstallerRelativePath = "user://Updates/ApplyUpdate.ps1";

    private static readonly HttpClient ApiClient = CreateApiClient();
    private static readonly HttpClient DownloadClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private bool _checkStarted;
    private bool _downloadStarted;
    private ReleaseInfo _pendingRelease;
    private ConfirmationDialog _updateDialog;
    private AcceptDialog _messageDialog;
    private Window _progressWindow;
    private Label _progressLabel;
    private ProgressBar _progressBar;
    private CancellationTokenSource _downloadCancellation;

    private sealed record ReleaseInfo(
        string Tag,
        string DisplayName,
        string Notes,
        string DownloadUrl,
        string Sha256,
        long Size,
        long BuildNumber);

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _ExitTree()
    {
        _downloadCancellation?.Cancel();
        _downloadCancellation?.Dispose();
        _downloadCancellation = null;

        if (Instance == this)
            Instance = null;
    }

    public async void CheckForUpdatesOnMenuAsync()
    {
        if (_checkStarted || !CanCheckForUpdates())
            return;

        _checkStarted = true;

        try
        {
            await ToSignal(
                GetTree().CreateTimer(1.25, true),
                SceneTreeTimer.SignalName.Timeout);

            if (!IsInsideTree() || !IsMainMenuActive())
                return;

            ReleaseInfo release = await FindLatestPreviewReleaseAsync();
            if (release == null || release.BuildNumber <= GetInstalledBuildNumber())
                return;

            _pendingRelease = release;
            ShowUpdateConfirmation(release);
        }
        catch (Exception exception)
        {
            // Update checks are optional; network failure must never block the game.
            GD.PushWarning($"UpdateManager: update check failed: {exception.Message}");
        }
    }

    private static HttpClient CreateApiClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BeforeTheFlatline-Updater/1.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private bool CanCheckForUpdates()
    {
        return OS.GetName() == "Windows" &&
            !OS.HasFeature("editor") &&
            !Engine.IsEditorHint() &&
            !GetTree().Paused;
    }

    private bool IsMainMenuActive()
    {
        return string.Equals(
            GetTree()?.CurrentScene?.SceneFilePath,
            "res://Scenes/Menu/Main.tscn",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetInstalledVersion()
    {
        Variant configured = ProjectSettings.GetSetting(
            "application/config/version",
            DefaultVersion);
        string value = configured.AsString();
        return string.IsNullOrWhiteSpace(value) ? DefaultVersion : value.Trim();
    }

    private static long GetInstalledBuildNumber() =>
        ParsePreviewBuildNumber(GetInstalledVersion());

    private static long ParsePreviewBuildNumber(string tag)
    {
        const string prefix = "windows-preview-";
        if (string.IsNullOrWhiteSpace(tag) ||
            !tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return -1;

        return long.TryParse(tag.Substring(prefix.Length), out long number)
            ? number
            : -1;
    }

    private async Task<ReleaseInfo> FindLatestPreviewReleaseAsync()
    {
        using HttpResponseMessage response = await ApiClient.GetAsync(ReleasesApiUrl);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        ReleaseInfo latest = null;
        foreach (JsonElement release in document.RootElement.EnumerateArray())
        {
            if (ReadBoolean(release, "draft", true))
                continue;

            string tag = ReadString(release, "tag_name");
            long buildNumber = ParsePreviewBuildNumber(tag);
            if (buildNumber <= GetInstalledBuildNumber())
                continue;

            if (!release.TryGetProperty("assets", out JsonElement assets) ||
                assets.ValueKind != JsonValueKind.Array)
                continue;

            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (!string.Equals(
                    ReadString(asset, "name"),
                    ExecutableAssetName,
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                string url = ReadString(asset, "browser_download_url");
                string digest = ReadString(asset, "digest");
                long size = ReadInt64(asset, "size");

                if (string.IsNullOrWhiteSpace(url) ||
                    !Uri.TryCreate(url, UriKind.Absolute, out Uri assetUri) ||
                    assetUri.Scheme != Uri.UriSchemeHttps ||
                    !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                    size <= 0)
                    continue;

                digest = digest.Substring("sha256:".Length).Trim();
                if (digest.Length != 64)
                    continue;

                latest = new ReleaseInfo(
                    tag,
                    ReadString(release, "name"),
                    ReadString(release, "body"),
                    url,
                    digest,
                    size,
                    buildNumber);
                break;
            }
        }

        return latest;
    }

    private void ShowUpdateConfirmation(ReleaseInfo release)
    {
        if (_updateDialog != null && GodotObject.IsInstanceValid(_updateDialog))
            _updateDialog.QueueFree();

        string notes = string.IsNullOrWhiteSpace(release.Notes)
            ? "This release contains the latest published game build."
            : release.Notes.Trim();

        if (notes.Length > 800)
            notes = notes.Substring(0, 800).TrimEnd() + "...";

        _updateDialog = new ConfirmationDialog
        {
            Title = "Update available",
            DialogText =
                $"A new version of Before The Flatline is available.\n\n" +
                $"Installed: {GetInstalledVersion()}\n" +
                $"Available: {release.Tag}\n" +
                $"Download size: {FormatBytes(release.Size)}\n\n" +
                $"{notes}\n\n" +
                "Your saved games are kept separately and will not be removed.",
            Size = new Vector2I(620, 380)
        };
        _updateDialog.GetOkButton().Text = "Update now";
        _updateDialog.GetCancelButton().Text = "Later";
        _updateDialog.Confirmed += () => _ = DownloadAndInstallAsync(release);

        GetTree().Root.AddChild(_updateDialog);
        _updateDialog.PopupCentered();
    }

    private async Task DownloadAndInstallAsync(ReleaseInfo release)
    {
        if (_downloadStarted || release == null)
            return;

        _downloadStarted = true;
        _downloadCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _downloadCancellation.Token;

        string downloadPath = ProjectSettings.GlobalizePath(DownloadRelativePath);
        string installerPath = ProjectSettings.GlobalizePath(InstallerRelativePath);

        try
        {
            string targetExecutable = Path.GetFullPath(OS.GetExecutablePath());
            string targetDirectory = Path.GetDirectoryName(targetExecutable);

            if (string.IsNullOrWhiteSpace(targetDirectory) ||
                !System.IO.File.Exists(targetExecutable))
                throw new InvalidOperationException(
                    "The running executable path could not be resolved. Download the latest build manually.");

            if (!CanWriteDirectory(targetDirectory))
                throw new UnauthorizedAccessException(
                    "The game folder is not writable. Move BeforeTheFlatline.exe to a folder you own, " +
                    "such as Documents or Games, and retry the update.");

            Directory.CreateDirectory(Path.GetDirectoryName(downloadPath));
            Directory.CreateDirectory(Path.GetDirectoryName(installerPath));
            ShowDownloadProgress(release);

            using HttpResponseMessage response = await DownloadClient.GetAsync(
                release.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            long downloaded = 0;
            using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var destination = new FileStream(
                downloadPath,
                FileMode.Create,
                System.IO.FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[1024 * 1024];
                while (true)
                {
                    int count = await source.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);
                    if (count <= 0)
                        break;

                    await destination.WriteAsync(
                        buffer.AsMemory(0, count),
                        cancellationToken);
                    downloaded += count;
                    UpdateDownloadProgress(downloaded, release.Size);
                }

                await destination.FlushAsync(cancellationToken);
            }

            if (downloaded != release.Size)
                throw new InvalidDataException(
                    $"The download was incomplete ({FormatBytes(downloaded)} of {FormatBytes(release.Size)}).");

            SetProgressText("Verifying the downloaded update...");
            string actualHash;
            await using (var verificationStream = new FileStream(
                downloadPath,
                FileMode.Open,
                System.IO.FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.SequentialScan))
            {
                actualHash = Convert.ToHexString(
                    SHA256.HashData(verificationStream)).ToLowerInvariant();
            }

            if (!string.Equals(
                actualHash,
                release.Sha256,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The downloaded file failed its SHA-256 integrity check. The current game was not changed.");

            SetProgressText("Preparing the update. The game will close and restart...");
            await Task.Delay(650, cancellationToken);

            WriteInstallerScript(installerPath);
            StartExternalUpdater(
                installerPath,
                targetExecutable,
                downloadPath);

            GetTree().Quit();
        }
        catch (OperationCanceledException)
        {
            DeleteFileIfPresent(downloadPath);
            CloseProgressWindow();
            ShowMessage(
                "Update cancelled",
                "The update was cancelled. Your current game and saved data have not been changed.");
            _downloadStarted = false;
        }
        catch (Exception exception)
        {
            DeleteFileIfPresent(downloadPath);
            CloseProgressWindow();
            ShowMessage(
                "Update could not be installed",
                exception.Message + "\n\nYour current game and saved data have not been changed.");
            GD.PushError($"UpdateManager: update failed: {exception}");
            _downloadStarted = false;
        }
    }

    private void ShowDownloadProgress(ReleaseInfo release)
    {
        CloseProgressWindow();

        _progressWindow = new Window
        {
            Title = "Updating Before The Flatline",
            Size = new Vector2I(520, 170)
        };

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        _progressWindow.AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        margin.AddChild(stack);

        _progressLabel = new Label
        {
            Text = $"Downloading {release.Tag}...",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        stack.AddChild(_progressLabel);

        _progressBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = true,
            CustomMinimumSize = new Vector2(470, 26)
        };
        stack.AddChild(_progressBar);

        var cancel = new Button
        {
            Text = "Cancel",
            CustomMinimumSize = new Vector2(110, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
        };
        cancel.Pressed += () => _downloadCancellation?.Cancel();
        stack.AddChild(cancel);

        _progressWindow.CloseRequested += () => _downloadCancellation?.Cancel();
        GetTree().Root.AddChild(_progressWindow);
        _progressWindow.PopupCentered();
    }

    private void UpdateDownloadProgress(long downloaded, long total)
    {
        if (_progressLabel == null || !GodotObject.IsInstanceValid(_progressLabel))
            return;

        double percent = total > 0
            ? Math.Clamp(downloaded * 100.0 / total, 0.0, 100.0)
            : 0.0;

        _progressLabel.Text =
            $"Downloading update... {FormatBytes(downloaded)} / {FormatBytes(total)}";
        if (_progressBar != null && GodotObject.IsInstanceValid(_progressBar))
            _progressBar.Value = percent;
    }

    private void SetProgressText(string text)
    {
        if (_progressLabel != null && GodotObject.IsInstanceValid(_progressLabel))
            _progressLabel.Text = text;
    }

    private void ShowMessage(string title, string message)
    {
        if (_messageDialog != null && GodotObject.IsInstanceValid(_messageDialog))
            _messageDialog.QueueFree();

        _messageDialog = new AcceptDialog
        {
            Title = title,
            DialogText = message,
            Size = new Vector2I(560, 260)
        };
        GetTree().Root.AddChild(_messageDialog);
        _messageDialog.PopupCentered();
    }

    private void CloseProgressWindow()
    {
        _progressLabel = null;
        _progressBar = null;
        if (_progressWindow != null && GodotObject.IsInstanceValid(_progressWindow))
        {
            _progressWindow.Hide();
            _progressWindow.QueueFree();
        }
        _progressWindow = null;
    }

    private static bool CanWriteDirectory(string directory)
    {
        string probePath = Path.Combine(
            directory,
            $".beforetheflatline-update-check-{Guid.NewGuid():N}.tmp");

        try
        {
            using var probe = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(probePath))
                    System.IO.File.Delete(probePath);
            }
            catch
            {
                // A failed probe cleanup does not affect the game or its saves.
            }
        }
    }

    private static void WriteInstallerScript(string scriptPath)
    {
        const string script = @"
param(
    [int]$ProcessId,
    [string]$TargetExe,
    [string]$DownloadedExe
)

$ErrorActionPreference = 'Stop'
$target = [System.IO.Path]::GetFullPath($TargetExe)
$download = [System.IO.Path]::GetFullPath($DownloadedExe)
$temp = $target + '.installing'
$backup = $target + '.previous'
$deadline = [DateTime]::UtcNow.AddMinutes(5)

try {
    while (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
        if ([DateTime]::UtcNow -gt $deadline) {
            throw 'The game did not close in time. The original executable was left unchanged.'
        }
        Start-Sleep -Milliseconds 400
    }

    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
        throw 'The original game executable could not be found.'
    }
    if (-not (Test-Path -LiteralPath $download -PathType Leaf)) {
        throw 'The verified update download could not be found.'
    }

    Copy-Item -LiteralPath $download -Destination $temp -Force
    if (Test-Path -LiteralPath $backup) {
        Remove-Item -LiteralPath $backup -Force
    }

    Move-Item -LiteralPath $target -Destination $backup -Force
    try {
        Move-Item -LiteralPath $temp -Destination $target -Force
    }
    catch {
        if (Test-Path -LiteralPath $backup) {
            Move-Item -LiteralPath $backup -Destination $target -Force
        }
        throw
    }

    $workingDirectory = [System.IO.Path]::GetDirectoryName($target)
    Start-Process -FilePath $target -WorkingDirectory $workingDirectory

    Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $download -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
}
catch {
    if ((-not (Test-Path -LiteralPath $target)) -and (Test-Path -LiteralPath $backup)) {
        Move-Item -LiteralPath $backup -Destination $target -Force
    }
    Write-Error $_
    exit 1
}
";
        System.IO.File.WriteAllText(
            scriptPath,
            script,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void StartExternalUpdater(
        string installerPath,
        string targetExecutable,
        string downloadPath)
    {
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string powershellPath = Path.Combine(
            systemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

        if (!System.IO.File.Exists(powershellPath))
            throw new FileNotFoundException(
                "Windows PowerShell could not be found. Download the update manually from the GitHub Releases page.",
                powershellPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = powershellPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(installerPath);
        startInfo.ArgumentList.Add("-ProcessId");
        startInfo.ArgumentList.Add(DiagnosticsProcess.GetCurrentProcess().Id.ToString());
        startInfo.ArgumentList.Add("-TargetExe");
        startInfo.ArgumentList.Add(targetExecutable);
        startInfo.ArgumentList.Add("-DownloadedExe");
        startInfo.ArgumentList.Add(downloadPath);

        DiagnosticsProcess process = DiagnosticsProcess.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException(
                "The update helper could not be started. Your current game was not changed.");
    }

    private static void DeleteFileIfPresent(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"UpdateManager: could not remove temporary update file: {exception.Message}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        const double megabyte = 1024.0 * 1024.0;
        return $"{bytes / megabyte:0.0} MB";
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    private static bool ReadBoolean(JsonElement element, string property, bool fallback)
    {
        return element.TryGetProperty(property, out JsonElement value) &&
            (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                ? value.GetBoolean()
                : fallback;
    }

    private static long ReadInt64(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out long number)
                ? number
                : 0;
    }
}
