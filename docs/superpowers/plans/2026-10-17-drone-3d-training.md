# Drone 3D Training Implementation Plan

**Goal:** Extend the existing small Unity-only demo with randomized waypoint targets, vertical actions, mastery-based difficulty, and measurable evaluation.

**Architecture:** Keep the pure C# grid environment shared by training and the rendered preview. Condition the Q-table on position, target, and difficulty stage. Preview and evaluation stay separate from training.

**Tech Stack:** Unity 6000.6.6f1, C#, tabular Q-learning; no additional dependencies.

**Spec:** The user accepted implementation of the lightweight upgrade proposed in chat: randomized targets, vertical movement, staged difficulty, and better metrics. Stabilization remains scripted.

## Global Constraints

- Keep the existing DroneLearning scene and menu entry points.
- Use a 9×9 arena with 3 altitude levels and 6 actions.
- Choose targets randomly from 12 documented waypoints; the map stays fixed.
- Stages: nearby ground targets, targets at multiple heights, then obstacles with different heights.
- Advance after at least 400 stage episodes and three successive 90% greedy evaluations, checked every 100 episodes.
- Version saved policies so the earlier four-action policy cannot be loaded into the new table.

## Review Focus

- Altitude boundaries and tall obstacles must terminate attempts consistently in training and preview.
- Target and stage must be included in Q-table indexing.
- Evaluation must leave Q-values, exploration, curriculum, and training randomness unchanged.
- Curriculum promotion must rely on measured performance and restart exploration for the harder stage.
- Invalid or outdated saved policies must fail before changing the live policy.

### Task 1: Goal-conditioned 3D learning

Modify `Assets/DroneDemo/DroneLearning.cs`; verify through `DroneDemoSetup.RunChecks()`.

- [x] Add position encoding, goal selection, altitude-dependent obstacles, and six-action transitions.
- [x] Add target/stage Q indexing and mastery progression with exploration restart.
- [x] Add success, collision, step-count, and vertical-move evaluation using a separate seed.
- [x] Version and validate policy snapshots, including curriculum progress.

### Task 2: Visible training and curriculum

Modify `Assets/DroneDemo/DroneDemo.cs`.

- [x] Render actual altitude, target changes, height-dependent obstacles, and a 3D trail.
- [x] Display stage mastery, evaluation metrics, preview height, and training charts.
- [x] Keep pause/freeze/reset/save/load behavior coherent across stage transitions.

### Task 3: Verify and document

Modify `Assets/DroneDemo/Editor/DroneDemoSetup.cs` and `README.md`.

- [x] Assert terminal behavior, indexing isolation, goal validity/reachability, curriculum progression, evaluation isolation, and snapshot integrity.
- [x] Compare trained and random policies with independent evaluation seeds and multiple training seeds.
- [x] Compile in Unity; confirm advancing Play mode, altitude changes, target changes, and no runtime errors; inspect a Game-view capture.
- [x] Document how to run, the waypoint limits, expected stages, and measured verification results.

Verification results: all algorithm checks passed for training seeds 1337, 42, and 2026 after 9,200 episodes each. Independent-seed obstacle evaluation reached 100% success, 0% collisions, and 7.6–7.7 moves/success. Unity compiled the final scripts without errors or warnings. Live Play mode advanced, promoted stages, and displayed altitude changes and randomized targets. Runtime JSON round-trip and reset checks passed. The final rendered heights and Game-view layout were verified; no new runtime errors were logged.
