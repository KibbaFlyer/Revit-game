# BIM Arena for Revit 2027

A .NET 10, x64 Revit add-in that turns the active 3D view into an original Doom-inspired survival shooter. A **Play BIM Arena** button appears on the existing **Add-Ins** tab. The setup follows the supplied RevisionAutomator application's `IExternalApplication` / `IExternalCommand` / `.addin` structure, with a new SDK-style solution.

**Status:** compiled against Revit 2027 API assemblies and tested at the engine level. Revit loading, Windows rendering, and real-model exports still require the Windows acceptance checks in [VALIDATION.md](VALIDATION.md). This package has not been run inside Revit on this Mac.

## Install and play

1. Copy and extract the entire `BimArena-Revit2027.zip` onto the Windows machine running Revit 2027.
2. Close Revit and run **Install.cmd**. The included binaries need no SDK to install. The installer writes only this user's `%APPDATA%\Autodesk\Revit\Addins\2027` folder.
3. Restart Revit 2027. If Revit asks whether to load the unsigned add-in, load it to use BIM Arena.
4. Open a 3D view. A section box around a floor or wing is a good starting arena. Ensure the floor and nearby walls are visible.
5. Choose **Add-Ins → BIM Arena → Play BIM Arena**, then click a clear **floor surface** in that view. Allow the geometry and reachable floor area to load.
6. Press **Enter** or click to deploy. Press **Esc** to exit and return to the original view.

The add-in stays modal while playing, keeping the view and document stable. No walls, families, view orientations, or other Revit elements are edited or created. Loading can be cancelled with Esc. If an arena exceeds the geometry budget, loading stops with instructions to reduce the view; incomplete collision geometry is never used.

| Control | Action |
| --- | --- |
| W / A / S / D | Move / strafe |
| Mouse | Look and aim |
| Left mouse button | Fire the shotgun; hold for repeat fire |
| Shift | Run |
| Space | Jump |
| R | Reload; an empty gun reloads automatically when firing |
| P | Pause / resume |
| M | Mute / unmute sound |
| F1 | Controls |
| Enter | Deploy, resume, or restart after defeat |
| Esc | Exit to Revit |

Eliminate each wave of purple sentry robots. Incoming orange projectiles and melee attacks damage you. Amber pickups replenish shells; green pickups restore health. Kills and wave starts also replenish a small amount of ammunition. Waves become larger and faster. Your score is local to the run.

## How the view becomes the level

The game renders a **snapshot of the active view in an owned WPF window aligned over its viewport**. It does not drive Revit's own camera or inject game entities into Revit's native renderer. This makes real-time input and animation independent of Revit's document refresh cycle. Closing the overlay reveals the view exactly as it was.

`CustomExporter` with `IModelExportContext` supplies tessellated model surfaces using the current view's visibility. The export adapter composes instance and link transforms and converts Revit's internal feet into metres around the selected spawn point. The same triangles feed both the renderer and a bounding-volume hierarchy for collision. Openings remain openings; walls, floors, furniture, equipment, and other exported meshes block movement and shots. Linked geometry is included when supplied by Revit's view export. Section boxes, detail settings, categories, phases, and filters determine the exported arena.

The controller uses an upright capsule, collision substeps, sliding, gravity, floor support, and stair stepping. Enemies use a bounded graph built by testing actual walkable movement from the spawn area. Hitscan pellets and enemy projectiles stop at geometry. There are no Revit API calls from the render or input loop.

## Scope and current limits

- This is an original shooter inspired by Doom's controls and combat loop, not a port of the Doom engine. All enemy meshes, weapon graphics, HUD, and synthesized sound are generated in code. No Doom game files or artwork are required.
- The level is static for the duration of a run. Doors stay in their modeled state, and game actions do not open or destroy Revit elements. Hide a closed door panel in the view before starting if that doorway should be passable.
- Materials use simplified flat colours with lighting. Glass is rendered opaque and collides. Textures, transparency sorting, Revit annotations, linework, and exact Revit visual styles are not reproduced.
- Only tessellated surfaces exported by Revit become geometry. Point clouds, symbolic-only objects, and unloaded links have no solid collision representation. Hidden geometry is intentionally absent. Imported/exporter-specific geometry and section-box behavior must be checked on representative models.
- Geometry is capped at **350,000 triangles** with a **90-second export timeout**. Use a local section box rather than an entire campus. Rendering speed depends on model complexity, graphics hardware, and WPF rendering support.
- Enemy navigation covers up to **1,800 reachable samples** on a 0.65 m grid within roughly 29 m of the clicked spawn. Very narrow passages may be walkable by the player but missed by this graph. Vertical ladders, lifts, moving platforms, and crouching are not implemented. Keep the arena near the selected floor area.
- You need a floor and sufficient standing clearance. Open-sided section cuts are physical openings; falling out of the model ends the run.
- This is a single-player, local session. Scores are not persisted. Errors, if any, are logged to `%LOCALAPPDATA%\BimArena\Logs`.

## Build from source

Requirements: .NET 10 SDK, Windows, and Revit 2027 or its matching `RevitAPI.dll` and `RevitAPIUI.dll` reference assemblies. Visual Studio must support .NET 10 and `.slnx`, or use the CLI. No third-party runtime packages are required.

```powershell
dotnet build BimArena.slnx -c Release
dotnet run --project tests/BimArena.Tests -c Release
.\scripts\Build.ps1
.\scripts\Install.ps1
```

The default reference location is `C:\Program Files\Autodesk\Revit 2027`. Override it if needed:

```powershell
dotnet build BimArena.slnx -c Release '-p:RevitApiDir=D:\Revit2027References'
.\scripts\Install.ps1 -Build -RevitInstallDir 'D:\Autodesk\Revit 2027'
```

`Build.ps1` runs engine regressions and publishes the add-in into `package/BimArena`. Autodesk assemblies are referenced with `Private=false` and are never deployed. The manifest receives an absolute installation path, so relocating the extracted source folder after installation is safe. Close Revit before updating or uninstalling.

Run `.\scripts\Uninstall.ps1` to unregister BIM Arena and remove its known installed assemblies. Other add-ins and unrelated files are preserved.

## Standalone Windows demo

Run **RunDemo.cmd** to try the same engine and renderer in a synthetic facility with a partition, real doorway, furniture, and stairs. The demo requires the **.NET 10 Windows Desktop Runtime** or SDK. It tests gameplay independently of Revit but does not validate Revit view export or viewport ownership.

```powershell
dotnet run --project src/BimArena.Demo -c Release
```

## Code map

| Project / file | Responsibility |
| --- | --- |
| `BimArena.Revit/App.cs` | Add-Ins panel, button, 3D-view availability |
| `BimArena.Revit/PlayCommand.cs` | Floor pick, snapshot, modal viewport ownership, error handling |
| `BimArena.Revit/ViewGeometryExport.cs` | View-specific triangles, materials, nested transforms, limits |
| `BimArena.Core` | Double-precision geometry, BVH, capsule controller, navigation, combat |
| `BimArena.Desktop` | WPF 3D rendering, HUD, audio, mouse and keyboard handling |
| `BimArena.Demo` | Windows gameplay harness with a synthetic building |
| `BimArena.Tests` | Dependency-free, cross-platform engine regressions |

## API references

- Autodesk confirms the [.NET 10 migration and Revit 2027 deployment changes](https://blog.autodesk.io/revit-2027-sdk-net-10-api-changes-and-additions/). Per-user installation is supported; this installer does not use the previous machine-wide ProgramData location.
- Autodesk's [custom export guide](https://help.autodesk.com/cloudhelp/2020/ENU/Revit-API/files/Revit_API_Developers_Guide/Advanced_Topics/Export/Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Export_Custom_export_html.html) describes view export contexts, geometry callbacks, and link/instance traversal.
- The [UIControlledApplication reference](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/4638c568-a118-1d57-ceed-a57595202644.htm) documents `CreateRibbonPanel(string)` on the Add-Ins tab; signatures were also checked against the 2027 assemblies.

The provided RevisionAutomator folder was inspected as a setup example and was not modified.
