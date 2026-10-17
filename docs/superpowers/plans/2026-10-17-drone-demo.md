# Small drone reinforcement learning demo

Goal: demonstrate repeated actions, rewards, Q-value updates, and an improving policy in a Unity scene.

Scope approved in conversation: a small working demo with obstacles; prioritize a visible learning loop over realistic flight.

Implementation choice: tabular Q-learning in C#, four grid movement actions, fixed altitude, fixed obstacle layout and destination. No Python or package installation. This replaces the earlier proposed PPO setup for a lightweight self-contained demonstration.

- [x] Build a deterministic grid environment and Q-learning update, including terminal collision, success, and timeout handling.
- [x] Build a 3D drone, obstacle arena, and live policy preview with a separate training loop.
- [x] Add training metrics and pause, speed, save/load, and reset controls.
- [x] Create a dedicated Unity scene through Editor APIs and document how to run it.
- [x] Check Q-updates and terminal behavior, compare learned and random policies, compile in Unity, and inspect the running scene when the Editor connection is available.

The learned policy applies to this fixed map and target. Flight stabilization is scripted; the learned behavior is obstacle navigation. Preview and evaluation do not update the policy.

Verification: the in-Editor checks passed after 2,000 seeded training episodes (22,565 updates), with random policy success 0% and learned policy success 100% over 90 trials. Live Play mode showed advancing frames, repeated preview episodes, and 100% evaluation success with no Console errors. The Game view capture was visually inspected.
