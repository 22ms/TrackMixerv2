using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TrackMixerv2
{
    public partial class App : Application
    {
        private MainWindow m_window;
        private readonly AppInstance mainInstance;

        public App()
        {
            this.InitializeComponent();
            mainInstance = AppInstance.FindOrRegisterForKey("main");
            Debug.Assert(mainInstance.IsCurrent);
            mainInstance.Activated += MainInstance_Activated;
        }
        private void MainInstance_Activated(object sender, AppActivationArguments e)
        {
            Debug.WriteLine(e.Data.ToString());
            m_window = new MainWindow();
            m_window.Activate();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            string debugPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrackMixerv2", "debug.txt");
            var mainInstance = AppInstance.FindOrRegisterForKey("main");

            if (!mainInstance.IsCurrent)
            {
                // Redirect the activation (and args) to the "main" instance, and exit.
                var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                Process.GetCurrentProcess().Kill();
                return;
            }
            File.AppendAllText(debugPath, "Test");
            // Create the main window if it doesn't already exist
            if (m_window == null)
            {
                m_window = new MainWindow();
            }
            m_window.Activate();
            // Check if the activation is due to file launch
            AppActivationArguments arguments = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (arguments.Kind == ExtendedActivationKind.File)
            {
                // Extract the file paths from the arguments
                var fileArgs = arguments.Data as Windows.ApplicationModel.Activation.FileActivatedEventArgs;
                if (fileArgs != null)
                {
                    var files = fileArgs.Files.Select(file => file.Path).ToArray();
                    // Call the AddNewTabs method on the existing main window
                    m_window.AddNewTabs(files);
                }
            }
        }
    }
}
