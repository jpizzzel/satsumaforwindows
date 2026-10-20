# Contributing to Satsuma

Thanks for wanting to help. Bug reports, ideas and pull requests are all welcome.

## Reporting bugs and ideas

Open an issue and pick the template that fits. For bugs, the most useful things are what you dragged, which key
you held, what you picked on the wheel, and the end of `%APPDATA%\Satsuma\log.txt`.

Found a security problem? Please don't open a public issue. See [SECURITY.md](SECURITY.md).

## Making a change

1. Fork the repo and make a branch for your change.
2. Keep it focused. One fix or feature per pull request is much easier to review than a big mix.
3. Run the tests before you push:

   ```
   dotnet test --project tests/Satsuma.Core.Tests
   ```

   You need the .NET 10 SDK. The core tests run on Windows, macOS and Linux. Put ffmpeg on your PATH to run the
   audio and video tests too, otherwise they're skipped.
4. If you touched the Windows app, try it for real: drag a few files with each key held.
5. Open a pull request and fill in the short template.

CI builds the app on Windows, runs the tests, and runs a self-test of the real exe on every pull request. I review
everything before it's merged, and I might ask for changes. That's normal, not a no.

## How the code is laid out

- `src/Satsuma.Core` is plain .NET: what goes on each wheel and the conversions themselves. New formats and tools
  usually only need changes here, plus a test.
- `src/Satsuma.App` is the Windows app: the mouse hook, the wheel window, the tray icon and the actions.
- `tests/Satsuma.Core.Tests` has the tests. Most of them make a small sample file, run an operation on it and check
  what comes out.
- `site/` is the website at satsumaforwindows.vercel.app.

## A few house rules

- Write comments and docs like you'd explain it to a friend. Short and plain.
- New dependencies need a good reason, and their license has to allow being bundled in an MIT app. (That's why
  PDFsharp is pinned to 6.2.x: newer versions moved to the AGPL.)
- Nothing should ever upload a user's files. Everything runs on their PC.

By contributing, you agree that your work is released under the [MIT License](LICENSE), same as the rest of the
project.
