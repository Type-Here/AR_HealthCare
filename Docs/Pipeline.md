# AR HealthCare — Medical Avatar Overlay System

**Real-time body tracking and medical avatar overlay for AR headsets, powered by MediaPipe and Unity.**

AR HealthCare is a Unity-based augmented reality application that overlays a 3D humanoid medical avatar on a real patient's body in real time. It uses Google MediaPipe Pose Landmark Detection to track the patient's body from the headset camera, transforms the tracked landmarks into Unity world-space joint positions, and drives a humanoid avatar using a hybrid Inverse Kinematics system. The result is a transparent medical avatar that follows the patient's movements — arms, legs, head, and torso — enabling healthcare professionals to visualize anatomical structures overlaid on the physical body.

**Primary target:** Magic Leap 2 (AR headset), with AR Foundation support for cross-platform deployment.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Project Structure](#project-structure)
  - [Assets/Scripts/Core](#assetsscriptscore)
  - [Assets/Scripts/Trackers](#assetsscriptstrackers)
  - [Assets/Scripts/DataClasses](#assetsscriptsdataclasses)
  - [Assets/Scripts/AnimatorIK](#assetsscriptsanimatorik)
  - [Assets/Scripts/Input](#assetsscriptsinput)
  - [Assets/Scripts/PoseMarkerMediapipe](#assetsscriptsposemarkermedipipe)
  - [Assets/Scripts/UI](#assetsscriptsui)
  - [Assets/Scripts/Debug](#assetsscriptsdebug)
  - [Assets/Scenes](#assetsscenes)
  - [Assets/Prefab](#assetsprefab)
  - [Assets/Resources](#assetsresources)
  - [Packages](#packages)
  - [Docs](#docs)
- [Pipeline Detail](#pipeline-detail)
  - [1. Camera Acquisition](#1-camera-acquisition)
  - [2. MediaPipe Pose Detection](#2-mediapipe-pose-detection)
  - [3. Tracker Layer](#3-tracker-layer)
  - [4. Tracking Manager](#4-tracking-manager)
  - [5. Avatar IK](#5-avatar-ik)
- [Hybrid IK System — Technical Design](#hybrid-ik-system--technical-design)
- [IK Approach Comparison](#ik-approach-comparison)
- [Data Model](#data-model)
- [UI System](#ui-system)
- [XR Interaction (Magic Leap 2)](#xr-interaction-magic-leap-2)
- [Requirements](#requirements)
- [Scene Setup Guide](#scene-setup-guide)
- [Running in Editor (Mock Mode)](#running-in-editor-mock-mode)
- [Running on Device (MediaPipe Mode)](#running-on-device-mediapipe-mode)
- [Configuration Reference](#configuration-reference)
- [Extending the System](#extending-the-system)
- [Performance Notes](#performance-notes)
- [Known Limitations](#known-limitations)
- [Troubleshooting](#troubleshooting)
- [Milestones](#milestones)

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                        AR HEADSET CAMERA                             │
│                    (Magic Leap 2 / AR Foundation)                    │
└─────────────────────────────────────────────┬────────────────────────┘
                                              │
                                              ▼
┌─────────────────────────┐     ┌──────────────────────────┐
│   ARFCameraBridge        │     │   MLCameraBridge          │
│   (AR Foundation path)   │     │   (Magic Leap native)     │
│   Captures CPU/GPU tex   │     │   MLCamera RGBA capture   │
└─────────────┬───────────┘     └─────────────┬────────────┘
              │                               │
              ▼                               ▼
┌──────────────────────────────────────────────────────────┐
│                    ImageSource                            │
│        ARFImageSource  /  MLCameraImageSource             │
│  (provides Texture to MediaPipe pipeline)                 │
└─────────────────────────┬────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────┐
│              PoseLandmarkerRunner (Modified)               │
│  MediaPipe Pose Landmark Detection (BlazePose model)      │
│  Outputs: poseLandmarks (2D) + poseWorldLandmarks (3D)    │
└─────────────────────────┬────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────┐
│                   IBodyTracker                            │
│  MediaPipeTracker (V1)  │  MediaPipeTrackerV2  │  Mock   │
│  World landmark coords  │  Screen-space proj.  │  Fake   │
└─────────────────────────┬────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────┐
│               TrackingManager                             │
│   Central API (ITrackingProvider)                         │
│   Polls tracker every frame, caches PatientTrackingData   │
└─────────────────────────┬────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────┐
│               HybridAvatarIK (Active)                     │
│   Phase 1: OnAnimatorIK — body root, IK goals, hints     │
│   Phase 2: LateUpdate — bone rotation corrections         │
│   + Dynamic scaling, spatial anchoring, smoothing         │
└──────────────────────────────────────────────────────────┘
                          │
                          ▼
                  ┌───────────────┐
                  │  Humanoid     │
                  │  3D Avatar    │
                  │  (overlaid    │
                  │   on patient) │
                  └───────────────┘
```

---

## Project Structure

### Assets/Scripts/Core

The core module contains the central tracking API and scene orchestration.

| File | Description |
|------|-------------|
| `ITrackingProvider.cs` | Public interface for consuming tracking data. Returns `PatientTrackingData` via `GetPose()`. All visualization consumers depend on this, not concrete trackers. |
| `TrackingManager.cs` | Central MonoBehaviour that implements `ITrackingProvider`. Acts as a dependency injection container — receives an `IBodyTracker` via `SetTracker()`, polls it every `Update()`, and caches the latest `PatientTrackingData` for consumers like the IK system. |
| `SceneTrackerSetup.cs` | Scene bootstrap script that injects the correct tracker into `TrackingManager` at `Awake()`. Supports choosing between `MockBodyTracker`, `MediaPipeTracker` (V1 world coords), or `MediaPipeTrackerV2` (screen-space projection) via inspector toggles (`useMockData`, `useV2Tracker`). Auto-wires references if not assigned. |
| `MedicalAvatarIK.cs` | Original IK driver (668 lines). Proven stable positioning with Unity's 2-bone solver. Used as the reference implementation; see [IK Approach Comparison](#ik-approach-comparison). |
| `UITracking.cs` | Simple toggle script: activates/deactivates a list of GameObjects when tracking starts/stops. Used by `UIManager` to show/hide the tracking environment. |
| `GeneralConfigs.cs` | Compile-time debug flag (`#define AR_HEALTHCARE_DEBUG`). When defined, enables verbose logging across the project. |
| `Android/AndroidPermissionRequest.cs` | Requests Android Camera permission at runtime on device. |
| `FollowHead/FollowHead3DBody.cs` | Makes a 3D GameObject follow the user's head with a configurable local offset. Useful for positioning the avatar relative to the camera. Supports yaw-only mode. |
| `FollowHead/FollowHeadCanvas.cs` | Makes a World Space Canvas follow the user's head horizontally, maintaining a fixed distance and height. |

### Assets/Scripts/Trackers

Concrete tracker implementations that convert device-specific tracking data into the unified `PatientTrackingData` format.

| File | Description |
|------|-------------|
| `IBodyTracker.cs` | Interface for all tracking backends. Methods: `Initialize()`, `GetTrackingData()`, `StopTracking()`. |
| `MediaPipeTracker.cs` | **V1 tracker (352 lines).** Converts MediaPipe `poseWorldLandmarks` to Unity world-space. Features: yaw-only camera alignment (prevents body orbiting with head tilt), hips-relative offsets, lockable world anchor (`lockAnchorPosition`), configurable axis inversion (`invertX/Y/Z`), manual offset, pre-allocated dictionaries (zero GC), cached `Camera.main`, profiling markers, distance and body height estimation. |
| `MediaPipeTrackerV2.cs` | **V2 tracker (186 lines).** Screen-space projection approach: XY from `poseLandmarks` (normalized 2D) → screen pixels, Z from `poseWorldLandmarks` (metric depth), then `Camera.ScreenToWorldPoint()`. No manual anchor or axis guessing needed — Unity's projection matrix handles it automatically. Simpler but requires stable 2D landmark confidence. |
| `MockBodyTracker.cs` | Fake tracker for Editor testing (129 lines). Generates realistic body proportions with configurable distance and height. Optional arm swing animation. Provides `EstimatedDistance` and `EstimatedBodyHeight` metadata for testing dynamic scaling. |

### Assets/Scripts/DataClasses

Data structures shared across the tracking pipeline.

| File | Description |
|------|-------------|
| `PatientTrackingData.cs` | Core data struct: `IsTracked` (bool), `Joints` (Dictionary<string, Pose> — world-space positions), `EstimatedDistance` (camera-to-patient in meters), `EstimatedBodyHeight` (patient height in meters). Used by every component in the pipeline. |
| `LandMarkPoints.cs` | Static dictionary mapping MediaPipe landmark indices (0–32) to human-readable joint names ("Nose", "LeftShoulder", "RightElbow", etc.). These strings are the joint keys used throughout the system. Based on the [MediaPipe Pose model](https://github.com/google-ai-edge/mediapipe/blob/master/docs/solutions/pose.md). |

### Assets/Scripts/AnimatorIK

Avatar animation drivers. Multiple approaches were developed and tested; `HybridAvatarIK` is the active production version.

| File | Description |
|------|-------------|
| **`HybridAvatarIK.cs`** | **Active IK system (743 lines).** Two-phase hybrid approach — see [Hybrid IK System](#hybrid-ik-system--technical-design). Phase 1 (OnAnimatorIK): body root placement + Unity IK goals with elbow/knee hints. Phase 2 (LateUpdate): direct bone rotation corrections from tracked joint directions. Includes dynamic scaling, spatial anchoring, dead-zone jitter filtering, velocity-based adaptive smoothing, tracking loss handling with graceful degradation (5s timeout) and recovery blend (1.5s), runtime debug overlay (GL lines for builds), and editor gizmos. |
| `IKSkeletonAnimator.cs` | **Experimental (516 lines).** Direct bone rotation driving adapted from ZED SDK's `ZEDSkeletonAnimator`. Drives ALL limb bones via `SetBoneLocalRotation`, eliminating 2-bone singularity. Includes foot IK with raycast grounding and foot locking. Better elbow/knee bending but suffers from cascading rotation errors due to stale parent transforms in `OnAnimatorIK`. Kept as reference for future improvements. |
| `MedicalAvatarIK_v2.cs` | **Experimental (514 lines).** Intermediate iteration with improved elbow hints and knee handling. Similar to `MedicalAvatarIK` with alternative weight configurations. Results were inconsistent — kept as reference. |
| `BaseIK/MediaPipeAvatarIK.cs` | **Legacy.** Early prototype using external IK targets (`Transform` references) and a raw `Vector3[]` landmark array. Requires manual assignment of hand/foot/elbow/knee targets in the inspector. |
| `BaseIK/MediaPipeAvatarIKBridge.cs` | **Legacy.** Event-driven bridge connecting `PoseLandmarkerRunner.ResultUpdated` directly to `MediaPipeAvatarIK.UpdatePose()`. Handles coordinate conversion (axis inversion, scale, camera offset). Replaced by the Tracker→Manager→IK pipeline. |

### Assets/Scripts/Input

Camera input bridges that provide textures from device cameras to the MediaPipe pipeline.

| Subfolder | File | Description |
|-----------|------|-------------|
| `ARF/` | `ARFCameraBridge.cs` | **AR Foundation path (462 lines).** Captures camera frames from `ARCameraManager` and exposes them as `CurrentCameraTexture`. Supports both **Sync** and **Async** CPU acquisition modes with configurable downscaling (`cpuDownscale`). Has GPU detection logic (reflection-based access to `ARCameraBackground` material) for future GPU readback support. Handles `NativeArray` buffer lifecycle and async conversion state. |
| `ARF/` | `ARFImageSource.cs` | **AR Foundation ImageSource (399 lines).** Implements the MediaPipe `ImageSource` abstraction. Wraps `ARFCameraBridge` to provide texture, resolution, and lifecycle management (Play/Pause/Resume/Stop). Returns camera texture dimensions and a single logical resolution. Designed as drop-in replacement for MediaPipe's default webcam source. |
| `ML/` | `MLCameraBridge.cs` | **Magic Leap native path (232 lines).** Direct integration with `MLCamera` API for Magic Leap 2. Handles Android camera permission request, camera device availability polling, connection, RGBA video capture configuration, and per-frame texture updates via `OnRawVideoFrameAvailable` callback. Configurable capture resolution and frame rate. |
| `ML/` | `MLCameraImageSource.cs` | **Magic Leap ImageSource (109 lines).** Wraps `MLCameraBridge` for the MediaPipe `ImageSource` abstraction. Same interface as `ARFImageSource` but backed by the native ML camera. |

### Assets/Scripts/PoseMarkerMediapipe

Modified MediaPipe sample scripts, adapted for the ARHealthCare pipeline.

| Path | Description |
|------|-------------|
| `Pose Landmark Detection/PoseLandmarkerRunner.cs` | **Modified MediaPipe runner (221 lines).** Extended from the original sample to expose `LatestResult` (public property) and `ResultUpdated` event for external consumers. Manages the pose detection loop: reads images from `ImageSource`, runs inference via `PoseLandmarker` task API, and dispatches results. Supports IMAGE, VIDEO, and LIVE_STREAM running modes. Handles GPU/CPU image read paths and `TextureFramePool` management. |
| `Pose Landmark Detection/PoseLandmarkDetectionConfig.cs` | Configuration class for MediaPipe detection. Exposes: Delegate (CPU/GPU auto-detect by platform), ImageReadMode, Model (BlazePoseLite/Full/Heavy), RunningMode, NumPoses, detection/presence/tracking confidence thresholds, segmentation mask toggle. Default: BlazePoseFull, LIVE_STREAM, GPU on device / CPU in editor. |
| `Common/Scripts/` | Shared infrastructure: `Bootstrap.cs` (GPU/asset initialization), `AppSettings.cs`, `AssetLoader.cs`, `ImageSource/` (base class + providers), `TaskApiRunner.cs` + `VisionTaskApiRunner.cs` (base runner classes), `Screen.cs`, `ImageSourceProvider.cs`, and utility scripts. These are modified from the MediaPipe Unity Plugin samples to fit the ARHealthCare architecture. |
| `UI/Scripts/` | MediaPipe config UI scripts: `SolutionMenu.cs`, `ImageSourceConfig.cs`, `PoseLandmarkDetectionConfigWindow.cs`, and modal dialog helpers. Used in the debug config window prefab. |

### Assets/Scripts/UI

User interface for the application — menu system, patient information panel, and XR interaction.

| File | Description |
|------|-------------|
| `UImanager.cs` | Main UI controller. Manages transitions between Main Menu → AR Tracking Environment → Patient Info, with fade animations. Coordinates `UITracking` to start/stop the tracking pipeline when entering/leaving AR mode. |
| `MainMenuFade.cs` | Fade in/out animation for the main menu Canvas using `CanvasGroup.alpha` interpolation. |
| `PatientInfoFade.cs` | Fade in/out animation for the patient info Canvas. |
| `PatientInfoUI.cs` | Populates the patient information panel with medical data (diagnosis, treatment plan, notes). Currently uses hardcoded test data. |
| `PatientInfoFollowBody.cs` | Makes the Patient Info Canvas follow the medical avatar's body position with configurable offset, forward distance, and camera-facing rotation. Smooth position/rotation following via lerp/slerp. |
| `HoverScript.cs` | `HoverScaleImage` — XR-compatible hover effect for UI images. Implements `IPointerEnterHandler`/`IPointerExitHandler` to smoothly scale elements on hover. Requires `TrackedDeviceGraphicRaycaster` on the Canvas. |
| `_HoverAnimation.cs` | Alternative hover animation component with programmatic `SetHoverState()` API. |
| `BackToMenuButton.cs` | Simple button handler calling `UIManager.ReturnToMenu()`. |
| `ScrollButton.cs` | Scroll up/down buttons for `ScrollRect` content navigation (useful on headsets without scroll input). |
| `ToDoInfoPrint.cs` | Displays a "feature under development" message in a Canvas element for 5 seconds when unimplemented buttons are pressed. |
| `ExitApplication.cs` | Quits the application (or stops Play mode in Editor). |
| `XRUIClickBridge.cs` | **Critical for Magic Leap 2 UI interaction.** Bypasses `XRUIInputModule`'s internal click pipeline (which doesn't propagate correctly on ML2/OpenXR). Uses `XRRayInteractor.TryGetCurrentUIRaycastResult` and fires `ExecuteEvents` directly on the hit UI GameObject. Supports trigger and bumper button bindings via OpenXR Input Actions. |

### Assets/Scripts/Debug

Development and debugging utilities.

| File | Description |
|------|-------------|
| `DebugVisualizer.cs` | Moves a GameObject to match a specific tracked joint position. Useful for visualizing individual landmarks as 3D objects in the scene. |
| `InteractableCube.cs` | XRI-powered test cube that responds to hover (color + scale change) and select (trigger click) events. Can invoke a UI Button on select. Used for validating XR interaction during development. |
| `InteractableRayCast.cs` | (`ML2PointerRayColor`) Manual pointer interaction for ML2. Uses `XRRayInteractor.TryGetCurrent3DRaycastHit` for hit detection consistent with the XRI line visualizer. Shows visual feedback (color/scale) on hover and trigger press. |
| `XRUICanvasFixer.cs` | Runtime fix that adds `TrackedDeviceGraphicRaycaster` to all World Space Canvases that lack one. Required because XRI 3.x `XRRayInteractor` only interacts with UI through `TrackedDeviceGraphicRaycaster`, not the standard `GraphicRaycaster`. Runs at `[DefaultExecutionOrder(-100)]` to execute before XRI initialization. |

### Assets/Scenes

| Scene | Description |
|-------|-------------|
| `DebugSceneLandmarkUI2.unity` | **Main working scene.** Contains: XR Rig, AR Camera, `PoseLandmarkerRunner` (GameObject "Solution"), `TrackingManager` + `SceneTrackerSetup`, humanoid avatar with `HybridAvatarIK`, UI Canvases (Menu, PatientInfo, BackButton), `XRUIClickBridge`, `XRUICanvasFixer`. Cleaned of debug MediaPipe Canvas overlays (M3). Ready for production. |
| `DebugSceneLandmarkUI.unity` | Earlier iteration of the debug scene. |
| `DebugSceneLandmark.unity` | Base landmark detection test scene. |
| `DebugSceneLandmarkUI2 Backup.unity` | Backup copy of the main scene. |

### Assets/Prefab

| Path | Description |
|------|-------------|
| `IntercatableCube.prefab` | Test interactable cube prefab. |
| `UI/Menu Canvas.prefab` | Main menu World Space Canvas with TrackedDeviceGraphicRaycaster. |
| `UI/PatientInfo Canvas.prefab` | Patient information World Space Canvas. |
| `UI/BackButton Canvas.prefab` | Back/navigation button Canvas. |
| `UI/ProblemItem.prefab` | Individual problem/diagnosis item for the patient info list. |
| `UI/UImanager.prefab` | UIManager prefab with references to all canvases. |

### Assets/Resources

Runtime-loaded resources including MediaPipe configuration prefabs and settings.

| Path | Description |
|------|-------------|
| `ARHealthBootstrap.prefab` | Bootstrap prefab for MediaPipe initialization (GPU, AssetLoader, ImageSourceProvider). |
| `ARHealCareAppSettings.asset` | Application settings ScriptableObject. |
| `ARH_Solution Menu.prefab` | MediaPipe solution selection menu. |
| `ARH_ImageSource Config Window.prefab` | Image source configuration window. |
| `ARH_Pose Landmark Detection Config Window.prefab` | Pose detection config window. |
| `User Camera.prefab` | User camera configuration prefab. |
| `UI/XRRayInteractor.preset` | XR Ray Interactor preset configuration. |

### Packages

Key dependencies (from `Packages/manifest.json`):

| Package | Version | Purpose |
|---------|---------|---------|
| `com.github.homuler.mediapipe` | 0.16.3 (local) | MediaPipe Unity Plugin — pose landmark detection, GPU management, task API |
| `com.magicleap.unitysdk` | local | Magic Leap 2 SDK — `MLCamera`, controller input, permissions |
| `com.unity.xr.arfoundation` | 6.3.3 | AR Foundation — cross-platform AR camera, tracking |
| `com.unity.xr.openxr` | 1.16.1 | OpenXR runtime — XR interaction, controller bindings |
| `com.unity.inputsystem` | 1.14.2 | New Input System — action-based input for ML2 controller |
| `com.unity.render-pipelines.universal` | 17.2.0 | URP rendering pipeline |
| `com.unity.animation.rigging` | 1.4.0 | Animation Rigging (available for future IK enhancements) |
| `com.magicleap.setuptool` | local | Magic Leap project setup tool |

---

**Important Note about AR Foundation**:
> While declared here and in root `README` as dependency, AR Foundation is **incompatible** with latest Magic Leap 2 packages and **was not used** actively in this project.
> We provided a bridge connection to Mediapipe for AR Foundantion compatible device as alternative module for portability.

### Docs

| File | Description |
|------|-------------|
| `AR HealthCare – Architecture & Design Overview.pdf` | Architecture and design document (PDF). |

---

## Pipeline Detail

### 1. Camera Acquisition

Two parallel paths are provided for camera input:

**AR Foundation path** (cross-platform):
- `ARFCameraBridge` subscribes to `ARCameraManager.frameReceived`
- Acquires CPU image via `XRCpuImage` (Sync or Async mode) with configurable downscaling
- Exposes the result as `CurrentCameraTexture` (a `Texture2D`)
- `ARFImageSource` wraps this texture for the MediaPipe `ImageSource` interface

**Magic Leap native path** (ML2 specific):
- `MLCameraBridge` connects to `MLCamera` API directly
- Handles Android camera permission flow
- Captures RGBA video frames via `OnRawVideoFrameAvailable`
- `MLCameraImageSource` wraps the capture texture for MediaPipe

### 2. MediaPipe Pose Detection

`PoseLandmarkerRunner` (modified from the MediaPipe Unity Plugin sample):
- Initialized by `Bootstrap` (GPU/asset/image source setup)
- Reads texture frames from `ImageSource` into a `TextureFramePool`
- Runs inference via MediaPipe `PoseLandmarker` task (BlazePose model)
- Produces two result sets per frame:
  - `poseLandmarks`: normalized 2D coordinates (0–1 image space)
  - `poseWorldLandmarks`: metric 3D coordinates (origin ≈ hips, gravity-aligned)
- Exposes `LatestResult` property and `ResultUpdated` event

### 3. Tracker Layer

Trackers implement `IBodyTracker` and convert raw MediaPipe results to `PatientTrackingData`:

**MediaPipeTracker (V1):**
1. Reads `poseWorldLandmarks[0].landmarks` (first person detected)
2. Computes hips center in MediaPipe space
3. For each landmark: subtracts hips center → relative offset
4. Applies axis inversion (negate Z for MediaPipe→Unity handedness conversion)
5. Rotates offsets by camera yaw only (prevents body orbiting with head tilt/roll)
6. Adds world anchor (lockable, with slow drift)
7. Estimates distance (camera→hips) and body height (torso span × 2)

**MediaPipeTrackerV2:**
1. XY from `poseLandmarks` (2D) → screen pixels
2. Z from `poseWorldLandmarks` (metric depth relative to hips)
3. `Camera.ScreenToWorldPoint(screenX, screenY, depth)` → world position
4. No axis inversion or manual anchoring needed — projective geometry handles it

**MockBodyTracker:**
- Generates realistic body proportions at configurable distance
- Optional arm swing animation
- Provides distance and height metadata
- Used for Editor testing without MediaPipe

### 4. Tracking Manager

`TrackingManager` is the central hub:
- Implements `ITrackingProvider` (the public API)
- `SetTracker(IBodyTracker)`: injects a tracker, calls `Initialize()`
- `Update()`: polls the active tracker, caches `PatientTrackingData`
- `GetPose()`: returns cached data to consumers (called from `OnAnimatorIK`, `LateUpdate`, etc.)
- `OnDestroy()`: calls `StopTracking()` on the active tracker

`SceneTrackerSetup` handles injection at scene startup:
```csharp
// In Awake():
if (useMockData)
    trackingManager.SetTracker(new MockBodyTracker());
else if (useV2Tracker)
    trackingManager.SetTracker(new MediaPipeTrackerV2(poseRunner));
else
    trackingManager.SetTracker(new MediaPipeTracker(poseRunner));
```

### 5. Avatar IK

`HybridAvatarIK` (the active system) reads `TrackingManager.GetPose()` and drives a humanoid avatar in two phases — see [Hybrid IK System](#hybrid-ik-system--technical-design).

---

## Hybrid IK System — Technical Design

`HybridAvatarIK` solves the fundamental limitations of both pure Unity IK and pure direct bone driving:

### The Problem

- **Pure IK (MedicalAvatarIK):** Unity's 2-bone solver positions limb endpoints correctly, but the elbow/knee angle is a *byproduct* of wrist/ankle distance from shoulder/hip. Near full arm extension, the solver hits a singularity — the elbow stays rigid even when the real elbow is bent.

- **Pure Direct Drive (IKSkeletonAnimator):** `SetBoneLocalRotation` in `OnAnimatorIK` reads `boneT.parent.rotation` which hasn't been updated after `animator.bodyRotation` changes. This causes cascading rotation errors, especially left/right asymmetry.

### The Solution: Two-Phase Hybrid

**Phase 1 — `OnAnimatorIK`** (same callback timing as MedicalAvatarIK):
1. **Body root**: Positions avatar hips at tracked hips center + vertical offset; orients avatar facing camera with anatomical up vector
2. **Dynamic scaling**: Scales avatar based on `EstimatedBodyHeight / referenceBodyHeight`
3. **IK goals**: Sets `AvatarIKGoal` positions for wrists (with 95% reach clamping to prevent singularity) and ankles
4. **IK hints**: Sets elbow and knee hint positions from tracked landmarks
5. **Head look**: `SetLookAtPosition` targeting the nose landmark

After Phase 1, Unity's built-in IK solver computes an approximate limb pose.

**Phase 2 — `LateUpdate`** (all transforms fully resolved post-IK):
1. For each limb segment (upper arm, lower arm, upper leg, lower leg — processed in order):
2. Read the **current** bone→child direction (post-IK, fully resolved)
3. Compute the **tracked** direction from landmark positions
4. Apply `FromToRotation` delta to align the bone with the tracking data
5. Blend with `correctionWeight` (0 = pure IK, 1 = full tracking correction, 0.5–0.8 recommended)
6. Smooth tracked directions with exponential moving average to reduce jitter

Because transforms are set directly via `boneT.rotation` (not via the Animator API), changes propagate **immediately** to child transforms within the same frame — no stale parent data.

### Key Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `correctionWeight` | 0.7 | How much Phase 2 overrides IK angles with tracked directions |
| `correctionSmoothing` | 0.3 | Smoothing for tracked direction changes (0 = snap, 0.9 = very smooth) |
| `verticalBodyOffset` | 0.3 | Compensates for avatar head being lower than real head |
| `movementDeadZone` | 0.02 | Minimum movement (meters) to filter micro-jitter |
| `useAdaptiveSmoothing` | true | Velocity-based smoothing (fast movement → less smoothing) |

---

## IK Approach Comparison

| System | File | Strengths | Weaknesses | Status |
|--------|------|-----------|------------|--------|
| **HybridAvatarIK** | `AnimatorIK/HybridAvatarIK.cs` | Best overall tracking, accurate elbow/knee bending, stable positioning, no stale transform issues | Knees don't fully bend when seated | **Active** |
| MedicalAvatarIK | `Core/MedicalAvatarIK.cs` | Stable positioning, proven robust | Rigid elbows/knees, 2-bone singularity | Reference |
| MedicalAvatarIK v2 | `AnimatorIK/MedicalAvatarIK_v2.cs` | Improved hints | Inconsistent improvements | Reference |
| IKSkeletonAnimator | `AnimatorIK/IKSkeletonAnimator.cs` | Better joint bending, foot grounding | Rotation errors, left/right asymmetry | Experimental |
| MediaPipeAvatarIK | `AnimatorIK/BaseIK/MediaPipeAvatarIK.cs` | Simple, event-driven | Requires external IK targets | Legacy |

---

## Data Model

### PatientTrackingData

```csharp
public struct PatientTrackingData
{
    public bool IsTracked;                           // Valid person detected
    public Dictionary<string, Pose> Joints;          // Joint name → world-space Pose
    public float EstimatedDistance;                   // Camera-to-hips distance (meters)
    public float EstimatedBodyHeight;                // Extrapolated body height (meters)
}
```

### Joint Keys (from LandMarkPoints)

Based on MediaPipe Pose 33-landmark model. Tracked subset used by IK:

| Index | Key | Description |
|-------|-----|-------------|
| 0 | `Nose` | Head target (look-at) |
| 11 | `LeftShoulder` | Left shoulder |
| 12 | `RightShoulder` | Right shoulder |
| 13 | `LeftElbow` | Left elbow (IK hint) |
| 14 | `RightElbow` | Right elbow (IK hint) |
| 15 | `LeftWrist` | Left hand (IK goal) |
| 16 | `RightWrist` | Right hand (IK goal) |
| 23 | `LeftHip` | Left hip |
| 24 | `RightHip` | Right hip |
| 25 | `LeftKnee` | Left knee (IK hint) |
| 26 | `RightKnee` | Right knee (IK hint) |
| 27 | `LeftAnkle` | Left foot (IK goal) |
| 28 | `RightAnkle` | Right foot (IK goal) |

---

## UI System

The application UI is built with World Space Canvases for AR compatibility:

### Flow
```
Main Menu  ──[Start AR]──►  AR Tracking Environment  ──[Back]──►  Main Menu
    │                              │
    └──[Patient Info]──►  Patient Info Panel  ──[Back]──►  Main Menu
```

- `UIManager` orchestrates all transitions with fade animations
- `UITracking` activates/deactivates tracking GameObjects on demand
- `PatientInfoFollowBody` makes the info panel follow the avatar's body position
- Unimplemented features show a "under development" toast via `ToDoInfoPrint`

### Canvas Configuration for XR

All World Space Canvases require `TrackedDeviceGraphicRaycaster` (not `GraphicRaycaster`) for XRI 3.x ray interaction. `XRUICanvasFixer` adds this component automatically at runtime as a safety net.

---

## XR Interaction (Magic Leap 2)

| Component | Purpose |
|-----------|---------|
| `XRRayInteractor` | Ray-based interaction from controller |
| `XRUIInputModule` | XR input processing (InputSystem mode with XR input enabled) |
| `TrackedDeviceGraphicRaycaster` | Required on each World Space Canvas for ray-UI interaction |
| `XRUIClickBridge` | Workaround for ML2/OpenXR: directly fires `ExecuteEvents` on UI hit, bypassing broken `UIPressInput` propagation |
| `InteractableCube` / `ML2PointerRayColor` | Debug scripts for testing 3D interaction with the controller |

**Controller Bindings (OpenXR):**
- Trigger: `<MagicLeapController>{RightHand}/triggerPressed`
- Bumper: `<MagicLeapController>{RightHand}/gripPressed`

---

## Requirements

- **Unity** 6.2+ (designed for 6.3 LTS)
- **AR Foundation** 6.3.3
- **MediaPipe Unity Plugin** 0.16.3 (local package)
- **Magic Leap Unity SDK** (local package)
- **OpenXR** 1.16.1
- **Input System** 1.14.2
- **Universal Render Pipeline** 17.2.0
- **Target Platform:** Android (ARM64) — Magic Leap 2, with support for other OpenXR headsets

---

## Scene Setup Guide

### Quick Setup (using existing scene)

1. Open `DebugSceneLandmarkUI2.unity`
2. Scene is pre-configured with all required components
3. Set `SceneTrackerSetup.useMockData = true` for Editor testing
4. Press Play

### Manual Setup (new scene)

1. **XR Rig:** Add an `XR Origin` with AR Camera containing `ARCameraManager` + `ARCameraBackground`

2. **Camera Bridge:** Add `ARFCameraBridge` to the AR Camera GameObject
   - Assign `ARCameraManager` reference
   - Set `cpuDownscale = 2`, `cpuMode = Async` for optimal performance

3. **MediaPipe:** Add a GameObject "Solution" with:
   - `ARFImageSource` (assign the camera bridge)
   - `PoseLandmarkerRunner`

4. **Tracking System:** Add an empty GameObject with:
   - `TrackingManager`
   - `SceneTrackerSetup` (assign `TrackingManager` and `PoseLandmarkerRunner`)

5. **Avatar:** Add a Humanoid model with:
   - `Animator` (Humanoid rig, `AlwaysAnimate` culling mode)
   - `HybridAvatarIK` (auto-finds `TrackingManager` if not assigned)

6. **UI (optional):** Add World Space Canvases with `TrackedDeviceGraphicRaycaster`
   - Add `XRUICanvasFixer` to the EventSystem as fallback

7. **Permissions:** Add `AndroidPermissionRequest` to any active GameObject

---

## Running in Editor (Mock Mode)

1. Open `DebugSceneLandmarkUI2.unity`
2. In `SceneTrackerSetup`, enable `useMockData`
3. Press Play
4. The avatar will animate with fake body proportions and arm swing
5. Enable Gizmos in Scene view to see:
   - 🔴 Red spheres: hips center
   - 🟡 Yellow spheres: anchor target
   - 🔵 Cyan spheres: key joint landmarks
   - 🟢 Green lines: tracked limb segments

### Testing Custom Distances

```csharp
// Via script:
var mgr = FindFirstObjectByType<TrackingManager>();
mgr.SetTracker(new MockBodyTracker(1.0f));  // 1 meter
mgr.SetTracker(new MockBodyTracker(2.0f));  // 2 meters (default)
mgr.SetTracker(new MockBodyTracker(3.0f));  // 3 meters
```

---

## Running on Device (MediaPipe Mode)

1. In `SceneTrackerSetup`, disable `useMockData`
2. Choose tracker version:
   - `useV2Tracker = false` → MediaPipeTracker V1 (world coordinates)
   - `useV2Tracker = true` → MediaPipeTrackerV2 (screen-space projection)
3. Build for Android (ARM64)
4. Deploy to Magic Leap 2
5. Grant camera permission when prompted
6. The system starts automatically:
   - `Bootstrap` initializes GPU and MediaPipe assets
   - `PoseLandmarkerRunner` starts the detection loop
   - `SceneTrackerSetup` injects the MediaPipe tracker
   - Avatar begins tracking the patient

### Configuration Tips for Device

- **Avatar too small/large:** Adjust `referenceBodyHeight` (default 1.7m) or use `manualAvatarScale` with `useDynamicScaling = false`
- **Avatar offset vertically:** Adjust `verticalBodyOffset` (positive = up)
- **Head appears too low:** Increase `verticalBodyOffset` (e.g., 0.3–0.5)
- **Body orbits with head rotation:** Ensure using MediaPipeTracker V1 (yaw-only alignment)
- **Jittery movements:** Increase `movementDeadZone` or reduce `positionLerpSpeed`

---

## Configuration Reference

### HybridAvatarIK Inspector Parameters

| Section | Parameter | Range | Default | Purpose |
|---------|-----------|-------|---------|---------|
| **IK Weights** | `globalWeight` | 0–1 | 1.0 | Master weight for all IK |
| | `bodyWeight` | 0–1 | 1.0 | Body root influence |
| | `headWeight` | 0–1 | 0.8 | Head look-at weight |
| | `handsWeight` | 0–1 | 1.0 | Hand IK goal weight |
| | `feetWeight` | 0–1 | 0.8 | Foot IK goal weight |
| **Smoothing** | `positionLerpSpeed` | — | 12 | Position interpolation speed |
| | `rotationSlerpSpeed` | — | 12 | Rotation interpolation speed |
| | `movementDeadZone` | — | 0.02 | Jitter filter threshold (meters) |
| | `useAdaptiveSmoothing` | — | true | Velocity-based smoothing toggle |
| | `maxVelocity` | — | 2.0 | Max velocity for adaptive normalization |
| **Spatial Anchor** | `useAnchor` | — | true | Enable hips-based anchor |
| | `anchorOffset` | — | (0,0,0) | Offset from hips to avatar root |
| | `verticalBodyOffset` | — | 0.3 | Vertical compensation |
| **Dynamic Scaling** | `useDynamicScaling` | — | true | Auto-scale from estimated height |
| | `referenceBodyHeight` | — | 1.7 | Reference height for scaling (meters) |
| | `manualAvatarScale` | 0.5–2.5 | 1.0 | Manual scale (when dynamic is off) |
| **IK Hints** | `elbowHintWeight` | 0–1 | 1.0 | Elbow hint influence |
| | `kneeHintWeight` | 0–1 | 0.5 | Knee hint influence |
| **Bone Correction** | `correctionWeight` | 0–1 | 0.7 | Phase 2 correction amount |
| | `correctionSmoothing` | 0–0.95 | 0.3 | Direction smoothing |
| **Debug** | `logOnce` | — | true | Log first tracking activation |
| | `drawGizmos` | — | true | Enable debug gizmos/GL overlay |

### MediaPipeTracker Parameters

| Parameter | Default | Purpose |
|-----------|---------|---------|
| `invertX` | true | Negate X axis in conversion |
| `invertY` | false | Negate Y axis |
| `invertZ` | true | Negate Z axis (MediaPipe→Unity handedness) |
| `scale` | 1.0 | Coordinate scale (1 = meters) |
| `manualOffset` | (0, -0.5, 2) | (lateral, height below eyes, forward distance) |
| `offsetFromCamera` | true | Offset relative to camera position |
| `lockAnchorPosition` | true | Freeze anchor at first detection |
| `anchorUpdateSpeed` | 0.3 | How fast locked anchor drifts (m/s) |

### PoseLandmarkDetectionConfig

| Parameter | Default | Purpose |
|-----------|---------|---------|
| `Delegate` | CPU (editor) / GPU (device) | Inference backend |
| `Model` | BlazePoseFull | Pose model (Lite/Full/Heavy) |
| `RunningMode` | LIVE_STREAM | Processing mode |
| `NumPoses` | 1 | Max persons to detect |
| `MinPoseDetectionConfidence` | 0.7 | Initial detection threshold |
| `MinPosePresenceConfidence` | 0.6 | Pose validity threshold |
| `MinTrackingConfidence` | 0.6 | Temporal tracking threshold |

---

## Extending the System

### Adding a New Tracking Backend

1. Create a class implementing `IBodyTracker`:
   ```csharp
   public class KinectTracker : IBodyTracker
   {
       public void Initialize() { /* setup */ }
       public PatientTrackingData GetTrackingData() { /* convert data */ }
       public void StopTracking() { /* cleanup */ }
   }
   ```
2. Populate `PatientTrackingData.Joints` using keys from `LandMarkPoints`
3. Inject via `TrackingManager.SetTracker(new KinectTracker())` or extend `SceneTrackerSetup`

No changes needed in `HybridAvatarIK`, `TrackingManager`, or the avatar rig.

### Adding a New Camera Device

If AR Foundation–compatible: no changes needed.

If custom camera:
1. Create `MyDeviceBridge` exposing `CurrentCameraTexture`
2. Create `MyDeviceImageSource` implementing the `ImageSource` base class
3. Register in `ImageSourceProvider`

### Extending Joint Mapping

1. Add entries to the tracker's mapping (e.g., `MediaPipeTracker._landmarkMap`)
2. Optionally update `LandMarkPoints.Points` for new joint names
3. Update `HybridAvatarIK.LimbSegments` if new limb segments need Phase 2 correction

---

## Performance Notes

Performance targets (Magic Leap 2):

| Component | Budget |
|-----------|--------|
| MediaPipe inference | < 15ms |
| `MediaPipeTracker.GetTrackingData` | < 1ms |
| `HybridAvatarIK.OnAnimatorIK` | < 2ms |
| Smoothing overhead | < 0.5ms |
| **Frame target** | **30 fps** |

### Optimizations Implemented (Milestone 4)

- **Camera caching**: `Camera.main` cached at initialization (avoids expensive per-frame lookup)
- **Dictionary pre-allocation**: `_jointsCache` created with capacity 33 (zero GC allocations per frame)
- **Static landmark map**: `_landmarkMap` built once at class load (no LINQ/allocations per frame)
- **sqrMagnitude**: Used instead of `magnitude` where possible (~30% faster)
- **Profiling markers**: `Profiler.BeginSample/EndSample` blocks for Unity Profiler analysis
- **Dead-zone filtering**: Skips position updates below threshold (reduces jitter AND computation)
- **Velocity-based smoothing**: Fast movements get less smoothing (responsive), slow movements get more (stable)
- **NativeArray reuse**: `ARFCameraBridge` reuses persistent buffer instead of allocating per frame

---

## Known Limitations

1. **Knee bending (seated):** When the patient is seated, knee bending detection is limited — the 2-bone solver needs a larger distance change between hip and ankle to trigger significant knee bend
2. **Joint key typing:** Joint names are string-based (not an enum) — name mismatches fail silently
3. **Single person:** Only the first detected person is tracked (`poseWorldLandmarks[0]`)
4. **No hand/finger tracking:** MediaPipe Pose provides wrist positions but not individual fingers
5. **GPU readback:** `ARFCameraBridge` GPU path is detected but not fully implemented yet
6. **Thread safety:** `MLCameraBridge` frame callback may come from non-main thread; needs marshaling for production
7. **Coordinate differences:** V1 and V2 trackers produce slightly different world positions; V1 is the primary tested path

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Avatar not moving | Check `SceneTrackerSetup.useMockData` is enabled for Editor. Verify `HybridAvatarIK` has an `ITrackingProvider` assigned. |
| Avatar positioned wrong | Adjust `verticalBodyOffset`, `anchorOffset`, or `manualOffset` on the tracker. |
| Avatar too large/small | Adjust `referenceBodyHeight` or use `manualAvatarScale` with `useDynamicScaling = false`. |
| Jittery movements | Increase `movementDeadZone` (0.02–0.05). Ensure `useAdaptiveSmoothing = true`. |
| Elbows too rigid | Increase `correctionWeight` (0.7–0.9). Check that elbow landmarks are tracked. |
| No MediaPipe output | Verify `PoseLandmarkerRunner` is on an active GameObject with `isRunning = true`. Check Bootstrap initialized correctly. |
| UI not responding to ray | Ensure Canvas has `TrackedDeviceGraphicRaycaster`. Verify `XRUIInputModule` is active and `XRUIClickBridge` is in scene. |
| Camera feed is black | Check `ARFCameraBridge` has `ARCameraManager` assigned. Verify camera permission is granted. |
| Native crash on device | Do NOT call `PoseLandmarkerRunner.Play()` manually — it's managed by Bootstrap lifecycle. |
| Body orbits with head | Use MediaPipeTracker V1 (yaw-only alignment). Check `lockAnchorPosition = true`. |
| Performance drops | Switch to `BlazePoseLite` model, increase `cpuDownscale`, use `Async` CPU mode. |

---

## Milestones

| Milestone | Description | Status |
|-----------|-------------|--------|
| **M1** | Base tracking: MediaPipe → Tracker → IK pipeline | ✅ Complete |
| **M2** | Dynamic spatial anchoring: distance & height estimation, auto-scaling | ✅ Complete |
| **M3** | Scene cleanup: removed debug MediaPipe Canvases, standalone runner | ✅ Complete |
| **M4** | Performance & smoothing: Camera caching, zero-GC, dead-zone, adaptive smoothing, tracking loss handling | ✅ Complete |
| **M5** | Device validation: build & test on Magic Leap 2, end-to-end validation | 🔄 In progress |

---

*Last updated: March 2026*
