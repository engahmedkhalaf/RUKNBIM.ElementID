using Autodesk.Navisworks.Api;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RUKNBIM.SmartSelect
{
    public partial class ElementIDViewModel : ObservableObject
    {
        private readonly NavisworksSearchEngine _searchEngine;
        private readonly IDataService _dataService;

        [ObservableProperty]
        private string _elementIdsInput;

        [ObservableProperty]
        private bool _isolateSelected;

        [ObservableProperty]
        private bool _zoomToSelected;

        private List<string> _lastFoundIds = new List<string>();
        private List<string> _lastMissingIds = new List<string>();

        public ElementIDViewModel()
        {
            IsolateSelected = true;
            ZoomToSelected = true;
            _searchEngine = new NavisworksSearchEngine();
            _dataService = new ExcelDataService();
        }

        [RelayCommand]
        private void Search()
        {
            if (string.IsNullOrWhiteSpace(ElementIdsInput))
            {
                MessageBox.Show("Please enter Element IDs to search.", "Element ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ids = ElementIdsInput.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(id => id.Trim())
                                     .Where(id => !string.IsNullOrEmpty(id))
                                     .Distinct()
                                     .ToList();

            if (!ids.Any()) return;

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;

            // Find elements natively
            var items = _searchEngine.FindElements(ids, doc);

            // Select natively
            NavisworksActions.SelectElements(doc, items);

            // Track found vs missing for reporting
            _lastMissingIds = _searchEngine.GetMissingIds(ids);
            _lastFoundIds = ids.Except(_lastMissingIds).ToList();

            int foundCount = _lastFoundIds.Count;
            int missingCount = _lastMissingIds.Count;

            MessageBox.Show($"Search Complete.\nFound: {foundCount}\nMissing: {missingCount}", "Element ID", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void Isolate()
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            var items = doc.CurrentSelection.SelectedItems;
            NavisworksActions.RestoreVisibility(doc);
            NavisworksActions.HideUnselected(doc, items);
            NavisworksActions.FocusCamera(doc);
            NavisworksActions.CreateSectionBox(doc, items);
        }

        [RelayCommand]
        private void HideUnselected()
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            var items = doc.CurrentSelection.SelectedItems;
            NavisworksActions.HideUnselected(doc, items);
        }

        [RelayCommand]
        private void RestoreVisibility()
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            NavisworksActions.RestoreVisibility(doc);
        }

        [RelayCommand]
        private void SectionBox()
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            var items = doc.CurrentSelection.SelectedItems;
            NavisworksActions.CreateSectionBox(doc, items);
        }

        [RelayCommand]
        private void FocusCamera()
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            NavisworksActions.FocusCamera(doc);
        }

        [RelayCommand]
        private void SaveViewpoint()
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            NavisworksActions.SaveViewpoint(doc, "");
        }

        [RelayCommand]
        private void RestoreViewpoint()
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            NavisworksActions.RestoreViewpoint(doc);
        }

        [RelayCommand]
        private void ImportExcel()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls|CSV Files|*.csv",
                Title = "Select Excel File with Element IDs"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var ids = _dataService.ImportIdsFromExcel(openFileDialog.FileName);
                    ElementIdsInput = string.Join(Environment.NewLine, ids);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading Excel file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ExportReport()
        {
            if (!_lastFoundIds.Any() && !_lastMissingIds.Any())
            {
                MessageBox.Show("No search has been performed yet to generate a report.", "Export Report", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "Save Element Report",
                FileName = "ElementReport.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    _dataService.ExportReportToExcel(saveFileDialog.FileName, _lastFoundIds, _lastMissingIds);
                    MessageBox.Show("Report exported successfully.", "Export Report", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
