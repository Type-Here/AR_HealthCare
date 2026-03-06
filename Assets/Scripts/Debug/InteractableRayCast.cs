using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Manual pointer interactable for Magic Leap 2 — OpenXR only (XRI 3.3+, no MLSDK).
/// Uses XRRayInteractor.TryGetCurrent3DRaycastHit instead of a manual Physics.Raycast,
/// so hit detection is always consistent with the line visualizer and XRI hover events.
/// Trigger and bumper button bindings are kept for visual feedback only.
/// For production prefer XRRayInteractor + IXRHoverInteractable / IXRSelectInteractable from XRI 3.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ML2PointerRayColor : MonoBehaviour
{
    [Header("XRI Ray Interactor Reference")]
    [Tooltip("Assign the XRRayInteractor from the scene. Auto-found in Awake if left empty.")]
    [SerializeField] private XRRayInteractor rayInteractor;

    [Header("Magic Leap 2 — Button Actions (XRI 3.3+, no MLSDK)")]
    // triggerPressed = correct OpenXR control name for ML2 trigger button
    [SerializeField] private InputAction triggerPressed =
        new InputAction(type: InputActionType.Button,
            binding: "<MagicLeapController>{RightHand}/triggerPressed",
            expectedControlType: "Button");

    // gripPressed = correct OpenXR control name for ML2 bumper button
    [SerializeField] private InputAction bumperPressed =
        new InputAction(type: InputActionType.Button,
            binding: "<MagicLeapController>{RightHand}/gripPressed",
            expectedControlType: "Button");

    private Renderer _r;
    private Color _originalColor;
    private Vector3 _originalScale;
    private bool _isHovered;

    private void Awake()
    {
        _r = GetComponent<Renderer>();
        if (_r == null) Debug.LogWarning("[ML2PointerRayColor] No Renderer: colour feedback disabled.");
        else _originalColor = _r.material.color;
        _originalScale = transform.localScale;

        if (rayInteractor == null)
        {
            rayInteractor = Object.FindFirstObjectByType<XRRayInteractor>();
            if (rayInteractor == null)
                Debug.LogWarning("[ML2PointerRayColor] No XRRayInteractor found — assign it in the Inspector.");
        }
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

        // Use the XRRayInteractor's own 3-D hit result — same ray that drives the line visualizer.
        bool hitCube = false;
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                hitCube = true;
                Vector3 p = hit.point;
                Debug.Log($"[ML2PointerRayColor] Hit: '{hit.collider.name}' at ({p.x:F3}, {p.y:F3}, {p.z:F3})");
            }
        }

        if (hitCube)
        {
            // Hover enter
            if (!_isHovered)
            {
                Debug.Log("[ML2PointerRayColor] Hover enter");
                _isHovered = true;
                if (_r) _r.material.color = Color.gray;
                transform.localScale = _originalScale * 1.2f;
            }

            if (triggerPressed.WasPressedThisFrame())
            {
                if (_r) _r.material.color = Color.red;
                Debug.Log("[ML2PointerRayColor] Trigger → Red");
            }
            else if (bumperPressed.WasPressedThisFrame())
            {
                if (_r) _r.material.color = Color.blue;
                Debug.Log("[ML2PointerRayColor] Bumper → Blue");
            }
        }
        else if (_isHovered)
        {
            // Hover exit
            Debug.Log("[ML2PointerRayColor] Hover exit");
            _isHovered = false;
            if (_r) _r.material.color = _originalColor;
            transform.localScale = _originalScale;
        }
    }
}