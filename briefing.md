# Executive Briefing: Rukn Navisworks Element ID

**Rukn Navisworks Element ID** is a high-performance productivity addin for Autodesk Navisworks Manage (supporting versions 2022 to 2026). It bridges the gap between Revit design modeling and Navisworks coordination by allowing operators and BIM managers to instantly locate, isolate, and audit Revit elements inside Navisworks using their Revit Element IDs.

---

## 🌟 Key Capabilities

### 1. Advanced Element Locator & Selector
* **WPF Interface**: Allows operators to paste lists of Revit Element IDs (newline or comma-separated) directly into a clean, modern window.
* **Instant Native Search**: Performs optimized native Navisworks queries (processed in batches of 500 to maintain speed) matching either string or integer properties for `"Item" -> "Element Id"` or `"Element ID" -> "Value"`.
* **Dynamic Aesthetics**: Features custom-styled WPF buttons that initialize with distinct, professional colors from a curated palette every time the window is launched.

### 2. Bulk Data Auditing (Excel & CSV)
* **Import Lists**: Load thousands of Revit Element IDs directly from Excel spreadsheets (`.xlsx`/`.xls`) or CSV files.
* **Export Reports**: Generate Excel validation sheets displaying side-by-side lists of **Found Elements** vs. **Missing Elements** for quality control audits.

### 3. One-Click Ribbon Utilities
The dedicated **Rukn** ribbon tab provides 6 utility commands (each represented by a unique, color-coded icon):
* 🔵 **Zoom to Selection**: Focuses the camera closer than default (80% focal distance) on the selected elements.
* 🟢 **Isolate Selection**: Instantly hides all elements except the active selection and its parent siblings.
* ⚪ **Clear Isolation**: Resets visibility back to normal.
* 🟠 **Auto Section Box**: Automatically fits and enables a section clipping box around the selection.
* 🔴 **Clear Section Box**: Disables all clipping section boxes.
* 🟡 **Save Viewpoint**: Saves the current viewpoint with an automated timestamp.

### 4. Interactive Info & Update Checker
* Clicking the 🟣 **Info** button on the ribbon loads the support card (Version `1.0.0.0`, Prepared by Ahmed Khalaf, contact info, and website link).
* It features a **live background updater** that compares the local version against the latest code on your GitHub repository, displaying a gold hyperlink to alert the operator if an update is available.

---

## 📦 Packaging & Distribution
* **Inno Setup Script (`setup.iss`)**: Included in the repository root. Compiling it compiles a single-file installer executable (**`Rukn.ElementID.Setup.exe`**).
* **Automated Installation**: When operators run the installer, it automatically deploys the correct binary version to their local `%AppData%\Autodesk\ApplicationPlugins` folder for all installed Navisworks versions (2022–2026).
