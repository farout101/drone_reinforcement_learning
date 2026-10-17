# Flood Rescue Demo Implementation Plan

**Goal:** Build the accepted flooded-village rescue mission and visual overhaul in the existing Unity scene.

**Architecture:** A shared deterministic `RescueMap` defines terrain, flight occupancy, survivors, and ground roads. Goal-conditioned Q-learning handles movement; `RescueMission` chooses survey waypoints, observes only within sensor range/line of sight, and reports after returning home. Procedural world art, camera, and HUD remain separate from the simulation.

**Tech Stack:** Unity 6000.6.6f1, existing Input System/URP, C#, no new dependencies.

**Spec:** User-approved plan in chat: flooded/damaged terrain, survivor search, human-route hazard reporting, return-to-base loop, orbit/pan/zoom and camera presets, readable rescue visuals, minimap, collapsible training panel.

## Global Constraints

- Preserve the existing scene and editor menu entry points.
- One fixed 9×9 village, three flight levels, four survivors, nine survey waypoints and home.
- No hidden-person coordinates passed to the search controller; discoveries require range and line of sight.
- Flooded ground and collapsed bridges block human routes; flight collisions use building occupancy and arena bounds.
- Mark partially observed roads unknown unless an observed blocking hazard establishes blockage.
- Start rescue sorties after the final training stage unlocks; repeat after reporting, and recover/relaunch after flight failure.
- Scripted search planning/sensors and learned navigation must be identified in documentation.
- Version policy snapshots for changed geometry/targets; keep old saves separate.

## Review Focus

- Survey-to-survey and return-home navigation need training starts beyond the original launch corner.
- The rendered building heights and flight collision cells must agree.
- Reports must not leak undiscovered survivors or unobserved hazard cells.
- Crashes must not appear as continuous flight segments in exported routes.
- Mouse/keyboard camera input must respect visible UI and work with the project's Input System.

### Task 1: Shared map, learning and mission

- [x] Add `Assets/DroneDemo/RescueMap.cs` and `RescueMission.cs`.
- [x] Update `DroneLearning.cs` for survey/home goals, distributed starts, shared map occupancy, and snapshot version 3.
- [x] Verify goal reachability, trained transit navigation, sensor occlusion, road knowledge, mission completion/return, and report contents.

### Task 2: Procedural village and camera

- [x] Add `RescueWorld.cs` with faceted terrain, floodwater, damaged buildings, bridge damage, trees, survivors, base, and rescue drone.
- [x] Add `RescueCamera.cs` with orbit, pan, zoom, reset, isometric/top/follow presets, and occluder fading.
- [x] Replace the old arena presentation and confirm model geometry matches the shared map.

### Task 3: Mission HUD and verification

- [x] Update `DroneDemo.cs` with mission loop, minimap, discovery markers, compact HUD, report/export, and collapsible training controls.
- [x] Update `Editor/DroneDemoSetup.cs` verification and `README.md` instructions.
- [x] Compile, test the complete mission using multiple training seeds, run Play mode, inspect several camera angles, check console errors, and leave the scene ready to run.

Verification: core checks passed for seeds 1337, 42, and 2026, with 100% transit evaluation success, four survivors, 99% observation coverage, three blocked roads/two clear roads, and complete returns in 40–46 moves without recovery. Live missions delivered reports and the JSON export was parsed and validated. Camera presets and rendered views were inspected; Unity Input System event checks exercised orbit, pan, zoom, and UI exclusion. Temporary test input devices/settings were removed/restored. Final compile passed. One invalid CLI wait condition during verification triggered Editor error-pause; the condition was corrected and Play mode resumed. No game runtime errors were logged after resumption.

Final presentation checks: the isometric, overhead, follow, and training-drawer captures were rendered and inspected. Default overview/top framing fits the village. Final live runs delivered multiple reports. Reset cleared learned values, mission findings, and old overlays; the exported report remains available as an example artifact.
