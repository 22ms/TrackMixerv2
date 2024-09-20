using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;

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

        public MainWindow(string[] files)
        {
            InitializeComponent();
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrackMixerv2");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            if (!File.Exists(TRACK_METADATA_JSON))
                File.WriteAllText(TRACK_METADATA_JSON, "");
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
            TabView.TabCloseRequested += TabView_TabCloseRequested;

            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(Path.Combine(Package.Current.InstalledLocation.Path, @"\Assets\video.ico"));
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
            ROOT_FOLDERS = new List<string>();
            string env = Environment.GetEnvironmentVariable(TM_ENV_NAME);
            if(env != null)
                ROOT_FOLDERS = Environment.GetEnvironmentVariable(TM_ENV_NAME).Split(';').ToList();
            if(launchFiles == null) // TODO: put the following inside of a new method, since redundant
            {
                TabView_AddTabButtonClick(TabView, new RoutedEventArgs());
            }
            else
            {
                AddNewTabs(launchFiles);
            }
        }

        public async Task AddNewRootFolder()
        {
            if(ROOT_FOLDERS == null)
                ROOT_FOLDERS = new List<string>();
            string newFolder = await PickFolderDialog();
            if (newFolder == null) return;
            ROOT_FOLDERS.Add(newFolder);
            Task.Run(() => Environment.SetEnvironmentVariable(TM_ENV_NAME, string.Join(';', ROOT_FOLDERS), EnvironmentVariableTarget.User)); // if we await this, it takes too long. so just pray.
        }

        public static string RootFoldersContainFile (string path)
        {
            if(ROOT_FOLDERS == null) return null;
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
            if(result == null) return null;
            return result.Path;
        }

        public async void AddNewTabs (string[] files)
        {
            if(files == null || files.Length == 0) return;
            foreach (string file in files)
            {
                if (RootFoldersContainFile(file) == null) // todo check if every file inside, make more user friendly in general
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
                newTab.Content = page;
                Style tabStyle = (Style)Application.Current.Resources["myTabViewItem"];
                newTab.Style = tabStyle;
                TabView.TabItems.Add(newTab);
            }
            if (TabView.SelectedIndex < 0)
            {
                try
                {
                    TabView.SelectedIndex = 0;
                }
                catch { }
            }
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
            if(TabView.TabItems.Count > TabView.SelectedIndex + 1)
                TabView.SelectedItem = TabView.TabItems[TabView.SelectedIndex+1];
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
                    AddNewTabs(videoFilePaths.ToArray());
                }
            });
        }
        private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            if (item == null) return;
            switch (item.Tag)
            {
                case "file":
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
                case "folder":
                    await AddNewRootFolder();
                    break;
            }
        }
    }
}
