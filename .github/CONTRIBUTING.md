# Contributing to SaveOver for Sheltered 2

Thank you for helping improve SaveOver. Contributions can include bug reports,
documentation, tests, accessibility improvements, save-format research, and code.

Before participating, read the [Code of Conduct](CODE_OF_CONDUCT.md). Questions
about using the application belong in [SUPPORT.md](SUPPORT.md).

## Before starting

- Search the [issues](https://github.com/saveover/Sheltered2/issues) and pull
  requests for existing work.
- Open an issue before a large feature or architectural change so its behavior
  and scope can be agreed upon.
- Never use your only copy of a save while developing or testing.
- Do not commit personal information, secrets, build output, or crash dumps.

## Development environment setup

- Windows 10 version 1809 or later, or Windows 11.
- The .NET SDK selected by [`global.json`](../global.json).
- Git.
- Visual Studio with .NET desktop development support, or another editor with the
  C# tooling needed for .NET and WinUI projects.

The project targets x86, x64, and ARM64. Use the platform matching your development
machine unless the change specifically concerns another architecture.

Fork the repository, then clone your fork:

```powershell
git clone https://github.com/YOUR-USERNAME/Sheltered2.git
cd Sheltered2
git remote add upstream https://github.com/saveover/Sheltered2.git
```

Confirm that the expected SDK is active and restore the project:

```powershell
dotnet --version
dotnet restore -r win-x64 -p:Configuration=Debug-Unpackaged -p:Platform=x64
```

For another architecture, change both values together:

| Platform | Runtime identifier |
| --- | --- |
| `x86` | `win-x86` |
| `x64` | `win-x64` |
| `ARM64` | `win-arm64` |

The packaged ([MSIX](https://learn.microsoft.com/en-us/windows/msix/overview)) configurations are `Debug` and `Release`; the unpackaged, or portable (EXE), configurations
are `Debug-Unpackaged` and `Release-Unpackaged`.

## Branch naming conventions

Create every change on a branch based on the latest `master`. Use a lowercase,
hyphen-separated description with one of these prefixes:

| Change | Pattern | Example |
| --- | --- | --- |
| Feature | `feature/<description>` | `feature/edit-faction-standing` |
| Bug fix | `fix/<description>` | `fix/inventory-quality-range` |
| Documentation | `docs/<description>` | `docs/improve-installation-guide` |
| Refactoring | `refactor/<description>` | `refactor/save-parser` |
| Tests | `test/<description>` | `test/save-writer-boundaries` |
| Maintenance | `chore/<description>` | `chore/update-toolkit` |

```powershell
git fetch upstream
git switch master
git merge --ff-only upstream/master
git switch -c feature/short-description
```

Avoid working directly on `master`, putting multiple unrelated changes on one
branch, or including issue titles verbatim when a shorter name is clearer.

## Local development workflow

Restore after changing the target architecture or package references. Then build
the unpackaged configuration matching your machine:

```powershell
dotnet build -c Debug-Unpackaged -r win-x64 -p:Platform=x64 --no-restore
```

Run the `Unpackaged` launch profile from Visual Studio, or start the executable
produced under the corresponding `bin` directory. During development:

1. Make one cohesive change.
2. Build early enough to catch XAML and compiler errors.
3. Exercise the affected behavior with a disposable copy of a save.
4. Review the diff for generated files, secrets, personal saves, and unrelated edits.
5. Complete the testing requirements below.
6. Commit the change and push the branch to your fork.

## Coding standards

- Prioritize correctness and save integrity over convenience or output size.
- Prefer C#, .NET, WinUI, Windows App SDK, and Community Toolkit abstractions over
  custom equivalents.
- Keep nullable reference types enabled and address new warnings.
- Use asynchronous file APIs for potentially blocking file operations.
- Preserve cancellation and actionable error messages across asynchronous calls.
- Never modify the source save before a backup has completed successfully.
- Keep parsing and writing culture-invariant where the save format requires it.
- Avoid suppressing trimming, compiler, or analyzer warnings without a documented
  and verified justification.
- Keep UI behavior accessible by keyboard and assistive technology.
- Add the existing SPDX and copyright header to new C# and XAML source files:

```text
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver
```

Use XML comment syntax where the file format requires it.

## Commit message conventions

Write concise commit messages that explain the completed change:

- Start the subject with an imperative verb such as `Add`, `Fix`, `Refactor`, or
  `Document`.
- Capitalize the subject and do not end it with a period.
- Keep the subject focused on one logical change and preferably within 72 characters.
- Use the body to explain motivation, non-obvious tradeoffs, migration concerns,
  or validation that does not fit in the subject.
- Reference related issues with `Fixes #123`, `Closes #123`, or `Refs #123` where
  appropriate.
- Do not use vague subjects such as `Updates`, `Changes`, or `Fix stuff`.

For example:

```text
Fix inventory quality range validation

Reject values outside the range accepted by Sheltered 2 before updating
the in-memory save model.

Fixes #123
```

Conventional Commits syntax is not required. A clean, readable history is more
important than forcing a prefix into every subject.

## Save-format changes

Changes to parsing or writing require particular care:

- Preserve unknown XML elements and values whenever possible.
- Treat malformed, oversized, or unexpected input as untrusted.
- Validate ranges before placing values in the UI or writing them back.
- Do not make a lossy transformation merely to simplify the object model.
- Confirm that a saved file can be reopened by SaveOver and loaded by the game.

Do not include copyrighted game assets unless their use and attribution have been
reviewed. Prefer original or freely licensed artwork.

## Testing requirements

There is not yet an automated test project. Until one exists, every pull request
that changes executable behavior should document its manual validation.

At minimum:

1. Build the affected configuration without warnings.
2. Start the application.
3. Load a representative save.
4. Navigate through every page affected by the change.
5. Make an edit and save.
6. Confirm that a timestamped backup was created.
7. Reopen the saved file and verify the edited values.
8. Exercise both light and dark themes for visual changes.
9. Exercise both navigation layouts for navigation changes.

For parser or writer changes, test missing, malformed, minimum, maximum, and
unexpected values where applicable.

## Documentation

Update documentation in the same pull request when behavior, requirements,
installation, or public APIs change. Write for users rather than mirroring commit
messages.

User-visible changes should be suitable for one of the Keep a Changelog categories:
Added, Changed, Deprecated, Removed, Fixed, or Security.

## Pull request process

- Keep each pull request focused on one coherent change.
- Explain the problem, the chosen solution, and important tradeoffs.
- Link related issues.
- Include screenshots or recordings for visible UI changes.
- List the configurations and scenarios you validated.
- Call out changes to save parsing, writing, packaging, permissions, or dependencies.
- Respond to review feedback and keep the branch free of unrelated generated files.

Before requesting review:

1. Rebase or fast-forward your branch onto the latest `master` and resolve conflicts.
2. Review the complete pull-request diff yourself.
3. Confirm the affected configurations build without warnings.
4. Add the testing performed to the pull-request description.
5. Update documentation and user-visible change notes where applicable.
6. Mark the pull request ready for review only when it is complete.

Draft pull requests are welcome for early design feedback, but they should clearly
identify unfinished work and should not be treated as merge-ready.

## Code review process

A maintainer reviews pull requests with the project's priorities in mind:
correctness and save safety first, followed by readability, maintainability,
meaningful performance improvements, security, and reliability.

- Review comments may ask for changes, clarification, additional validation, or a
  smaller scope.
- Respond to each substantive comment by changing the code or explaining the
  reasoning behind the current approach.
- Push revisions to the same branch so the discussion and CI history remain
  together.
- Do not resolve a review thread until the concern has been addressed or agreement
  has been reached.
- A maintainer may close inactive, unsafe, out-of-scope, or superseded pull
  requests after explaining why.
- Approval means the change is acceptable for the project; maintainers retain
  responsibility for deciding when and how it is merged.

Reviewers should be specific, constructive, and apply the
[Code of Conduct](CODE_OF_CONDUCT.md). Authors and reviewers are encouraged to
challenge technical decisions without making the discussion personal.

By submitting a contribution, you agree to license it under
[GPL-3.0-or-later](LICENSE.txt).
