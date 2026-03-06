using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ARHealthCare.UI
{
    /// <summary>
    /// Aggiunge automaticamente TrackedDeviceGraphicRaycaster a ogni Canvas World Space
    /// che ne è privo. Con XRI 3.x / OpenXR il XRRayInteractor può colpire l'UI
    /// SOLO attraverso TrackedDeviceGraphicRaycaster — il GraphicRaycaster standard
    /// funziona solo con input a schermo.
    ///
    /// Aggiungere questo script a qualsiasi GameObject attivo nella scena
    /// (es. EventSystem o XR Interaction Manager).
    /// L'esecuzione avviene in Awake con execution-order = -100 (prima di tutto),
    /// garantendo che i Canvas siano già pronti prima che XRUIInputModule inizi a poilare.
    ///
    /// Unity Inspector: non serve alcuna assegnazione manuale. Lo script trova
    /// automaticamente tutti i Canvas nella scena inclusi quelli da prefab.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class XRUICanvasFixer : MonoBehaviour
    {
        [Header("Impostazioni TrackedDeviceGraphicRaycaster")]
        [Tooltip("Se true, rimuove il GraphicRaycaster standard prima di aggiungere " +
                 "TrackedDeviceGraphicRaycaster. Raccomandato: entrambi non devono coesistere.")]
        [SerializeField] private bool removeStandardGraphicRaycaster = true;

        [Tooltip("Se true, logga ogni Canvas modificato nella Console.")]
        [SerializeField] private bool logChanges = true;

        private void Awake()
        {
            FixAllWorldSpaceCanvases();
        }

        /// <summary>
        /// Itera tutti i Canvas attivi nella scena e aggiunge TrackedDeviceGraphicRaycaster
        /// a quelli in World Space che ne sono privi.
        /// </summary>
        public void FixAllWorldSpaceCanvases()
        {
            // Trova tutti i Canvas, inclusi quelli disabilitati (includeinactive = true)
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int fixed_count = 0;

            foreach (Canvas canvas in allCanvases)
            {
                // Solo Canvas World Space; Screen Space Overlay / Camera non ne hanno bisogno
                if (canvas.renderMode != RenderMode.WorldSpace)
                    continue;

                // Già correttamente configurato
                if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null)
                    continue;

                // Rimuovi il raycaster standard se presente (due raycaster causano double-hit)
                if (removeStandardGraphicRaycaster)
                {
                    GraphicRaycaster standard = canvas.GetComponent<GraphicRaycaster>();
                    if (standard != null)
                    {
                        if (logChanges)
                            Debug.Log($"[XRUICanvasFixer] Rimosso GraphicRaycaster da '{canvas.name}'");
                        Destroy(standard);
                    }
                }

                // Aggiungi il raycaster XRI
                TrackedDeviceGraphicRaycaster xrRaycaster =
                    canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

                // Parametri consigliati per ML2 / visori AR a distanza > 1 m
                // checkFor2DOcclusion: false → non vogliamo occlusione 2D (semitrasparente = interagibile)
                // checkFor3DOcclusion: false → lento su hardware mobile, da abilitare solo se serve
                xrRaycaster.checkFor2DOcclusion = false;
                xrRaycaster.checkFor3DOcclusion = false;

                fixed_count++;

                if (logChanges)
                    Debug.Log($"[XRUICanvasFixer] Aggiunto TrackedDeviceGraphicRaycaster a '{canvas.name}' " +
                              $"(layer {canvas.gameObject.layer})");
            }

            if (logChanges)
                Debug.Log($"[XRUICanvasFixer] Fix completato — {fixed_count} Canvas World Space aggiornati " +
                          $"(totale scansionati: {allCanvases.Length})");
        }
    }
}
