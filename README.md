# Satsuma

**Convert and act on files on Windows by holding a key while you drag them.**

Start dragging a file (from File Explorer, the desktop, a browser download bar...), then hold a key. A wheel
opens under the pointer. Drop the file on what you want and the result is saved next to the original. Drop on
the middle of the wheel, or let go of the key, to cancel and carry on with a normal drag.

| Hold   | Wheel   | What it offers |
|--------|---------|----------------|
| Shift  | Convert | Other formats for that kind of file |
| Ctrl   | Tools   | Compress, resize, rotate, strip metadata, merge, split, normalize, zip, extract... |
| Alt    | Actions | Copy, Move (cut), Copy path, Float, Share, Terminal, Show in Explorer |

Everything runs locally. Nothing is uploaded.

Satsuma is an independent, clean-room Windows take on the drag-and-hold idea popularized by
[Tangerine for Mac](https://tangerineformac.com/) (by LEAP STUDIO). It is not affiliated with Tangerine and
shares no code or assets with it; it was written from the publicly described behaviour.

## What it can do

**Convert (Shift)**

| From | To |
|------|----|
| Images: JPG, PNG, WEBP, HEIC, AVIF, GIF, BMP, TIFF, ICO, SVG, PSD, RAW... | JPG, PNG, WEBP, AVIF, GIF, BMP, TIFF, ICO, PDF (and HEIC where the bundled ImageMagick can encode it) |
| Video: MP4, MOV, MKV, AVI, WEBM, WMV, FLV, M4V... | MP4, MOV, MKV, WEBM, AVI, GIF, or audio only: MP3, WAV, M4A, FLAC, OGG, OPUS, AIFF |
| Audio: MP3, WAV, M4A, AAC, FLAC, OGG, OPUS, WMA, AIFF... | MP3, WAV, M4A, FLAC, OGG, OPUS, AIFF |
| PDF | PNG or JPG (one image per page, rendered by Windows' own PDF engine) |
| Subtitles: SRT, VTT, ASS | SRT, VTT, ASS |

Animated GIFs stay animated when converted to WEBP or TIFF. Transparent images get a white background when
converted to JPG or BMP. Photos are rotated upright according to their EXIF orientation.

**Tools (Ctrl)**

- Images: Compress, 50%, 25%, Rotate, No metadata, Combine PDF (several images into one PDF)
- Video: Compress (H.264, at most 1080p-ish quality), 720p, Mute, Rotate
- Audio: Compress (MP3 96 kbps), Normalize (EBU R128 loudness), Trim silence
- PDF: Merge (several PDFs, in the order dragged), Split pages, Rotate
- Archives: Extract (ZIP, 7Z, RAR, TAR, GZ, TGZ, BZ2, XZ)
- Anything, including folders: Zip

**Actions (Alt)**

- **Copy** / **Move**: put the files on the clipboard; paste in Explorer to copy or move them there
- **Copy path**: quoted full paths, like Explorer's "Copy as path"
- **Float**: park the files in a small always-on-top shelf, then drag them out later
- **Share**: the Windows share sheet (Nearby Sharing, Mail, Teams...)
- **Terminal**: Windows Terminal (or PowerShell) in the file's folder
- **Show**: select the file in File Explorer

When several files are dragged, a wheel only shows what applies to all of them. Results never overwrite
anything: an existing `photo.png` makes the new one `photo (1).png`. Edits get a suffix, like
`clip (compressed).mp4`.

Progress shows in the bottom-right corner. Click a finished card to show the result in File Explorer, or press
its X to cancel a running job.

## Install

Requirements: Windows 10 (1809 or later) or Windows 11, x64.

1. Download `Satsuma-win-x64` from the latest successful run on the repository's **Actions** tab and unzip it
   anywhere.
2. Run `Satsuma.exe`. The welcome screen explains the keys; afterwards Satsuma lives in the system tray.
3. Optional: tick "Start Satsuma when I sign in".

The download is self-contained (no .NET install needed) and includes `tools\ffmpeg.exe` for audio and video.
Without it, image, PDF and archive features still work.

Settings (tray icon, left click): turn each wheel on or off, save results to a fixed folder instead of next to
the original, and pick the JPG/WEBP/AVIF quality. Settings and a log live in `%APPDATA%\Satsuma`.

## Build from source

You need the .NET 10 SDK. The core library and its tests also build and run on Linux and macOS.

```powershell
dotnet test --project tests/Satsuma.Core.Tests
dotnet publish src/Satsuma.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Put `ffmpeg.exe` in `publish\tools\` (the CI workflow downloads a build from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds)), or have it on `PATH`. The media tests use
`SATSUMA_FFMPEG` or `PATH` to find it and are skipped when it is missing.

`Satsuma.exe --self-test <folder>` checks the Windows-only parts on a real desktop: the mouse hook, the wheel and
progress windows, PDF rendering, the tray icon and a few end-to-end conversions. It writes `self-test.log` and
PNG previews of the wheels into the folder. CI runs it on every push.

## How it works

```
src/Satsuma.Core      platform-neutral: what's on each wheel and how to run it
  FileKinds             file type by extension
  OperationCatalog      every conversion/tool, filtered per wheel and per set of files
  WheelLayout           segment geometry and hit testing (one ring, or two when crowded)
  Images/ Media/ Documents/ Archives/   Magick.NET, ffmpeg, PDFsharp, SharpCompress
src/Satsuma.App       the Windows app (WPF)
  Interop/DragWatcher   low-level mouse hook: notices a drag plus a held modifier
  Wheel/WheelWindow     transparent, non-activating, topmost OLE drop target drawn as the wheel
  Operations/           actions, share sheet, Float shelf, PDF to image via Windows.Data.Pdf
  Jobs/                 background runner and the progress cards
tests/Satsuma.Core.Tests
```

Windows has no API to change what Explorer does when a modifier is held mid-drag, so Satsuma watches the mouse
with a low-level hook (it only observes, never blocks input). When a drag is under way and Shift, Ctrl or Alt is
down, it places an invisible drop target under the pointer. If the drag carries files, that target turns into
the wheel. Dropping returns "copy" to the source app, so Explorer never moves or deletes the original even though
Shift normally means "move" there.

## Known limitations

- Dragging from an app running as administrator onto Satsuma (not elevated) is blocked by Windows.
- Office documents (DOCX, XLSX, PPTX) are not converted.
- HEIC output depends on the codecs inside Magick.NET's native library; when it can't encode HEIC the option is
  hidden.
- Video and image conversion run on the CPU.

## Licences

Satsuma's dependencies: Magick.NET (Apache-2.0), PDFsharp 6.2.4 (MIT; deliberately pinned, as 6.5+ moved to
AGPL), SharpCompress (MIT). The release bundles an unmodified GPL build of ffmpeg as a separate program in
`tools\`, with its licence in `tools\FFMPEG-LICENSE.txt`; its source is available from the FFmpeg project and
BtbN/FFmpeg-Builds.
