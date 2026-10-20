# Third-party notices

Satsuma is built on other people's open source work. This file lists what ships in the download, with each
component's license and copyright notice.

| Component | License | Copyright |
|-----------|---------|-----------|
| .NET runtime, WPF and Windows Forms | MIT | .NET Foundation and Contributors |
| C#/WinRT runtime and the Windows SDK projection (Microsoft.Windows.SDK.NET) | MIT (C#/WinRT), see below for the projection | Microsoft Corporation |
| Magick.NET 15.0.0 | Apache-2.0 | Dirk Lemstra |
| ImageMagick (bundled inside Magick.NET) | ImageMagick License | ImageMagick Studio LLC |
| PDFsharp 6.2.4 | MIT | empira Software GmbH |
| SharpCompress 0.50.6 | MIT | Adam Hathcock |
| FFmpeg (separate program in `tools\`) | GPL v3 | The FFmpeg developers |

## .NET runtime, WPF and Windows Forms

The download is self-contained, so it includes the .NET runtime along with WPF and Windows Forms. Source:
https://github.com/dotnet/runtime, https://github.com/dotnet/wpf and https://github.com/dotnet/winforms.

```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## C#/WinRT and the Windows SDK projection

Satsuma uses Windows Runtime APIs (PDF rendering and the share sheet) through the Microsoft.Windows.SDK.NET
projection, which includes `WinRT.Runtime.dll` from C#/WinRT (https://github.com/microsoft/CsWinRT). C#/WinRT
is under the MIT license. The projection package itself is published by Microsoft under the Windows SDK
license terms at https://aka.ms/WinSDKLicenseURL.

```
MIT License

Copyright (c) Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Magick.NET and ImageMagick

Magick.NET (https://github.com/dlemstra/Magick.NET) handles the image conversions.

```
Copyright 2013-2026 Dirk Lemstra https://github.com/dlemstra/Magick.NET.
Licensed under the Apache License, Version 2.0.
```

You can read the Apache License 2.0 at https://www.apache.org/licenses/LICENSE-2.0.

Magick.NET bundles ImageMagick (https://imagemagick.org), which is under the ImageMagick License:

```
Copyright @ 1999 ImageMagick Studio LLC, a non-profit organization dedicated to
making software imaging solutions freely available.

Licensed under the ImageMagick License (the "License"); you may not use this
file except in compliance with the License. You may obtain a copy of the
License at

  https://imagemagick.org/license/

Unless required by applicable law or agreed to in writing, software distributed
under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
CONDITIONS OF ANY KIND, either express or implied. See the License for the
specific language governing permissions and limitations under the License.
```

Magick.NET's native library also bundles a lot of other open source libraries (image codecs, compression,
fonts and so on), each with its own license. Magick.NET collects all of those notices, plus the full
ImageMagick License text, in its `Notice.txt`. The download includes a copy of that file as
`MAGICK-NET-NOTICE.txt`, and you can also find it at https://github.com/dlemstra/Magick.NET.

## PDFsharp

PDFsharp 6.2.4 (https://github.com/empira/PDFsharp) handles merging, splitting and rotating PDFs. It's
pinned to 6.2.4 on purpose, since later versions moved to the AGPL.

```
MIT License

Copyright (c) empira Software GmbH, Troisdorf (Germany)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## SharpCompress

SharpCompress 0.50.6 (https://github.com/adamhathcock/sharpcompress) handles extracting archives.

```
The MIT License (MIT)

Copyright (c) 2025 Adam Hathcock

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

SharpCompress includes code from the Legion of the Bouncy Castle, also under the MIT license:

```
Copyright (c) 2000 - 2011 The Legion Of The Bouncy Castle (http://www.bouncycastle.org)
```

## FFmpeg

The download includes an unmodified GPL v3 build of FFmpeg (`tools\ffmpeg.exe`) from
https://github.com/BtbN/FFmpeg-Builds. Satsuma runs it as a separate program and doesn't link against it.
Satsuma doesn't ship ffprobe.

- `tools\FFMPEG-LICENSE.txt` is the license that came with that build.
- `tools\FFMPEG-VERSION.txt` names the exact build, where it was downloaded from, and its version.

You can get the FFmpeg source from the FFmpeg project at https://ffmpeg.org, and the build scripts that
produced this binary from https://github.com/BtbN/FFmpeg-Builds.
