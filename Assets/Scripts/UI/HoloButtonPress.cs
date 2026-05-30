using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Attach to any Button that uses a HoloMed/Button* shader.
/// Animates _PressDepth on pointer down/up to give tactile depth feedback.
/// Works with all three variants: Dark, Light, Gray.
/// </summary>
[RequireComponent(typeof(Button))]
public class HoloButtonPress : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Press animation")]
    [Tooltip("How fast the button 'sinks' on press (higher = snappier)")]
    public float pressSpeed   = 12f;

    [Tooltip("How fast it returns to idle")]
    public float releaseSpeed = 8f;

    [Tooltip("Maximum _PressDepth value (0-1). 0.85 gives a solid but not over-done press.")]
    [Range(0f, 1f)]
    public float maxPressDepth = 0.85f;

    // ---- internals ----
    private Material   _mat;
    private float      _targetDepth;
    private float      _currentDepth;
    private Coroutine  _anim;
    private static readonly int PressDepthID = Shader.PropertyToID("_PressDepth");

    void Awake()
    {
        // Grab the Image component and get an INSTANCE material
        // (so we don't modify the shared asset on disk)
        var img = GetComponent<Image>();
        if (img != null)
            _mat = img.material = new Material(img.material);
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }

    public void OnPointerDown(PointerEventData _)
    {
        _targetDepth = maxPressDepth;
        RestartAnim();
    }

    public void OnPointerUp(PointerEventData _)   => Release();
    public void OnPointerExit(PointerEventData _) => Release();

    private void Release()
    {
        _targetDepth = 0f;
        RestartAnim();
    }

    private void RestartAnim()
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimatePress());
    }

    private IEnumerator AnimatePress()
    {
        while (!Mathf.Approximately(_currentDepth, _targetDepth))
        {
            float speed  = (_targetDepth > _currentDepth) ? pressSpeed : releaseSpeed;
            _currentDepth = Mathf.MoveTowards(_currentDepth, _targetDepth,
                                               speed * Time.deltaTime);
            _mat.SetFloat(PressDepthID, _currentDepth);
            yield return null;
        }
        _currentDepth = _targetDepth;
        _mat.SetFloat(PressDepthID, _currentDepth);
        _anim = null;
    }
}
