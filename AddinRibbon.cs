using System;
using Autodesk.Navisworks.Api.Plugins;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Navisworks.Api;

namespace RUKNBIM.SmartSelect
{
    // Loader plugin: forces Navisworks to load this assembly at startup so the
    // ribbon CommandHandler below is discovered. AddInLocation.None keeps it off
    // the default "Add-Ins" tab.
    [PluginAttribute("RUKNBIM.SmartSelect.Loader", "RUKN", DisplayName = "Loader")]
    [AddInPluginAttribute(AddInLocation.None)]
    public class ElementIDLoader : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            // Silently verify license in background on startup to catch revoked licenses
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await SupabaseLicensingClient.ValidateLicenseAsync();
                }
                catch { }
            });
            return 0;
        }
    }

    [PluginAttribute("RUKNBIM.SmartSelect", "RUKN", DisplayName = "RUKNBIM Smart Select")]
    [Strings("RUKNBIM.SmartSelect.name")]
    [RibbonLayout("RUKNBIM.SmartSelect.xaml")]
    [RibbonTab("RuknTab", DisplayName = "RUKNBIM Smart Select")]
    [Command("ZoomToSelection", DisplayName = "Zoom To\nSelection", Icon = "Images\\R_icon_blue_16px.png", LargeIcon = "Images\\R_icon_blue_32px.png")]
    [Command("IsolateSelection", DisplayName = "Isolate\nSelection", Icon = "Images\\R_icon_green_16px.png", LargeIcon = "Images\\R_icon_green_32px.png")]
    [Command("ClearIsolation", DisplayName = "Clear\nIsolation", Icon = "Images\\R_icon_teal_16px.png", LargeIcon = "Images\\R_icon_teal_32px.png")]
    [Command("AutoSectionBox", DisplayName = "Auto\nSection Box", Icon = "Images\\R_icon_orange_16px.png", LargeIcon = "Images\\R_icon_orange_32px.png")]
    [Command("ClearSectionBox", DisplayName = "Clear\nSection Box", Icon = "Images\\R_icon_red_16px.png", LargeIcon = "Images\\R_icon_red_32px.png")]
    [Command("SaveViewpoint", DisplayName = "Save\nViewpoint", Icon = "Images\\R_icon_gold_16px.png", LargeIcon = "Images\\R_icon_gold_32px.png")]
    [Command("ShowInfo", DisplayName = "License\nManager", Icon = "Images\\Info_16.png", LargeIcon = "Images\\Info_32.png")]
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
            var loginWindow = new LoginWindow();
            loginWindow.ShowDialog();
        }
    }
}
