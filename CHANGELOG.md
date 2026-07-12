# Changelog

All notable changes to HeicToClipboard are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.3.0] - Unreleased

### Added

- Automatic update check: opening the app manually (the settings window) checks https://venes.org/heictoclipboard/ for a new version and offers to download and install it. The download is verified with a SHA-256 checksum before the installed exe is swapped and the app restarts.
- New Updates section in the settings window showing the installed version and update status.
- Download page and web installer at https://venes.org/heictoclipboard/ (`site/index.html`, `site/install.ps1`), plus `release.ps1` for staging and publishing releases with a `latest.json` update feed.

## [0.2.0] - 2026-07-09

### Fixed

- Multi-file selections no longer lose files from the clipboard when Explorer starts the conversions with a delay; all selected images end up in one clipboard batch.
- Corrupt or invalid files no longer show the misleading "install HEIF Image Extensions" hint; they get a clear invalid-file or decode-error message instead.
- The settings window no longer crashes when settings.json contains values outside the allowed ranges; values are clamped safely.
- The Explorer context menu now shows "C&C to JPEG" as intended (the ampersand was previously swallowed as a menu accelerator).
- Error messages report the configured size limit instead of a hardcoded "9.8 MB".
- Unexpected errors show a message box instead of failing silently.

### Changed

- Photos with an embedded color profile (for example iPhone Display P3) are converted to sRGB during decoding, so colors look right when pasted into apps that treat JPEG as sRGB.
- The size limit is reached with far fewer encoding attempts: the full quality ladder is tried at full scale first, then the image is downscaled using an estimated scale factor. Small size limits that previously failed now succeed by downscaling further.
- Lower peak memory use during conversion (several redundant full-size image copies removed).

## [0.1.2] - 2026-03-27

### Fixed

- Clipboard file-drop handling.
- GitHub release asset upload.

## [0.1.1] - 2026-03-10

### Added

- Settings window with output folder, max file size, JPEG quality, and longest-side resolution control.

## [0.1.0] - 2026-03-07

### Added

- Initial release: Explorer context-menu entry that converts HEIC/HEIF to JPEG and places the files on the clipboard.
