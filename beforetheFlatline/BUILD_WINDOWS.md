# Windows Standalone Build

This project is a Godot **C#/.NET** project. Build it with the same Godot .NET version used by the project:

- Godot **4.7.2 .NET editor** (not the standard GDScript-only editor).
- The matching **4.7.2 .NET Windows export templates**.
- **.NET 8 SDK**, because `BeforetheFlatline.csproj` targets `net8.0`.

## Build

Open PowerShell in this project folder (the folder containing `project.godot`).

If Godot is on your PATH:

```powershell
.\Build-Windows.ps1
```

Or provide the Godot .NET Windows console executable (the build script also locates the matching console binary when given the regular editor executable):

```powershell
.\Build-Windows.ps1 -GodotPath "C:\Godot\Godot_v4.7.2-stable_mono_win64_console.exe"
```

You can also set `GODOT_PATH` before running the script:

```powershell
$env:GODOT_PATH = "C:\Godot\Godot_v4.7.2-stable_mono_win64.exe"
.\Build-Windows.ps1
```

The script builds the C# solution in `ExportRelease` mode, imports project resources, and exports the `Windows Desktop` preset.

## Output

```
builds/
└── windows/
    └── BeforeTheFlatline.exe
```

The preset targets Windows x64 and embeds the game PCK and C# build outputs in the executable. The development `Saves/` directory is excluded from the packaged game.

The editor continues to use `res://Saves` for project-local development saves. Exported builds use `user://Saves`, which is writable on the player's computer and keeps save files outside the read-only packaged resources.

## Export from the Godot editor

After installing the matching export templates, you can also open the project and use **Project → Export → Windows Desktop → Export Project**. The export destination is `builds/windows/BeforeTheFlatline.exe`; create the `builds/windows` folder first if it does not exist.

## Release notes

This is a portable executable build, not an installer. There is no code-signing certificate configured. Test the exported executable on a clean Windows account or another Windows machine before distributing it.
