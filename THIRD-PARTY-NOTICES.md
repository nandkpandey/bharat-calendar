# Third-party notices

Bharat Calendar's own source code is licensed under the MIT License (see [LICENSE](LICENSE)).
The installer and the published `BharatCalendar.exe` also contain the third-party components listed below.

## .NET 8 runtime and Windows Desktop runtime (WPF, Windows Forms)

The installer bundles the .NET runtime (`Microsoft.NETCore.App.Runtime.win-x64`) and the Windows Desktop runtime
(`Microsoft.WindowsDesktop.App.Runtime.win-x64`) so the app runs without installing .NET separately.

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

The .NET runtime itself includes third-party libraries under their own licenses. Microsoft lists them here:

- https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
- https://github.com/dotnet/wpf/blob/main/THIRD-PARTY-NOTICES.TXT
- https://github.com/dotnet/winforms/blob/main/THIRD-PARTY-NOTICES.TXT

## Inno Setup

The installer is built with Inno Setup, Copyright (C) 1997-2026 Jordan Russell and Martijn Laan.
Inno Setup is free software; its license permits distributing installers created with it.
License: https://jrsoftware.org/files/is/license.txt

## Holiday data

Holiday dates come from Government of India, Department of Personnel & Training, Office Memoranda
F.No.12/2/2023-JCA dated 03.07.2025 (2026 holidays) and 16.07.2026 (2027 holidays). They are public
information, transcribed here as factual data.

## Fonts and icons

The app uses fonts installed with Windows (Segoe UI, Segoe Fluent Icons) and doesn't redistribute them.
The app icon was created for this project (see `tools/make-icon.ps1`).
