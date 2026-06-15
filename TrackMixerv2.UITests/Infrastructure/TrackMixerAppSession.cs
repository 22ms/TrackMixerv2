using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Diagnostics;
using System.Threading;
using TrackMixerv2;

namespace TrackMixerv2.UITests.Infrastructure;

public sealed class TrackMixerAppSession : IDisposable
{
    private readonly string? _clipPath;
    private readonly string? _clipDirectory;
    private readonly bool _deleteClipDirectoryOnDispose;
    private readonly Dictionary<string, string> _extraEnvironment = new(StringComparer.Ordinal);
    private string? _ownedTestHomeDirectory;

    public UIA3Automation Automation { get; } = new();
    public Application Application { get; private set; } = null!;
    public Window MainWindow { get; private set; } = null!;

    public string? ClipPath => _clipPath;

    public TrackMixerAppSession(
        string? clipPath = null,
        string? libraryRoot = null,
        bool deleteClipDirectoryOnDispose = true)
    {
        _clipPath = clipPath;
        _clipDirectory = clipPath == null ? libraryRoot : Path.GetDirectoryName(clipPath);
        _deleteClipDirectoryOnDispose = deleteClipDirectoryOnDispose;
    }

    public void SetTestStorageDirectory(string testHome)
    {
        Directory.CreateDirectory(testHome);
        _extraEnvironment[LocalSettingsStore.JsonPathEnvVar] =
            Path.Combine(testHome, "local_settings.json");
        _extraEnvironment[AppState.TrackMetadataJsonEnvVar] =
            Path.Combine(testHome, "track_metadata.json");
    }

    public void Launch()
    {
        EnsureIsolatedTestStorage();
        KillExistingInstances();

        string exePath = TrackMixerPaths.ResolveExePath();
        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
        };

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            string key = entry.Key.ToString()!;
            startInfo.Environment[key] = entry.Value?.ToString() ?? string.Empty;
        }

        foreach (KeyValuePair<string, string> entry in _extraEnvironment)
            startInfo.Environment[entry.Key] = entry.Value;

        startInfo.Environment[UiTestBootstrap.EnabledEnvVar] = "1";
        startInfo.Environment[UiTestBootstrap.SuppressRootPromptEnvVar] = "1";

        if (!string.IsNullOrWhiteSpace(_clipPath))
            startInfo.Environment[UiTestBootstrap.LaunchFileEnvVar] = _clipPath;

        if (!string.IsNullOrWhiteSpace(_clipDirectory))
            startInfo.Environment[UiTestBootstrap.RootFolderEnvVar] = _clipDirectory;

        Application = Application.Launch(startInfo);

        MainWindow = UiWait.Until(
            FindMainWindow,
            TimeSpan.FromSeconds(30),
            "main application window");

        MainWindow.Focus();
    }

    public void Relaunch()
    {
        Close();
        Launch();
    }

    public void Close()
    {
        try
        {
            Application?.Kill();
        }
        catch
        {
        }

        KillExistingInstances();
    }

    public AutomationElement? FindByAutomationId(string automationId, AutomationElement? root = null)
    {
        root ??= MainWindow;
        return root.FindFirstDescendant(Automation.ConditionFactory.ByAutomationId(automationId));
    }

    public int CountByAutomationId(string automationId, AutomationElement? root = null)
    {
        root ??= MainWindow;
        return root.FindAllDescendants(Automation.ConditionFactory.ByAutomationId(automationId)).Length;
    }

    public bool WindowTitleContains(string text) =>
        MainWindow.Title.Contains(text, StringComparison.OrdinalIgnoreCase);

    public bool VideoTitleContains(string text)
    {
        var videoTitle = FindByAutomationId("VideoTitle");
        if (videoTitle == null)
            return false;

        if (videoTitle.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        string description = videoTitle.Properties.FullDescription.ValueOrDefault;
        if (!string.IsNullOrEmpty(description) &&
            description.Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            string value = videoTitle.Patterns.Value.PatternOrDefault?.Value.Value ?? string.Empty;
            return value.Contains(text, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void ClickNextTrack()
    {
        var nextTrack = FindByAutomationId("NextTrackButton")?.AsButton()
            ?? throw new InvalidOperationException("Next track button was not found.");
        nextTrack.Invoke();
    }

    public void ConfirmDeleteCurrentVideo()
    {
        var deleteButton = FindByAutomationId("DeleteVideoButton")?.AsButton()
            ?? throw new InvalidOperationException("Delete video button was not found.");
        deleteButton.Invoke();

        var confirmButton = UiWait.Until(
            () => FindByAutomationId("DeleteVideoConfirmButton")?.AsButton()
                ?? FindConfirmDeleteYesButton(),
            TimeSpan.FromSeconds(10),
            "delete confirmation Yes button");

        confirmButton.Invoke();
    }

    public bool MixerPageIsLoaded() => FindByAutomationId("RatingSlider") != null;

    /// <summary>
    /// Invokes the central play/pause button in the media transport controls.
    /// </summary>
    public void ClickPlayPause()
    {
        var btn = FindByAutomationId("PlayPauseButton")?.AsButton()
            ?? throw new InvalidOperationException("PlayPauseButton not found in the automation tree.");
        btn.Invoke();
    }

    /// <summary>
    /// Returns true when the transport controls report the player is in the playing state.
    /// The PlayPauseButton accessible name is set to "Pause" (the action that would stop it)
    /// when media is actively playing, and "Play" when paused.
    /// </summary>
    public bool IsPlaying()
    {
        var btn = FindByAutomationId("PlayPauseButton");
        if (btn == null)
            return false;
        string name = btn.Properties.Name.ValueOrDefault ?? string.Empty;
        return name.Equals("Pause", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures the player is paused. If it is currently playing, clicks pause and waits.
    /// </summary>
    public void EnsurePaused()
    {
        if (!IsPlaying())
            return;
        ClickPlayPause();
        UiWait.UntilTrue(() => !IsPlaying(), TimeSpan.FromSeconds(10), "player to reach paused state");
    }

    /// <summary>
    /// Minimizes the main window to simulate the application losing foreground focus.
    /// </summary>
    public void MinimizeWindow()
    {
        MainWindow.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);
    }

    /// <summary>
    /// Restores the main window from a minimized state and brings it to the foreground.
    /// </summary>
    public void RestoreWindow()
    {
        MainWindow.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        Thread.Sleep(300);
        MainWindow.Focus();
    }

    public void ClickKeybindCell(string action)
    {
        // The cell is a Border (not surfaced in the UIA control view), but its content is
        // hit-test transparent, so clicking the shortcut label's screen location lands on
        // the underlying clickable cell and starts recording.
        var target = FindByAutomationId($"KeybindCell_{action}")
            ?? FindByAutomationId($"KeybindShortcut_{action}")
            ?? throw new InvalidOperationException($"Keybind cell '{action}' was not found.");
        target.Click();
    }

    public string? GetKeybindShortcut(string action) =>
        FindByAutomationId($"KeybindShortcut_{action}")?.Name;

    public int GetVolumeSliderCount()
    {
        int count = 0;
        for (int i = 0; i < TrackMixerPaths.FixtureAudioTrackCount + 2; i++)
        {
            if (FindByAutomationId($"VolumeSlider_{i}") != null)
                count++;
        }

        return count;
    }

    public int GetTabCount()
    {
        var tabView = FindByAutomationId("MainTabView");
        if (tabView == null)
            return 0;

        int tabItems = tabView.FindAllDescendants(Automation.ConditionFactory.ByControlType(ControlType.TabItem)).Length;
        if (tabItems > 0)
            return tabItems;

        int listItems = tabView.FindAllDescendants(Automation.ConditionFactory.ByControlType(ControlType.ListItem)).Length;
        return listItems;
    }

    /// <summary>
    /// Clicks the first tab header item in the TabView, moving keyboard focus away from
    /// the player area. This simulates the common state where a user has interacted with
    /// the tab strip rather than the media controls.
    /// </summary>
    public void ClickTabHeader()
    {
        var tabView = FindByAutomationId("MainTabView")
            ?? throw new InvalidOperationException("MainTabView not found.");

        AutomationElement[] items =
            tabView.FindAllDescendants(Automation.ConditionFactory.ByControlType(ControlType.TabItem));

        if (items.Length == 0)
            items = tabView.FindAllDescendants(Automation.ConditionFactory.ByControlType(ControlType.ListItem));

        if (items.Length == 0)
            throw new InvalidOperationException("No tab items found inside MainTabView.");

        items[0].Click();
    }

    public void Dispose()
    {
        Close();
        Automation.Dispose();

        if (!string.IsNullOrWhiteSpace(_ownedTestHomeDirectory) &&
            Directory.Exists(_ownedTestHomeDirectory))
        {
            try { Directory.Delete(_ownedTestHomeDirectory, recursive: true); } catch { }
        }

        if (_deleteClipDirectoryOnDispose &&
            !string.IsNullOrWhiteSpace(_clipDirectory) &&
            Directory.Exists(_clipDirectory))
        {
            try { Directory.Delete(_clipDirectory, recursive: true); } catch { }
        }
    }

    private void EnsureIsolatedTestStorage()
    {
        if (_extraEnvironment.ContainsKey(LocalSettingsStore.JsonPathEnvVar))
            return;

        _ownedTestHomeDirectory = TrackMixerPaths.CreateIsolatedTestHome();
        SetTestStorageDirectory(_ownedTestHomeDirectory);
    }

    private static void KillExistingInstances()
    {
        foreach (var process in Process.GetProcessesByName("TrackMixerv2"))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
            }
            catch
            {
            }
        }
    }

    private Window? FindMainWindow()
    {
        DismissBlockingDialogs();

        try
        {
            Window? mainWindow = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(2));
            if (mainWindow != null)
                return mainWindow;
        }
        catch
        {
        }

        Window? namedWindow = FindWindowByName("Track Mixer UI Test")
            ?? FindWindowByName("Track Mixer");
        if (namedWindow != null)
            return namedWindow;

        return Application.GetAllTopLevelWindows(Automation).FirstOrDefault();
    }

    private Window? FindWindowByName(string windowName)
    {
        var desktop = Automation.GetDesktop();
        var element = desktop.FindFirstDescendant(Automation.ConditionFactory.ByName(windowName));
        return element?.AsWindow();
    }

    private Button? FindConfirmDeleteYesButton()
    {
        foreach (AutomationElement element in Automation.GetDesktop().FindAllDescendants(
                     Automation.ConditionFactory.ByName("Yes")))
        {
            try
            {
                return element.AsButton();
            }
            catch
            {
            }
        }

        return null;
    }

    private void DismissBlockingDialogs()
    {
        var desktop = Automation.GetDesktop();
        foreach (var dialog in desktop.FindAllDescendants(Automation.ConditionFactory.ByControlType(ControlType.Window)))
        {
            try
            {
                var dismiss = dialog.FindFirstDescendant(Automation.ConditionFactory.ByName("Dismiss"));
                dismiss?.AsButton()?.Invoke();
            }
            catch
            {
            }
        }
    }
}
