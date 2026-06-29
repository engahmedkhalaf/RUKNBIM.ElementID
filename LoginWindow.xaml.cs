using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RUKNBIM.ElementID
{
    public partial class LoginWindow : Window
    {
        public LicenseInfo ResultLicense { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            RefreshLicenseStatus();
        }

        private void Menu_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid menuGrid && menuGrid.Tag != null)
            {
                int index = int.Parse(menuGrid.Tag.ToString());
                SwitchTab(index);
            }
        }

        private void SwitchTab(int index)
        {
            // Reset all menu styles
            ResetMenuItemStyle(MenuGetLicense, IndicatorGetLicense, TextGetLicense);
            ResetMenuItemStyle(MenuActivateLicense, IndicatorActivateLicense, TextActivateLicense);
            ResetMenuItemStyle(MenuLicenseStatus, IndicatorLicenseStatus, TextLicenseStatus);
            ResetMenuItemStyle(MenuInformation, IndicatorInformation, TextInformation);

            // Hide all tab content
            TabGetLicense.Visibility = Visibility.Collapsed;
            TabActivateLicense.Visibility = Visibility.Collapsed;
            TabLicenseStatus.Visibility = Visibility.Collapsed;
            TabInformation.Visibility = Visibility.Collapsed;

            // Activate chosen tab
            switch (index)
            {
                case 0:
                    SetActiveMenuItemStyle(MenuGetLicense, IndicatorGetLicense, TextGetLicense);
                    TabGetLicense.Visibility = Visibility.Visible;
                    break;
                case 1:
                    SetActiveMenuItemStyle(MenuActivateLicense, IndicatorActivateLicense, TextActivateLicense);
                    TabActivateLicense.Visibility = Visibility.Visible;
                    break;
                case 2:
                    SetActiveMenuItemStyle(MenuLicenseStatus, IndicatorLicenseStatus, TextLicenseStatus);
                    TabLicenseStatus.Visibility = Visibility.Visible;
                    RefreshLicenseStatus();
                    break;
                case 3:
                    SetActiveMenuItemStyle(MenuInformation, IndicatorInformation, TextInformation);
                    TabInformation.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void SetActiveMenuItemStyle(Grid grid, System.Windows.Shapes.Rectangle indicator, TextBlock text)
        {
            grid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#141C2F"));
            indicator.Visibility = Visibility.Visible;
            text.Foreground = Brushes.White;
        }

        private void ResetMenuItemStyle(Grid grid, System.Windows.Shapes.Rectangle indicator, TextBlock text)
        {
            grid.Background = Brushes.Transparent;
            indicator.Visibility = Visibility.Collapsed;
            text.Foreground = Brushes.Gray;
        }

        // --- Tab 1: Get License Actions ---
        private async void StartTrial_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var info = await SupabaseLicensingClient.StartTrialAsync();
                if (info.Status == LicenseStatus.Valid)
                {
                    MessageBox.Show(
                        $"Free trial active!\n\n{info.DaysRemaining} day(s) remaining.",
                        "Trial Started",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    RefreshLicenseStatus();
                    SwitchTab(2);
                }
                else if (info.Status == LicenseStatus.Expired)
                {
                    MessageBox.Show(
                        "This machine has already used its free trial.",
                        "Trial Already Used",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
                else
                {
                    MessageBox.Show($"Could not start trial: {info.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not start trial: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RequestCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string email = string.IsNullOrWhiteSpace(EmailInput.Text) ? "(Not specified by user)" : EmailInput.Text.Trim();
                string subject = "RUKNBIM API Add-in Activation Code Request";
                
                string body = $"Hello Ahmed,\n\n" +
                              $"I would like to request an activation code for the RUKNBIM API Add-in.\n\n" +
                              $"--- Device Details ---\n" +
                              $"Machine ID: {Environment.MachineName} ({Environment.UserName})\n" +
                              $"Email Address: {email}\n";

                string mailtoUrl = $"mailto:engkhalaf7@gmail.com?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
                
                Process.Start(new ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open email client: {ex.Message}\n\nYou can manually email engkhalaf7@gmail.com.", "Support", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string email = string.IsNullOrWhiteSpace(EmailInput.Text) ? "(Not specified)" : EmailInput.Text.Trim();
                string subject = "RUKNBIM API Add-in License Purchase";
                string body = $"Hello Ahmed,\n\nI would like to purchase a full license for RUKNBIM API.\n\nEmail: {email}\nMachine: {Environment.MachineName} ({Environment.UserName})\n";
                string mailto = $"mailto:engkhalaf7@gmail.com?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
                Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open email client: {ex.Message}\n\nYou can manually email engkhalaf7@gmail.com.", "License Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // --- Tab 2: Activate Actions ---
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

            SetLoadingState(true);
            ShowInfo("Connecting to Supabase...");

            try
            {
                var licenseInfo = await SupabaseLicensingClient.LoginAsync(email, password);

                if (licenseInfo.Status == LicenseStatus.Valid)
                {
                    ResultLicense = licenseInfo;
                    ShowSuccess("Activation Successful!");
                    
                    // Switch to Status Tab and update
                    RefreshLicenseStatus();
                    SwitchTab(2);
                }
                else
                {
                    ShowError(licenseInfo.Message ?? "Invalid credentials or license expired.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Activation failed: {ex.Message}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        // --- Tab 3: Status Details & Deactivation ---
        private void RefreshLicenseStatus()
        {
            // Run status check safely
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var info = await SupabaseLicensingClient.ValidateLicenseAsync();
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (info.Status == LicenseStatus.Valid)
                        {
                            StatusValText.Text = "Active";
                            StatusValText.Foreground = Brushes.LightGreen;
                            EmailValText.Text = info.Email ?? "Activated Account";
                            string typeText = info.LicenseType ?? "Trial";
                            TypeValText.Text = $"{typeText} ({info.DaysRemaining} {(info.DaysRemaining == 1 ? "Day" : "Days")} Remaining)";
                            ExpiryValText.Text = info.TrialEndDate.HasValue 
                                ? info.TrialEndDate.Value.ToLocalTime().ToString("dd MMMM yyyy") 
                                : "N/A";
                            BtnStartTrial.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            StatusValText.Text = info.Status == LicenseStatus.Expired ? "Expired" : "Not Activated";
                            StatusValText.Foreground = Brushes.Red;
                            EmailValText.Text = info.Email ?? "N/A";
                            TypeValText.Text = "N/A";
                            ExpiryValText.Text = info.TrialEndDate.HasValue 
                                ? info.TrialEndDate.Value.ToLocalTime().ToString("dd MMMM yyyy") 
                                : "N/A";
                            BtnStartTrial.Visibility = Visibility.Visible;
                        }
                    });
                }
                catch
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusValText.Text = "Error Loading Status";
                        StatusValText.Foreground = Brushes.Red;
                    });
                }
            });
        }

        private void DeactivateButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to deactivate and remove your local license cache?", 
                "Deactivate License", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                SupabaseLicensingClient.DeleteCache();
                ResultLicense = null;
                
                // Clear inputs
                EmailInput.Clear();
                PasswordInput.Clear();
                StatusText.Text = "";

                // Reset Status panel labels
                StatusValText.Text = "Not Activated";
                StatusValText.Foreground = Brushes.Red;
                EmailValText.Text = "N/A";
                TypeValText.Text = "N/A";
                ExpiryValText.Text = "N/A";

                MessageBox.Show("License has been deactivated successfully.", "Deactivated", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Redirect back to Get License
                SwitchTab(0);
            }
        }

        // --- Tab 4: Info link actions ---
        private void SupportLink_Click(object sender, MouseButtonEventArgs e)
        {
            RequestCode_Click(sender, e);
        }

        private void WebLink_Click(object sender, MouseButtonEventArgs e)
        {
            Purchase_Click(sender, e);
        }

        // --- Shared Helpers ---
        private void SetLoadingState(bool isLoading)
        {
            EmailInput.IsEnabled = !isLoading;
            PasswordInput.IsEnabled = !isLoading;
            ActivateButton.IsEnabled = !isLoading;
        }

        private void ShowError(string message)
        {
            StatusText.Text = message;
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3B30"));
        }

        private void ShowInfo(string message)
        {
            StatusText.Text = message;
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A86FF"));
        }

        private void ShowSuccess(string message)
        {
            StatusText.Text = message;
            StatusText.Foreground = Brushes.LightGreen;
        }

        private void OpenLink(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
