using UnityEngine;
using UnityEngine.EventSystems;

namespace ARHealthCare.UI
{

    [RequireComponent(typeof(RectTransform))]
    public class HoverScaleImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Scale")]
        [SerializeField] private float hoverScaleMultiplier = 1.1f;
        [SerializeField] private float smoothSpeed = 12f; // quanto velocemente interpola

        private RectTransform rectTransform;
        private Vector3 initialScale;
        private Vector3 targetScale;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            initialScale = rectTransform.localScale;
            targetScale = initialScale;
        }

        private void OnEnable()
        {
            // in caso l'oggetto venga riattivato dopo
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            //initialScale = rectTransform.localScale;
            targetScale = initialScale;
            rectTransform.localScale = initialScale;
        }

        private void Update()
        {
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * smoothSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = initialScale * hoverScaleMultiplier;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = initialScale;
        }
    }
}