using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TrackMixerv2
{
    public partial class App : Application
    {
        private MainWindow m_window;
        private AppInstance mainInstance;
        private DispatcherQueue dispatcherQueue;

        public App()
        {
            this.InitializeComponent();
        }
        private void MainInstance_Activated(object sender, AppActivationArguments e)
        {
            Activate(e);
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            mainInstance = AppInstance.FindOrRegisterForKey("main");
            var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (!mainInstance.IsCurrent)
            {
                await mainInstance.RedirectActivationToAsync(activatedEventArgs); 
                Process.GetCurrentProcess().Kill();
                return;
            }
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            mainInstance.Activated += MainInstance_Activated;
            Activate(activatedEventArgs);
        }

        public void Activate(AppActivationArguments args)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                string[] files = null;
                // Check if the activation is due to file launch
                if (args.Kind == ExtendedActivationKind.File)
                {
                    // Extract the file paths from the arguments
                    var fileArgs = args.Data as FileActivatedEventArgs;
                    if (fileArgs != null)
                    {
                        files = fileArgs.Files.Select(file => file.Path).ToArray();
                        // Call the AddNewTabs method on the existing main window
                    }
                }
                if (m_window == null)
                {
                    m_window = new MainWindow(files); // first startup
                }
                else
                {
                    m_window.AddNewTabs(files);
                }
                m_window.Activate();
            });
        }
    }
}
