# Satsuma

Drag a file, hold a key, drop it on what you want. That's the whole app.

I loved [Tangerine](https://tangerineformac.com/) on the Mac and wanted the same thing on Windows, so I built
it. Start dragging something (from File Explorer, the desktop, wherever), hold a key, and a little wheel pops up
under your cursor. Drop the file on a slice and the result shows up next to the original.

- **Shift** converts it to another format
- **Ctrl** opens tools like compress, resize, merge and zip
- **Alt** opens actions like copy, copy path and share

Changed your mind? Drop it in the middle of the wheel, or just let go of the key and the drag goes back to normal.

It all runs on your machine. Nothing gets uploaded anywhere.

Satsuma isn't affiliated with Tangerine or LEAP STUDIO. I rebuilt the idea from scratch based on how the Mac
app is described. None of its code or artwork is in here.

## What's on the wheels

### Shift: convert
<img width="302" height="307" alt="image" src="https://github.com/user-attachments/assets/f5b822a3-e91e-4f7a-86a0-25ccf69d48f1" />

Images (JPG, PNG, WEBP, HEIC/HEIF, AVIF, JPEG XL, GIF, BMP, TIFF, ICO, SVG, PSD, TGA, and camera RAW from Sony,
Nikon and Canon (CR2) plus DNG) can become JPG, PNG, WEBP, AVIF, GIF, BMP, TIFF, ICO or PDF. HEIC output shows
up too, if the bundled ImageMagick build can write it.

Videos (MP4, MOV, MKV, AVI, WEBM, WMV and friends) can become MP4, MOV, MKV, WEBM, AVI or a GIF. You can also
pull just the audio out as MP3, WAV, M4A, FLAC, OGG, OPUS or AIFF.

Audio files convert between all of those same audio formats.

PDFs turn into PNG or JPG images, one per page. This uses the PDF renderer that's already in Windows, so
there's nothing extra to install.

Subtitles go between SRT, VTT and ASS.

A few small things it handles for you: animated GIFs stay animated as WEBP, transparent images get a white
background when they become JPGs, and photos get rotated the right way up before they're saved.

### Ctrl: tools
<img width="316" height="307" alt="image" src="https://github.com/user-attachments/assets/4942f588-ecc1-454d-8666-3c9de4ac855a" />

| Files | Tools |
|-------|-------|
| Images | Compress, shrink to 50% or 25%, rotate, strip metadata, combine several into one PDF |
| Video | Compress, shrink to 720p, mute, rotate |
| Audio | Compress, normalize loudness, trim silence off the ends |
| PDFs | Merge (in the order you dragged them), split into pages, rotate |
| Archives | Extract ZIP, 7Z, RAR, TAR, GZ and the like |
| Anything, folders too | Zip it |

### Alt: actions
<img width="397" height="398" alt="image" src="https://github.com/user-attachments/assets/8c33d857-b9ef-41c3-845c-391c3d06c461" />

Copy and Move put the files on your clipboard, so you can paste them into any Explorer window (Move cuts
them). Copy path gives you the full quoted path, same as Explorer's "Copy as path". Float parks the files in a
tiny window that stays on top until you drag them somewhere else, which is handy when you're juggling a bunch of
windows. Share opens the Windows share sheet. Terminal opens Windows Terminal in that folder, and Show selects
the file in Explorer.

If you drag several files at once, the wheel only shows what works for all of them. Nothing ever gets
overwritten either: if `photo.png` already exists you'll get `photo (1).png`, and edited copies get a label
like `clip (compressed).mp4`.

While something's working you'll see a progress card in the bottom-right corner. Click it when it's done to
jump to the file, or hit the X to cancel.

## Getting it

You'll need Windows 10 (1809 or newer) or Windows 11 on an x64 PC. Windows 11 on ARM works too. It just runs
Satsuma through its built-in x64 emulation.

Grab it from [satsumaforwindows.vercel.app](https://satsumaforwindows.vercel.app/) or this repo's
[Releases page](https://github.com/jpizzzel/satsumaforwindows/releases/latest). Every version has a zip called
something like `Satsuma-v0.1.0-win-x64.zip`.

1. Download the zip.
2. Right-click it, pick Properties, tick Unblock at the bottom of the General tab, and click OK. This tells
   Windows the file is fine and saves you some warnings later.
3. Extract it somewhere you'll keep it, like a folder in your Documents. Don't run it from inside the zip.
4. Run `Satsuma.exe`. The welcome screen walks you through the keys. After that it lives in your system tray.

The first time you run it, Windows might show a blue "Windows protected your PC" box. Click "More info" and
then "Run anyway". That box shows up because the app isn't code-signed yet, so SmartScreen doesn't know who
made it. It isn't a sign that anything's wrong with the file.

Some antivirus tools get suspicious of apps that watch the mouse, and watching the mouse is how Satsuma notices
you're dragging something. It only watches for drags, and it never uploads anything. If your antivirus
complains or Satsuma acts strange, the log at `%APPDATA%\Satsuma\log.txt` is the first place to look.

You don't need to install .NET, it's bundled in. ffmpeg comes along too, in the `tools` folder, and handles
all the audio and video. If you remove it, images, PDFs and archives still work.

Windows sometimes tucks new tray icons into the overflow area behind the ^ arrow next to the clock. You can
drag the Satsuma icon from there onto the taskbar so it's always visible. Left click it to open settings.
Right click it for Settings, Pause, Open log folder and Quit. If you run `Satsuma.exe` again while it's already
going, it opens settings too.

In settings you can turn each wheel on or off, send everything to one folder instead of next to the original,
set the JPG/WEBP quality, and have Satsuma start when you sign in. Settings and the log file live in
`%APPDATA%\Satsuma`.

## Building it yourself

Grab the .NET 10 SDK. The core library and its tests run fine on Linux and macOS too, which is nice for
working on the conversion code.

```powershell
dotnet test --project tests/Satsuma.Core.Tests
dotnet publish src/Satsuma.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Then drop `ffmpeg.exe` into `publish\tools\` (CI grabs one from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds)), or just have it on your PATH. The media tests
look for it through `SATSUMA_FFMPEG` or PATH, and skip themselves if it's not there.

There's also `Satsuma.exe --self-test <folder>`. It pokes at all the Windows-only parts on a real desktop (the
mouse hook, the wheel, PDF rendering, the tray icon) and runs a few real conversions. Then it writes a
`self-test.log` and PNG snapshots of the wheels into that folder. CI runs it on every push to main and on pull requests.

## Making a release

This part is for me. There's nothing to bump by hand, the version comes from the tag. The easy way is the
button: open the Actions tab, pick Build, click Run workflow, type a version like `v0.2.0` and hit the green
button. That works from a phone too. Or from a terminal:

```
git tag v0.2.0
git push origin v0.2.0
```

Either way, CI builds it, runs the tests and the self-test, and then publishes a Release for that tag with the zip
attached. If the release job fails partway, it's safe to just re-run it. It'll replace the zip on the existing
release instead of making a second one.

## How it works

Windows doesn't give you any way to change what Explorer does when you hold a key mid-drag. So Satsuma
watches the mouse with a low-level hook. It only looks and never blocks anything. When it sees a drag going on
with Shift, Ctrl or Alt held down, it slips an invisible drop target under your cursor. If the drag turns
out to be files, that target draws itself as the wheel.

When you drop, Satsuma tells the source app it was a copy. That matters because Shift normally means "move" in
Explorer, and you really don't want your original file disappearing.

The code is split in two:

```
src/Satsuma.Core     plain .NET: what goes on each wheel, and the conversions themselves
  FileKinds            figures out file types from the extension
  OperationCatalog     the list of everything, filtered per wheel and per set of files
  WheelLayout          the slice math and hit testing (one ring, or two if it gets crowded)
  Images/ Media/ Documents/ Archives/   Magick.NET, ffmpeg, PDFsharp, SharpCompress
src/Satsuma.App      the Windows app (WPF)
  Interop/DragWatcher  the mouse hook
  Wheel/WheelWindow    the wheel itself: a see-through, always-on-top drop target
  Operations/          actions, share sheet, the Float shelf, PDF to image
  Jobs/                runs things in the background and shows the progress cards
tests/Satsuma.Core.Tests
```

## Things that don't work (yet)

- You can't drag from an app running as administrator onto Satsuma. Windows blocks it.
- No Word, Excel or PowerPoint conversions.
- HEIC output depends on what's compiled into Magick.NET. If it can't write HEIC, the option just doesn't show
  up.
- Everything runs on the CPU, so big videos take a while.

## Contributing

Satsuma is open source, and help is welcome. Bug reports, ideas and pull requests all go through GitHub; the
[contributing guide](CONTRIBUTING.md) explains how to build it, run the tests and send a change. Please report
security problems privately, as described in [SECURITY.md](SECURITY.md).

## Licenses

Satsuma itself is MIT licensed, see [LICENSE](LICENSE). It uses Magick.NET (Apache-2.0), PDFsharp 6.2.4 (MIT) and SharpCompress (MIT). PDFsharp is pinned on
purpose, since version 6.5 switched to the AGPL. The full list of what's in the download, with licenses and
copyright notices, is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). That file comes along in the zip
too.

The download includes an unmodified GPL build of ffmpeg in `tools\`. Satsuma runs it as a separate program
and doesn't link against it. Its license is in `tools\FFMPEG-LICENSE.txt`, and `tools\FFMPEG-VERSION.txt`
says exactly which build it is and where it came from. You can get the source from the FFmpeg project or
BtbN/FFmpeg-Builds. The app doesn't ship ffprobe, only ffmpeg.
