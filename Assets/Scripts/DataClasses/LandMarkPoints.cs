// Dictionary of landmark points for easier access
// With key as landmark index and value as landmark name
// Reference: https://github.com/google-ai-edge/mediapipe/blob/master/docs/solutions/pose.md
using System.Collections.Generic;
namespace ARHealthCare.DataClasses
{
    public static class LandMarkPoints
    {
        public static readonly Dictionary<int, string> Points = new Dictionary<int, string>
    {
        {0, "Nose"},
        {1, "Left Eye Inner"},
        {2, "Left Eye"},
        {3, "Left Eye Outer"},
        {4, "Right Eye Inner"},
        {5, "Right Eye"},
        {6, "Right Eye Outer"},
        {7, "Left Ear"},
        {8, "Right Ear"},
        {9, "Mouth Left"},
        {10, "Mouth Right"},
        {11, "Left Shoulder"},
        {12, "Right Shoulder"},
        {13, "Left Elbow"},
        {14, "Right Elbow"},
        {15, "Left Wrist"},
        {16, "Right Wrist"},
        {17, "Left Pinky"},
        {18, "Right Pinky"},
        {19, "Left Index"},
        {20, "Right Index"},
        {21, "Left Thumb"},
        {22, "Right Thumb"},
        {23, "Left Hip"},
        {24, "Right Hip"},
        {25, "Left Knee"},
        {26, "Right Knee"},
        {27, "Left Ankle"},
        {28, "Right Ankle"},
        {29, "Left Heel"},
        {30, "Right Heel"},
        {31, "Left Foot Index"},
        {32, "Right Foot Index"}
    };
    }
}