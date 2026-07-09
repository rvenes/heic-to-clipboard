# Changelog

All notable changes to HeicToClipboard are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
