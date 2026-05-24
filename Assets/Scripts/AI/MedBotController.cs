using System.Collections;
using UnityEngine;

namespace ARHealthCare.AI
{
    /// <summary>
    /// Floating 3D AI medical assistant character.
    /// Follows the user's head (yaw-only), bobs gently, and changes glow color per mode.
    /// The GameObject hierarchy is built procedurally in code — no external 3D asset needed.
    /// </summary>
    public class MedBotController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [Tooltip("Offset from camera position in local camera-yaw space (right, up, forward)")]
        public Vector3 followOffset = new Vector3(0.35f, -0.10f, 0.90f);
        [Tooltip("How fast MedBot follows head movement")]
        public float followSpeed = 5f;

        [Header("Float Animation")]
        public float floatAmplitude = 0.03f;
        public float floatFrequency = 1.2f;
        public float ringRotateSpeed = 12f;

        [Header("Mode Colors")]
        public Color studentColor  = new Color(0.2f, 0.4f, 1.0f);   // blue
        public Color physicianColor = new Color(0.1f, 0.9f, 0.3f);   // green

        private Camera _cam;
        private Transform _ringTransform;
        private Renderer _sphereRenderer;
        private Light _modeLight;
        private Vector3 _baseLocalPos;
        private bool _isStudentMode = true;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _cam = Camera.main;
            BuildGeometry();
        }

        private void Start()
        {
            SetMode(true); // default: student mode
            StartCoroutine(FloatAnimation());
        }

        private void LateUpdate()
        {
            if (_cam == null) { _cam = Camera.main; return; }

            Quaternion yaw = Quaternion.Euler(0f, _cam.transform.eulerAngles.y, 0f);
            Vector3 target = _cam.transform.position + yaw * followOffset;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * followSpeed);

            // Face toward the camera (yaw only)
            Vector3 toCamera = _cam.transform.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * followSpeed);
            }
        }

        private IEnumerator FloatAnimation()
        {
            while (true)
            {
                float offset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
                transform.localPosition = _baseLocalPos + Vector3.up * offset;

                if (_ringTransform != null)
                    _ringTransform.Rotate(Vector3.up, ringRotateSpeed * Time.deltaTime, Space.Self);

                yield return null;
            }
        }

        /// <summary>Switch between Student (blue) and Physician (green) mode.</summary>
        public void SetMode(bool isStudent)
        {
            _isStudentMode = isStudent;
            Color c = isStudent ? studentColor : physicianColor;
            ApplyModeColor(c);
        }

        public bool IsStudentMode => _isStudentMode;

        private void ApplyModeColor(Color c)
        {
            if (_sphereRenderer != null)
            {
                var mat = _sphereRenderer.material;
                mat.SetColor(EmissionColor, c * 1.5f);
                mat.color = Color.Lerp(Color.white, c, 0.3f);
            }
            if (_modeLight != null)
            {
                _modeLight.color = c;
            }
        }

        private void BuildGeometry()
        {
            // Sphere body 
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "MedBot_Body";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * 0.10f;
            Destroy(sphere.GetComponent<Collider>());

            _sphereRenderer = sphere.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.EnableKeyword("_EMISSION");
            _sphereRenderer.material = mat;


            // Ring accent (flattened capsule) 
            var ring = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ring.name = "MedBot_Ring";
            ring.transform.SetParent(sphere.transform, false);
            ring.transform.localScale = new Vector3(1.8f, 0.08f, 1.8f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Destroy(ring.GetComponent<Collider>());
            _ringTransform = ring.transform;

            var ringMat = new Material(Shader.Find("Standard"));
            ringMat.EnableKeyword("_EMISSION");
            ring.GetComponent<Renderer>().material = ringMat;


            // Mode light (soft point light to enhance glow)
            var lightGO = new GameObject("MedBot_Light");
            lightGO.transform.SetParent(transform, false);
            _modeLight = lightGO.AddComponent<Light>();
            _modeLight.type = LightType.Point;
            _modeLight.intensity = 0.4f;
            _modeLight.range = 0.6f;

            // Store base local position for float animation
            _baseLocalPos = transform.localPosition;
        }
    }
}
