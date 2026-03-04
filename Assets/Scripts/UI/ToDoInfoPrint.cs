/// Print inside Element when user clicks on a Button whose feature is not implemented yet.
/// This is a temporary solution to inform users about the development status of certain features.
using TMPro;
using UnityEngine;


namespace ARHealthCare.UI
{
    public class ToDoInfoPrint : MonoBehaviour
    {

        [Tooltip("Reference to the Canvas where the text will be displayed")]
        public CanvasGroup element; // Reference to the CanvasElement where the text will be displayed
        [SerializeField][Tooltip("Duration (in seconds) for which the information will be displayed")]
        private float displayDuration = 5f; // Duration for which the information will be displayed
        [SerializeField][Tooltip("Text Element where the information will be printed." + 
        "If left empty, a new TextMeshProUGUI component will be created on the element.")]
        private TextMeshProUGUI infoText; // Reference to the TextMeshProUGUI component

        /// <summary>
        /// Call this method when the button is clicked to display the "To Do" information.
        /// </summary>
        public void DisplayToDoInfo()
        {
            if (element == null)
            {
                Debug.LogError("[ToDoInfoPrint] 'element' CanvasGroup non assegnato nell'Inspector!", this);
                return;
            }

            // If no TextMeshProUGUI component is found create one and add it to the element
            if (infoText == null)
            {
                
                infoText = element.gameObject.AddComponent<TextMeshProUGUI>();
                infoText.fontSizeMax = 22; // Set a maximum font size to ensure readability
                infoText.fontSizeMin = 20; // Set a minimum font size to prevent it from becoming too small
                infoText.enableAutoSizing = true; // Enable auto-sizing to adjust font size based on content
                infoText.alignment = TextAlignmentOptions.Center; // Center the text
                infoText.color = new Color(0.55f, 0f, 0f); // Dark red color
                infoText.fontStyle = FontStyles.Bold; // Make the text bold for better visibility
                infoText.outlineWidth = 0.1f; // Set outline width 
                infoText.outlineColor = Color.black; // Set outline color to white for contrast
                infoText.text = ""; // Initialize with empty text            
            }

            infoText.text = "This feature is under development. Stay tuned for updates!";

            // Activate the element to make it visible for 5 seconds, then hide it again
            element.alpha = 1f; 
            //if invoke is already scheduled, cancel it to reset the timer
            if (IsInvoking("HideElement")) CancelInvoke("HideElement");
            Invoke("HideElement", displayDuration); // Hide after 5 seconds
        }

        /// <summary>
        /// Hides the element after it has been displayed for 5 seconds.
        /// </summary>
        private void HideElement()
        {
            element.alpha = 0f; // Hide the element
            infoText.text = ""; // Clear the text for the next time it is displayed
        }


    }
}