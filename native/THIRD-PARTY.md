# Third-party components

This is an independent implementation. No Murbong source, branding, icons, identity or signing material is included in the native app. Local upstream research and reference screenshots are excluded from the public source and distribution.

- Microsoft Game Bar SDK 7.3.2607010: Microsoft's Game Bar SDK license, included as GAMEBAR-LICENSE.txt in staged packages.
- Microsoft .NET 10 runtime and ASP.NET Core runtime: runtime redistribution licenses from their published packages. The staged self-contained runtime includes its license/notices files.
- Microsoft.Windows.CsWinRT: MIT; generated C# projections reference the official Game Bar metadata. Build tools are development dependencies, not bundled tools in the app.
- Noto Sans and Noto Sans SC: unchanged font files imported read-only from an installed CS2 panorama/fonts directory. Their embedded notices identify SIL Open Font License 1.1. See Widget/Fonts/FONT-NOTICES.txt, Widget/Fonts/OFL.txt and Widget/Fonts/provenance.json. Public provenance contains generic paths and file hashes only. No Valve UI images, logo or Stratum2.uifont content is bundled.

Stratum2 is a separate commercial font family. The optional standard TTF input is not supplied by this project; only package it when its applicable license permits that use. Selecting it without the file is prevented in the UI.

Project MIT terms apply only to project-authored code and illustrations, not Microsoft object code or the OFL fonts. Distribution includes applicable Microsoft SDK/runtime notices. End users and redistributors must retain and follow those separate component terms; the SDK/runtime is not offered as a stand-alone relicensed SDK. No ownership or endorsement by Valve/Microsoft is claimed.
