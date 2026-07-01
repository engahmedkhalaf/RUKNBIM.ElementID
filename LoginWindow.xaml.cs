using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RUKNBIM.SmartSelect
{
    public partial class LoginWindow : Window
    {
        public LicenseInfo ResultLicense { get; private set; }
        private bool _isActivated = false;

        public LoginWindow()
        {
            InitializeComponent();
            RefreshLicenseStatus();
        }

        private void RefreshLicenseStatus()
        {
            // Disable inputs and button while checking
            SetLoadingState(true);
            StatusTextVal.Text = "Checking...";
            StatusTextVal.Foreground = Brushes.Gray;
            StatusDot.Fill = Brushes.Gray;

            Task.Run(async () =>
            {
                try
                {
                    var info = await SupabaseLicensingClient.ValidateLicenseAsync();
                    
                    Dispatcher.Invoke(() =>
                    {
                        UpdateLicenseUI(info);
                        SetLoadingState(false);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusTextVal.Text = "Error Checking Status";
                        StatusTextVal.Foreground = Brushes.Red;
                        StatusDot.Fill = Brushes.Red;
                        DetailsTextVal.Text = ex.Message;
                        SetLoadingState(false);
                    });
                }
            });
        }

        private void UpdateLicenseUI(LicenseInfo info)
        {
            if (info != null && info.Status == LicenseStatus.Valid)
            {
                _isActivated = true;
                ResultLicense = info;

                // Update Status details
                StatusTextVal.Text = "Activated";
                StatusTextVal.Foreground = Brushes.LightGreen;
                StatusDot.Fill = Brushes.LightGreen;

                string typeText = info.LicenseType ?? "Trial";
                if (info.TrialEndDate.HasValue)
                {
                    DetailsTextVal.Text = $"{typeText} ({info.DaysRemaining} day(s) remaining, expires {info.TrialEndDate.Value.ToLocalTime():dd MMMM yyyy})";
                }
                else
                {
                    DetailsTextVal.Text = $"{typeText} (Lifetime / Perpetual License)";
                }

                // Fill inputs if we have cached details
                EmailInput.Text = info.Email ?? "";
                CodeInput.Text = "************************"; // Masked code for safety
                EmailInput.IsEnabled = false;
                CodeInput.IsEnabled = false;

                // Toggle main button to Sign Out
                BtnActivate.Content = "Deactivate / Sign Out";
                BtnActivate.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")); // Red
            }
            else
            {
                _isActivated = false;
                ResultLicense = null;

                // Update Status details
                StatusTextVal.Text = info?.Status == LicenseStatus.Expired ? "Expired" : "Not Activated";
                StatusTextVal.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316")); // Orange/Red
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));

                DetailsTextVal.Text = info?.Message ?? "No active license found.";

                // Clear and enable inputs
                EmailInput.Text = "";
                CodeInput.Text = "";
                EmailInput.IsEnabled = true;
                CodeInput.IsEnabled = true;

                // Toggle main button to Activate
                BtnActivate.Content = "Activate License";
                BtnActivate.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")); // Blue
            }
        }

        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            if (_isActivated)
            {
                // Sign Out action
                if (MessageBox.Show("Are you sure you want to deactivate and remove your local license cache?", 
                    "Deactivate License", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    SupabaseLicensingClient.DeleteCache();
                    RefreshLicenseStatus();
                    MessageBox.Show("License has been deactivated successfully.", "Deactivated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            // Activation action
            var email = EmailInput.Text.Trim();
            var code = CodeInput.Text.Trim();

            if (string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Please enter your email address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("Please enter your activation code.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetLoadingState(true);
            StatusTextVal.Text = "Connecting...";
            StatusTextVal.Foreground = Brushes.SkyBlue;

            try
            {
                var licenseInfo = await SupabaseLicensingClient.LoginAsync(email, code);

                if (licenseInfo.Status == LicenseStatus.Valid)
                {
                    ResultLicense = licenseInfo;
                    UpdateLicenseUI(licenseInfo);
                    MessageBox.Show("Activation Successful!", "License Activated", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(licenseInfo.Message ?? "Invalid credentials or license expired.", "Activation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    RefreshLicenseStatus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Activation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshLicenseStatus();
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async void BtnTestConn_Click(object sender, RoutedEventArgs e)
        {
            BtnTestConn.IsEnabled = false;
            BtnTestConn.Content = "Testing...";

            try
            {
                bool isReachable = await SupabaseLicensingClient.PingAsync();
                if (isReachable)
                {
                    MessageBox.Show("✔ Connected to Supabase servers successfully.", "Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("✘ Connection failed. Please check your internet connection.", "Connection Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"✘ Connection failed: {ex.Message}", "Connection Test", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnTestConn.IsEnabled = true;
                BtnTestConn.Content = "Test Connection";
            }
        }

        private void RequestCode_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string email = string.IsNullOrWhiteSpace(EmailInput.Text) ? "(Not specified by user)" : EmailInput.Text.Trim();
                string subject = "RUKNBIM API Add-in Activation Code Request";
                
                string body = $"Hello Ahmed,\n\n" +
                              $"I would like to request an activation code for the RUKNBIM Smart Select Add-in.\n\n" +
                              $"--- Device Details ---\n" +
                              $"Machine ID: {Environment.MachineName} ({Environment.UserName})\n" +
                              $"Email Address: {email}\n";

                string mailtoUrl = $"mailto:support@ruknbim.com?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
                
                Process.Start(new ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open email client: {ex.Message}\n\nYou can manually email support@ruknbim.com.", "Support", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RuknBimBadge_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://www.ruknbim.com/") { UseShellExecute = true });
            }
            catch { }
        }

        private void SupportLink_Click(object sender, MouseButtonEventArgs e)
        {
            RequestCode_Click(sender, e);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SetLoadingState(bool isLoading)
        {
            EmailInput.IsEnabled = !isLoading;
            CodeInput.IsEnabled = !isLoading;
            BtnActivate.IsEnabled = !isLoading;
            BtnTestConn.IsEnabled = !isLoading;
        }
    }
}
