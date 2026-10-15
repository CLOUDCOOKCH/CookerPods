# Native preview notices

CookerPods source is original MIT-licensed code; see LICENSE-CookerPods.txt in the binary package. No LibrePods, ear-web, or Continuity implementation code, device captures, icons, or binaries are included.

The self-contained preview includes Microsoft .NET runtime components. Their MIT license and third-party notices are included as DOTNET-LICENSE.TXT and DOTNET-THIRD-PARTY-NOTICES.TXT. The Windows Desktop runtime license is included as WINDOWSDESKTOP-LICENSE.TXT, with WPF-THIRD-PARTY-NOTICES.TXT and WINFORMS-THIRD-PARTY-NOTICES.TXT from matching upstream release tags (provenance in native/notices). BUILDINFO.json records the resolved .NET, Windows Desktop runtime and SDK versions.

The Windows API projection and WinRT support are supplied by Microsoft.Windows.SDK.NET.Ref 10.0.19041.57. Windows SDK terms apply to the SDK projection: https://aka.ms/WinSDKLicenseURL . Project information: https://aka.ms/WinSDKProjectURL . WinRT.Runtime originates in Microsoft's C#/WinRT project: https://github.com/microsoft/CsWinRT (MIT license). These third-party components are not relicensed as CookerPods code.

The academic/research references cited in README.md describe the wire format used to develop an original, deliberately limited decoder. Their source implementations and assets are not redistributed. No clean-room claim is made. Tests use independently authored synthetic frames.

The app package contains no kernel driver. The separate original CookerPods control-driver source/development artifacts use documented Microsoft WDK/KMDF APIs; no NTPods/LibrePods implementation is incorporated. Microsoft driver API/sample architecture references are recorded in native/CookerPods.Control.Driver/README.md.

This is an unsigned development preview with unverified physical AirPods compatibility. It contains no updater, telemetry uploader, network API, kernel driver, or administrator request.
