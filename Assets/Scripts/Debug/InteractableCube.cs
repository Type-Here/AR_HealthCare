using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Reacts to XRI hover / select events from the XRSimpleInteractable on the same GameObject.
/// No manual raycast — delegates all hit detection to the XRRayInteractor pipeline,
/// which is already working (line visualizer, hover/exit blue cube, etc.).
/// </summary>
public class InteractableCube : MonoBehaviour
{
    [Header("Menu Interact")]
    public Button startButton;

    private Renderer _r;
    private Color _originalColor;
    private Vector3 _originalScale;
    private XRSimpleInteractable _interactable;

    private void Awake()
    {
        _r = GetComponent<Renderer>();
        if (_r != null) _originalColor = _r.material.color;
        _originalScale = transform.localScale;

        _interactable = GetComponent<XRSimpleInteractable>();
        if (_interactable == null)
            Debug.LogWarning("[InteractableCube] XRSimpleInteractable not found. Hover/select events won't fire.");
    }

    private void OnEnable()
    {
        if (_interactable == null) return;
        _interactable.hoverEntered.AddListener(OnHoverEnter);
        _interactable.hoverExited.AddListener(OnHoverExit);
        _interactable.selectEntered.AddListener(OnSelectEnter);
    }

    private void OnDisable()
    {
        if (_interactable == null) return;
        _interactable.hoverEntered.RemoveListener(OnHoverEnter);
        _interactable.hoverExited.RemoveListener(OnHoverExit);
        _interactable.selectEntered.RemoveListener(OnSelectEnter);
    }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        Debug.Log("[InteractableCube] Hover Enter");
        if (_r) _r.material.color = Color.gray;
        transform.localScale = _originalScale * 1.2f;
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        Debug.Log("[InteractableCube] Hover Exit");
        if (_r) _r.material.color = _originalColor;
        transform.localScale = _originalScale;
    }

    private void OnSelectEnter(SelectEnterEventArgs args)
    {
        Debug.Log("[InteractableCube] Selected (trigger) → Hit!");
        Hit();
        if (startButton != null)
            startButton.onClick.Invoke();
    }

    private void Hit()
    {
        if (_r) _r.material.color = Color.red;
        Debug.Log("[InteractableCube] Cube HIT");
    }
}
