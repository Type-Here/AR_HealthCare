using UnityEngine;
// using UnityEngine.Animations.Rigging; // Se serve accedere al Rig Builder via codice

public class MediaPipeAvatarIK : MonoBehaviour
{
    [Header("Impostazioni Generali")]
    public float movementScale = 1.0f; // Aumentare se l'avatar si muove poco
    public Vector3 globalOffset;       // Per spostare l'avatar nello spazio

    [Header("Target IK (Mani e Piedi)")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    public Transform leftFootTarget;
    public Transform rightFootTarget;

    [Header("Hint IK (Gomiti e Ginocchia)")]
    public Transform leftElbowHint;
    public Transform rightElbowHint;
    public Transform leftKneeHint;
    public Transform rightKneeHint;

    [Header("Hint IK (Testa)")]
    public Transform headTarget;

    // Riferimento opzionale per muovere l'intero corpo (Root/Hips)
    // Se non si usa, l'avatar stara' fermo sul posto e muovera' solo gli arti
    public Transform hipsBone; 

    // Questa funzione viene chiamata dallo script con i punti presi dalla camera del Magic Leap 2
    public void UpdatePose(Vector3[] landmarks)
    {
        // Controllo di sicurezza: MediaPipe deve dare 33 punti
        if (landmarks == null || landmarks.Length < 33) return;

        // --- MAPPARE I PUNTI ---
        
        // 1. BRACCIA
        // Sinistra: Polso (15) e Gomito (13)
        UpdateIKPoint(leftHandTarget, landmarks[15]);
        UpdateIKPoint(leftElbowHint, landmarks[13]);

        // Destra: Polso (16) e Gomito (14)
        UpdateIKPoint(rightHandTarget, landmarks[16]);
        UpdateIKPoint(rightElbowHint, landmarks[14]);

        // 2. GAMBE
        // Sinistra: Caviglia (27) e Ginocchio (25)
        UpdateIKPoint(leftFootTarget, landmarks[27]);
        UpdateIKPoint(leftKneeHint, landmarks[25]);

        // Destra: Caviglia (28) e Ginocchio (26)
        UpdateIKPoint(rightFootTarget, landmarks[28]);
        UpdateIKPoint(rightKneeHint, landmarks[26]);

        // 3. POSIZIONE CORPO
        // Calcoliamo il centro dei fianchi (media tra punto 23 e 24)
        if (hipsBone != null)
        {
            Vector3 hipsPos = (landmarks[23] + landmarks[24]) * 0.5f;
            hipsBone.position = (hipsPos * movementScale) + globalOffset;
        }

        // 4. POSIZIONE Testa
        UpdateIKPoint(headTarget, landmarks[0]); // Landmark 0 e' il naso
    }

    // Funzione helper per applicare posizione, scala e offset
    void UpdateIKPoint(Transform target, Vector3 rawPos)
    {
        if (target != null)
        {
            // Applichiamo la posizione.
            // NOTA: Se il modello si muove al contrario, prova a invertire assi qui
            // Es: new Vector3(-rawPos.x, -rawPos.y, rawPos.z)
            target.position = (rawPos * movementScale) + globalOffset;
        }
    }
}