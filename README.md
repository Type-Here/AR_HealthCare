# AR HealthCare — Medical Avatar Overlay System

**Real-time body tracking and 3D medical avatar overlay for AR headsets, powered by MediaPipe and Unity.**

> Overlay a humanoid medical avatar on a real patient's body in augmented reality — arms, legs, head, and torso follow the patient's movements in real time.

![Unity](https://img.shields.io/badge/Unity-6.3_LTS-black?logo=unity)
![Platform](https://img.shields.io/badge/Platform-Magic_Leap_2-blue)
![MediaPipe](https://img.shields.io/badge/MediaPipe-0.16.3-brightgreen)
![AR Foundation](https://img.shields.io/badge/AR_Foundation-6.3.3-orange)
![XR Interaction Toolkit](https://img.shields.io/badge/XRI_Toolkit-3.3.0-gree)
![Status](https://img.shields.io/badge/Status-M4_Complete-success)

---

## Overview

AR HealthCare uses a **Magic Leap 2** AR headset (or any AR Foundation–compatible device) to capture the patient's body via the headset camera, run **Google MediaPipe BlazePose** inference **locally**, and overlay a fully rigged humanoid avatar that mirrors the patient's posture in real time. The system is built for healthcare professionals who need to visualize anatomical structures and clinical info on a live patient.

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

## First-time Setup on a New Machine

When cloning this repository on a new machine, most files are tracked in git **except the MediaPipe model binaries** (`Assets/StreamingAssets/*.bytes`), which are too large to commit and must be downloaded separately. Everything else (native libraries, packages, manifest) is versioned and will be present after a normal clone.

### 1 — MediaPipe model files (StreamingAssets)

The pose inference model binaries are loaded at runtime from `Assets/StreamingAssets/`. This folder is empty in the repo.

Place the following files in `Assets/StreamingAssets/`:

| File | Model | Source |
|------|-------|--------|
| `pose_landmarker_full.bytes` | BlazePose Full (**default**) | Download → rename `.task` → `.bytes` |
| `pose_landmarker_lite.bytes` | BlazePose Lite | Download → rename `.task` → `.bytes` |
| `pose_landmarker_heavy.bytes` | BlazePose Heavy | Download → rename `.task` → `.bytes` |

Download URLs (Google MediaPipe):
```
https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_full/float16/1/pose_landmarker_full.task
https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task
https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_heavy/float16/1/pose_landmarker_heavy.task
```

> **Why these are missing**: `Assets/StreamingAssets/` is **not excluded** by `.gitignore` (only the Addressables sub-path `aa*` is excluded), but the `.bytes` model files were never committed because of their size (~25–30 MB each). They must always be downloaded separately from Google.

> **Why tracking fails silently**: `Bootstrap.cs` calls `AssetLoader.PrepareAssetAsync(config.ModelPath)` which maps to `StreamingAssetsResourceManager`. If the file is missing, `PoseLandmarkerRunner.Run()` exits early with no output — tracking never starts and no obvious error is shown.

---

### 2 — MediaPipe Unity Plugin package

The repo does not includes the `.tgz` at `Packages/com.github.homuler.mediapipe-0.16.3/com.github.homuler.mediapipe-0.16.3.tgz` and it is tracked in git — **no action needed on a standard clone**.

The `.tgz` contains pre-compiled native libraries, so it must match the **target CPU architecture and OS**. The version used in this project is **0.16.3** built for Android ARM64 (Magic Leap 2 target).

**If you need to build the package for x86/64 for the Magic Leap 2:**

1. Go to **[homuler/MediaPipeUnityPlugin Releases](https://github.com/homuler/MediaPipeUnityPlugin/releases/tag/v0.16.3)** — pre-built `.tgz` packages for common targets are attached to each release.
2. If the pre-built package is not available, use the **GitHub Actions workflows** in the repo ([`.github/workflows/`](https://github.com/homuler/MediaPipeUnityPlugin/tree/master/.github/workflows)) to build for your specific architecture:
   - Fork or clone the repo, trigger the relevant workflow (e.g., `build_android.yml`), and download the artifact.
3. Replace the file at:
   ```
   AR_HealthCare/Packages/com.github.homuler.mediapipe-0.16.3/com.github.homuler.mediapipe-0.16.3.tgz
   ```
4. Re-import the package in Unity (right-click `Packages/manifest.json` → **Reimport**).

---

### 3 — Magic Leap SDK local packages

The two Magic Leap packages are stored as local `.tgz` archives. Verify each folder contains its archive:

```
AR_HealthCare/Packages/com.magicleap.setuptool/      ← must contain com.magicleap.setuptool-*.tgz
AR_HealthCare/Packages/com.magicleap.unitysdk/       ← must contain com.magicleap.unitysdk-*.tgz
```

If missing, obtain them from the Magic Leap Developer Portal or copy from the original machine.

---

### 4 — Android native library and Gradle template

Both files are **tracked in git** (neither `*.so` nor `*.gradle` are excluded by `.gitignore`) and will be present after a normal clone:

| File | Location | Notes |
|------|----------|---------|
| `libc++_shared.so` | `Assets/Plugins/Android/` | C++ shared runtime — in repo |
| `mainTemplate.gradle` | `Assets/Plugins/Android/` | Custom Gradle config — in repo |

`libc++_shared.so` is missing (e.g. corrupted clone), you can extract it from the Android NDK (`<NDK>/toolchains/llvm/prebuilt/<host>/sysroot/usr/lib/aarch64-linux-android/`) or copy it from another working checkout. A missing `.so` causes a `java.lang.UnsatisfiedLinkError` crash at app startup.

---

### 5 — Android Build Settings: Application Entry Point

Magic Leap 2 requires `Activity`-based entry point. `GameActivity` is not supported.

**In Unity:**
1. Go to **Edit → Project Settings → Player → Android → Other Settings → Configuration**
2. Find **Application Entry Point**
3. Select **Activity only** — deselect `GameActivity`

**Verify `Assets/Plugins/Android/AndroidManifest.xml`:**

The manifest should only contain the `Activity` block. If you regenerated it or it shows a `GameActivity` section, open the file and remove it (it is clearly marked by comments). The manifest in this repo is already correct if cloned without modifications.

> **Symptom when wrong**: the app installs but immediately crashes or shows a black screen, never reaching Unity `Start()`.

---

### Quick checklist before first build

```
# ── Must be obtained manually (NOT in git) ──────────────────────────────────
[ ] Assets/StreamingAssets/pose_landmarker_full.bytes        download from Google (see §1)
[ ] Assets/StreamingAssets/pose_landmarker_lite.bytes        optional
[ ] Assets/StreamingAssets/pose_landmarker_heavy.bytes       optional
[ ] Packages/com.github.homuler.mediapipe-0.16.3/*.tgz       required
[ ] Assets/Plugins/Android/libc++_shared.so                  required

# ── In git — just verify after clone ────────────────────────────────────────

[ ] Packages/com.magicleap.setuptool/*.tgz                   present
[ ] Packages/com.magicleap.unitysdk/*.tgz                    present
[ ] Assets/Plugins/Android/mainTemplate.gradle               present

# ── Unity settings (one-time, per machine) ───────────────────────────────────
[ ] Player Settings → Application Entry Point = Activity only
[ ] AndroidManifest.xml → no GameActivity block
```

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
| **Tracking works on original machine but not on new machine** | See [First-time Setup](#first-time-setup-on-a-new-machine) — model files and native libraries are not in git |
| No MediaPipe output after build | `Assets/StreamingAssets/pose_landmarker_full.bytes` missing — download and rename the `.task` file |
| App crashes on device immediately | `libc++_shared.so` missing from `Assets/Plugins/Android/` or wrong Application Entry Point |
| App installs but shows black screen | Application Entry Point includes `GameActivity` — set to `Activity` only in Player Settings |
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
| Package compile errors after clone | MediaPipe `.tgz` may be wrong architecture — replace with x86\_64 build from drive |

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
