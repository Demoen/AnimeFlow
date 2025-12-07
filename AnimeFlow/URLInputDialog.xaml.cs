using System;
using System.Windows;
using System.Windows.Input;

namespace AnimeFlow
{
    public partial class URLInputDialog : Window
    {
        public string Url { get; private set; } = string.Empty;

        public URLInputDialog()
        {
            InitializeComponent();
            UrlTextBox.Focus();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter a URL.", "Invalid URL", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!Uri.IsWellFormedUriString(UrlTextBox.Text, UriKind.Absolute))
            {
                System.Windows.MessageBox.Show("Please enter a valid URL.", "Invalid URL", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            Url = UrlTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UrlTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Open_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }
    }
}
