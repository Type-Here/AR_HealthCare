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

        if (tmp != null)
        {
            tmp.text = text;
        }
        else
        {
            Debug.LogError("ProblemItem prefab NON contiene TextMeshProUGUI!");
        }
    }


    void Start()
    {
        AddProblem("Il goat");
        AddProblem("ZA PAWAAAA");
        AddProblem("Piedi");
        AddProblem("LOL");
        AddProblem("Il goat");
        AddProblem("ZA PAWAAAAeeuneu cujisoa cuifdid cuhfhrfinjd");
        AddProblem("Piedi");
        AddProblem("LOL");
        AddProblem("Il goat");
        AddProblem("ZA PAWAAAA");
        AddProblem("Piedi");
        AddProblem("LOL");
    }
}
