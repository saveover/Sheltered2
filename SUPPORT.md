# Support

Editing save files requires precision, as the file may represent a thriving shelter, a 
failed shelter, or, after extensive modification, a shelter the game no longer recognizes. 
Although SaveOver generates backups, users are strongly advised to maintain an additional 
personal backup.

By participating in this project or interacting with this repository, organization, 
or community, you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md) and abide 
by its terms.

## Before asking for help

Please try the following:

1. Try loading into your original, backed up, save and see if the issue persists.
2. Confirm that you are using the
   [latest SaveOver release](https://github.com/saveover/Sheltered2/releases/latest).
3. Confirm that you downloaded the correct architecture: x86, x64, or ARM64.
4. Restart SaveOver and repeat the smallest sequence that reproduces the problem.
5. Search the [existing issues](https://github.com/saveover/Sheltered2/issues)
   for the same symptom.
6. Don't fall for the [X-Y Problem](https://meta.stackexchange.com/questions/66377/what-is-the-xy-problem/66378#66378).
7. Explain the problem to a [rubber duck 🦆](https://rubberduckdebugging.com/)! The duck
   cannot merge a pull request, but it has an exceptional record at identifying
   missing steps.

If SaveOver crashes, check for:

```text
%LOCALAPPDATA%\SaveOver\Sheltered2\Logs\crashes.log
```

The log may not be created for failures that occur entirely inside native Windows
or WinUI code.

## Where to ask

- Use [GitHub Issues](https://github.com/saveover/Sheltered2/issues) for
  reproducible bugs and feature requests.
- Use the [SaveOver Discord community](https://discord.gg/nzQSeGcta8) for
  questions, discussion, and help determining whether something is a bug.

Please do not use an unrelated issue or pull request as a support thread. It makes
the original conversation difficult to follow.

## Writing a useful bug report

Include:

- The SaveOver version shown under **Settings → About**.
- Your Windows version and processor architecture.
- Whether you used the portable or MSIX build.
- The Sheltered 2 game version, if known.
- Clear steps to reproduce the problem.
- What you expected and what happened instead.
- The exact error text, copied as text rather than supplied only in a screenshot.
- Relevant entries from `crashes.log` or Windows Event Viewer.
- Whether the problem also happens with a different save.

Screenshots and short recordings are welcome, but accompany important text with
a written copy so it remains searchable and accessible.

## Sharing save files

Only attach a save when it is necessary and you are comfortable making its
contents available to repository maintainers—and potentially to the public if
attached to an issue. Remove anything you regard as personal or sensitive.

Always keep an untouched local copy. A file attached to an issue is diagnostic
material, not a backup service.

## Security and private reports

Do not publish credentials, private information, or a suspected security
vulnerability in a public issue. Contact a SaveOver maintainer privately through
the [Discord community](https://discord.gg/nzQSeGcta8) so a private reporting
channel can be arranged.

## Scope

Support is provided by volunteers on a best-effort basis. There is no guaranteed
response time. The project can help diagnose SaveOver behavior, but cannot provide
support for the game itself, damaged hardware, unrelated mods, or saves modified
by unknown tools.

For code changes, see [CONTRIBUTING.md](CONTRIBUTING.md).
