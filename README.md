# SaveOver — Sheltered 2 Save Editor

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![Discord](https://img.shields.io/badge/Discord-join-5865F2?logo=discord&logoColor=white)](https://discord.gg/nzQSeGcta8)

A free, open-source save editor for **Sheltered 2** on Windows.

> **Unofficial.** SaveOver is an independent project. It is not affiliated with,
> endorsed by, or supported by Unicube or Team17. "Sheltered 2" and related marks
> belong to their respective owners.

---

## What it does

- **Family members** — names, health, stats, skill trees, needs, relationships
- **Pets** — name, age, health, hunger, and the three training skills
- **Unstick a member** — queue a position reset for anyone trapped in the world
- **Doesn't touch what it doesn't understand** — edits are applied onto the
  original decrypted document, so anything SaveOver doesn't model is preserved
  byte for byte

## Your saves are safe

Every write creates a timestamped backup first, then stages the new file to a
temporary path and swaps it into place. A crash mid-write cannot leave you with
half a save.

Keep your own backups anyway. Saves are stored under
`%USERPROFILE%\AppData\LocalLow\Unicube\Sheltered2`.

## Single-player only

SaveOver is for single-player saves. Don't use it on multiplayer, online, or
competitive games — editing those risks your account, and it isn't what this
project is for.

## Requirements

- Windows 10 version 1809 (build 17763) or later, or Windows 11
- x64 or ARM64

## Install

Download the latest build from [Releases](https://github.com/saveover/Sheltered2/releases).
Every release ships with its complete corresponding source, as required by the GPL.

## Getting help

- **Something broken?** [Open an issue](https://github.com/saveover/Sheltered2/issues) —
  include the game version, the SaveOver version, and what happened
- **Not sure if it's a bug?** Ask in [#support on Discord](https://discord.gg/nzQSeGcta8)

## Contributing

Pull requests are welcome. New files need the two-line SPDX header:

    // SPDX-License-Identifier: GPL-3.0-or-later
    // Copyright (C) 2026 SaveOver contributors

By submitting a PR you agree to license your contribution under GPL-3.0-or-later.

## Supporting the project

SaveOver is free and stays free. If you want to fund the time that goes into it:

**[Buy Me a Coffee](https://buymeacoffee.com/saveover)** — one-off coffees, or a
membership. Members get early builds, credits, and Discord roles; Patrons and
Founders also get a vote on which game gets an editor next. Details on the page.

**Crypto** — addresses are in the app's Donate page. Crypto donations are
anonymous and untracked, so they can't unlock roles, votes, or credits. They're a
one-way thank-you and nothing more. If you want the membership perks, use Buy Me
a Coffee.

## License

GPL-3.0-or-later. See [LICENSE](LICENSE).

You may use, modify, and redistribute this software. If you distribute a modified
version, you must release its source under the same license.

**Not covered by that license** — see [NOTICE](NOTICE):

- `Assets/Skills/**` — skill icons belonging to the game's owners, included for
  interoperability
- `Assets/Brand/**` — Buy Me a Coffee and SaveOver brand assets

The **SaveOver** name and logo are not licensed under the GPL. Forks are welcome
and must use a different name and logo. See [TRADEMARK.md](TRADEMARK.md).
