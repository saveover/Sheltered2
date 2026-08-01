# Changelog

All notable changes to SaveOver for Sheltered 2 will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/2.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-08-01

### Added

- Support for adding new cats and dogs to a save, with unique IDs and species-appropriate defaults.
- Dog-specific skill editing for shelter, utility, and combat skills, including available skill points and bulk training controls.
- Support for adding and removing item stacks from shelter storage and overflow inventories.
- Category filtering and catalog search for finding existing inventory stacks and adding new items.
- A **Set all to 3 stars** action for updating the quality of every inventory stack.
- A comprehensive item catalog with friendly names, categories, and locally packaged artwork.
- Privacy-filtered rolling application logs and an **Open logs folder** button under **Settings → Diagnostics**.

### Changed

- The Pets editor now identifies cats and dogs and displays species-appropriate training controls.
- Partially trained dog skills remain unchanged unless a new training state is selected.
- The Inventory editor now uses clearer item cards, star-based quality controls, improved container summaries, and confirmation before deleting stacks.
- Character, pet, inventory, and supporting pages now use content-based responsive layouts with improved accessibility metadata.
- Save parsing and writing now run away from the UI thread, while navigation is temporarily disabled during active load and save operations.
- Diagnostic logging now captures application, navigation, file-operation, and unhandled-error information without recording decrypted save contents or local file paths.
- Release automation now creates a reviewable draft with a structured changelog template and appends VirusTotal results without publishing the release automatically.
- Updated the README with application branding, status badges, current feature details, and diagnostic-log guidance.
- Updated the application version to `0.2.0`.

### Fixed

- Fixed inventory quality values being interpreted as one-based instead of the game’s zero-based format; the editor’s one-to-three-star display now writes the correct save values.
- Fixed item-specific quality restrictions, including Petrol Cans being allowed to use unsupported quality levels.
- Fixed known inventory items being shown as unmapped when their definition-key casing differed.
- Fixed dog training changes not being written to the dog-specific skill data in the save.
- Fixed malformed decrypted content with an unexpected root element being presented as an empty editable save instead of being rejected.
- Fixed responsive layouts selecting unsuitable arrangements when the navigation pane reduced the available content width.
- Prevented the application window from being resized below the minimum usable size for the navigation and editor controls.

## [0.1.2] - 2026-07-30

### Added

- Drag-and-drop support for opening Sheltered 2 .dat save files.
- A Remember last opened save setting, enabled by default.
- A startup prompt for reopening the previously loaded save.
- An Open save folder button for quickly accessing the Sheltered 2 save directory.
- Automatic VirusTotal scanning of release archives, with analysis links added to the GitHub release notes.

### Changed

- Redesigned the Home page with a clearer load, edit, and save workflow.
- Added responsive layouts for compact and wide window sizes.
- Improved status messages and visual feedback for loaded and modified saves.
- Updated the Save button to better reflect the current workspace state.
- Improved feedback when dropping unsupported files or multiple files.
- Release automation now creates a draft release, uploads and scans its archives, and publishes it after the scans complete.
- Updated the application version to 0.1.2.0.

### Fixed

- Content dialogs now follow the application’s current light or dark theme.
- The stored save path is cleared when remembering the last opened save is disabled.
- Missing previously opened saves are detected and removed from the stored settings.

## [0.1.1] - 2026-07-30

### Added

- Configurable backup folder under **Settings → Save games**.
- Controls to choose, open, or reset the backup folder.
- Backup retention options for keeping the latest 5, 10, 20, or all backups for each save file.
- Save confirmation before overwriting the original save, enabled by default.
- A **Never show again** option in the save confirmation dialog.
- A setting to re-enable or disable save confirmations.
- Unsaved-change tracking across characters, pets, relationships, skills, and inventory.
- A confirmation prompt before loading another file when the current save contains unsaved changes.
- Dedicated project documentation for support, contributing, community conduct, and release history.
- Lightweight pull-request CI for packaged and unpackaged x64 builds.
- A dedicated tag-triggered release workflow for x86, x64, and ARM64 artifacts.

### Changed

- Backups are now stored outside the Sheltered 2 Steam Cloud directory by default:
  - `%LOCALAPPDATA%\SaveOver\Sheltered2\Backups`
  - This is to prevent Steam Cloud backing up the backups, which would then lead to an increasing number of backup saves not easily removed.
- The **Save file** button is now enabled only when the loaded save contains unsaved changes.
- Saving without making changes no longer overwrites the save or creates an unnecessary backup.
- Successful save messages now show the backup destination.
- Backup retention applies independently to each source save file.
- Backup cleanup now recognizes only files produced by SaveOver.
- Selecting the Sheltered 2 Steam Cloud directory as the backup location resets the setting to the safe default and explains why.
- Backup pruning is disabled inside the Steam Cloud directory to avoid files being restored and repeatedly deleted by synchronization.
- Pull requests now run representative x64 validation instead of the complete release matrix.
- Documentation-only pull requests skip the expensive Windows builds while still receiving a completed CI result.
- Full release builds now run automatically only for version tags.
- GitHub Actions dependencies are pinned to immutable commit hashes.
- Workflow permissions, concurrency handling, and execution timeouts have been tightened.
- The README has been reorganized with clearer installation, quick-start, requirements, support, technology, and licensing sections.

### Fixed

- Prevented unsaved edits from being silently discarded when another save file is loaded.
- Fixed save-button state becoming inconsistent after edits, saves, or file changes.
- Fixed change tracking for items added to or removed from observable collections.
- Fixed stale event subscriptions after replacing or resetting editable collections.
- Fixed detached model objects continuing to mark the active save as changed.
- Fixed unnecessary backup creation when serialized save content has not changed.
- Fixed backup retention potentially matching unrelated files with similar names.
- Fixed backup filename ordering and cleanup when multiple backups share the same timestamp.
- Fixed the contributing guide's `global.json` path.
- Fixed README links to the project support and community documents.

### Removed

- Removed the repository-level GitHub funding configuration for an organization-wide.

## [0.1.0] - 2026-07-28

Initial public release of SaveOver for Sheltered 2.

### Added

- Added support for loading, editing, and saving Sheltered 2 save files.
- Added character editing for:
  - Names and personal information.
  - Health and status values.
  - Attributes, needs, traits, and relationships.
  - Skill levels and skill-tree progression.
- Added pet editing for names, age, health, hunger, and training skills.
- Added partial inventory editing for:
  - Shelter water.
  - Existing item stack quantities.
  - Item integrity and quality.
  - Inventory containers and categories.
- Added work-in-progress Crafting and Factions pages.
- Added automatic timestamped backups before modifying a save.
- Added staged, atomic save replacement to reduce the risk of corrupting a save if writing is interrupted.
- Added strict save-file decoding, validation, and error reporting.
- Added support for x86, x64, and ARM64 builds.
- Added packaged MSIX and self-contained portable distributions.
- Added adaptive left and top navigation layouts.
- Added light, dark, and system theme preferences.
- Added persistent navigation, sound, and spatial-audio preferences.
- Added a redesigned Home page with adaptive navigation tiles.
- Added an About section containing version, dependency, source, licence, and issue-reporting links.
- Added a redesigned Donate page with:
  - Patreon, Ko-fi, and Buy Me a Coffee links.
  - Cryptocurrency donation addresses.
  - Copy-to-clipboard feedback and animations.
  - Non-monetary contribution guidance.
- Added application icons, tiles, splash screens, and light/dark artwork variants.
- Added accessibility metadata, heading levels, live-region feedback, keyboard-friendly controls, and adaptive layouts.
- Added global crash logging for unhandled managed and XAML exceptions.
- Added GitHub Actions automation for portable builds, MSIX packages, version stamping, and GitHub Releases.
- Added project documentation, contribution guidance, licensing information, and asset attribution.

### Changed

- Migrated observable models and commands to CommunityToolkit.Mvvm.
- Migrated applicable controls, layouts, extensions, and triggers to the Windows Community Toolkit.
- Replaced custom observable and trigger implementations with framework and toolkit abstractions.
- Replaced most Windows Runtime file operations with standard .NET file APIs.
- Refactored save handling around ordinary file paths for simpler and more reliable file access.
- Reworked the Donate and Home pages around data-driven templates and reusable models.
- Standardized page layouts, margins, spacing, typography, cards, and scrolling behavior.
- Improved responsive behavior using visual states and adaptive control sizing.
- Improved integer input handling and percentage normalization.
- Improved save-file selection feedback and disabled conflicting actions while a file is loading.
- Refactored application settings for packaged and unpackaged deployments.
- Simplified publish profiles and project configuration.
- Updated the application identity and display name to **SaveOver for Sheltered 2**.
- Renamed the portable executable to `SaveOver for Sheltered2.exe`.
- Aligned assembly, file, product, package, and About-page versioning.
- Embedded release debugging symbols instead of distributing a separate PDB file.
- Compressed self-contained single-file executables to substantially reduce their distributed size.
- Pinned the .NET SDK used by local and CI builds for reproducible publishing.

### Fixed

- Fixed unpackaged WinUI initialization and deployment failures.
- Fixed crashes caused by trimming WinRT and Windows SDK projection assemblies in portable releases.
- Fixed stale or missing assets in unpackaged build and publish outputs.
- Fixed backup filename collisions when multiple backups are created close together.
- Fixed save writes potentially leaving partially written files by introducing staged replacement.
- Fixed InfoBar colors not updating correctly after changing the application theme.
- Fixed inconsistent navigation selection and layout behavior.
- Fixed scrolling and content-sizing problems across several pages.
- Fixed clipboard feedback animations becoming inconsistent after repeated use.
- Fixed controls accepting unsuitable fractional values where the save format requires integers.
- Fixed application metadata, icon, tile, splash-screen, and manifest inconsistencies.
- Fixed portable release artifacts containing unnecessary loose files.

[Unreleased]: https://github.com/saveover/Sheltered2/compare/v0.1.2...HEAD
[0.2.0]: https://github.com/saveover/Sheltered2/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/saveover/Sheltered2/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/saveover/Sheltered2/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/saveover/Sheltered2/releases/tag/v0.1.0
