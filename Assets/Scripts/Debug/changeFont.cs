using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Script to mass-apply a TMP font in the scene
public class TMP_FontChanger : MonoBehaviour {
    [SerializeField] public TMP_FontAsset targetFont;
}

#if UNITY_EDITOR
[CustomEditor(typeof(TMP_FontChanger))]
public class TMP_FontChangerEditor : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        TMP_FontChanger changer = (TMP_FontChanger)target;
        if (GUILayout.Button("Apply Font to All in Scene")) {
            // Finds all TMP objects and updates their font
            TextMeshProUGUI[] texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (TextMeshProUGUI text in texts) {
                text.font = changer.targetFont;
            }
            Debug.Log("Font updated on " + texts.Length + " objects!");
        }
    }
}
#endif