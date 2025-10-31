using UnityEngine;
using Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

[DefaultExecutionOrder(100)]
public class PinchZoomCamera : MonoBehaviour
{
    [Header("Zoom")]
    [SerializeField, Min(0.01f)] private float sensibilidadeZoom = 1.0f;
    [SerializeField, Min(0f)]    private float suavizacaoZoom = 12f;
    [Tooltip("ORTHO: OrthoSize | PERSPECTIVE: FOV")]
    public float zoomMin = 2f;
    public float zoomMax = 20f;

    [Header("Pan / manter foco")]
    [SerializeField, Min(0f)] private float suavizacaoPan = 12f;

    [Header("PC / Editor")]
    [SerializeField] private bool permitirScrollNoCursor = true;
    [SerializeField] private bool permitirPanALT = true;

    private CinemachineVirtualCamera vcam;
    private CinemachineTransposer transposer;
    private CinemachineFramingTransposer framing;
    private Camera outCam;

    private float alvoZoom;
    private Vector3 alvoDeltaLocal;

#if ENABLE_INPUT_SYSTEM
    private bool enhancedOn;
#endif

    private void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
        if (!vcam)
        {
            Debug.LogError("[PinchZoomCamera] Coloque este script na CinemachineVirtualCamera.", this);
            enabled = false;
            return;
        }

        transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        framing    = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();

        outCam = BuscarOutputCamera();

        if (vcam.m_Lens.Orthographic)
            alvoZoom = Mathf.Clamp(vcam.m_Lens.OrthographicSize, zoomMin, zoomMax);
        else
            alvoZoom = Mathf.Clamp(vcam.m_Lens.FieldOfView, zoomMin, zoomMax);
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (!enhancedOn)
        {
            EnhancedTouchSupport.Enable();
            enhancedOn = true;
        }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (enhancedOn)
        {
            EnhancedTouchSupport.Disable();
            enhancedOn = false;
        }
#endif
    }

    private void Update()
    {
        outCam = BuscarOutputCamera();

        // 1) MOBILE – novo input system
#if ENABLE_INPUT_SYSTEM
        TratarPinçaNovoInput();
#else
        // 2) MOBILE – legacy
        TratarPinçaLegacy();
#endif
        // 3) PC / Editor
        TratarMousePC();

        // 4) aplicar zoom suavizado
        if (vcam.m_Lens.Orthographic)
            vcam.m_Lens.OrthographicSize = LerpExp(vcam.m_Lens.OrthographicSize, alvoZoom, suavizacaoZoom);
        else
            vcam.m_Lens.FieldOfView = LerpExp(vcam.m_Lens.FieldOfView, alvoZoom, suavizacaoZoom);

        // 5) aplicar pan/compensação
        AplicarPanSuavizado();
    }

    // =========================================================
    // PINÇA – NOVO INPUT SYSTEM
    // =========================================================
#if ENABLE_INPUT_SYSTEM
    private void TratarPinçaNovoInput()
    {
        var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        if (touches.Count < 2) return;

        var t0 = touches[0];
        var t1 = touches[1];
        if (!t0.isInProgress || !t1.isInProgress) return;

        Vector2 p0 = t0.screenPosition;
        Vector2 p1 = t1.screenPosition;
        Vector2 p0Prev = p0 - t0.delta;
        Vector2 p1Prev = p1 - t1.delta;

        float dist     = Vector2.Distance(p0, p1);
        float distPrev = Vector2.Distance(p0Prev, p1Prev);
        float delta    = dist - distPrev;

        Vector2 centro = (p0 + p1) * 0.5f;
        ZoomNoPontoDeTela(centro, delta);
    }
#endif

    // =========================================================
    // PINÇA – LEGACY
    // =========================================================
    private void TratarPinçaLegacy()
    {
        if (Input.touchCount < 2) return;

        UnityEngine.Touch t0 = Input.GetTouch(0);
        UnityEngine.Touch t1 = Input.GetTouch(1);

        if (t0.phase == UnityEngine.TouchPhase.Ended || t0.phase == UnityEngine.TouchPhase.Canceled ||
            t1.phase == UnityEngine.TouchPhase.Ended || t1.phase == UnityEngine.TouchPhase.Canceled)
            return;

        Vector2 p0 = t0.position;
        Vector2 p1 = t1.position;
        Vector2 p0Prev = p0 - t0.deltaPosition;
        Vector2 p1Prev = p1 - t1.deltaPosition;

        float dist     = Vector2.Distance(p0, p1);
        float distPrev = Vector2.Distance(p0Prev, p1Prev);
        float delta    = dist - distPrev;

        Vector2 centro = (p0 + p1) * 0.5f;
        ZoomNoPontoDeTela(centro, delta);
    }

    // =========================================================
    // MOUSE / PC
    // =========================================================
    private void TratarMousePC()
    {
        if (!permitirScrollNoCursor || outCam == null) return;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                Vector2 pos = mouse.position.ReadValue();
                ZoomNoPontoDeTela(pos, scroll);
            }

            if (permitirPanALT)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.altKey.isPressed && mouse.leftButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue() * 0.005f;
                    ArrastarNoEditor(delta);
                }
            }
        }
#else
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            Vector2 pos = Input.mousePosition;
            ZoomNoPontoDeTela(pos, scroll);
        }

        if (permitirPanALT && Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButton(0))
        {
            Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 0.005f;
            ArrastarNoEditor(delta);
        }
#endif
    }

    // =========================================================
    // NÚCLEO: ZOOM FOCALIZADO
    // =========================================================
    private void ZoomNoPontoDeTela(Vector2 screenPos, float delta)
    {
        if (outCam == null) return;

        // 1) ponto no mundo antes do zoom
        Vector3 focoAntes = ScreenToWorldInPlane(outCam, screenPos);

        // 2) atualiza alvo de zoom
        if (vcam.m_Lens.Orthographic)
        {
            float fator = 2.0f;
            alvoZoom = Mathf.Clamp(alvoZoom - delta * fator * sensibilidadeZoom, zoomMin, zoomMax);
        }
        else
        {
            float fator = 10f;
            alvoZoom = Mathf.Clamp(alvoZoom - delta * fator * sensibilidadeZoom, zoomMin, zoomMax);
        }

        // 3) ponto no mundo depois do zoom
        Vector3 focoDepois = ScreenToWorldInPlane(outCam, screenPos, true);
        Vector3 deltaMundo = focoAntes - focoDepois;

        // 4) converte para eixos da câmera de saída (right/up)
        Vector3 right = outCam.transform.right;
        Vector3 up    = outCam.transform.up;

        float dx = Vector3.Dot(deltaMundo, right);
        float dy = Vector3.Dot(deltaMundo, up);

        // acumula pan
        alvoDeltaLocal += new Vector3(dx, dy, 0f);
    }

    private void AplicarPanSuavizado()
    {
        if (alvoDeltaLocal.sqrMagnitude < 1e-8f) return;

        Vector3 passo = alvoDeltaLocal * (1f - Mathf.Exp(-suavizacaoPan * Time.unscaledDeltaTime));

        if (transposer != null)
        {
            Vector3 cur = transposer.m_FollowOffset;
            transposer.m_FollowOffset = new Vector3(cur.x + passo.x, cur.y + passo.y, cur.z);
        }
        else if (framing != null)
        {
            Vector3 cur = framing.m_TrackedObjectOffset;
            framing.m_TrackedObjectOffset = new Vector3(cur.x + passo.x, cur.y + passo.y, cur.z);
        }
        else
        {
            // fallback: move a própria vcam
            transform.position += outCam.transform.right * passo.x + outCam.transform.up * passo.y;
        }

        // decai o acumulado
        alvoDeltaLocal -= passo;
    }

    private void ArrastarNoEditor(Vector2 delta)
    {
        if (outCam == null) return;

        float escala = vcam.m_Lens.Orthographic ? vcam.m_Lens.OrthographicSize : 1f;
        Vector3 deltaMundo =
            (outCam.transform.right * -delta.x + outCam.transform.up * -delta.y) * escala;

        Vector3 right = outCam.transform.right;
        Vector3 up    = outCam.transform.up;

        float dx = Vector3.Dot(deltaMundo, right);
        float dy = Vector3.Dot(deltaMundo, up);

        alvoDeltaLocal += new Vector3(dx, dy, 0f);
    }

    // =========================================================
    // UTIL
    // =========================================================
    private Camera BuscarOutputCamera()
    {
        // pega a camera que o CinemachineBrain está dirigindo
        foreach (Camera c in Camera.allCameras)
        {
            var brain = c.GetComponent<CinemachineBrain>();
            if (brain && c.isActiveAndEnabled)
                return c;
        }
        return Camera.main;
    }

    private Vector3 ScreenToWorldInPlane(Camera camRef, Vector2 screen, bool usarAlvo = false)
    {
        if (camRef == null) return Vector3.zero;

        Ray r = camRef.ScreenPointToRay(screen);

        if (vcam.m_Lens.Orthographic)
        {
            // projeta no plano da câmera
            Vector3 p = r.GetPoint(10f);
            Vector3 d = p - camRef.transform.position;
            d -= Vector3.Project(d, camRef.transform.forward);
            return camRef.transform.position + d;
        }
        else
        {
            float fov = usarAlvo ? alvoZoom : camRef.fieldOfView;
            float focoDist = 10f * (60f / Mathf.Max(1f, fov));
            Plane plano = new Plane(-camRef.transform.forward, camRef.transform.position + camRef.transform.forward * focoDist);
            if (plano.Raycast(r, out float enter))
                return r.GetPoint(enter);
            return camRef.transform.position + camRef.transform.forward * focoDist;
        }
    }

    private float LerpExp(float a, float b, float s)
    {
        if (s <= 0f) return b;
        return Mathf.Lerp(a, b, 1f - Mathf.Exp(-s * Time.unscaledDeltaTime));
    }
}
