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
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public class TrackMetadata
        {
            public double Rating;
            public List<double> Sliders;
            public TrackMetadata(double rating, List<double> sliders)
            {
                Rating = rating;
                Sliders = sliders;
            }
        }

        private DispatcherQueue dispatcherQueue;
        public static string TM_ENV_NAME = "TRACKMIXER_ROOT_FOLDERS";
        public static string TRACK_METADATA_JSON = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrackMixerv2", "track_metadata.json");
        public static List<string> ROOT_FOLDERS;
        public static Dictionary<string, TrackMetadata> TRACK_METADATA = new Dictionary<string, TrackMetadata>();
        private string[] launchFiles = null;
        public static bool MainWindowActivated;
        public static MainWindow Instance;
        public static List<string> RecentVideos;
        private static string tempFilesRecordPath = Path.Combine(Path.GetTempPath(), "TrackMixerTempFiles.txt");

        public MainWindow(string[] files)
        {
            Instance = this;
            InitializeComponent();
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrackMixerv2");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            if (!File.Exists(TRACK_METADATA_JSON))
                File.WriteAllText(TRACK_METADATA_JSON, "");
            CleanupTemporaryFiles();
            //File.WriteAllText(TRACK_METADATA_JSON, "");
            TRACK_METADATA = JsonConvert.DeserializeObject<Dictionary<string, TrackMetadata>>(File.ReadAllText(TRACK_METADATA_JSON));
            if (TRACK_METADATA == null)
                TRACK_METADATA = new Dictionary<string, TrackMetadata>();
            launchFiles = files;

            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            //this.SetTitleBar(CustomDragRegion);
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
            appWindow.SetIcon(Path.Combine(Package.Current.InstalledLocation.Path, @"\Assets\video.ico"));

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
            ROOT_FOLDERS = new List<string>();
            string env = Environment.GetEnvironmentVariable(TM_ENV_NAME);
            if (env != null)
                ROOT_FOLDERS = Environment.GetEnvironmentVariable(TM_ENV_NAME).Split(';').ToList();
            if (launchFiles == null) // TODO: put the following inside of a new method, since repeating, will never happen:d
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("RecentVideosJson"))
                {
                    string recentVideosJson = (string)ApplicationData.Current.LocalSettings.Values["RecentVideosJson"];
                    List<string> recentVideos = JsonConvert.DeserializeObject<List<string>>(recentVideosJson);
                    if (recentVideos.Count > 0)
                        AddNewTabs([.. recentVideos]);
                    else
                        TabView_AddTabButtonClick(TabView, new RoutedEventArgs());
                }
                else
                {
                    TabView_AddTabButtonClick(TabView, new RoutedEventArgs());
                }
            }
            else
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("DoubleClickOnNewTab"))
                {
                    AddNewTabs(launchFiles, (bool)ApplicationData.Current.LocalSettings.Values["DoubleClickOnNewTab"]);
                }
                else
                {
                    AddNewTabs(launchFiles);
                }
            }
        }
        public void SaveRecentVideos()
        {
            Debug.WriteLine("Saving videos...");
            List<string> recentVideos = new List<string>();
            foreach (Object obj in TabView.TabItems)
            {
                if (obj is TabViewItem tabViewItem && tabViewItem.Content is MixerPage page) 
                {
                    // look, this all sucks, but idgaf anymore abt this project
                    recentVideos.Add(page.path);
                }
            }
            string recentVideosJson = JsonConvert.SerializeObject(recentVideos);
            ApplicationData.Current.LocalSettings.Values["RecentVideosJson"] = recentVideosJson;
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
                Content = "To enable automatic playlist sorting, please add root folders. Track Mixer will search these folders and their subdirectories to create playlists. Currently, you need to manually modify your environment variables to edit the root folders.",
                CloseButtonText = "Dismiss"
            };

            await rootFolderDialog.ShowAsync();

            if (ROOT_FOLDERS == null)
                ROOT_FOLDERS = new List<string>();
            string newFolder = await PickFolderDialog();
            if (newFolder == null) return false;
            ROOT_FOLDERS.Add(newFolder);
            Task.Run(() => Environment.SetEnvironmentVariable(TM_ENV_NAME, string.Join(';', ROOT_FOLDERS), EnvironmentVariableTarget.User)); // if we await this, it takes too long. so just pray.

            {
                if (TabView.SelectedItem is TabViewItem tabViewItem && tabViewItem.Content is MixerPage page)
                {
                    page.PlayMedia();
                }
            }

            return true;
        }

        public static string RootFoldersContainFile(string path)
        {
            foreach (var folder in ROOT_FOLDERS)
            {
                if (path.StartsWith(folder))
                {
                    return folder;
                }
            }
            return null;
        }

        async Task<IReadOnlyList<StorageFile>> OpenFilesDialog()
        {
            // Create a file picker
            var openPicker = new FileOpenPicker();

            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            openPicker.SettingsIdentifier = "TrackPicker";
            // Set options for your file picker
            openPicker.ViewMode = PickerViewMode.List;
            //openPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            openPicker.FileTypeFilter.Add("*");

            // Open the picker for the user to pick a file
            IReadOnlyList<StorageFile> files = await openPicker.PickMultipleFilesAsync();
            return files;
        }
        async Task<string> PickFolderDialog()
        {
            var folderPicker = new FolderPicker();
            folderPicker.SettingsIdentifier = "TrackPicker";
            //folderPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary; // Suggest a start location
            folderPicker.CommitButtonText = "Add root folder";
            // Get the current window's HWND by passing in the Window object
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            // Associate the HWND with the file picker
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
                // Old behavior: Open all files in new tabs
                foreach (string file in files)
                {
                    if (RootFoldersContainFile(file) == null)
                        await AddNewRootFolder();

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
                    TabView.TabItems.Add(newTab);
                }
            }
            else
            {
                // Replace the current tab with the first file
                string firstFile = files[0];

                if (RootFoldersContainFile(firstFile) == null) // Check if the first file is in root folders
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
                    // If no tab is selected, create a new tab for the first file
                    var newTab = new TabViewItem();
                    newTab.Header = Helper.GetTitleFromPath(firstFile);
                    newTab.IconSource = new SymbolIconSource() { Symbol = Symbol.SlideShow };

                    MixerPage page = new MixerPage(firstFile);
                    page.AllowDrop = true;
                    page.DragOver += MixedMediaPlayer_DragOver;
                    page.Drop += MixedMediaPlayer_Drop;
                    page.OpenFileFlyout.Click += MenuFlyoutItem_Click;
                    page.AddRootFlyout.Click += MenuFlyoutItem_Click;
                    page.ExportFlyout.Click += MenuFlyoutItem_Click;
                    newTab.Content = page;

                    Style tabStyle = (Style)Application.Current.Resources["myTabViewItem"];
                    newTab.Style = tabStyle;
                    TabView.TabItems.Add(newTab);
                    TabView.SelectedItem = newTab;
                }

                // Add any remaining files to new tabs
                for (int i = 1; i < files.Length; i++)
                {
                    string file = files[i];
                    if (RootFoldersContainFile(file) == null)
                        await AddNewRootFolder();

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
                    TabView.TabItems.Add(newTab);
                }
            }

            // Ensure a tab is selected if none are
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
            if (files.Count <= 0) return; // operation cancelled

            List<string> filePaths = files.Select(file => file.Path).ToList();
            AddNewTabs(filePaths.ToArray());
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Tab.Content is MixerPage)
            {
                MixerPage page = (args.Tab.Content as MixerPage);
                page.DragOver -= MixedMediaPlayer_DragOver;
                page.Drop -= MixedMediaPlayer_Drop;
                page.OpenFileFlyout.Click -= MenuFlyoutItem_Click;
                page.AddRootFlyout.Click -= MenuFlyoutItem_Click;
                page.ExportFlyout.Click -= MenuFlyoutItem_Click;
                page.Dispose(); // possible memory leak
            }
            sender.TabItems.Remove(args.Tab);
            GC.Collect();
        }

        private void NewTabInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            TabView_AddTabButtonClick(TabView, new RoutedEventArgs());
            args.Handled = true;
        }

        private void CloseTabInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (TabView.SelectedItem is MixerPage)
            {
                (TabView.SelectedItem as MixerPage).Dispose(); // possible memory leak
            }
            TabView.TabItems.Remove(TabView.SelectedItem);
            GC.Collect();
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
                                    .Where(file => Helper.IsVideoFile(file))
                                    .Select(file => file.Path)
                                    .ToList();

                    if (ApplicationData.Current.LocalSettings.Values.ContainsKey("DragAndDropOnNewTab"))
                    {
                        AddNewTabs(videoFilePaths.ToArray(), (bool)ApplicationData.Current.LocalSettings.Values["DragAndDropOnNewTab"]);
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
                        // Handle exceptions if needed
                    }
                }
                File.Delete(tempFilesRecordPath);
            }
        }
        private async Task ExportCurrentFileAsync()
        {
            MixerPage page = (TabView.SelectedItem as TabViewItem)?.Content as MixerPage;
            if (page == null)
                return;

            page.PauseMedia();

            double[] levels = page.GetVolumeLevels();
            double[] normalizedLevels = levels.Select(l => l / 100.0).ToArray();
            string inputPath = page.GetCurrentPath();

            // Create a ContentDialog with the required UI elements
            var dialog = new ContentDialog
            {
                Title = "Export Options",
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            // Create UI controls
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

            // Use a Grid to better control layout
            var exportPathGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            exportPathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Label
            exportPathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // TextBox
            exportPathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Button

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

            // Create separate panels for time inputs and target size
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

            // Event handlers
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

                double targetFileSizeMB = 25; // Default value
                bool hasTargetSize = !string.IsNullOrWhiteSpace(targetSizeTextBox.Text);

                try
                {
                    if (hasStartTime)
                        startTime = ParseTimeInput(startTextBox.Text);

                    if (hasEndTime)
                        endTime = ParseTimeInput(endTextBox.Text);

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

                // Get media info
                var mediaInfo = await FFmpeg.GetMediaInfo(inputPath);
                var audioStreams = mediaInfo.AudioStreams.ToList();

                // Calculate duration in seconds
                double durationSeconds = mediaInfo.Duration.TotalSeconds;
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

                // Define audio bitrate (e.g., 128 kbps)
                double audioBitrate = 128 * 1000; // in bits per second

                // Calculate total target bitrate
                double totalTargetBitrate = (targetFileSizeMB * 8 * 1024 * 1024) / durationSeconds; // in bits per second

                // Subtract audio bitrate from total bitrate to get video bitrate
                double videoBitrate = totalTargetBitrate - audioBitrate;

                if (videoBitrate <= 0)
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

                // Convert bitrate to kbps for FFmpeg
                string videoBitrateKbps = (videoBitrate / 1000).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string audioBitrateKbps = (audioBitrate / 1000).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                // Build filter_complex parameter
                var filterParts = new List<string>();
                for (int i = 0; i < audioStreams.Count; i++)
                {
                    filterParts.Add($"[0:a:{i}]volume={normalizedLevels[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}[a{i}]");
                }
                string filterComplex = string.Join(";", filterParts);
                string audioInputs = string.Join("", Enumerable.Range(0, audioStreams.Count).Select(i => $"[a{i}]"));
                filterComplex += $";{audioInputs}amix=inputs={audioStreams.Count}[mixedaudio]";

                var conversion = FFmpeg.Conversions.New()
                    .AddParameter($"-i \"{inputPath}\"")
                    .AddParameter($"-filter_complex \"{filterComplex}\"")
                    .AddParameter("-map [mixedaudio]")
                    .AddParameter("-map 0:v")
                    .AddParameter("-c:v libx264")
                    .AddParameter($"-b:v {videoBitrateKbps}k")
                    .AddParameter($"-c:a aac -b:a {audioBitrateKbps}k");

                if (hasStartTime)
                    conversion.AddParameter($"-ss {startTime}");

                if (hasEndTime)
                    conversion.AddParameter($"-to {endTime}");

                if (clipboardOnly)
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(inputPath)}");

                    // Record the temp file path
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

        private TimeSpan ParseTimeInput(string input)
        {
            input = input.Trim();

            if (string.IsNullOrEmpty(input))
                throw new FormatException("Time input is empty");

            // If input is digits only, assume seconds
            if (double.TryParse(input, out double seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }

            // Handle MM:SS format
            if (input.Count(c => c == ':') == 1)
            {
                input = "00:" + input;
            }

            if (TimeSpan.TryParse(input, out TimeSpan result))
            {
                return result;
            }

            throw new FormatException("Invalid time format. Use SS, MM:SS, or HH:MM:SS.");
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

                // If the child is a control and has a TabStop, disable it
                if (child is Control control)
                {
                    control.IsTabStop = false;
                }

                // Recursively disable TabStop for children
                DisableTabStops(child);
            }
        }
    }
}
