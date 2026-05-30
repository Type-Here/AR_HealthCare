using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Attach to any Button using a HoloMed/Button* shader.
/// - Feeds _RectSize automatically from the RectTransform (works in EDIT mode too)
/// - Animates _PressDepth on pointer down/up (Play mode only)
/// Works with ButtonDark, ButtonLight, ButtonGray.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ButtonShaderManager : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Press animation (Play mode)")]
    public float pressSpeed   = 12f;
    public float releaseSpeed = 8f;
    [Range(0f, 1f)]
    public float maxPressDepth = 0.85f;

    private Material      _mat;
    private RectTransform _rt;
    private Image         _img;
    private float         _targetDepth;
    private float         _currentDepth;
    private Coroutine     _anim;
    private Vector2       _lastSize = Vector2.zero;

    private static readonly int PressDepthID = Shader.PropertyToID("_PressDepth");
    private static readonly int RectSizeID   = Shader.PropertyToID("_RectSize");

    void OnEnable()
    {
        _rt  = GetComponent<RectTransform>();
        _img = GetComponent<Image>();
        AcquireMaterial();
        PushRectSize();
    }

    void AcquireMaterial()
    {
        if (_img == null) return;

        if (Application.isPlaying)
        {
            // runtime: instance the material so we don't edit the shared asset
            if (_mat == null || _img.material == _img.defaultMaterial)
                _mat = _img.material = new Material(_img.material);
        }
        else
        {
            // edit mode: write directly to the assigned material so you SEE it live
            _mat = _img.material;
        }
    }

    void Update()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        Vector2 size = _rt.rect.size;
        if (size != _lastSize)
        {
            PushRectSize();
            _lastSize = size;
        }
    }

    void PushRectSize()
    {
        if (_mat == null) AcquireMaterial();
        if (_mat == null || _rt == null) return;
        Vector2 s = _rt.rect.size;
        _mat.SetVector(RectSizeID, new Vector4(s.x, s.y, 0f, 0f));
    }

    // --- press (Play mode) ---
    public void OnPointerDown(PointerEventData _)
    {
        if (!Application.isPlaying) return;
        _targetDepth = maxPressDepth;
        RestartAnim();
    }
    public void OnPointerUp(PointerEventData _)   => Release();
    public void OnPointerExit(PointerEventData _) => Release();

    void Release()
    {
        if (!Application.isPlaying) return;
        _targetDepth = 0f;
        RestartAnim();
    }

    void RestartAnim()
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimatePress());
    }

    IEnumerator AnimatePress()
    {
        while (!Mathf.Approximately(_currentDepth, _targetDepth))
        {
            float speed   = (_targetDepth > _currentDepth) ? pressSpeed : releaseSpeed;
            _currentDepth = Mathf.MoveTowards(_currentDepth, _targetDepth, speed * Time.deltaTime);
            _mat.SetFloat(PressDepthID, _currentDepth);
            yield return null;
        }
        _currentDepth = _targetDepth;
        _mat.SetFloat(PressDepthID, _currentDepth);
        _anim = null;
    }
}
