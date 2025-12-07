using System;
using System.Windows;

namespace AnimeFlow
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set up global exception handling
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Configure environment for VapourSynth
            ConfigureEnvironment();

            // Initialize logging
            InitializeLogging();
        }

        private void ConfigureEnvironment()
        {
            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var vsPath = System.IO.Path.Combine(basePath, "Dependencies", "vapoursynth");
                var rifePath = System.IO.Path.Combine(basePath, "Dependencies", "rife");
                
                // Add VapourSynth to PATH
                var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                if (!path.Contains(vsPath))
                {
                    Environment.SetEnvironmentVariable("PATH", $"{vsPath};{path}");
                }
                
                // Set VapourSynth plugin paths
                var vsPluginsPath = System.IO.Path.Combine(vsPath, "vapoursynth64", "plugins");
                var vsCoreplugins = System.IO.Path.Combine(vsPath, "vapoursynth64", "coreplugins");
                Environment.SetEnvironmentVariable("VS_PLUGINS_PATH", vsPluginsPath);
                
                // Set Python path for VapourSynth
                var pythonPath = Environment.GetEnvironmentVariable("PYTHONPATH") ?? string.Empty;
                var newPythonPath = $"{vsPath};{vsPluginsPath};{vsCoreplugins}";
                if (!string.IsNullOrEmpty(pythonPath))
                {
                    newPythonPath = $"{newPythonPath};{pythonPath}";
                }
                Environment.SetEnvironmentVariable("PYTHONPATH", newPythonPath);
            }
            catch
            {
                // Continue even if environment setup fails
            }
        }

        private void InitializeLogging()
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AnimeFlow",
                "logs"
            );

            System.IO.Directory.CreateDirectory(logPath);

            // Configure Serilog (if using)
            // Log.Logger = new LoggerConfiguration()
            //     .WriteTo.File(Path.Combine(logPath, "animeflow.log"))
            //     .CreateLogger();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running.",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );

            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            System.Windows.MessageBox.Show(
                $"A fatal error occurred:\n\n{exception?.Message}\n\nThe application will now close.",
                "Fatal Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        }
    }
}
