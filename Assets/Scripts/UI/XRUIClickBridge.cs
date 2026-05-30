using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Bypass the click/drag pipeline of XRUIInputModule for Magic Leap 2 — OpenXR (XRI 3.3+).
/// 
/// Click: all events (PointerDown → PointerClick → PointerUp) are fired on the frame 
///        where WasPressedThisFrame is true — identical to the original implementation, tested on ML2.
/// 
/// Drag: during the frame after the press, if the trigger remains pressed (IsPressed) 
///       and the ray moves beyond dragThresholdPixels, BeginDrag/Drag/EndDrag are fired. 
///       The drag does NOT cancel the already occurred click — ScrollRect does not implement 
///       IPointerClickHandler so it is not disturbed, while child Buttons of the scroll receive 
///       the click normally.
/// </summary>
public class XRUIClickBridge : MonoBehaviour
{
    [Header("XRI Ray Interactor")]
    [SerializeField] private XRRayInteractor rayInteractor;

    [Header("Magic Leap 2 — Button Actions (OpenXR)")]
    [SerializeField] private InputAction triggerPressed =
        new(type: InputActionType.Button,
            binding: "<MagicLeapController>{RightHand}/triggerPressed",
            expectedControlType: "Button");

    [SerializeField] private InputAction bumperPressed =
        new(type: InputActionType.Button,
            binding: "<MagicLeapController>{RightHand}/gripPressed",
            expectedControlType: "Button");

    [Header("Options")]
    [SerializeField] private bool useBumperToo = false;

    [Tooltip("Distanza minima (pixel schermo) prima che un hold avvii il drag.")]
    [SerializeField] private float dragThresholdPixels = 8f;

    [Tooltip("Moltiplicatore sul delta per rendere lo scroll più sensibile.")]
    [SerializeField] private float dragScale = 3f;

    [SerializeField] private bool verbose = true;

    private PointerEventData _pointerData;

    // Drag state
    private GameObject _dragTarget;
    private Vector2 _dragOriginScreenPos;
    private bool _dragStarted;
    private bool _prevHeld;   // manual release detection — more reliable than WasReleasedThisFrame on ML2

    private void Awake()
    {
        if (rayInteractor == null)
        {
            rayInteractor = FindFirstObjectByType<XRRayInteractor>();
            if (rayInteractor == null)
                Debug.LogWarning("[XRUIClickBridge] Nessun XRRayInteractor trovato — assegnalo dall'Inspector.");
        }

        _pointerData = new PointerEventData(EventSystem.current);
    }

    private void OnEnable()
    {
        triggerPressed.Enable();
        bumperPressed.Enable();
    }

    private void OnDisable()
    {
        triggerPressed.Disable();
        bumperPressed.Disable();
    }

    private void Update()
    {
        if (rayInteractor == null) return;

        bool pressedThisFrame = triggerPressed.WasPressedThisFrame() || (useBumperToo && bumperPressed.WasPressedThisFrame());
        bool heldNow          = triggerPressed.IsPressed()           || (useBumperToo && bumperPressed.IsPressed());
        bool releasedThisFrame = _prevHeld && !heldNow;
        _prevHeld = heldNow;

        // ── Press: fire full click sequence (proven working on ML2) ──────────
        if (pressedThisFrame)
        {
            if (!rayInteractor.TryGetCurrentUIRaycastResult(out RaycastResult hit))
            {
                if (verbose) Debug.Log("[XRUIClickBridge] Press: nessun hit UI.");
                return;
            }

            GameObject target = hit.gameObject;
            if (target == null) return;

            if (verbose) Debug.Log($"[XRUIClickBridge] Click → '{target.name}'");

            _pointerData.position              = hit.screenPosition;
            _pointerData.pointerCurrentRaycast = hit;
            _pointerData.pointerPressRaycast   = hit;

            GameObject pressed = ExecuteEvents.ExecuteHierarchy(target, _pointerData, ExecuteEvents.pointerDownHandler);
            _pointerData.pointerPress = pressed;
            ExecuteEvents.ExecuteHierarchy(target, _pointerData, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.ExecuteHierarchy(target, _pointerData, ExecuteEvents.pointerUpHandler);
            _pointerData.pointerPress = null;

            // Arm drag tracking for subsequent held frames
            _dragTarget         = target;
            _dragOriginScreenPos = hit.screenPosition;
            _dragStarted        = false;
        }

        // ── Held (not the press frame): drag detection ────────────────────────
        if (heldNow && !pressedThisFrame && _dragTarget != null)
        {
            if (rayInteractor.TryGetCurrentUIRaycastResult(out RaycastResult hit))
            {
                Vector2 rawDelta = hit.screenPosition - _dragOriginScreenPos;

                if (!_dragStarted && rawDelta.magnitude >= dragThresholdPixels)
                {
                    _pointerData.delta    = rawDelta * dragScale;
                    _pointerData.position = hit.screenPosition;
                    ExecuteEvents.ExecuteHierarchy(_dragTarget, _pointerData, ExecuteEvents.beginDragHandler);
                    _dragStarted = true;
                    if (verbose) Debug.Log($"[XRUIClickBridge] BeginDrag → '{_dragTarget.name}'");
                }

                if (_dragStarted)
                {
                    Vector2 frameDelta = (hit.screenPosition - _dragOriginScreenPos) * dragScale;
                    _pointerData.delta    = frameDelta;
                    _pointerData.position = hit.screenPosition;
                    ExecuteEvents.ExecuteHierarchy(_dragTarget, _pointerData, ExecuteEvents.dragHandler);
                    _dragOriginScreenPos = hit.screenPosition;
                }
            }
        }

        // ── Release ───────────────────────────────────────────────────────────
        if (releasedThisFrame && _dragTarget != null)
        {
            if (_dragStarted)
            {
                _pointerData.delta = Vector2.zero;
                ExecuteEvents.ExecuteHierarchy(_dragTarget, _pointerData, ExecuteEvents.endDragHandler);
                if (verbose) Debug.Log($"[XRUIClickBridge] EndDrag → '{_dragTarget.name}'");
            }
            _dragTarget  = null;
            _dragStarted = false;
        }
    }
}
