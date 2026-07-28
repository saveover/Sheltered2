# SaveOver for Sheltered 2

![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/saveover/sheltered2/dotnet-desktop.yml)
[![Latest release](https://img.shields.io/github/v/release/saveover/Sheltered2)](https://github.com/saveover/Sheltered2/releases/latest)
[![GitHub License](https://img.shields.io/github/license/saveover/sheltered2)](LICENSE.txt)
[![Windows](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)](#requirements)
[![Discord](https://img.shields.io/discord/1529883189861158923?logo=discord&logoColor=white&color=%235865F2)](https://discord.gg/nzQSeGcta8)

A free, open-source save editor for **Sheltered 2** on Windows. Edit characters,
pets, and inventory through a modern graphical interface while automatic backups
help protect the original save.

> [!IMPORTANT]
> SaveOver is an independent, unofficial project. It is not affiliated with,
> endorsed by, or supported by Unicube or Team17. Always keep your own backup
> before editing a save.

## Features

- Edit character identity, health, attributes, needs, traits, relationships, and skills.
- Edit pet identity, age, health, hunger, happiness, and training skills.
- Edit shelter water and existing inventory stack quantities, integrity, and quality.
- Creates a timestamped backup beside the save before every write.
- Stages changes in a temporary file before replacing the original.
- Choose a light, dark, or system theme and a left or top navigation layout.
- Run as an x86, x64, or ARM64 application.

> [!NOTE]
> Crafting and faction editing are visible in the application but are still under
> development.

## Quick start

1. Download the archive for your computer from the
   [latest release](https://github.com/saveover/Sheltered2/releases/latest):
   - `win-x64` for most Windows computers.
   - `win-arm64` for Windows on ARM.
   - `win-x86` only for 32-bit Windows.
2. Extract the entire ZIP archive.
3. Run `SaveOver for Sheltered2.exe`.
4. Select **Load save file** and open the relevant `.dat` file.
5. Use the navigation pages to make your changes.
6. Return to **Home** and select **Save file**.

Sheltered 2 normally stores saves in:

```text
%USERPROFILE%\AppData\LocalLow\Unicube\Sheltered2
```

SaveOver writes a timestamped backup into the same directory as default before changing the
selected file.

## Installation

### Portable release

The portable release is self-contained: it includes .NET and the Windows App SDK,
so no additional runtime installation is required. Download, extract, and run it.

Because releases are currently unsigned, Windows may display a Microsoft Defender
SmartScreen warning. Confirm that the file came from the
[official releases page](https://github.com/saveover/Sheltered2/releases) before
choosing to run it.

### MSIX

MSIX packages are produced for Microsoft Store submission. Use the Store version
when it becomes available; unsigned CI-generated MSIX packages are not intended
for ordinary installation.

## Requirements

- Windows 10 version 1809 (build 17763) or later, or Windows 11.
- An x86, x64, or ARM64 processor matching the downloaded release.
- A Sheltered 2 save file.

The portable release does not require a separately installed .NET or Windows App
SDK runtime.

## Documentation and support

- [Support and troubleshooting](SUPPORT.md)
- [Contributing guide](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Release history](https://github.com/saveover/Sheltered2/releases)
- [Issue tracker](https://github.com/saveover/Sheltered2/issues)

