using UnityEngine;
using System.Collections;

// Script che gestisce l'animazione di scomparsa del menu principale con effetto fade out.
public class MainMenuFade : MonoBehaviour
{
    // Riferimento al CanvasGroup per controllare l'opacità e l'interattività del menu
    public CanvasGroup canvasGroup;
    
    public float fadeDuration = 1.0f;

    // Inizializza il CanvasGroup se non è stato assegnato dall'inspector.
    void Awake()
    {
        // Se lo script è sullo stesso oggetto del CanvasGroup, ottieni il componente automaticamente
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    // Avvia l'animazione di scomparsa del menu principale.
    public void FadeOut()
    {
        StartCoroutine(FadeOutRoutine());
    }

    // Coroutine che anima il fade out del menu.
    // Riduce progressivamente l'opacità e disabilita le interazioni.
    private IEnumerator FadeOutRoutine()
    {
        float time = 0f;

        // Disabilita input e raycast subito per evitare interazioni durante il fade
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Anima l'opacità da 1 a 0 durante la durata specificata
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, time / fadeDuration);
            yield return null;
        }

        // Assicura che l'alpha sia esattamente 0 e disattiva l'oggetto
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
