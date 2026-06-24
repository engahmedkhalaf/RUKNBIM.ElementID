# Rukn Navisworks Element ID

An Autodesk Navisworks Manage plugin designed for fast selection, isolation, section-boxing, and tracking of elements by their Revit IDs.

---

## 🚀 Key Features

* **Advanced Element Selection**: Input, select, and highlight Revit Element IDs inside Navisworks models.
* **Bulk Import & Export**: Import Excel/CSV files containing lists of Element IDs, and export verification reports detailing which elements were found or missing.
* **Curated Ribbon Utilities**:
  * 🔵 **Zoom to Selection**: Instantly center and zoom on the active selection.
  * 🟢 **Isolate Selection**: Fast isolation (hides non-ancestor siblings and sibling subtrees).
  * 🟢 **Clear Isolation**: Restore model visibility.
  * 🟠 **Auto Section Box**: Automatically fit a section box around selected items.
  * 🔴 **Clear Section Box**: Clear active clipping boxes.
  * 🟡 **Save Viewpoint**: Save the current viewpoint.
  * 🟣 **Info**: Opens the developer & support card.
* **Live Update Checker**: The Info window automatically checks this GitHub repository in the background and notifies the operator if a newer version is available.
* **Aesthetic WPF UI**: Visual Studio styled dark dialogs with premium random color schemes generated on load.

---

## 🛠️ Build & Setup

### Requirements
* **Framework**: `.NET Framework 4.8`
* **Navisworks Manage**: Versions 2022 to 2026

### Development
1. Open the project in Visual Studio.
2. Build the solution.
3. The post-build script automatically registers the bundle to:
   `%AppData%\Autodesk\ApplicationPlugins\RUKNBIM.ElementID.bundle\`

*Note: Ensure Autodesk Navisworks is closed before building to avoid file-lock errors during copy.*

---

## 📦 Creating the Installer (.exe)

This project contains an Inno Setup script ([setup.iss](file:///d:/API%20Khalaf/Rukn.Bim.Api/WIP/NAVIS/RUKNBIM.ElementID/setup.iss)) to compile a single-file executable installer for operators.

1. Install [Inno Setup Compiler](https://jrsoftware.org/isdl.php).
2. Open **[setup.iss](file:///d:/API%20Khalaf/Rukn.Bim.Api/WIP/NAVIS/RUKNBIM.ElementID/setup.iss)** in the compiler.
3. Press `F9` to build.
4. The generated installer `Rukn.ElementID.Setup.exe` will be saved under the `Output/` directory, ready to be shared.
