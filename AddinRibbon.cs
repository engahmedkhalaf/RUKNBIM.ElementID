using System;
using Autodesk.Navisworks.Api.Plugins;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Navisworks.Api;

namespace RUKNBIM.ElementID
{
    // Loader plugin: forces Navisworks to load this assembly at startup so the
    // ribbon CommandHandler below is discovered. AddInLocation.None keeps it off
    // the default "Add-Ins" tab.
    [PluginAttribute("RUKNBIM.ElementID.Loader", "RUKN", DisplayName = "Loader")]
    [AddInPluginAttribute(AddInLocation.None)]
    public class ElementIDLoader : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            return 0;
        }
    }

    [PluginAttribute("RUKNBIM.ElementID", "RUKN", DisplayName = "RUKNBIM Smart Select")]
    [Strings("RUKNBIM.ElementID.name")]
    [RibbonLayout("RUKNBIM.ElementID.xaml")]
    [RibbonTab("RuknTab", DisplayName = "RUKNBIM Smart Select")]
    [Command("ZoomToSelection", DisplayName = "Zoom To\nSelection", Icon = "Images\\R_icon_blue_16px.png", LargeIcon = "Images\\R_icon_blue_32px.png")]
    [Command("IsolateSelection", DisplayName = "Isolate\nSelection", Icon = "Images\\R_icon_green_16px.png", LargeIcon = "Images\\R_icon_green_32px.png")]
    [Command("ClearIsolation", DisplayName = "Clear\nIsolation", Icon = "Images\\R_icon_teal_16px.png", LargeIcon = "Images\\R_icon_teal_32px.png")]
    [Command("AutoSectionBox", DisplayName = "Auto\nSection Box", Icon = "Images\\R_icon_orange_16px.png", LargeIcon = "Images\\R_icon_orange_32px.png")]
    [Command("ClearSectionBox", DisplayName = "Clear\nSection Box", Icon = "Images\\R_icon_red_16px.png", LargeIcon = "Images\\R_icon_red_32px.png")]
    [Command("SaveViewpoint", DisplayName = "Save\nViewpoint", Icon = "Images\\R_icon_gold_16px.png", LargeIcon = "Images\\R_icon_gold_32px.png")]
    [Command("ShowInfo", DisplayName = "Info", Icon = "Images\\Info_16.png", LargeIcon = "Images\\Info_32.png")]
    public class RibbonCommandHandler : CommandHandlerPlugin
    {
        private static bool CheckLicense()
        {
            try
            {
                // Run license check on ThreadPool to avoid UI deadlocks
                var info = System.Threading.Tasks.Task.Run(async () => 
                    await SupabaseLicensingClient.ValidateLicenseAsync()
                ).GetAwaiter().GetResult();

                if (info.Status == LicenseStatus.Valid)
                {
                    return true;
                }

                // Show Login Window if not valid
                var loginWindow = new LoginWindow();
                var result = loginWindow.ShowDialog();

                if (result == true && loginWindow.ResultLicense != null && loginWindow.ResultLicense.Status == LicenseStatus.Valid)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Licensing system error: {ex.Message}", "RUKNBIM Licensing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        public override int ExecuteCommand(string name, params string[] parameters)
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;

            // Allow the Info command to run without a license
            if (name != "ShowInfo")
            {
                if (!CheckLicense())
                {
                    return 0;
                }
            }

            switch (name)
            {
                case "ZoomToSelection":
                    NavisworksActions.FocusCamera(doc);
                    break;
                case "IsolateSelection":
                    NavisworksActions.RestoreVisibility(doc);
                    NavisworksActions.HideUnselected(doc, doc.CurrentSelection.SelectedItems);
                    NavisworksActions.FocusCamera(doc);
                    break;
                case "ClearIsolation":
                    NavisworksActions.RestoreVisibility(doc);
                    NavisworksActions.ClearSectionBox(doc);
                    break;
                case "AutoSectionBox":
                    NavisworksActions.CreateSectionBox(doc, doc.CurrentSelection.SelectedItems);
                    break;
                case "ClearSectionBox":
                    NavisworksActions.ClearSectionBox(doc);
                    break;
                case "SaveViewpoint":
                    NavisworksActions.SaveViewpoint(doc, "");
                    break;
                case "ShowInfo":
                    ShowInfoWindow();
                    break;
            }
            return 0;
        }

        private static void ShowInfoWindow()
        {
            var localAssemblyVer = typeof(RibbonCommandHandler).Assembly.GetName().Version;
            var localVer = new System.Version(localAssemblyVer.Major, localAssemblyVer.Minor, localAssemblyVer.Build);

            var window = new Window
            {
                Title = "RUKNBIM Info",
                Width = 420,
                Height = 280,
                Background = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#0B132B")),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                WindowStyle = WindowStyle.ToolWindow
            };

            var stackPanel = new StackPanel { Margin = new Thickness(25), VerticalAlignment = VerticalAlignment.Center };

            var versionLabel = new TextBlock
            {
                Text = $"Version: {localAssemblyVer}",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stackPanel.Children.Add(versionLabel);

            var licenseStatusLabel = new TextBlock
            {
                Text = "License: Checking...",
                Foreground = Brushes.Gray,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(licenseStatusLabel);

            var updateStatusLabel = new TextBlock
            {
                Text = "Checking for updates...",
                Foreground = Brushes.Gray,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stackPanel.Children.Add(updateStatusLabel);

            var preparedLabel = new TextBlock
            {
                Text = "Prepared by: Ahmed Khalaf (BIM Manager)",
                Foreground = Brushes.LightGray,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(preparedLabel);

            var mobileLabel = new TextBlock
            {
                Text = "Mobile: +966542554127",
                Foreground = Brushes.LightGray,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(mobileLabel);

            // Contact Support
            var supportPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var supportLabel = new TextBlock
            {
                Text = "Contact Support: ",
                Foreground = Brushes.LightGray,
                FontSize = 13
            };
            supportPanel.Children.Add(supportLabel);

            var supportLink = new TextBlock
            {
                Text = "engkhalaf7@gmail.com",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#3A86FF")),
                FontSize = 13,
                Cursor = Cursors.Hand,
                TextDecorations = TextDecorations.Underline
            };
            supportLink.MouseDown += (s, e) => {
                try { System.Diagnostics.Process.Start("mailto:engkhalaf7@gmail.com"); } catch { }
            };
            supportPanel.Children.Add(supportLink);
            stackPanel.Children.Add(supportPanel);

            // Website
            var webPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var webLabel = new TextBlock
            {
                Text = "Website: ",
                Foreground = Brushes.LightGray,
                FontSize = 13
            };
            webPanel.Children.Add(webLabel);

            var webLink = new TextBlock
            {
                Text = "rukn-bim-website-opka.vercel.app",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3A86FF")),
                FontSize = 13,
                Cursor = Cursors.Hand,
                TextDecorations = TextDecorations.Underline
            };
            webLink.MouseDown += (s, e) => {
                try { System.Diagnostics.Process.Start("https://rukn-bim-website-opka.vercel.app"); } catch { }
            };
            webPanel.Children.Add(webLink);
            stackPanel.Children.Add(webPanel);

            // Check licensing and trial status asynchronously
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var info = await SupabaseLicensingClient.ValidateLicenseAsync();
                    window.Dispatcher.Invoke(() =>
                    {
                        if (info.Status == LicenseStatus.Valid)
                        {
                            licenseStatusLabel.Text = $"License: Active ({info.DaysRemaining} days remaining)";
                            licenseStatusLabel.Foreground = Brushes.LightGreen;
                        }
                        else if (info.Status == LicenseStatus.Expired)
                        {
                            licenseStatusLabel.Text = "License: Trial Expired";
                            licenseStatusLabel.Foreground = Brushes.Red;
                        }
                        else
                        {
                            licenseStatusLabel.Text = "License: Not Activated";
                            licenseStatusLabel.Foreground = Brushes.Orange;
                        }
                    });
                }
                catch
                {
                    window.Dispatcher.Invoke(() =>
                    {
                        licenseStatusLabel.Text = "License: Could not verify";
                        licenseStatusLabel.Foreground = Brushes.Gray;
                    });
                }
            });

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using (var client = new System.Net.WebClient())
                    {
                        client.Headers.Add("User-Agent", "Rukn-Bim-Plugin");
                        string xml = client.DownloadString("https://raw.githubusercontent.com/engahmedkhalaf/RUKNBIM.ElementID/main/PackageContents.xml");
                        var match = System.Text.RegularExpressions.Regex.Match(xml, "AppVersion=\"([^\"]+)\"");
                        if (match.Success)
                        {
                            var remoteVer = new System.Version(match.Groups[1].Value);

                            window.Dispatcher.Invoke(() =>
                            {
                                if (remoteVer > localVer)
                                {
                                    updateStatusLabel.Text = $"Update available: v{remoteVer} (Click to open GitHub)";
                                    updateStatusLabel.Foreground = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#FFD000"));
                                    updateStatusLabel.Cursor = Cursors.Hand;
                                    updateStatusLabel.TextDecorations = TextDecorations.Underline;
                                    updateStatusLabel.MouseDown += (s, e) =>
                                    {
                                        try { System.Diagnostics.Process.Start("https://github.com/engahmedkhalaf/RUKNBIM.ElementID"); } catch { }
                                    };
                                }
                                else
                                {
                                    updateStatusLabel.Text = "Plugin is up to date";
                                    updateStatusLabel.Foreground = Brushes.LightGreen;
                                }
                            });
                        }
                    }
                }
                catch
                {
                    window.Dispatcher.Invoke(() =>
                    {
                        updateStatusLabel.Text = "Could not check for updates";
                        updateStatusLabel.Foreground = Brushes.Gray;
                    });
                }
            });

            window.Content = stackPanel;
            window.ShowDialog();
        }
    }
}
