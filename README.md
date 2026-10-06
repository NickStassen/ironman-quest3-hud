# ironman-quest3-hud

An Iron Man-style mixed-reality HUD for the Meta Quest 3. The app detects objects in the headset's own camera feed and draws world-locked overlays (boxes, labels, distance, target lock) on passthrough. A 3D-printed repulsor prop built around a Touch Plus controller aims and fires VR "blasts" at detected objects, with nicklink over BLE for LEDs and sound.

**Direction:** headset camera first. Unity 6 + Meta XR Core SDK + MRUK, Passthrough Camera API frames, on-device YOLO first, then offload to an RTX 5080 running DeepStream 8 + NvDCF. A head-mounted Jetson camera is an optional later phase.

**Status:** planning done; Phase 0 (Quest dev setup) next.

## Docs
- [docs/PLAN.md](docs/PLAN.md): phased plan, architecture, risks, week-1 checklist
- [docs/RESEARCH.md](docs/RESEARCH.md): research brief (as of Oct 6, 2026)

## Related repos
- [deepstream-house-tracker](https://github.com/NickStassen/deepstream-house-tracker): DeepStream detection + NvDCF tracking + JSON events
- [nicklink](https://github.com/NickStassen/nicklink): STM32F103 board with an LSM6DSV IMU
- [jetson-nano-ov5647](https://github.com/NickStassen/jetson-nano-ov5647): OV5647 CSI camera driver for Jetson Nano
