# HeicToClipboard

HeicToClipboard adds a Windows Explorer context-menu entry for `.heic` and `.heif` files:

`C&C to JPEG`

When you use it, the tool converts the selected image files to JPEG, stores the JPEGs in a dedicated temp folder, and places them on the clipboard as files so you can paste them directly into Discord, forums, email clients, and other apps that accept pasted file attachments.

No permanent JPEG files are written next to the originals.

## Requirements

- Windows 11
- `.NET 8` only if you build from source or use a framework-dependent build
- `HEIF Image Extensions` installed from Microsoft Store so Windows can decode HEIC/HEIF files

If HEIF support is missing, the app shows this message:

`HEIF/HEIC support is missing in Windows. Install HEIF Image Extensions from Microsoft Store.`

## Installation

Official binaries are built by GitHub Actions.

### Install from a release

1. Download `HeicToClipboard.exe` from a GitHub release.
2. Open PowerShell in the repository root or extracted release folder.
3. Run:

```powershell
.\install.ps1 -SourceExe .\HeicToClipboard.exe
```

The script installs the executable to:

`%LocalAppData%\Programs\HeicToClipboard\`

and registers the Explorer context-menu entry for `.heic` and `.heif` under the current user account.

### Install after building locally

1. Build the project:

```powershell
.\build.ps1
```

2. Install it:

```powershell
.\install.ps1
```

If the installed executable is missing, `install.ps1` looks for:

`artifacts\publish\win-x64\HeicToClipboard.exe`

and offers to copy it into the install directory before it writes the registry entries.

## Uninstall

Remove the Explorer integration:

```powershell
.\uninstall.ps1
```

Remove the Explorer integration and the installed executable folder:

```powershell
.\uninstall.ps1 -RemoveInstalledFiles
```

## Build From Source

Build, test, and publish a self-contained single-file `win-x64` release:

```powershell
.\build.ps1
```

The published output is written to:

`artifacts\publish\win-x64\`

## How To Use

1. In Explorer, right-click one or more `.heic` or `.heif` files.
2. Choose `C&C to JPEG`.
3. Open Discord or another app that supports pasted file attachments.
4. Press `Ctrl+V`.

The clipboard contains the converted JPEG files as a file-drop list. For exactly one successful conversion, the clipboard also includes a bitmap representation in addition to the file attachment entry.

## Explorer Notes

- The tool uses a classic Explorer verb for reliability.
- On Windows 11 it may appear under `Show more options`.
- Multi-file selection is supported.

## What The Tool Does

- Reads HEIC or HEIF files through Windows-native imaging support
- Converts them to JPEG
- Starts at quality `95`
- Tries lower quality steps if needed
- Scales down gradually only when required
- Targets a hard per-file limit of `9.8 MB`
- Writes JPEGs only to `%TEMP%\HeicClipboardConvert\`
- Cleans up its own temp files older than 24 hours on each run

## Security

No telemetry. No network access.

The app does not upload files, does not run a background process, and does not keep permanent copies next to the source images.

## Limitations

- The app depends on the Windows HEIF codec being installed.
- CI does not validate live HEIC decoding because GitHub Actions runners may not have HEIF support installed.
- Official binaries are published for `win-x64`.
- Temp JPEG files remain in the dedicated temp folder until later cleanup so paste targets can access them after you press `Ctrl+V`.

## Testing In Explorer And Discord

1. Build or download `HeicToClipboard.exe`.
2. Run `.\install.ps1`.
3. In Explorer, right-click a `.heic` file.
4. Choose `C&C to JPEG`.
5. Open a Discord chat.
6. Press `Ctrl+V`.
7. Confirm that Discord shows a JPEG attachment instead of the original HEIC file.

To test multi-file behavior:

1. Select multiple `.heic` or `.heif` files in Explorer.
2. Right-click and choose `C&C to JPEG`.
3. Press `Ctrl+V` in Discord.
4. Confirm that Discord shows multiple JPEG attachments.

## Donations

If this project saves you time and you want to support maintenance, a donations or sponsorship link can be added in the future.
