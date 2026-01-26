using UnityEngine;
using UnityEngine.UI;

public class ScrollButtons : MonoBehaviour
{
    public ScrollRect scrollRect;
    public float scrollStep = 0.15f;

    public void ScrollUp()
{
    //Debug.Log("ScrollUp premuto");
    scrollRect.verticalNormalizedPosition =
        Mathf.Clamp01(scrollRect.verticalNormalizedPosition + scrollStep);
}

public void ScrollDown()
{
    //Debug.Log("ScrollDown premuto");
    scrollRect.verticalNormalizedPosition =
        Mathf.Clamp01(scrollRect.verticalNormalizedPosition - scrollStep);
}

}
