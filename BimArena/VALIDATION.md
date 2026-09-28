# Validation report

Date: 2026-09-28. Target: Revit 2027 / .NET 10 / Windows x64.

## Completed

- Entire five-project solution built successfully: **0 warnings, 0 errors**.
- **23 of 23 engine regressions passed** on .NET 10. The latest run took approximately 5.7 seconds.
- The add-in output is a Windows x64 managed DLL. Core and desktop dependencies are included.
- `.addin` application entry point, command name, availability class, reference paths, and installer payload were checked against the project output.
- Autodesk API assemblies are not included in the deliverable.

The compilation environment was macOS arm64 with a private .NET SDK 10.0.401 and Microsoft Windows Desktop targeting pack 10.0.12. Revit 2027 API assemblies were obtained for compilation only from the Nice3point.Revit.Api.RevitAPI and RevitAPIUI 2027.0.0 reference packages. The shipped project normally references the user's installed Revit 2027 assemblies, without a NuGet dependency on those packages. No mock Revit API was used for the build.

## Regression coverage

1. Two-sided triangle rays, range limits, and nearest-surface selection.
2. BVH agreement with a brute-force oracle for 500 deterministic randomized rays.
3. Capsule contact against triangle edges at body height.
4. Stable floor support without drift.
5. Sprint collision with a 10 mm wall.
6. Sliding along walls while moving diagonally.
7. Traversal through a real opening in a partition.
8. Rejection of a 400 mm gap by the standing player capsule.
9. Ascent of 180 mm stair treads.
10. Rejection of a tall obstacle as an automatic step.
11. Head collision when jumping under a low ceiling.
12. Spawn rejection where standing headroom is insufficient.
13. Furniture blocking movement and weapon rays.
14. Reachable enemy paths between rooms through a doorway.
15. Enemies protected from shots by intervening walls.
16. Visible enemies taking hits, dying, and awarding score.
17. Paused simulation, bounded time catch-up, and restart reset.
18. Enemy waves spawning on reachable, unoccupied geometry.
19. Sphere ray origin and behind-target cases.
20. Head-height capsule hits, vertical cap hits, and misses above the target.
21. Stair stepping unable to lift a player through low headroom.
22. Stable support on a sloped floor.

Some test cases combine closely related assertions; the executable reports 23 named tests. Full latest output is in [test-results.txt](test-results.txt).

## Required Windows / Revit acceptance checks

These are **pending**, not claimed as passed. Revit and the WPF runtime cannot execute on the development Mac.

| Check | Expected result |
| --- | --- |
| Install, restart Revit 2027 | Exactly one Play BIM Arena button appears on Add-Ins, without a separate custom tab |
| No document / 2D view / view template | Button is unavailable; defensive command check also rejects invalid views |
| Perspective and orthographic 3D views | Floor click loads the arena and the game aligns with the chosen viewport |
| Mixed monitor DPI, tiled views, maximize, Alt-Tab | Overlay alignment stays correct; focus loss pauses and releases cursor |
| Escape while selecting or loading | Selection/export cancels cleanly; no game window remains |
| Escape from gameplay; Alt-F4 | Window closes, mouse is released, and Revit responds normally |
| Host floor, wall, family furniture, stairs, openings | Visual surfaces and player/weapon collision coincide |
| Rotated, translated, mirrored and nested families / links | Exported geometry is correctly positioned; linked walls block shots |
| Hidden categories, section boxes, phases and design options | Arena follows the actual view export; section cuts and floor extents are intentional |
| Glass, closed doors, imported geometry, point clouds | Behavior matches the limitations in README; no claim of volume for non-mesh items |
| Large view beyond triangle budget | Loading stops with a useful message; no incomplete arena starts |
| Several waves, reload, damage, pickups, defeat, restart | Combat state, audio, and HUD behave consistently |
| Compare model state before and after | No document edits or camera changes; no gameplay undo entries |
| Install update / uninstall | Only BIM Arena's own per-user manifest and assemblies are affected |

Start with the standalone demo to validate Windows input, audio, drawing, and gameplay. Then use a small Revit test model with a floor, partition, doorway, ceiling, chair, stairs, and one transformed linked model before testing a large production view.
