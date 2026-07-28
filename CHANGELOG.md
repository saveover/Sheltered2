# Changelog

All notable changes to SaveOver for Sheltered 2 will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/2.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
