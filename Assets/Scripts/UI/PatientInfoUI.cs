using UnityEngine;
using TMPro;

public class PatientProblemsUI : MonoBehaviour
{
    public Transform contentParent;
    public GameObject problemItemPrefab;

    public void AddProblem(string text)
    {
        GameObject item = Instantiate(problemItemPrefab, contentParent);

        TextMeshProUGUI tmp = item.GetComponentInChildren<TextMeshProUGUI>();
        tmp.fontSizeMax = 14; // Set a maximum font size to ensure readability
        tmp.fontSizeMin = 10; // Set a minimum font size to prevent it from becoming too small
        tmp.enableAutoSizing = true; // Enable auto-sizing to adjust font size based on content

        if (tmp != null)
        {
            tmp.text = text;
        }
        else
        {
            Debug.LogError("ProblemItem prefab NON contiene TextMeshProUGUI!");
        }
    }


    void Awake()
    {
        AddProblem("Paziente: Luigi il Compagnone"
            + "\nEtà: 25 anni"
            + "\nDiagnosi: Rottura Crociato Anteriore (Dx)\n"
            + "\nIntervento (previsto): Artroscopia con ricostruzione del legamento"   
            + "\nData intervento (prevista): 1 Aprile 2026\n"
            + "\nTrattamento attuale: Fisioterapia pre-operatoria, antidolorifici NSAIDs al bisogno."
            + "\nNote: Paziente sportivo, pratica calcio a livello amatoriale."
            + "\nNecessaria riabilitazione post-operatoria di almeno 6 mesi.");
    }
}
