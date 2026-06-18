using Autodesk.Navisworks.Api.Plugins;
using System.Windows;
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

    [PluginAttribute("RUKNBIM.ElementID", "RUKN", DisplayName = "KH Addins")]
    [Strings("RUKNBIM.ElementID.name")]
    [RibbonLayout("RUKNBIM.ElementID.xaml")]
    [RibbonTab("KHAddinsTab", DisplayName = "KH Addins")]
    [Command("SelectByRevitID", DisplayName = "Select By\nRevit ID", Icon = "Images\\ElementID_16.ico", LargeIcon = "Images\\ElementID_32.png")]
    [Command("ZoomToSelection", DisplayName = "Zoom To\nSelection", Icon = "Images\\ElementID_16.ico", LargeIcon = "Images\\ElementID_32.png")]
    [Command("IsolateSelection", DisplayName = "Isolate\nSelection", Icon = "Images\\ElementID_16.ico", LargeIcon = "Images\\ElementID_32.png")]
    [Command("ClearIsolation", DisplayName = "Clear\nIsolation", Icon = "Images\\ElementID_16.ico", LargeIcon = "Images\\ElementID_32.png")]
    [Command("AutoSectionBox", DisplayName = "Auto\nSection Box", Icon = "Images\\ElementID_16.ico", LargeIcon = "Images\\ElementID_32.png")]
    [Command("ClearSectionBox", DisplayName = "Clear\nSection Box", Icon = "Images\\ElementID_16.ico", LargeIcon = "Images\\ElementID_32.png")]
    [Command("SaveViewpoint", DisplayName = "Save\nViewpoint", Icon = "Images\\ElementID_16.ico", LargeIcon = "Images\\ElementID_32.png")]
    public class RibbonCommandHandler : CommandHandlerPlugin
    {
        private static Window _navigatorWindow;

        public override int ExecuteCommand(string name, params string[] parameters)
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;

            switch (name)
            {
                case "SelectByRevitID":
                    if (_navigatorWindow == null)
                    {
                        var view = new ElementIDView();
                        _navigatorWindow = new Window
                        {
                            Title = "Element ID",
                            Content = view,
                            Width = 350,
                            Height = 550,
                            Topmost = true,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen
                        };
                        _navigatorWindow.Closed += (s, e) => _navigatorWindow = null;
                        _navigatorWindow.Show();
                    }
                    else
                    {
                        _navigatorWindow.Focus();
                    }
                    break;
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
                    break;
                case "AutoSectionBox":
                    NavisworksActions.CreateSectionBox(doc, doc.CurrentSelection.SelectedItems);
                    break;
                case "ClearSectionBox":
                    try
                    {
                        dynamic state = Autodesk.Navisworks.Api.ComApi.ComApiBridge.State;
                        dynamic clipPlanes = state.CurrentView.ClipPlanes;
                        clipPlanes.Mode = 0; // nwEClipMode.eNone
                    }
                    catch { }
                    break;
                case "SaveViewpoint":
                    NavisworksActions.SaveViewpoint(doc, "");
                    break;
            }
            return 0;
        }
    }
}
