using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Xabe.FFmpeg;

namespace TrackMixerv2
{
    public sealed partial class MainWindow : Window
    {
        private DispatcherQueue dispatcherQueue;
        public static string TM_ENV_NAME = "TRACKMIXER_ROOT_FOLDERS";
        private string[] launchFiles = null;
        public static bool MainWindowActivated;
        public static MainWindow Instance;
        private static string tempFilesRecordPath = Path.Combine(Path.GetTempPath(), "TrackMixerTempFiles.txt");

        public MainWindow(string[] files)
        {
            Instance = this;
            InitializeComponent();
            if (UiTestBootstrap.IsEnabled)
                Title = "Track Mixer UI Test";
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrackMixerv2");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            string metadataPath = AppState.TrackMetadataJson;
            string metadataDirectory = Path.GetDirectoryName(metadataPath);
            if (!string.IsNullOrWhiteSpace(metadataDirectory) && !Directory.Exists(metadataDirectory))
                Directory.CreateDirectory(metadataDirectory);
            if (!File.Exists(metadataPath))
                File.WriteAllText(metadataPath, "");
            CleanupTemporaryFiles();
            AppState.TRACK_METADATA = JsonConvert.DeserializeObject<Dictionary<string, TrackMetadata>>(File.ReadAllText(AppState.TrackMetadataJson));
            if (AppState.TRACK_METADATA == null)
                AppState.TRACK_METADATA = new Dictionary<string, TrackMetadata>();
            PlaylistHelper.EnsureRootFolderAsync = () => Instance.AddNewRootFolder();
            launchFiles = files;

            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(0, 0, 0, 0);
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
            this.Closed += MainWindow_Closed;

            CustomDragRegion.Loaded += CustomDragRegion_Loaded;
            SizeChanged += MainWindow_SizeChanged;
            TabView.LayoutUpdated += TabView_LayoutUpdated;
            TabView.AddTabButtonClick += TabView_AddTabButtonClick;
            TabView.TabItemsChanged += TabView_TabItemsChanged;
            TabView.TabCloseRequested += TabView_TabCloseRequested;

            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(GetAssetPath("Assets", "video.ico"));

            this.Activated += (sender, args) =>
            {
                MainWindowActivated =
                    args.WindowActivationState == WindowActivationState.CodeActivated ||
                    args.WindowActivationState == WindowActivationState.PointerActivated;
            };
        }

        private void TabView_TabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args)
        {
            foreach (Object obj in TabView.TabItems)
            {
                if (obj is TabViewItem tabViewItem)
                {
                    tabViewItem.IsEnabledChanged += (object sender, DependencyPropertyChangedEventArgs e) => { SaveRecentVideos(); } ;
                }
            }
            SaveRecentVideos();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            CustomDragRegion.Loaded -= CustomDragRegion_Loaded;
            SizeChanged -= MainWindow_SizeChanged;
            TabView.LayoutUpdated -= TabView_LayoutUpdated;
            TabView.AddTabButtonClick -= TabView_AddTabButtonClick;
            TabView.TabCloseRequested -= TabView_TabCloseRequested;
        }

        private void TabView_LayoutUpdated(object sender, object e)
        {
            SetTitleBarDragRegion();
            if (TabView.SelectedItem == null) return;
            Title = (TabView.SelectedItem as TabViewItem).Header.ToString();
        }

        private void CustomDragRegion_Loaded(object sender, RoutedEventArgs e)
        {
            SetTitleBarDragRegion();
        }

        private async void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            await Task.Delay(50); // dont know why. it just so happens to work so why change it?
            SetTitleBarDragRegion();
        }

        private void SetTitleBarDragRegion()
        {
            if (TabView == null || CustomDragRegion == null || AppWindow == null) return;
            int x = (int)(TabView.ActualWidth - CustomDragRegion.ActualWidth);
            RectInt32[] dragRects = new RectInt32[]
            {
                new RectInt32(x, 0, (int)CustomDragRegion.ActualWidth, (int)CustomDragRegion.ActualHeight)
            };
            AppWindow.TitleBar.SetDragRectangles(dragRects);
        }

        private async void TabView_Loaded(object sender, RoutedEventArgs e)
        {
            DisableTabStops(TabView);
            AppState.ROOT_FOLDERS = new List<string>();
            string env = Environment.GetEnvironmentVariable(TM_ENV_NAME);
            if (env != null)
                AppState.ROOT_FOLDERS = Environment.GetEnvironmentVariable(TM_ENV_NAME).Split(';').ToList();

            if (UiTestBootstrap.SuppressRootFolderPrompt)
                RootFolderPromptSuppressed = true;

            if (!string.IsNullOrWhiteSpace(UiTestBootstrap.RootFolder))
                AppState.ROOT_FOLDERS = new List<string> { UiTestBootstrap.RootFolder };
            else if (launchFiles != null && launchFiles.Length > 0)
            {
                string? clipDirectory = Path.GetDirectoryName(launchFiles[0]);
                if (!string.IsNullOrWhiteSpace(clipDirectory))
                    AppState.ROOT_FOLDERS = new List<string> { clipDirectory };
            }

            if (launchFiles != null && launchFiles.Length > 0)
            {
                if (LocalSettingsStore.ContainsKey(LocalSettingsStore.Keys.DoubleClickOnNewTab))
                    AddNewTabs(launchFiles, LocalSettingsStore.GetBool(LocalSettingsStore.Keys.DoubleClickOnNewTab));
                else
                    AddNewTabs(launchFiles);
                return;
            }

            bool hasRecentVideos = false;
            List<string> recentVideos = new List<string>();
            if (LocalSettingsStore.ContainsKey(LocalSettingsStore.Keys.RecentVideosJson))
            {
                string recentVideosJson = LocalSettingsStore.GetString(LocalSettingsStore.Keys.RecentVideosJson) ?? "[]";
                recentVideos = JsonConvert.DeserializeObject<List<string>>(recentVideosJson) ?? new List<string>();
                hasRecentVideos = recentVideos.Count > 0;
            }

            if (UiTestBootstrap.IsEnabled && !hasRecentVideos)
                return;

            if (hasRecentVideos)
                AddNewTabs([.. recentVideos]);
            else
                TabView_AddTabButtonClick(TabView, new RoutedEventArgs());
        }
        public void SaveRecentVideos()
        {
            List<string> recentVideos = new List<string>();
            foreach (Object obj in TabView.TabItems)
            {
                if (obj is TabViewItem tabViewItem && tabViewItem.Content is MixerPage page) 
                {
                    recentVideos.Add(page.path);
                }
            }
            string recentVideosJson = JsonConvert.SerializeObject(recentVideos);
            LocalSettingsStore.SetString(LocalSettingsStore.Keys.RecentVideosJson, recentVideosJson);
        }

        public static bool RootFolderPromptSuppressed
        {
            get
            {
                if (LocalSettingsStore.ContainsKey(LocalSettingsStore.Keys.SuppressRootFolderPrompt))
                    AppState.RootFolderPromptSuppressed = LocalSettingsStore.GetBool(LocalSettingsStore.Keys.SuppressRootFolderPrompt);
                return AppState.RootFolderPromptSuppressed;
            }
            set
            {
                AppState.RootFolderPromptSuppressed = value;
                LocalSettingsStore.SetBool(LocalSettingsStore.Keys.SuppressRootFolderPrompt, value);
            }
        }

        public async Task<bool> AddNewRootFolder()
        {
            {
                if (TabView.SelectedItem is TabViewItem tabViewItem && tabViewItem.Content is MixerPage page)
                {
                    page.PauseMedia();
                }
            }

            ContentDialog rootFolderDialog = new ContentDialog()
            {
                XamlRoot = this.TabView.XamlRoot,
                Title = "Add Root Folders (e.g., C:\\Users\\Mark\\Videos\\NVIDIA) for Automatic Playlist Sorting",
                Content = "To enable automatic playlist sorting, please add root folders. Track Mixer will search these folders and their subdirectories to create playlists.\n\nYou can still use Track Mixer without root folders (you just won't get automatic sorting).",
                PrimaryButtonText = "Add root folder",
                SecondaryButtonText = "Don't ask again",
                CloseButtonText = "Not now"
            };

            ContentDialogResult dialogResult = await rootFolderDialog.ShowAsync();
            if (dialogResult == ContentDialogResult.Secondary)
            {
                RootFolderPromptSuppressed = true;
                {
                    if (TabView.SelectedItem is TabViewItem tabViewItem && tabViewItem.Content is MixerPage page)
                    {
                        page.PlayMedia();
                    }
                }
                return false;
            }
            if (dialogResult != ContentDialogResult.Primary)
            {
                {
                    if (TabView.SelectedItem is TabViewItem tabViewItem && tabViewItem.Content is MixerPage page)
                    {
                        page.PlayMedia();
                    }
                }
                return false;
            }

            if (AppState.ROOT_FOLDERS == null)
                AppState.ROOT_FOLDERS = new List<string>();
            string newFolder = await PickFolderDialog();
            if (newFolder == null) return false;
            AppState.ROOT_FOLDERS.Add(newFolder);
            Task.Run(() => Environment.SetEnvironmentVariable(TM_ENV_NAME, string.Join(';', AppState.ROOT_FOLDERS), EnvironmentVariableTarget.User)); // if we await this, it takes too long. so just pray.

            {
                if (TabView.SelectedItem is TabViewItem tabViewItem && tabViewItem.Content is MixerPage page)
                {
                    page.PlayMedia();
                }
            }

            return true;
        }

        private TabViewItem CreateTabForFile(string file)
        {
            var newTab = new TabViewItem();
            newTab.Header = Helper.GetTitleFromPath(file);
            newTab.IconSource = new SymbolIconSource() { Symbol = Symbol.SlideShow };

            MixerPage page = new MixerPage(file);
            page.AllowDrop = true;
            page.DragOver += MixedMediaPlayer_DragOver;
            page.Drop += MixedMediaPlayer_Drop;
            page.OpenFileFlyout.Click += MenuFlyoutItem_Click;
            page.AddRootFlyout.Click += MenuFlyoutItem_Click;
            page.ExportFlyout.Click += MenuFlyoutItem_Click;
            newTab.Content = page;

            Style tabStyle = (Style)Application.Current.Resources["myTabViewItem"];
            newTab.Style = tabStyle;
            return newTab;
        }

        private void CloseTab(TabViewItem tab)
        {
            if (tab == null)
                return;

            if (tab.Content is MixerPage page)
            {
                page.DragOver -= MixedMediaPlayer_DragOver;
                page.Drop -= MixedMediaPlayer_Drop;
                page.OpenFileFlyout.Click -= MenuFlyoutItem_Click;
                page.AddRootFlyout.Click -= MenuFlyoutItem_Click;
                page.ExportFlyout.Click -= MenuFlyoutItem_Click;
                page.Dispose();
            }
            TabView.TabItems.Remove(tab);
        }

        async Task<IReadOnlyList<StorageFile>> OpenFilesDialog()
        {
            var openPicker = new FileOpenPicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            openPicker.SettingsIdentifier = "TrackPicker";
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.FileTypeFilter.Add("*");

            IReadOnlyList<StorageFile> files = await openPicker.PickMultipleFilesAsync();
            return files;
        }
        async Task<string> PickFolderDialog()
        {
            var folderPicker = new FolderPicker();
            folderPicker.SettingsIdentifier = "TrackPicker";
            folderPicker.CommitButtonText = "Add root folder";
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hWnd);
            var result = await folderPicker.PickSingleFolderAsync();
            if (result == null) return null;
            return result.Path;
        }

        public async void AddNewTabs(string[] files, bool onNewTab = true)
        {
            if (files == null || files.Length == 0) return;

            if (onNewTab)
            {
                foreach (string file in files)
                {
                    if (AppState.RootFoldersContainFile(file) == null && !RootFolderPromptSuppressed)
                        await AddNewRootFolder();

                    TabView.TabItems.Add(CreateTabForFile(file));
                }
            }
            else
            {
                string firstFile = files[0];

                if (AppState.RootFoldersContainFile(firstFile) == null && !RootFolderPromptSuppressed)
                    await AddNewRootFolder();

                var currentTab = TabView.SelectedItem as TabViewItem;
                if (currentTab != null)
                {
                    currentTab.Header = Helper.GetTitleFromPath(firstFile);
                    currentTab.IconSource = new SymbolIconSource() { Symbol = Symbol.SlideShow };

                    ((MixerPage)currentTab.Content).OpenNewMedia(firstFile);
                }
                else
                {
                    var newTab = CreateTabForFile(firstFile);
                    TabView.TabItems.Add(newTab);
                    TabView.SelectedItem = newTab;
                }

                for (int i = 1; i < files.Length; i++)
                {
                    string file = files[i];
                    if (AppState.RootFoldersContainFile(file) == null && !RootFolderPromptSuppressed)
                        await AddNewRootFolder();

                    TabView.TabItems.Add(CreateTabForFile(file));
                }
            }

            if (TabView.SelectedIndex < 0)
            {
                try
                {
                    TabView.SelectedIndex = 0;
                }
                catch { }
            }

            DisableTabStops(TabView);
        }

        private async void TabView_AddTabButtonClick(TabView sender, object args)
        {
            IReadOnlyList<StorageFile> files = await OpenFilesDialog();
            if (files.Count <= 0) return;

            List<string> filePaths = files.Select(file => file.Path).ToList();
            AddNewTabs(filePaths.ToArray());
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            CloseTab(args.Tab as TabViewItem);
        }

        private void NewTabInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            TabView_AddTabButtonClick(TabView, new RoutedEventArgs());
            args.Handled = true;
        }

        private void CloseTabInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            CloseTab(TabView.SelectedItem as TabViewItem);
            args.Handled = true;
        }

        private void NextTabInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (TabView.TabItems.Count > TabView.SelectedIndex + 1)
                TabView.SelectedItem = TabView.TabItems[TabView.SelectedIndex + 1];
            args.Handled = true;
        }

        private void PreviousTabInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (TabView.SelectedIndex - 1 >= 0)
                TabView.SelectedItem = TabView.TabItems[TabView.SelectedIndex - 1];
            args.Handled = true;
        }

        private void MixedMediaPlayer_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
            e.Handled = true;
        }

        private void MixedMediaPlayer_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            dispatcherQueue.TryEnqueue(async () =>
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items == null || items.Count == 0)
                    {
                        e.Handled = true;
                        return;
                    }
                    List<string> videoFilePaths = items.OfType<StorageFile>()
                                    .Where(file => VideoFileHelper.IsVideoFile(file))
                                    .Select(file => file.Path)
                                    .ToList();

                    if (LocalSettingsStore.ContainsKey(LocalSettingsStore.Keys.DragAndDropOnNewTab))
                    {
                        AddNewTabs(videoFilePaths.ToArray(), LocalSettingsStore.GetBool(LocalSettingsStore.Keys.DragAndDropOnNewTab));
                    }
                    else
                    {
                        AddNewTabs(videoFilePaths.ToArray());
                    }
                }
            });
        }
        private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            if (item == null) return;
            switch (item.Tag)
            {
                case "openFile":
                    {
                        MixerPage page = (TabView.SelectedItem as TabViewItem).Content as MixerPage;
                        if (page == null) return;
                        page.PauseMedia();
                        IReadOnlyList<StorageFile> files = await OpenFilesDialog();
                        if (files.Count <= 0)
                        {
                            page.PlayMedia();
                            return;
                        }
                        string file = files.First().Path;
                        page.OpenNewMedia(file);
                        break;
                    }
                case "addRootFolder":
                    {
                        await AddNewRootFolder();
                        break;
                    }
                case "exportFile":
                    {
                        await ExportCurrentFileAsync();
                        break;
                    }
            }
        }
        private void CleanupTemporaryFiles()
        {
            if (File.Exists(tempFilesRecordPath))
            {
                var files = File.ReadAllLines(tempFilesRecordPath);
                foreach (var file in files)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                    }
                }
                File.Delete(tempFilesRecordPath);
            }
        }
        private async Task<bool> IsFFmpegAvailable()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrEmpty(FFmpeg.ExecutablesPath))
                {
                    var exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
                    processStartInfo.FileName = Path.Combine(FFmpeg.ExecutablesPath, exeName);
                }

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task ExportCurrentFileAsync()
        {
            if (!await IsFFmpegAvailable())
            {
                ContentDialog ffmpegMissingDialog = new ContentDialog()
                {
                    XamlRoot = this.TabView.XamlRoot,
                    Title = "FFmpeg installation not found in PATH",
                    Content = new StackPanel()
                    {
                        Children =
                        {
                            new TextBlock {
                                Text = "To make the export feature work, you need ffmpeg installed and added to PATH. The easiest way to do this is to use winget:",
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 10)
                            },
                            new TextBox {
                                Text = "winget install -e --id Gyan.FFmpeg",
                                IsReadOnly = true,
                                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                            }
                        }
                    },
                    CloseButtonText = "Dismiss"
                };

                await ffmpegMissingDialog.ShowAsync();
                return;
            }

            MixerPage page = (TabView.SelectedItem as TabViewItem)?.Content as MixerPage;
            if (page == null)
                return;

            page.PauseMedia();

            double[] levels = page.GetVolumeLevels();
            double[] normalizedLevels = levels.Select(l => l / 100.0).ToArray();
            string inputPath = page.GetCurrentPath();

            var dialog = new ContentDialog
            {
                Title = "Export Options",
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var clipboardOnlyCheckBox = new CheckBox
            {
                Content = "Clipboard only",
                Margin = new Thickness(0, 0, 0, 10)
            };

            var exportPathTextBox = new TextBox
            {
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 5, 0)
            };

            var exportPathButton = new Button
            {
                Content = "...",
                Width = 30
            };

            var exportPathGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            exportPathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            exportPathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            exportPathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var exportPathLabel = new TextBlock
            {
                Text = "Export path:",
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            exportPathGrid.Children.Add(exportPathLabel);
            Grid.SetColumn(exportPathLabel, 0);

            exportPathGrid.Children.Add(exportPathTextBox);
            Grid.SetColumn(exportPathTextBox, 1);

            exportPathGrid.Children.Add(exportPathButton);
            Grid.SetColumn(exportPathButton, 2);

            var startTextBox = new TextBox
            {
                PlaceholderText = "00:00:00",
                Width = 150,
                Margin = new Thickness(5, 0, 0, 0)
            };

            var endTextBox = new TextBox
            {
                PlaceholderText = "00:00:30",
                Width = 150,
                Margin = new Thickness(5, 0, 0, 0)
            };

            var targetSizeTextBox = new TextBox
            {
                PlaceholderText = "25",
                Width = 150,
                Margin = new Thickness(5, 0, 0, 0)
            };

            var timePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            timePanel.Children.Add(new TextBlock { Text = "Start:", VerticalAlignment = VerticalAlignment.Center });
            timePanel.Children.Add(startTextBox);
            timePanel.Children.Add(new TextBlock { Text = "End:", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            timePanel.Children.Add(endTextBox);

            var targetSizePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            targetSizePanel.Children.Add(new TextBlock { Text = "Target file size (MB):", VerticalAlignment = VerticalAlignment.Center });
            targetSizePanel.Children.Add(targetSizeTextBox);

            var contentPanel = new StackPanel();
            contentPanel.Children.Add(clipboardOnlyCheckBox);
            contentPanel.Children.Add(exportPathGrid);
            contentPanel.Children.Add(timePanel);
            contentPanel.Children.Add(targetSizePanel);

            dialog.Content = contentPanel;

            clipboardOnlyCheckBox.Checked += (s, e) =>
            {
                exportPathTextBox.IsEnabled = false;
                exportPathButton.IsEnabled = false;
            };
            clipboardOnlyCheckBox.Unchecked += (s, e) =>
            {
                exportPathTextBox.IsEnabled = true;
                exportPathButton.IsEnabled = true;
            };

            exportPathButton.Click += async (s, e) =>
            {
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
                savePicker.FileTypeChoices.Add("Video Files", new List<string> { Path.GetExtension(inputPath) });
                savePicker.SuggestedFileName = $"{Path.GetFileNameWithoutExtension(inputPath)}_MIXED{Path.GetExtension(inputPath)}";

                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

                var outputFile = await savePicker.PickSaveFileAsync();
                if (outputFile != null)
                {
                    exportPathTextBox.Text = outputFile.Path;
                }
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                bool clipboardOnly = clipboardOnlyCheckBox.IsChecked == true;
                string outputPath = exportPathTextBox.Text;

                if (!clipboardOnly && string.IsNullOrEmpty(outputPath))
                {
                    await new ContentDialog
                    {
                        Title = "Error",
                        Content = "Please specify an export path or select 'Clipboard only'.",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    }.ShowAsync();
                    page.PlayMedia();
                    return;
                }

                TimeSpan startTime = TimeSpan.Zero;
                TimeSpan endTime = TimeSpan.Zero;
                bool hasStartTime = !string.IsNullOrWhiteSpace(startTextBox.Text);
                bool hasEndTime = !string.IsNullOrWhiteSpace(endTextBox.Text);

                double targetFileSizeMB = 25;
                bool hasTargetSize = !string.IsNullOrWhiteSpace(targetSizeTextBox.Text);

                try
                {
                    if (hasStartTime)
                        startTime = ExportPipeline.ParseTimeInput(startTextBox.Text);

                    if (hasEndTime)
                        endTime = ExportPipeline.ParseTimeInput(endTextBox.Text);

                    if (hasStartTime && hasEndTime && startTime >= endTime)
                    {
                        throw new Exception("Start time must be less than end time.");
                    }

                    if (hasTargetSize)
                    {
                        if (!double.TryParse(targetSizeTextBox.Text, out targetFileSizeMB) || targetFileSizeMB <= 0)
                        {
                            throw new Exception("Target file size must be a positive number.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    await new ContentDialog
                    {
                        Title = "Invalid Input",
                        Content = $"Error parsing input: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    }.ShowAsync();
                    page.PlayMedia();
                    return;
                }

                var mediaInfo = await FFmpeg.GetMediaInfo(inputPath);
                var audioStreams = mediaInfo.AudioStreams.ToList();

                double durationSeconds = ExportPipeline.ComputeExportDurationSeconds(
                    mediaInfo.Duration.TotalSeconds,
                    hasStartTime,
                    startTime,
                    hasEndTime,
                    endTime);

                if (durationSeconds <= 0)
                {
                    await new ContentDialog
                    {
                        Title = "Error",
                        Content = "Unable to determine media duration.",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    }.ShowAsync();
                    page.PlayMedia();
                    return;
                }

                double audioBitrate = 128 * 1000;
                double videoBitrateKbps = ExportPipeline.ComputeVideoBitrateKbps(targetFileSizeMB, durationSeconds, audioBitrate);

                if (videoBitrateKbps <= 0)
                {
                    await new ContentDialog
                    {
                        Title = "Error",
                        Content = "Target file size is too small for the given duration.",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    }.ShowAsync();
                    page.PlayMedia();
                    return;
                }

                string videoBitrateKbpsText = videoBitrateKbps.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string audioBitrateKbps = (audioBitrate / 1000).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string filterComplex = ExportPipeline.BuildFilterComplex(audioStreams.Count, normalizedLevels);

                var conversion = FFmpeg.Conversions.New()
                    .AddParameter($"-i \"{inputPath}\"")
                    .AddParameter($"-filter_complex \"{filterComplex}\"")
                    .AddParameter("-map [mixedaudio]")
                    .AddParameter("-map 0:v")
                    .AddParameter("-c:v libx264")
                    .AddParameter($"-b:v {videoBitrateKbpsText}k")
                    .AddParameter($"-c:a aac -b:a {audioBitrateKbps}k");

                if (hasStartTime)
                    conversion.AddParameter($"-ss {startTime}");

                if (hasEndTime)
                    conversion.AddParameter($"-to {endTime}");

                if (clipboardOnly)
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(inputPath)}");

                    File.AppendAllLines(tempFilesRecordPath, new[] { tempPath });

                    conversion.SetOutput(tempPath);
                    conversion.SetOverwriteOutput(true);

                    await conversion.Start();

                    await CopyFileToClipboardAsync(tempPath);

                    await new ContentDialog
                    {
                        Title = "Success",
                        Content = "File exported to clipboard successfully!",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    }.ShowAsync();
                }
                else
                {
                    conversion.SetOutput(outputPath);
                    conversion.SetOverwriteOutput(true);

                    await conversion.Start();

                    await CopyFileToClipboardAsync(outputPath);

                    await new ContentDialog
                    {
                        Title = "Success",
                        Content = "File exported and copied to clipboard successfully!",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    }.ShowAsync();
                }
            }

            page.PlayMedia();
        }

        private async Task CopyFileToClipboardAsync(string filePath)
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
            var dataPackage = new DataPackage();
            dataPackage.SetStorageItems(new List<StorageFile> { storageFile });
            Clipboard.SetContent(dataPackage);
        }

        public static void DisableTabStops(DependencyObject root)
        {
            if (root == null) return;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is Control control)
                {
                    control.IsTabStop = false;
                }

                DisableTabStops(child);
            }
        }

        private static string? packagedAssetRoot;

        private static string GetAssetPath(params string[] relativeParts)
        {
            packagedAssetRoot ??= TryGetPackagedAssetRoot();
            string root = packagedAssetRoot ?? AppContext.BaseDirectory;
            return Path.Combine([root, .. relativeParts]);
        }

        private static string? TryGetPackagedAssetRoot()
        {
            try
            {
                return Package.Current.InstalledLocation.Path;
            }
            catch
            {
                return null;
            }
        }
    }
}
