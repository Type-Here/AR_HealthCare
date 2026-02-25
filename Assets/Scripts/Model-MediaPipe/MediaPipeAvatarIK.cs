using UnityEngine;
// using UnityEngine.Animations.Rigging; // if you want to use Unity's built-in Rigging system instead of custom IK

public class MediaPipeAvatarIK : MonoBehaviour
{
    [Header("Impostazioni Generali")]
    public float movementScale = 1.0f;
    public Vector3 globalOffset;       // To move the avatar

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

    // Optional reference to move the entire body (Root/Hips)
    // If not used, the avatar will stay in place and only move limbs
    public Transform hipsBone; 


    // Function called by the script that receives the landmarks from the Magic Leap 2 camera
    public void UpdatePose(Vector3[] landmarks)
    {
        Debug.Log("Updating Avatar Pose with MediaPipe Landmarks...");
        Debug.Log($"Received {landmarks.Length} landmarks.");
        Debug.Log($"Example Landmark (Nose): {landmarks[0]}");


        // Check if we have enough landmarks (MediaPipe Pose provides 33 landmarks)
        if (landmarks == null || landmarks.Length < 33) return;

        // --- MAPPING ---
        
        // 1. ARMS
        // Left: Wrist (15) and Elbow (13)
        UpdateIKPoint(leftHandTarget, landmarks[15]);
        UpdateIKPoint(leftElbowHint, landmarks[13]);

        // Right: Wrist (16) and Elbow (14)
        UpdateIKPoint(rightHandTarget, landmarks[16]);
        UpdateIKPoint(rightElbowHint, landmarks[14]);

        // 2. LEGS
        // Left: Ankle (27) and Knee (25)
        UpdateIKPoint(leftFootTarget, landmarks[27]);
        UpdateIKPoint(leftKneeHint, landmarks[25]);

        // Right: Ankle (28) and Knee (26)
        UpdateIKPoint(rightFootTarget, landmarks[28]);
        UpdateIKPoint(rightKneeHint, landmarks[26]);

        // 3. BODY POSITION (Hips)
        // Center of hips (average of left and right hip, points 23 and 24)
        if (hipsBone != null)
        {
            Vector3 hipsPos = (landmarks[23] + landmarks[24]) * 0.5f;
            hipsBone.position = (hipsPos * movementScale) + globalOffset;
        }

        // 4. Head Position (Nose - Landmark 0)
        UpdateIKPoint(headTarget, landmarks[0]); // Landmark 0 = Nose
    }

    // Helper function to apply position, scale, and offset
    void UpdateIKPoint(Transform target, Vector3 rawPos)
    {
        if (target != null)
        {
            // Apply scaling and global offset to the raw position from MediaPipe
            // NOTE: Depending on the coordinate system of your avatar and the one used by MediaPipe, 
            // you might need to swap axes or invert some of them. Adjust as necessary.
            // Eg: new Vector3(-rawPos.x, -rawPos.y, rawPos.z)
            target.position = (rawPos * movementScale) + globalOffset;
        }
    }
}