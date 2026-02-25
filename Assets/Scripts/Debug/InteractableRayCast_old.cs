using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class ML2PointerRayColor_old : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private LayerMask layerMask = ~0; // tutto

    [Header("Magic Leap 2 - Input Actions")]
    [SerializeField] private InputAction pointerPos =
        new InputAction(type: InputActionType.PassThrough, binding: "<MagicLeapController>/pointer/position", expectedControlType: "Vector3");

    [SerializeField] private InputAction pointerRot =
        new InputAction(type: InputActionType.PassThrough, binding: "<MagicLeapController>/pointer/rotation", expectedControlType: "Quaternion");

    [SerializeField] private InputAction triggerPressed =
        new InputAction(type: InputActionType.Button, binding: "<MagicLeapController>/triggerPressed", expectedControlType: "Button");

    [SerializeField] private InputAction bumperPressed =
        new InputAction(type: InputActionType.Button, binding: "<MagicLeapController>/gripPressed", expectedControlType: "Button");

    private Renderer _r;

    private void Awake()
    {
        _r = GetComponent<Renderer>();
        if (_r == null) Debug.LogWarning("Nessun Renderer sul cubo: non posso cambiare colore.");
    }

    private void OnEnable()
    {
        pointerPos.Enable();
        pointerRot.Enable();
        triggerPressed.Enable();
        bumperPressed.Enable();
    }

    private void OnDisable()
    {
        pointerPos.Disable();
        pointerRot.Disable();
        triggerPressed.Disable();
        bumperPressed.Disable();
    }

    private void Update()
    {
        Vector3 origin = pointerPos.ReadValue<Vector3>();
        Quaternion rot = pointerRot.ReadValue<Quaternion>();
        Vector3 dir = rot * Vector3.forward;

        // Debug ray visivo (Scene view)
        Debug.DrawRay(origin, dir * maxDistance, Color.green);

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
        {
            // Solo se sto colpendo QUESTO cubo
            if (hit.collider.gameObject == gameObject)
            {
                if (triggerPressed.WasPressedThisFrame())
                {
                    if (_r) _r.material.color = Color.red;
                    Debug.Log("Hit cubo + Trigger -> Rosso");
                }
                else if (bumperPressed.WasPressedThisFrame())
                {
                    if (_r) _r.material.color = Color.blue;
                    Debug.Log("Hit cubo + Bumper -> Blu");
                }
            }
            else
            {
                Debug.Log("Raycast hit: " + hit.collider.name);
            }
        }
        else
        {
            //Debug.Log("Raycast: niente hit");
        }
    }
}