# Technical Reference: RUKNBIM.ElementID API

The plugin is structured in a clean **MVVM (Model-View-ViewModel)** architectural pattern, integrated with Autodesk Navisworks Manage API.

---

## 🛠️ Namespaces & Class Reference

### 1. Plugin Lifecycle & Entry Point (`RUKNBIM.ElementID`)
* **`ElementIDLoader`** (Inherits `AddInPlugin`)
  * **Purpose**: Assembly load booster. Runs silently on Navisworks startup (`AddInLocation.None`) to ensure the command handler below is discovered and registered.
* **`RibbonCommandHandler`** (Inherits `CommandHandlerPlugin`)
  * **Purpose**: Handles ribbon click events.
  * **Key Methods**:
    * `ExecuteCommand(string name, params string[] parameters)`: Listens for command strings registered in the ribbon layout and triggers their respective API actions.
    * `ShowInfoWindow()`: Spawns the WPF developer support dialog in a separate background thread with a live update check.

---

### 2. User Interface Layer (`RUKNBIM.ElementID`)
* **`ElementIDView`** (Inherits `UserControl`)
  * **Purpose**: WPF view designed to capture Revit Element IDs and host data utility controls. Sets its `DataContext` to a new `ElementIDViewModel` instance on instantiation.
* **`ElementIDViewModel`** (Inherits `ObservableObject` / MVVM Toolkit)
  * **Purpose**: Controls the view logic and handles commands via `[RelayCommand]`.
  * **Properties**:
    * `ElementIdsInput` (string): Text string binding for pasted Element IDs.
    * `SearchBtnColor`, `ImportBtnColor`, `ExportBtnColor` (strings): Random hex color codes generated in the constructor to style the WPF buttons.
  * **Key Methods**:
    * `Search()`: Compiles a distinct list of IDs, invokes the search engine, updates active document selection, and alerts the operator of results.
    * `ImportExcel()` / `ExportReport()`: Triggers the Excel data service handlers.

---

### 3. Data Integration Layer (`RUKNBIM.ElementID`)
* **`IDataService`** (Interface)
  * Defines the contract for importing/exporting Element ID listings.
* **`ExcelDataService`** (Implements `IDataService`)
  * **Purpose**: Communicates with spreadsheets via the `EPPlus` library.
  * **Key Methods**:
    * `ImportIdsFromExcel(string filePath)`: Opens a workbook, reads cell values column-by-column from the first worksheet, and returns a clean list of string IDs.
    * `ExportReportToExcel(string filePath, ...)`: Builds a report sheet splitting elements into "Found Elements" and "Missing Elements" columns.

---

### 4. Navisworks Core Engine (`RUKNBIM.ElementID`)
* **`NavisworksSearchEngine`**
  * **Purpose**: Compiles and executes native search filters.
  * **Key Methods**:
    * `FindElements(IEnumerable<string> ids, Document doc)`: Splits incoming query IDs into chunks of **500 items** and dynamically compiles `SearchCondition` rules searching both integer and string formats for:
      1. `"Item"` category $\rightarrow$ `"Element Id"` property.
      2. `"Element ID"` category $\rightarrow$ `"Value"` property.
    * `GetMissingIds(IEnumerable<string> ids)`: Computes the difference to isolate unmatched elements.
* **`NavisworksActions`**
  * **Purpose**: Interacts directly with Navisworks selection, camera, visibility, and sectioning systems.
  * **Key Methods**:
    * `SelectElements(Document doc, ModelItemCollection items)`: Selects elements in the active viewport.
    * `HideUnselected(Document doc, ModelItemCollection items)`: Optimized visibility isolate that walks the target selection ancestors and hides only their non-ancestor siblings (improves speed in large models).
    * `FocusCamera(Document doc)`: Focuses camera on selection bounding box and zooms closer (80% focal distance adjustment).
    * `CreateSectionBox(Document doc, ModelItemCollection items)`: Switches to box mode and fits clipping planes around selection.
    * `ClearSectionBox(Document doc)`: Disables clipping plane mode.

---

## 🎨 Layout and Configuration Files
* **[PackageContents.xml](file:///d:/API%20Khalaf/Rukn.Bim.Api/WIP/NAVIS/RUKNBIM.ElementID/PackageContents.xml)**: Standard Autodesk ApplicationPackage definition declaring compatibility with Navisworks platform series `Nw19` to `Nw23` (Versions 2022 to 2026).
* **[en-US/RUKNBIM.ElementID.xaml](file:///d:/API%20Khalaf/Rukn.Bim.Api/WIP/NAVIS/RUKNBIM.ElementID/en-US/RUKNBIM.ElementID.xaml)**: Declares the ribbon layout (RibbonTab, RibbonPanel, and NWRibbonButtons) loaded by the Navisworks GUI engine.
