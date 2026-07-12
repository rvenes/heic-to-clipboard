# HeicToClipboard

Right click HEIC → C&C to JPEG → Ctrl+V

HeicToClipboard adds a Windows Explorer context-menu entry for `.heic` and `.heif` files.

It converts the selected images to JPEG and places them on the clipboard so they can be pasted directly into apps that do not support HEIC images (for example Discord, forums, email, or chat apps).

No permanent JPEG files are written next to the originals.

If you start `HeicToClipboard.exe` directly, it opens a small settings window where you can change output folder, max file size, JPEG quality, optional resolution cap, and how long temporary files are kept.

---

## Requirements

- Windows 10 or Windows 11
- **HEIF Image Extensions** installed from Microsoft Store

If HEIF support is missing the app will show a message explaining how to install it.

---

## Installation

### Easiest: from venes.org

Open **PowerShell** and run:

    irm https://venes.org/heictoclipboard/install.ps1 | iex

This downloads the latest release from https://venes.org/heictoclipboard/, verifies its SHA-256 checksum, installs it, and registers the context menu. No admin rights needed.

### Manual: from GitHub

1. Download the ZIP from the **GitHub Releases page**
2. Extract the ZIP
3. Open **PowerShell** in the extracted folder
4. Run:

powershell -ExecutionPolicy Bypass -File install.ps1 -SourceExe HeicToClipboard.exe

This installs the tool to:

%LocalAppData%\Programs\HeicToClipboard

and adds the **C&C to JPEG** option to the Explorer context menu for `.heic` and `.heif` files.

The command temporarily bypasses the PowerShell execution policy only for this install step.

---

## Usage

1. Right click one or more `.heic` files
2. Select **C&C to JPEG**
3. Go to Discord or another app
4. Press **Ctrl+V**

The images will be pasted as JPEG attachments.

If you start `HeicToClipboard.exe` directly from:

`%LocalAppData%\Programs\HeicToClipboard`

the app opens its settings window instead of converting files.

---

## Updates

When you open the settings window (start the exe without files), the app checks https://venes.org/heictoclipboard/latest.json for a new version. If one is available it asks before downloading; the download is verified with a SHA-256 checksum, the installed exe is swapped in place, and the app restarts. Nothing is sent to the server, and no check happens during normal context-menu conversions.

---

## What the tool does

- Converts HEIC / HEIF → JPEG
- Handles selections of many files at once: all selected images end up together in one clipboard batch, even when Explorer starts the conversions with a delay
- Converts wide-gamut photos (for example iPhone Display P3) to standard sRGB, so colors look right when pasted into apps that expect plain JPEG
- Picks the highest JPEG quality that fits under the size limit, and downscales efficiently when the limit is small
- Shows a clear error message for invalid or corrupt files, and a separate hint when the Windows HEIF codec is missing
- Uses these default settings out of the box:
  - JPEG quality starts at 95
  - maximum file size target is **9.8 MB per file**
  - original resolution is kept unless downscaling is needed to stay under the size limit
  - output is written to the temp folder
- Can be configured to change:
  - max file size
  - JPEG quality
  - longest-side resolution cap while keeping aspect ratio
  - output folder
- Stores temporary files in the default mode:

%TEMP%\HeicClipboardConvert

Temporary files are cleaned automatically when temp-folder mode is active. The default age is 24 hours and can be changed in the settings window.

---

## Security

- No telemetry
- No background services

The application never uploads files. The only network access is the version check against `venes.org/heictoclipboard` when the settings window is opened.

By default it writes generated JPEG files to the local temp folder and cleans up its own old temp files automatically.

If you choose a custom output folder in settings, files are written there instead, including network locations if you select one.

---

## Development

Build and test locally with:

    .\build.ps1

This restores, builds, runs the test suite, and publishes a self-contained exe to `artifacts\publish\win-x64`.

A few integration tests decode real HEIC files from a local sample folder. They are skipped automatically when the folder or the Windows HEIF codec is not available, so the test suite passes on any machine.

---

## Support the project

If this tool saved you time, you can support development with a small donation.

These tools are created and maintained in spare time and released as free and open-source software.

Donations help keep the projects maintained and improving.

Donate here:

https://www.paypal.com/donate/?business=VSSTWS8ETDPXW&no_recurring=0&item_name=Support+development+of+free+open-source+tools.+Donations+help+maintain+and+improve+these+projects.&currency_code=USD
