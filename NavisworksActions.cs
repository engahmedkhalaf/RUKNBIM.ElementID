using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using System;
using System.Linq;

namespace RUKNBIM.ElementID
{
    public static class NavisworksActions
    {
        public static void SelectElements(Document doc, ModelItemCollection items)
        {
            doc.CurrentSelection.Clear();
            if (items != null && items.Count > 0)
            {
                doc.CurrentSelection.CopyFrom(items);
                try
                {
                    Autodesk.Navisworks.Api.Interop.LcRmFrameworkInterface.ExecuteCommand(
                        "RoamerGUI_MODE_SELECT",
                        Autodesk.Navisworks.Api.Interop.LcUCIPExecutionContext.eTOOLBAR
                    );
                }
                catch { }
            }
        }

        public static void HideUnselected(Document doc, ModelItemCollection items)
        {
            if (items == null || items.Count == 0) return;

            // Ultra-fast isolate: Hide only the siblings of the selected items and their ancestors.
            var toHide = new ModelItemCollection();
            var allAncestors = new ModelItemCollection();

            foreach (var item in items)
            {
                allAncestors.AddRange(item.AncestorsAndSelf);
            }

            foreach (var item in items)
            {
                var ancestors = item.AncestorsAndSelf.ToList();
                foreach (var ancestor in ancestors)
                {
                    if (ancestor.Parent != null)
                    {
                        foreach (var sibling in ancestor.Parent.Children)
                        {
                            if (!allAncestors.Contains(sibling))
                            {
                                toHide.Add(sibling);
                            }
                        }
                    }
                    else
                    {
                        // Root level siblings
                        foreach (var model in doc.Models)
                        {
                            if (!allAncestors.Contains(model.RootItem))
                            {
                                toHide.Add(model.RootItem);
                            }
                        }
                    }
                }
            }

            if (toHide.Count > 0)
            {
                doc.Models.SetHidden(toHide, true);
            }
        }

        public static void RestoreVisibility(Document doc)
        {
            doc.Models.ResetAllHidden();
        }

        public static void FocusCamera(Document doc)
        {
            if (doc.CurrentSelection.SelectedItems.Count > 0)
            {
                try
                {
                    var items = doc.CurrentSelection.SelectedItems;
                    BoundingBox3D box = items.BoundingBox();
                    if (box != null)
                    {
                        var center = new Point3D(
                            (box.Min.X + box.Max.X) / 2.0,
                            (box.Min.Y + box.Max.Y) / 2.0,
                            (box.Min.Z + box.Max.Z) / 2.0
                        );

                        // Create a copy of current viewpoint to adjust
                        var vp = doc.CurrentViewpoint.CreateCopy();
                        
                        // Align camera using native ZoomBox first
                        vp.ZoomBox(box);

                        // Adjust position and focal distance to zoom in a little bit closer (80% of the default distance)
                        Vector3D direction = center - vp.Position;
                        double originalDistance = direction.Length;
                        if (originalDistance > 0.001)
                        {
                            double targetDistance = originalDistance * 0.8; // 80% of the default zoom-to-box distance
                            vp.Position = center - direction.Multiply(0.8);
                            vp.FocalDistance = targetDistance;
                        }

                        doc.CurrentViewpoint.CopyFrom(vp);
                    }
                    else
                    {
                        doc.ActiveView.FocusOnCurrentSelection();
                    }
                }
                catch
                {
                    doc.ActiveView.FocusOnCurrentSelection();
                }
            }
        }

        public static void CreateSectionBox(Document doc, ModelItemCollection items)
        {
            if (items == null || items.Count == 0) return;

            try
            {
                var vp = doc.CurrentViewpoint.ToViewpoint();
                var clipPlanes = vp?.InternalClipPlanes;
                bool isSectioningEnabled = clipPlanes != null && clipPlanes.IsEnabled();

                if (!isSectioningEnabled)
                {
                    Autodesk.Navisworks.Api.Interop.LcRmFrameworkInterface.ExecuteCommand(
                        "RoamerGUI_OM_SECTION_MASTER_ENABLE",
                        Autodesk.Navisworks.Api.Interop.LcUCIPExecutionContext.eTOOLBAR
                    );
                }

                // Switch to Box mode
                Autodesk.Navisworks.Api.Interop.LcRmFrameworkInterface.ExecuteCommand(
                    "RoamerGUI_OM_SECTION_Mode_Box",
                    Autodesk.Navisworks.Api.Interop.LcUCIPExecutionContext.eTOOLBAR
                );

                // Fit Selection
                Autodesk.Navisworks.Api.Interop.LcRmFrameworkInterface.ExecuteCommand(
                    "RoamerGUI_OM_SECTION_FIT_SELECTION",
                    Autodesk.Navisworks.Api.Interop.LcUCIPExecutionContext.eTOOLBAR
                );
            }
            catch
            {
                // Fallback
                doc.ActiveView.FocusOnCurrentSelection();
            }
        }

        public static void ClearSectionBox(Document doc)
        {
            try
            {
                var vp = doc.CurrentViewpoint.ToViewpoint();
                var clipPlanes = vp?.InternalClipPlanes;
                bool isSectioningEnabled = clipPlanes != null && clipPlanes.IsEnabled();

                if (isSectioningEnabled)
                {
                    Autodesk.Navisworks.Api.Interop.LcRmFrameworkInterface.ExecuteCommand(
                        "RoamerGUI_OM_SECTION_MASTER_ENABLE",
                        Autodesk.Navisworks.Api.Interop.LcUCIPExecutionContext.eTOOLBAR
                    );
                }
            }
            catch
            {
                // Fallback using COM
                try
                {
                    dynamic state = ComApiBridge.State;
                    dynamic clipPlanes = state.CurrentView.ClipPlanes;
                    clipPlanes.Mode = 0; // nwEClipMode.eNone
                }
                catch { }
            }
        }

        public static void SaveViewpoint(Document doc, string name)
        {
            SavedViewpoint newViewPoint = new SavedViewpoint(doc.CurrentViewpoint.ToViewpoint());
            newViewPoint.DisplayName = string.IsNullOrWhiteSpace(name) ? $"Saved View {DateTime.Now:HH:mm}" : name;
            doc.SavedViewpoints.AddCopy(newViewPoint);
        }

        public static void RestoreViewpoint(Document doc)
        {
            if (doc.SavedViewpoints.Value.Count > 0)
            {
                // Get the most recently added viewpoint
                var lastView = doc.SavedViewpoints.Value.LastOrDefault(x => x is SavedViewpoint) as SavedViewpoint;
                if (lastView != null)
                {
                    doc.CurrentViewpoint.CopyFrom(lastView.Viewpoint);
                }
            }
        }
    }
}
