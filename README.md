# ironman-quest3-hud

An Iron Man-style mixed-reality HUD for the Meta Quest 3. A head-mounted Jetson Nano camera detects objects and streams them to a Quest 3 app, which draws HUD overlays (boxes, labels, distance, target lock) on passthrough. A 3D-printed repulsor prop with a [nicklink](https://github.com/NickStassen/nicklink) IMU inside aims and fires "blasts" at detected objects.

Status: planning. The full plan is coming in `docs/PLAN.md`.

Builds on:
- [deepstream-house-tracker](https://github.com/NickStassen/deepstream-house-tracker): Jetson detection + tracking + JSON events
- [jetson-nano-ov5647](https://github.com/NickStassen/jetson-nano-ov5647): CSI camera driver
- [nicklink](https://github.com/NickStassen/nicklink): STM32F103 board with an LSM6DSV IMU
