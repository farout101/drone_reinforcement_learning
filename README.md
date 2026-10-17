# RAVEN — Flood Rescue Drone Demo

A small Unity reinforcement-learning demonstration set in a flooded village. The drone searches survey sectors, locates survivors, assesses roads for ground rescuers, and flies back to base with a report.

## Run

In Unity **6000.6.6f1**, select **Drone RL → Open Demo**, press **Play**, and open the **Game** tab. No Python or new packages are needed.

Training starts automatically. The drone calibrates at base during stages 1–2, then launches rescue sorties in stage 3. Early sorties can fail while navigation improves; the drone recovers and relaunches. A completed survey returns to base, delivers its report, waits 12 seconds, and begins another sortie.

## Camera and controls

| Control | Action |
| --- | --- |
| Right mouse drag | Orbit |
| Middle mouse drag | Pan |
| Mouse wheel | Zoom |
| **1 / Isometric** | Reset to the village overview |
| **2 / Top-down** | Map-like overhead view |
| **3 / Follow** | Follow the drone |
| **R** | Reset camera |
| **Overlays** | Toggle flight trail, road assessments, and location labels |
| **Field report** | Open live findings or the last delivered report |
| **Training** | Open the collapsible learning dashboard |
| **Pause** | Pause simulation; the camera remains usable |
| **New sortie** | Restart the search without clearing the learned policy |

Camera drags and zoom respect the visible HUD panels. Buildings between the camera and drone fade; a reticle and label keep the drone identifiable. Try isometric and top-down for the full area, then follow for close flight views.

Click inside the Game view to focus mouse and keyboard controls. The report drawer opens on delivery and closes when the next sortie begins; you can reopen the last delivery at any time.

## Rescue mission

The fixed village contains uneven ground, a flooded river and road, a collapsed northern bridge, an intact southern bridge, damaged buildings, rubble, and four survivor locations.

The mission controller chooses among **nine survey waypoints**, independently of survivor locations. Q-learning chooses the six-direction flight actions between waypoints and home. At each flight step, observations reveal cells within a **6.3-unit horizontal sensor range**, subject to building line of sight. Survivor markers and report entries appear only after detection. This is a geometric sensor simulation, not camera-image recognition.

The minimap shows observed areas, survivors, the current flight path, and road status:

- **Red — blocked:** an observed segment contains deep water, rubble, or bridge collapse.
- **Green — clear:** the entire road has been observed without a blocking hazard.
- **Gray — unknown:** the road has not been fully observed and no blocking hazard is established yet.

A flooded road can be blocked for people while remaining open to aerial flight. Building occupancy and flight boundaries determine drone collisions. Water depth over 0.3 simulation units, rubble, and collapsed bridges determine ground-route blockage. These rules are part of this simulated scenario.

If a flight collides or stalls, recovery starts a new flight segment at base. Already collected observations remain in the current mission, and reports preserve the segment break instead of drawing a route across the recovery teleport.

## Reports

**Live assessment** contains current observations; **Last delivery** contains the report returned to base. A completed report includes:

- Discovered survivor coordinates and surface elevations.
- Road names, passable/blocked/unknown status, and damage reasons.
- Coordinates of observed blockages and each road's grid tiles.
- Observation coverage, flight moves, recovery count, and separate flight segments.
- Confirmation that the drone returned to base.

Select **Export delivered report / JSON** to write `RescueReports/latest-report.json` in the project directory. Only an actual delivered report can be exported. Use Pause to review a delivery indefinitely; otherwise the next sortie starts automatically and the previous delivery remains available.

Grid coordinates run from 0–8. World X/Z are `(coordinate - 4) * 2.5`. Flight cells encode `x + 9*z + 81*level`; level 0/1/2 corresponds to world heights 2.0/4.4/6.8. Roads use `x + 9*z`. Rendered building heights and the flight occupancy model use the same map definition.

## Reinforcement learning

The learner is **tabular, goal-conditioned Q-learning**, entirely in C#. State contains drone position, destination, and training stage. The stages are basic flight, survey transit, and rescue navigation. Advancement requires at least 400 stage episodes and three successive evaluations at 90% success or better, checked every 100 stage episodes.

- Six actions: north, east, south, west, up, down.
- Goal +8; collision −3; move −0.04; timeout −1.
- Distance potential shaping: `Phi = -0.08 * ManhattanDistance`; shaping is `0.97*Phi(next)-Phi(current)` with zero terminal potential.
- Q update: learning rate 0.22, discount 0.97; no bootstrap after termination.
- Exploration decays from 100% toward 6% within each stage and restarts on promotion.
- Later stages train from distributed legal positions and survey/home waypoints, supporting survey-to-survey travel and return flights.
- Evaluation uses separate randomness and zero exploration; it never updates the policy or training RNG.

**Navigation is learned. Search waypoint selection, survivor detection, damage assessment, flight stabilization, and recovery are scripted.** Mission discovery/report counts are separate from navigation training rewards. The demonstration uses one fixed map and thirteen destinations, so its results do not establish transfer to unseen disaster areas.

The training drawer provides speed, exploration preview, freeze/resume, save/load, reset, evaluation, and recent reward history. Freezing learning leaves the visible mission running. Version 3 saves use a separate key from the earlier grid demos and preserve the policy/curriculum, not a partially completed mission.

## Verification

Select **Drone RL → Verify Learning**. Checks cover flight boundaries, roof clearance, target reachability, terminal updates, sensor range/occlusion, report knowledge limits, route classification, recovery segment separation, evaluation isolation, and snapshot integrity.

For training seeds **1337, 42, and 2026**, tests after 30,000 final-stage episodes achieved **100% transit evaluation success** across 260 separate trials. Each complete mission found **4/4 survivors**, observed **99%** of the map, returned to base in **40–46 moves with no recoveries**, and correctly reported three blocked roads and two clear roads. These are seeded results for this map.

## Main files

- `RescueMap.cs`: terrain, flight occupancy, survivors, roads, and sensor line of sight.
- `DroneLearning.cs`: Q-learning, curriculum, evaluation, policy snapshots.
- `RescueMission.cs`: search sequencing, observations, recovery, reports.
- `RescueWorld.cs`: procedural village, drone, survivors, overlays, occlusion fading.
- `RescueCamera.cs`: orbit/pan/zoom and camera presets using the existing Input System.
- `DroneDemo.cs`: mission/training loops, HUD, minimap, reports, export.
- `Editor/DroneDemoSetup.cs`: scene creation and verification.
