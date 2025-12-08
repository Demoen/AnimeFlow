using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace AnimeFlow
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";
        }

        private void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            // In a real implementation, this would check GitHub releases
            System.Windows.MessageBox.Show(
                "You are running the latest version of AnimeFlow.",
                "Update Check",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information
            );
        }

        private void ViewLicense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licensePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LICENSE.txt");
                if (System.IO.File.Exists(licensePath))
                {
                    Process.Start(new ProcessStartInfo(licensePath) { UseShellExecute = true });
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "License file not found.",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open license file: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error
                );
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
