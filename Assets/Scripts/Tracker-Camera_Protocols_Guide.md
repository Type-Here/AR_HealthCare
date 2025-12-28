# ARHealthCare – Body Tracking & Medical Overlay Pipeline

This README provides a comprehensive, yet to be updated, guide for integrating AR Foundation, MediaPipe, and a humanoid medical avatar using the ARHealthCare architecture.

It describes:

* How to set up the scene
* Which scripts to attach and where
* How the internal pipeline works (bridge → image source → runner → tracker → IK)
* How to add support for new devices or tracking systems
* Data models and interfaces

---

## 1. Requirements

* **Unity** 6.2+ (designed for 6.3 LTS)
* **AR Foundation** 6.2.0
* **MediaPipe Unity Plugin** 0.16.3
* **Target Platforms**: AR headsets (Magic Leap 2, Quest, HoloLens, etc.) using AR Foundation / OpenXR

Project folders used:

* `ARHealthCare/Core`
* `ARHealthCare/DataClasses`
* `ARHealthCare/Input`
* `ARHealthCare/Trackers`
* Modified MediaPipe runner under `Mediapipe.Unity.ModifiedSample.PoseLandmarkDetection`.

---

## 2. High-Level Architecture

```
AR Foundation Camera
        ↓
  ARFCameraBridge (feeds Texture)
        ↓
   ARFImageSource (MediaPipe ImageSource)
        ↓
PoseLandmarkerRunner (Modified)
        ↓
   IBodyTracker (MediaPipeTracker / Mock)
        ↓
   TrackingManager (central API)
        ↓
MedicalAvatarIK (Avatar overlay)
```

### Summary

* **ARFCameraBridge** translates AR Foundation camera frames into a Unity `Texture2D` (CPU sync/async or GPU).
* **ARFImageSource** exposes that texture to MediaPipe as an `ImageSource`.
* **PoseLandmarkerRunner** performs pose inference and updates `LatestResult`.
* **MediaPipeTracker** converts MediaPipe results into `PatientTrackingData`.
* **TrackingManager** acts as a service provider for pose data.
* **MedicalAvatarIK** applies tracking to a humanoid avatar.

---

## 3. Scene Setup

### 3.1 Setup AR Camera

1. In your AR Camera object ensure:

   * `Camera`
   * `ARCameraManager`
   * `ARCameraBackground`

2. Add **ARFCameraBridge** (either on the AR Camera or a separate GameObject).

#### ARFCameraBridge Inspector Settings:

* **Camera Manager** → drag your `ARCameraManager`.
* **cpuDownscale** → integer scale factor for reducing CPU texture resolution.
* **preferGpu** → enable only if you complete GPU integration.
* **cpuMode** → `Sync` or `Async`.

Output:

* **CurrentCameraTexture**: updated every frame.

---

### 3.2 MediaPipe Integration

1. Place the modified **PoseLandmarkerRunner** prefab/object in the scene.

   * It must expose:

     ```csharp
     public PoseLandmarkerResult LatestResult { get; private set; }
     ```
   * Updated inside `OnPoseLandmarkDetectionOutput`.

2. Add **ARFImageSource** to a GameObject (e.g., `MediaPipeBootstrap`).

#### ARFImageSource Inspector Settings:

* **AR Bridge** → drag `ARFCameraBridge`.
* **AR Camera Manager** → drag `ARCameraManager`.

3. In the MediaPipe `ImageSourceProvider`, set **ARFImageSource** as the default ImageSource.

---

### 3.3 TrackingManager & Dependency Injection

Create an empty GameObject: `TrackingSystem`.

Add components:

* `TrackingManager`
* `SceneTrackerSetup`

#### SceneTrackerSetup Inspector

* **trackingManager** → assign the `TrackingManager`.
* **useMockData**:

  * `true` → uses `MockBodyTracker` (no MediaPipe required).
  * `false` → uses full MediaPipeTracking.
* **poseRunner** → drag the `PoseLandmarkerRunner`.

The setup script injects the correct tracker into `TrackingManager`.

---

### 3.4 Medical Avatar & IK

On your humanoid avatar GameObject add:

* `Animator` (Humanoid rig)
* `MedicalAvatarIK`

`MedicalAvatarIK`:

* Retrieves pose data via:

  ```csharp
  TrackingManager.Instance.GetPose();
  ```
* Applies IK to arms (Right/Left Hand) and directly manipulates the head bone.
* Uses:

  * `"RightHand"`
  * `"LeftHand"`
  * `"Head"`

> Note: MediaPipeTracker does not yet output these keys; they are currently provided by the Mock tracker. Extend MediaPipeTracker when needed.

---

### 3.5 Android Permissions

Add `AndroidPermissionRequest` on any active scene object.
This requests `Camera` permission at runtime on Android/ML2.

---

## 4. Data Model

### 4.1 PatientTrackingData

```csharp
public struct PatientTrackingData
{
    public bool IsTracked;
    public Dictionary<string, Pose> Joints;
}
```

* `IsTracked` indicates whether a valid person is detected.
* `Joints` maps joint names to Unity `Pose` (position + rotation).

**Important:** The struct’s default constructor does not initialize `Joints`. Trackers must initialize it.

---

### 4.2 IBodyTracker Interface

```csharp
public interface IBodyTracker
{
    void Initialize();
    PatientTrackingData GetTrackingData();
    void StopTracking();
}
```

Defines a portable tracking backend.

Existing implementations:

* **MockBodyTracker** — generates fake data (good for testing).
* **MediaPipeTracker** — consumes PoseLandmarkerRunner output.

---

## 5. MediaPipeTracker Mapping

* Reads `PoseLandmarkerRunner.LatestResult`.
* Uses world landmarks (`poseWorldLandmarks`).
* Currently maps a small subset:

  * Nose (0)
  * Shoulders (11,12)
  * Elbows (13,14)
  * Wrists (15,16)
* Keys are normalized (spaces removed): `LeftShoulder`, `RightShoulder`, etc.

> Extend the mapping to output `Head`, `RightHand`, `LeftHand` when integrating with your IK.

---

## 6. Adding a New Tracking Backend

To support another device’s body tracking (Kinect, OpenXR body tracking, proprietary SDK, etc.):

1. Create a new class implementing `IBodyTracker`.

2. Populate `PatientTrackingData.Joints` with your device’s joint positions.

3. In `SceneTrackerSetup`, add an enum entry and inject the new tracker.

No changes required in:

* MedicalAvatarIK
* TrackingManager
* Avatar rig

---

## 7. Adding a New Camera Device

If the device is AR Foundation–compatible → nothing special required.

If the device has a custom camera:

1. Create `MyDeviceCameraBridge` exposing `CurrentCameraTexture`.
2. Duplicate/adapt `ARFImageSource` into `MyDeviceImageSource`.
3. Register it in the MediaPipe `ImageSourceProvider`.

---

## 8. TODO / Future Improvements

* Extend MediaPipeTracker joint mapping to match IK needs.
* Improve `ConvertLandmark` coordinate handling.
* Allow inspector-based selection of axis inversion/scaling.
* Optional replacement of TrackingManager singleton with DI.
* GPU pipeline: implement real GPU path (GpuBuffer → MediaPipe input).

---

## 9. Troubleshooting

* **Avatar not moving** → Ensure `useMockData = true` and IK keys exist.
* **PoseLandmarkerRunner has no input** → Check `ARFImageSource` is the active ImageSource.
* **Texture is black** → Verify ARFCameraBridge has access to the ARCameraManager.
* **Latency/spikes** → Switch CPU mode to `Async` in ARFCameraBridge.

---

This README describes a complete working pipeline and extensible architecture for AR-based medical overlays with MediaPipe and Unity.
