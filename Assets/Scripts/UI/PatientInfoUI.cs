using UnityEngine;
using TMPro;

public class PatientProblemsUI : MonoBehaviour
{
    public Transform contentParent;
    public GameObject problemItemPrefab;

    public void AddProblem(string text)
{
    GameObject item = Instantiate(problemItemPrefab, contentParent);

    var tmp = item.GetComponent<TextMeshProUGUI>();
    if (tmp != null)
        tmp.text = "• " + text;
    else
        Debug.LogError("Prefab NON contiene TextMeshProUGUI!");
}


    void Start()
    {
        AddProblem("Il goat");
        AddProblem("ZA PAWAAAA");
        AddProblem("Piedi");
        AddProblem("LOL");
        AddProblem("Il goat");
        AddProblem("ZA PAWAAAA");
        AddProblem("Piedi");
        AddProblem("LOL");
        AddProblem("Il goat");
        AddProblem("ZA PAWAAAA");
        AddProblem("Piedi");
        AddProblem("LOL");
    }
}
