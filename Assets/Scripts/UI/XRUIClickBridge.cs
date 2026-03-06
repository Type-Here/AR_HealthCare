using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Bypass del pipeline click interno di XRUIInputModule per Magic Leap 2 — OpenXR (XRI 3.3+).
/// Usa XRRayInteractor.TryGetCurrentUIRaycastResult (stesso interactor che guida il line visualizer)
/// e spara ExecuteEvents direttamente sul GameObject UI colpito, scavalcando il canale
/// XRUIInputModule → UIPressInput che non propaga correttamente su ML2.
///
/// Pattern identico a ML2PointerRayColor (InteractableRayCast.cs) che funziona già per i 3D object.
///
/// Setup:
///   1. Aggiungi questo script su qualunque GO in scena (es. EventSystem o XR Rig).
///   2. Assegna il XRRayInteractor dall'inspector (oppure viene cercato automaticamente in Awake).
///   3. Scegli il bottone (trigger / bumper) che deve fare click sull'UI.
/// </summary>
public class XRUIClickBridge : MonoBehaviour
{
    [Header("XRI Ray Interactor")]
    [Tooltip("XRRayInteractor della scena. Trovato automaticamente in Awake se lasciato vuoto.")]
    [SerializeField] private XRRayInteractor rayInteractor;

    [Header("Magic Leap 2 — Button Actions (OpenXR, nessun MLSDK)")]
    // Stessa binding usata in ML2PointerRayColor — triggerPressed funziona già per il cubo 3D.
    [SerializeField] private InputAction triggerPressed =
        new InputAction(type: InputActionType.Button,
            binding: "<MagicLeapController>{RightHand}/triggerPressed",
            expectedControlType: "Button");

    // Opzionale: bumper come alternativa al trigger.
    [SerializeField] private InputAction bumperPressed =
        new InputAction(type: InputActionType.Button,
            binding: "<MagicLeapController>{RightHand}/gripPressed",
            expectedControlType: "Button");

    [Header("Opzioni")]
    [Tooltip("Se true, sia trigger che bumper fanno click sull'UI.")]
    [SerializeField] private bool useBumperToo = false;

    [Tooltip("Log di debug — disabilita in produzione.")]
    [SerializeField] private bool verbose = true;

    // PointerEventData riusabile: evita GC allocation ogni frame.
    private PointerEventData _pointerData;

    private void Awake()
    {
        if (rayInteractor == null)
        {
            rayInteractor = Object.FindFirstObjectByType<XRRayInteractor>();
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

        bool shouldClick = triggerPressed.WasPressedThisFrame()
                        || (useBumperToo && bumperPressed.WasPressedThisFrame());

        if (!shouldClick) return;

        // Usa il risultato UI del XRRayInteractor — stesso hit che guida hover e line visualizer.
        if (!rayInteractor.TryGetCurrentUIRaycastResult(out RaycastResult hit))
        {
            if (verbose) Debug.Log("[XRUIClickBridge] Bottone premuto ma nessun hit UI corrente.");
            return;
        }

        GameObject target = hit.gameObject;
        if (target == null) return;

        if (verbose)
            Debug.Log($"[XRUIClickBridge] Click UI → '{target.name}' (pos world: {hit.worldPosition})");

        // Aggiorna il PointerEventData con i dati dell'hit corrente.
        _pointerData.position        = hit.screenPosition;
        _pointerData.pointerCurrentRaycast = hit;
        _pointerData.pointerPressRaycast   = hit;

        // Simula la sequenza completa PointerDown → PointerClick → PointerUp
        // in modo che UnityEngine.UI.Button.onClick venga invocato correttamente.
        GameObject pressed = ExecuteEvents.ExecuteHierarchy(target, _pointerData, ExecuteEvents.pointerDownHandler);
        _pointerData.pointerPress = pressed;

        ExecuteEvents.ExecuteHierarchy(target, _pointerData, ExecuteEvents.pointerClickHandler);
        ExecuteEvents.ExecuteHierarchy(target, _pointerData, ExecuteEvents.pointerUpHandler);

        // Pulizia per il frame successivo.
        _pointerData.pointerPress = null;
    }
}
