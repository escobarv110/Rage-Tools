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

RAGE Tools is developed with reference to existing GTA V modding research and tools, including:

* [CodeWalker](https://github.com/dexyfex/CodeWalker)
* [Sollumz](https://github.com/Sollumz/Sollumz)
* GTA V RAGE engine research and publicly available technical information

RAGE Tools is an independent project and is **not affiliated with Rockstar Games, CodeWalker, or Sollumz**.

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

This project is provided as an open-source development tool for the FiveM / Cfx.re community.

* Personal and commercial use is allowed.
* You are free to modify, edit, and adapt the source code.
* You may use the tool in personal or commercial development projects.
* You may not redistribute, rebrand, or resell a modified or unmodified version of this project as your own product.
* You may not sell access to, sublicense, or commercially redistribute modified versions of this project.
* The original project and its source code must remain properly credited when redistributed for permitted purposes.

In short: use it, modify it, and build with it - but don't take a modified version, rebrand it, and sell it as your own.

The full text is in [`LICENSE`](LICENSE).

## Support the project

RAGE Tools is free and it stays free - no paywalls, no locked features, no "pro" version. You never have to pay a thing to use it.

If it saved you hours (or a headache) and you feel like saying thanks, you can chip in here. Every donation goes straight back into development: more tools, more fixes and faster updates.

**[Support RAGE Tools](https://checkout.revolut.com/pay/3e3d382f-ca07-454d-8d82-1a72fe9fe6cc)**

Starring the repo, reporting bugs and sharing it with other modders helps just as much.
