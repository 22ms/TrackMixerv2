using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace TrackMixerv2
{
    public partial class App : Application
    {
        private MainWindow m_window;
        private AppInstance mainInstance;
        private DispatcherQueue dispatcherQueue;

        public App()
        {
            if (UiTestBootstrap.IsEnabled)
            {
                UnhandledException += (_, e) =>
                {
                    try
                    {
                        File.WriteAllText(AppPaths.UiTestCrashLogPath, e.Exception?.ToString() ?? "Unknown UI test crash");
                    }
                    catch
                    {
                    }
                };
            }

            this.InitializeComponent();
        }
        private void MainInstance_Activated(object sender, AppActivationArguments e)
        {
            Activate(e);
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            if (!UiTestBootstrap.IsEnabled)
            {
                mainInstance = AppInstance.FindOrRegisterForKey("main");
                if (!mainInstance.IsCurrent)
                {
                    await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                    Process.GetCurrentProcess().Kill();
                    return;
                }

                mainInstance.Activated += MainInstance_Activated;
            }

            Activate(activatedEventArgs);
        }

        public void Activate(AppActivationArguments args)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                string[] files = ResolveActivationFiles(args);
                if (m_window == null)
                {
                    m_window = new MainWindow(files);
                }
                else if (files != null && files.Length > 0)
                {
                    if (LocalSettingsStore.ContainsKey(LocalSettingsStore.Keys.DoubleClickOnNewTab))
                    {
                        m_window.AddNewTabs(files, LocalSettingsStore.GetBool(LocalSettingsStore.Keys.DoubleClickOnNewTab));
                    }
                    else
                    {
                        m_window.AddNewTabs(files);
                    }
                }
                m_window.Activate();
            });
        }

        private static string[] ResolveActivationFiles(AppActivationArguments args)
        {
            if (UiTestBootstrap.IsEnabled && !string.IsNullOrWhiteSpace(UiTestBootstrap.LaunchFile))
                return new[] { UiTestBootstrap.LaunchFile };

            if (args.Kind == ExtendedActivationKind.File)
            {
                var fileArgs = args.Data as FileActivatedEventArgs;
                if (fileArgs != null)
                    return fileArgs.Files.Select(file => file.Path).ToArray();
            }

            return null;
        }
    }
}
