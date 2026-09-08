# RAGE Tools

**RAGE Tools** is an all-in-one development suite for **GTA V**. It brings multiple workflows into a single application, reducing the need to switch between different tools and providing a consistent development environment.

The project is inspired by workflows and functionality commonly used with **CodeWalker**, **Sollumz**, and research into the **GTA V RAGE engine and source code**.

## Features

* **World Viewer**
* **Light Editor**
* **Material Editor**
* **NavMesh Editor**
* **YCD Editor**
* **MLO Creator**
* **Particles Editor & Creator**
* **Texture Painting**
* **RPF Explorer**
* **Cinematic Mode**

## Development Status

RAGE Tools is actively being developed. Some features may still be incomplete, unstable, or contain bugs.

We are looking for **real-world testers** to help identify issues and improve the overall quality of the application.

If you use RAGE Tools, please report:

* Bugs and crashes
* Performance issues
* Incorrect behavior
* Feature suggestions
* General feedback

[Our Discord](https://discord.gg/cDuMjansT2)

## Antivirus Warning

> **Windows Defender or other antivirus software may occasionally detect RAGE Tools as a potential threat.**

This may occur because the application is new or unsigned. If you encounter a detection, please report the detection name and details so it can be investigated.

## Roadmap

More tools, features, improvements, and optimizations are currently in development and will be added over time.

## Credits & References

RAGE Tools builds on the work of the GTA V modding community. It incorporates or derives
from:

- **[CodeWalker](https://github.com/dexyfex/CodeWalker)** — dexyfex and contributors.
  Resource formats and lighting math.
- **[FbxWriter](https://github.com/hamish-milne/FbxWriter)** — Hamish Milne. FBX reading
  and writing, reaching RAGE Tools via CodeWalker.
- **[Sollumz](https://github.com/Sollumz/Sollumz)** — the Sollumz contributors.
- **[GIMS Evo](https://github.com/3Doomer/GIMS-Evo)** — 3Doomer.
- GTA V RAGE engine research and publicly available technical information.

Full copyright and license notices for each are in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

RAGE Tools is an independent project and is not affiliated with Rockstar Games, CodeWalker,
Sollumz, or GIMS Evo.

<img width="512" height="512" alt="Rage_R_logo" src="https://github.com/user-attachments/assets/7b30646d-39fb-4680-9e54-3a5a5169b699" />

## Source code

The full source lives in this repository:

```
RageLightEditor.sln
RageLightEditor/        the application (C# / .NET 8, WinForms + SharpDX D3D11 + Dear ImGui)
CodeWalker.Core/        resource formats and lighting math (dexyfex, MIT)
RageTools.Mcp/          the local API server other tools talk to
native/ragetools_asi/   the RageToolsLive FiveM plugin (C++)
docs/                   usage notes, FiveM live linking, research notes
build-release.ps1       the release build (obfuscated, self-tested, zipped)
```

Build it with the .NET 8 SDK on Windows:

```
dotnet build RageLightEditor.sln -c Release
```

The exe lands in `RageLightEditor/bin/Release/net8.0-windows/`. No game data ships with the source: the tool reads your own GTA V install at runtime.

## Contributing

`main` only takes pull requests. Fork the repository, make your change on a branch, and open a pull request against `main`; it is reviewed and merged from there. Keep a pull request to one change so it can be read and tested on its own.

## License

RAGE Tools is free software, licensed under the **GNU General Public License, version 3**.

RAGE Tools is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
See the [LICENSE](LICENSE) file for the full terms.

In practice this means:

- You may use RAGE Tools for anything, personal or commercial, with no fee and no
  permission required.
- You may study and modify the source code.
- You may redistribute copies, modified or not, including for a fee.
- **If you distribute RAGE Tools or a modified version, you must release the complete
  corresponding source code of what you distribute under the GPL version 3 as well**, and
  preserve the copyright and license notices. Recipients get the same freedoms you did.

This applies to distributing the program itself. Assets, maps, and files that you *author
with* RAGE Tools are your own work and are not covered by this license.

The project is licensed this way because it builds on GPL-licensed work — see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
