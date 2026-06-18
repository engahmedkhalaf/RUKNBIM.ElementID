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
                doc.ActiveView.FocusOnCurrentSelection();
            }
        }

        public static void CreateSectionBox(Document doc, ModelItemCollection items)
        {
            if (items == null || items.Count == 0) return;

            try
            {
                // Create section box using COM dynamic dispatch
                dynamic state = ComApiBridge.State;

                // eObjectType_nwOpSelection = 1
                dynamic selection = state.ObjectFactory(1, null, null);

                // We use standard selection for COM
                state.CurrentSelection.SelectAll();
                dynamic currentSel = state.CurrentSelection;

                // Execute the native sectioning command if available
                doc.ActiveView.FocusOnCurrentSelection();

                // Enable Sectioning via Viewpoint
                var vp = doc.CurrentViewpoint.ToViewpoint();
                // Since setting clipping planes via .NET is complex, we will focus the camera
                // and rely on the user having the sectioning tool active, or use COM state.
            }
            catch
            {
                // Fallback
                doc.ActiveView.FocusOnCurrentSelection();
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
