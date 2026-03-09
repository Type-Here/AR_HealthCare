# AR HealthCare — Medical Avatar Overlay System

**Real-time body tracking and 3D medical avatar overlay for AR headsets, powered by MediaPipe and Unity.**

> Overlay a humanoid medical avatar on a real patient's body in augmented reality — arms, legs, head, and torso follow the patient's movements in real time.

![Unity](https://img.shields.io/badge/Unity-6.3_LTS-black?logo=unity)
![Platform](https://img.shields.io/badge/Platform-Magic_Leap_2-blue)
![MediaPipe](https://img.shields.io/badge/MediaPipe-0.16.3-brightgreen)
![AR Foundation](https://img.shields.io/badge/AR_Foundation-6.3.3-orange)
![Status](https://img.shields.io/badge/Status-M4_Complete-success)

---

## Overview

AR HealthCare uses a **Magic Leap 2** AR headset (or any AR Foundation–compatible device) to capture the patient's body via the headset camera, run **Google MediaPipe BlazePose** inference locally, and overlay a fully rigged humanoid avatar that mirrors the patient's posture in real time. The system is built for healthcare professionals who need to visualize anatomical structures on a live patient.

---

## Features

- **Real-time body tracking** via MediaPipe BlazePose (33 landmarks, ~30 fps on device)
- **Two-phase Hybrid IK** — Unity 2-bone solver + post-IK bone correction in `LateUpdate` for accurate elbow/knee bending
- **Dynamic spatial anchoring** — avatar auto-scales and anchors to the patient's hips based on estimated distance and body height
- **Dual camera paths** — AR Foundation (`ARFCameraBridge`) and Magic Leap native (`MLCameraBridge`) 
- **Two tracker variants** — world-coordinate V1 with yaw-only camera alignment and screen-space projection V2
- **Mock tracker** for Editor testing without a device or MediaPipe pipeline
- **World Space UI** — main menu, patient info panel, AR tracking environment, XR ray interaction
- **XR controller interaction** — Magic Leap 2 trigger/bumper via OpenXR Input System bindings
- **M4 performance optimizations** — zero per-frame GC, cached camera, dead-zone jitter filter, velocity-based adaptive smoothing, tracking loss graceful degradation

---

## Demo

| Mode | What you see |
|------|-------------|
| Mock (Editor) | Avatar animates with fake proportions + arm swing |
| MediaPipe (Device) | Avatar follows real patient tracked via headset camera |

> Scene gizmos (Editor): 🔴 hips center · 🟡 anchor target · 🔵 joint landmarks · 🟢 tracked limb segments

---

## Architecture

```
AR Headset Camera
    │
    ├── ARFCameraBridge (AR Foundation)
    └── MLCameraBridge  (Magic Leap native)
              │
        ImageSource (ARFImageSource / MLCameraImageSource)
              │
        PoseLandmarkerRunner  ←  MediaPipe BlazePose
              │
        IBodyTracker
          ├── MediaPipeTracker    (world landmarks, yaw-only alignment)
          ├── MediaPipeTrackerV2  (screen-space projection)
          └── MockBodyTracker     (Editor testing)
              │
        TrackingManager  (ITrackingProvider — central API)
              │
        HybridAvatarIK
          ├── Phase 1 — OnAnimatorIK  (body root + IK goals + hints)
          └── Phase 2 — LateUpdate    (direct bone rotation correction)
              │
        Humanoid 3D Avatar  (overlaid on patient)
```

For full pipeline details see [`Docs/README.md`](Docs/README.md).

---

## Project Structure

```
Assets/
├── Scripts/
│   ├── Core/               # TrackingManager, ITrackingProvider, SceneTrackerSetup
│   ├── Trackers/           # IBodyTracker, MediaPipeTracker (V1+V2), MockBodyTracker
│   ├── DataClasses/        # PatientTrackingData, LandMarkPoints
│   ├── AnimatorIK/         # HybridAvatarIK (active), IKSkeletonAnimator, MedicalAvatarIK_v2
│   ├── Input/
│   │   ├── ARF/            # ARFCameraBridge, ARFImageSource
│   │   └── ML/             # MLCameraBridge, MLCameraImageSource
│   ├── PoseMarkerMediapipe/ # Modified MediaPipe runner, config, common infrastructure
│   ├── UI/                 # UIManager, patient info, XRUIClickBridge, hover/fade scripts
│   └── Debug/              # DebugVisualizer, XRUICanvasFixer, InteractableCube
├── Scenes/
│   └── DebugSceneLandmarkUI2.unity   # Main scene (production-ready)
├── Prefab/UI/              # Menu Canvas, PatientInfo Canvas, BackButton Canvas
└── Resources/              # Bootstrap prefab, AppSettings, config windows
Docs/
└── README.md               # Full technical documentation
Packages/
├── com.github.homuler.mediapipe-0.16.3/
└── com.magicleap.unitysdk/
```

---

## Requirements

| Requirement | Version |
|-------------|---------|
| Unity | 6.2+ (tested on 6.3 LTS) |
| AR Foundation | 6.3.3 |
| MediaPipe Unity Plugin | 0.16.3 (included as local package) |
| Magic Leap SDK | latest (included as local package) |
| OpenXR | 1.16.1 |
| Input System | 1.14.2 |
| URP | 17.2.0 |
| **Build target** | Android ARM64 |

---

## Quick Start

### In Editor (Mock Mode — no device needed)

1. Open `Assets/Scenes/DebugSceneLandmarkUI2.unity`
2. Select the `_TrackerSetup` GameObject → enable **`useMockData`** on `SceneTrackerSetup`
3. Press **Play**
4. Enable **Gizmos** in Scene view to visualize joint positions

### On Device (Magic Leap 2)

1. Select `_TrackerSetup` → disable `useMockData`, choose tracker version:
   - `useV2Tracker = false` → MediaPipeTracker V1 (recommended)
   - `useV2Tracker = true` → MediaPipeTrackerV2 (screen-space)
2. **Build Settings** → Platform: Android, Architecture: ARM64
3. Deploy to Magic Leap 2
4. Grant **Camera** permission when prompted
5. Point the headset at a person — the avatar overlays automatically

---

## Hybrid IK System

The active IK driver is `HybridAvatarIK` — a two-phase approach that combines the stability of Unity's built-in 2-bone solver with post-IK direct bone correction:

**Phase 1 — `OnAnimatorIK`**
- Positions avatar root at patient hips (with vertical offset + dynamic scale)
- Sets IK goals for hands and feet; IK hints for elbows and knees
- Head look-at targets the nose landmark

**Phase 2 — `LateUpdate`**
- At this point all bone transforms are *fully resolved* (post-IK)
- For each limb segment: computes `FromToRotation` delta between IK result and tracked direction
- Applies blended correction (`correctionWeight`, default 0.7) with exponential smoothing

This eliminates both the **2-bone singularity** (rigid elbows at full extension) and the **stale parent transform** problem (cascading rotation errors from direct bone driving in `OnAnimatorIK`).

<details>
<summary>IK Approach History</summary>

| System | Approach | Result |
|--------|----------|--------|
| `HybridAvatarIK` | Phase 1 IK + Phase 2 bone correction | **Best — active** |
| `MedicalAvatarIK` | Pure Unity 2-bone IK | Stable but rigid elbows/knees |
| `MedicalAvatarIK_v2` | IK + improved hints | Inconsistent improvements |
| `IKSkeletonAnimator` | Direct bone drive (ZED-style) | Better bends, but rotation errors |

</details>

---

## Configuration

Key parameters on `HybridAvatarIK` (Inspector):

| Parameter | Default | Notes |
|-----------|---------|-------|
| `correctionWeight` | 0.7 | Phase 2 bone correction amount (0 = pure IK, 1 = full tracking) |
| `verticalBodyOffset` | 0.3 m | Compensates avatar head appearing lower than real head |
| `referenceBodyHeight` | 1.7 m | Reference height for dynamic scaling |
| `movementDeadZone` | 0.02 m | Jitter filter threshold |
| `useAdaptiveSmoothing` | true | Velocity-based smoothing |
| `drawGizmos` | true | Debug overlay in Scene view and builds |

Key parameters on `MediaPipeTracker`:

| Parameter | Default | Notes |
|-----------|---------|-------|
| `manualOffset` | (0, -0.5, 2) | (lateral, height below eyes, forward distance in meters) |
| `lockAnchorPosition` | true | Freeze world anchor at first detection |
| `anchorUpdateSpeed` | 0.3 | Drift speed of locked anchor (m/s) |

---

## XR Interaction (Magic Leap 2)

UI interaction uses a dual-layer approach for Magic Leap 2 + OpenXR:

- All World Space Canvases use `TrackedDeviceGraphicRaycaster` (not the standard `GraphicRaycaster`)
- `XRUIClickBridge` bypasses the broken `UIPressInput` propagation on ML2: it fires `ExecuteEvents` directly on the UI hit object using `XRRayInteractor.TryGetCurrentUIRaycastResult`
- `XRUICanvasFixer` adds `TrackedDeviceGraphicRaycaster` at runtime as a safety net (`[DefaultExecutionOrder(-100)]`)

Controller bindings (OpenXR):
- **Trigger**: `<MagicLeapController>{RightHand}/triggerPressed`
- **Bumper**: `<MagicLeapController>{RightHand}/gripPressed`

---

## Troubleshooting

<details>
<summary>Common issues and fixes</summary>

| Problem | Fix |
|---------|-----|
| Avatar not moving in Editor | Enable `useMockData` on `SceneTrackerSetup` |
| Avatar too high/low | Adjust `verticalBodyOffset` on `HybridAvatarIK` |
| Avatar too small/large | Adjust `referenceBodyHeight` or use `manualAvatarScale` |
| Body orbits when rotating head | Use MediaPipeTracker V1; check `lockAnchorPosition = true` |
| Elbows stay rigid | Increase `correctionWeight` to 0.8–0.9 |
| Jittery movement | Increase `movementDeadZone` (0.02–0.05) |
| UI not responding to ray | Check `TrackedDeviceGraphicRaycaster` on Canvas, verify `XRUIClickBridge` is in scene |
| Camera feed is black | Verify `ARFCameraBridge` has `ARCameraManager` assigned; check camera permission |
| Native crash on device | Do **not** call `PoseLandmarkerRunner.Play()` manually — it's managed by Bootstrap |
| No MediaPipe output | Check `PoseLandmarkerRunner.isRunning`; verify Bootstrap completed initialization |

</details>

---

## Milestones

| # | Milestone | Status |
|---|-----------|--------|
| M1 | Base pipeline: Camera → MediaPipe → Tracker → IK | ✅ Complete |
| M2 | Dynamic spatial anchoring, distance/height estimation, auto-scaling | ✅ Complete |
| M3 | Scene cleanup — removed debug MediaPipe Canvas overlays | ✅ Complete |
| M4 | Performance: zero-GC, camera cache, dead-zone, adaptive smoothing, tracking loss handling | ✅ Complete |
| M5 | Device validation on Magic Leap 2, end-to-end user testing | 🔄 In progress |

---

## Documentation

Full technical documentation is available in [`Docs/README.md`](Docs/README.md), including:

- Detailed pipeline breakdown (all 5 stages)
- Complete script reference for every file
- Full configuration reference tables
- Guide for adding new trackers and camera backends
- Performance budget and optimization notes

---

## Dependencies

| Package | Version | License |
|---------|---------|---------|
| [MediaPipe Unity Plugin](https://github.com/homuler/MediaPipeUnityPlugin) | 0.16.3 | MIT |
| Unity AR Foundation | 6.3.3 | Unity ToS |
| Magic Leap SDK | — | Magic Leap ToS |
| Unity OpenXR | 1.16.1 | Unity ToS |
| Unity Input System | 1.14.2 | Unity ToS |

---

## Acknowledgement

`MediaPipeUnityPlugin` is a fantastic open-source project that made this possible. Special thanks to the maintainers and contributors for their work on the plugin and documentation.

---

## Disclaimer

This project is released "as is", without any warranties. It is intended as a technical prototype for demonstration purposes only. It is not intended for production use or clinical applications. Always consult with qualified professionals for medical use cases.

For specific components, please refer to the original licenses of the dependencies.

The disclaimer above and the license information from `main` branch apply to this project as a whole. Individual files may have additional license notices if they include code from third-party sources.

---

*Last updated: March 2026*
