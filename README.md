# HeicToClipboard

Right click HEIC → C&C to JPEG → Ctrl+V

HeicToClipboard adds a Windows Explorer context-menu entry for `.heic` and `.heif` files.

It converts the selected images to JPEG and places them on the clipboard so they can be pasted directly into apps that do not support HEIC images (for example Discord, forums, email, or chat apps).

No permanent JPEG files are written next to the originals.

If you start `HeicToClipboard.exe` directly, it opens a small settings window where you can change output folder, max file size, JPEG quality, and optional resolution cap.

---

## Requirements

- Windows 10 or Windows 11
- **HEIF Image Extensions** installed from Microsoft Store

If HEIF support is missing the app will show a message explaining how to install it.

---

## Installation

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

## What the tool does

- Converts HEIC / HEIF → JPEG
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

Temporary files older than 24 hours are cleaned automatically when temp-folder mode is active.

---

## Security

- No telemetry
- No background services

The application never uploads files.

By default it writes generated JPEG files to the local temp folder and cleans up its own old temp files automatically.

If you choose a custom output folder in settings, files are written there instead, including network locations if you select one.

---

## Support the project

If this tool saved you time, you can support development with a small donation.

These tools are created and maintained in spare time and released as free and open-source software.

Donations help keep the projects maintained and improving.

Donate here:

https://www.paypal.com/donate/?business=VSSTWS8ETDPXW&no_recurring=0&item_name=Support+development+of+free+open-source+tools.+Donations+help+maintain+and+improve+these+projects.&currency_code=USD
