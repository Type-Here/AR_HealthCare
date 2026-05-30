using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using ARHealthCare.Network;

public class PatientProblemsUI : MonoBehaviour
{
    public Transform contentParent;
    public GameObject problemItemPrefab;

    public void AddProblem(string text)
    {
        GameObject item = Instantiate(problemItemPrefab, contentParent);

        // Force item to stretch horizontally so TMP wraps correctly instead of overflowing.
        if (item.TryGetComponent<RectTransform>(out var rt))
        {
            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }

        TextMeshProUGUI tmp = item.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.fontSizeMax = 9;
            tmp.fontSizeMin = 6;
            tmp.enableAutoSizing = true;
            tmp.text = text;
        }
        else
        {
            Debug.LogError("ProblemItem prefab NON contiene TextMeshProUGUI!");
        }
    }

    public void PopulateFromRecord(PatientRecord record)
    {
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        string notes = string.IsNullOrEmpty(record.notes) ? "" : $"\nNote: {record.notes}";
        AddProblem(
            $"Paziente: {record.display_name}"
            + $"\nEtà: {record.age} anni"
            + $"\nDiagnosi: {record.diagnosis}\n"
            + $"\nIntervento: {record.planned_procedure}"
            + $"\nData prevista: {record.procedure_date}\n"
            + $"\nTrattamento: {record.current_treatment}"
            + notes);
    }

    private void Awake()
    {
        StartCoroutine(LoadDefaultPatient());
    }

    private IEnumerator LoadDefaultPatient()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "default_patient.json");

        // On Android, StreamingAssets must be read via UnityWebRequest
        using var req = UnityWebRequest.Get(path);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var record = JsonUtility.FromJson<PatientRecord>(req.downloadHandler.text);
            if (record != null)
            {
                PopulateFromRecord(record);
                yield break;
            }
        }

        Debug.LogWarning("PatientProblemsUI: default_patient.json not found or invalid, using built-in fallback.");
        PopulateBuiltInFallback();
    }

    private void PopulateBuiltInFallback()
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
