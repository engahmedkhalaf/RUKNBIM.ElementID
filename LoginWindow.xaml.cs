using System;
using System.Windows;
using System.Windows.Media;

namespace RUKNBIM.ElementID
{
    public partial class LoginWindow : Window
    {
        public LicenseInfo ResultLicense { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailInput.Text.Trim();
            var password = PasswordInput.Password;

            if (string.IsNullOrEmpty(email))
            {
                ShowError("Please enter your email address.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Please enter your password.");
                return;
            }

            // Disable controls during validation
            SetLoadingState(true);
            ShowInfo("Activating, please wait...");

            try
            {
                var licenseInfo = await SupabaseLicensingClient.LoginAsync(email, password);

                if (licenseInfo.Status == LicenseStatus.Valid)
                {
                    ResultLicense = licenseInfo;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowError(licenseInfo.Message ?? "Invalid credentials or license expired.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Activation error: {ex.Message}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            EmailInput.IsEnabled = !isLoading;
            PasswordInput.IsEnabled = !isLoading;
            ActivateButton.IsEnabled = !isLoading;
        }

        private void ShowError(string message)
        {
            StatusText.Text = message;
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3B30")); // Red
        }

        private void ShowInfo(string message)
        {
            StatusText.Text = message;
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A86FF")); // Blue
        }
    }
}
