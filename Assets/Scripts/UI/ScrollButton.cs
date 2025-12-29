using UnityEngine;
using UnityEngine.UI;

public class ScrollButtons : MonoBehaviour
{
    public ScrollRect scrollRect;
    public float scrollStep = 0.15f;

    public void ScrollUp()
    {
        scrollRect.verticalNormalizedPosition =
            Mathf.Clamp01(scrollRect.verticalNormalizedPosition + scrollStep);
    }

    public void ScrollDown()
    {
        scrollRect.verticalNormalizedPosition =
            Mathf.Clamp01(scrollRect.verticalNormalizedPosition - scrollStep);
    }
}
