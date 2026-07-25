# SaveOver's Sheltered 2 Save Editor

[![License: GPL v3](https://img.shields.io/github/license/saveover/sheltered2?label=License)](LICENSE.txt)
[![Discord](https://img.shields.io/discord/1529883189861158923?logo=discord&logoColor=white&label=Discord&color=%235865F2)](https://discord.gg/nzQSeGcta8)


A free, open-source save editor for **Sheltered 2** on Windows.

> **Unofficial.** SaveOver is an independent project. It is not affiliated with,
> endorsed by, or supported by Unicube or Team17. "Sheltered 2" and related trademarks
> belong to their respective owners.

---

## Features

This save editor allows you to edit the following:

- **Characters**: names, health, stats, skill trees, needs, relationships, etc.
- **Pets**: name, age, health, hunger, and the three training skills
- **Inventory (WIP)**: add/remove items and quantity
- **Crafting (WIP)**: unlock crafting recipes
- **Factions (WIP)**: increase/decrease your standing with the different factions and unlock rewards

## Your saves are safe

Every write creates a timestamped backup first, then stages the new file to a
temporary path and swaps it into place. A crash mid-write cannot leave you with
half a save.

Keep your own backups anyway. Saves are stored under
`%USERPROFILE%\AppData\LocalLow\Unicube\Sheltered2`.

## Requirements

- Windows 10 version 1809 (build 17763) or later, or Windows 11
- x64 or ARM64

## Install

Download the latest build from [Releases](https://github.com/saveover/Sheltered2/releases).

## Getting help

- **Something broken?** [Open an issue](https://github.com/saveover/Sheltered2/issues) —
  include the game version, the SaveOver version, and what happened

## Contributing

Pull requests are welcome. New files need the two-line SPDX header:

    // SPDX-License-Identifier: GPL-3.0-or-later
    // Copyright (C) 2026 SaveOver

By submitting a PR you agree to license your contribution under GPL-3.0-or-later.

## License

GPL-3.0-or-later. See [LICENSE.txt](LICENSE).

You may use, modify, and redistribute this software. If you distribute a modified
version, you must release its source under the same license.

**Not covered by that license** — see [NOTICE.txt](NOTICE):

- `Assets/Skills/**` — skill icons belonging to the game's owners, included for
  interoperability
- `Assets/Brand/**` — Buy Me a Coffee and SaveOver brand assets

The **SaveOver** name and logo are not licensed under the GPL. Forks are welcome
and must use a different name and logo. See [TRADEMARK.md](TRADEMARK).
