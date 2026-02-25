// Get Hover info from Input System and animate UI element by scaling it up/down
using UnityEngine;

namespace ARHealthCare.UI
{
    public class HoverAnimation : MonoBehaviour
    {
        [Tooltip("Scale factor when hovered")]
        public float hoverScale = 1.2f;

        [Tooltip("Speed of scaling animation")]
        public float animationSpeed = 5f;

        private Vector3 originalScale;
        private Vector3 targetScale;
        private bool isHovered = false;

        void Start()
        {
            originalScale = transform.localScale;
            targetScale = originalScale;
        }

        void Update()
        {
            // Smoothly interpolate to target scale
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, 
                                    Time.deltaTime * animationSpeed);
        }

        // Call this method to set hover state
        public void SetHoverState(bool hover)
        {
            isHovered = hover;
            targetScale = isHovered ? originalScale * hoverScale : originalScale;
        }

        public void OnPointerEnter()
        {
            SetHoverState(true);
        }
        
        public void OnPointerExit()
        {
            SetHoverState(false);
        }
    }
}