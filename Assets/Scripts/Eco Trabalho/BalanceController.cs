using UnityEngine;
using UnityEngine.InputSystem;

public class BalanceController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Transform pendulo;
    [SerializeField] private Transform pratoEsquerda;
    [SerializeField] private Transform pratoDireita;

    [Header("Inclinação")]
    [SerializeField] public float maxAngle = 25f;
    [HideInInspector] public float externalAngleOffset = 0f;

    public float MaxAngle => maxAngle; // ✅ acesso público seguro
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private float deadZone = 0.03f;

    [Header("Ajustes de leitura")]
    [Tooltip("Troque para -1 se o lado estiver invertido")]
    [SerializeField] private float inputSign = 1f;
    [SerializeField] private float inputGain = 1f;

    [Header("Filtro Passa-Baixas (opcional)")]
    [SerializeField] private bool lowPassEnabled = false;
    [Tooltip("Meia-vida (s) do filtro exponencial. Menor = responde mais rápido; maior = mais estável.")]
    [SerializeField, Min(0.001f)] private float lowPassHalfLife = 0.10f;

    [Header("Curva de Sensibilidade (opcional)")]
    [SerializeField] private bool sensitivityCurveEnabled = false;
    [Tooltip("Curve mapeia -1..1 para -1..1 (dica: S suave no centro). Eixo X = input normalizado, eixo Y = saída.")]
    [SerializeField] private AnimationCurve sensitivityCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

    private Quaternion baseRotationLocal;
    private Quaternion pratoEsquerdaInicial;
    private Quaternion pratoDireitaInicial;

    private float currentAngle;
    private float targetAngle;

    // Estado do filtro
    private float filteredInput = 0f;
    private const float LN2 = 0.6931471805599453f;

    private void Awake()
    {
        // Fixa Landscape Left como orientação natural
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }

    void Start()
    {
        if (pendulo == null) pendulo = transform;

        // Deixe no Inspector: X = -90 e Y = 90 (base natural)
        baseRotationLocal = pendulo.localRotation;

        if (pratoEsquerda != null) pratoEsquerdaInicial = pratoEsquerda.rotation;
        if (pratoDireita != null)  pratoDireitaInicial  = pratoDireita.rotation;

        currentAngle = 0f;
        pendulo.localRotation = baseRotationLocal;
        filteredInput = 0f;
    }

    void Update()
    {
        float raw = LerTiltParaLandscapeLeft();

        float use = raw;
        if (lowPassEnabled)
        {
            float alpha = 1f - Mathf.Exp(-LN2 * Time.deltaTime / Mathf.Max(0.0001f, lowPassHalfLife));
            filteredInput = Mathf.Lerp(filteredInput, raw, alpha);
            use = filteredInput;
        }

        if (Mathf.Abs(use) < deadZone) use = 0f;

        if (sensitivityCurveEnabled && sensitivityCurve != null)
        {
            float clamped = Mathf.Clamp(use, -1f, 1f);
            use = Mathf.Clamp(sensitivityCurve.Evaluate(clamped), -1f, 1f);
        }

        float processed = use * inputGain * inputSign;
        targetAngle = Mathf.Clamp(processed * maxAngle, -maxAngle, +maxAngle);

        currentAngle = Mathf.Lerp(currentAngle, targetAngle, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));

        float totalAngle = currentAngle + externalAngleOffset;
    pendulo.localRotation = baseRotationLocal * Quaternion.AngleAxis(totalAngle, Vector3.right);

    }

    void LateUpdate()
    {
        if (pratoEsquerda != null) pratoEsquerda.rotation = pratoEsquerdaInicial;
        if (pratoDireita  != null) pratoDireita.rotation  = pratoDireitaInicial;
    }

    private float LerTiltParaLandscapeLeft()
    {
        if (Accelerometer.current == null) return 0f;

        Vector3 acc = Accelerometer.current.acceleration.ReadValue();

        switch (Input.deviceOrientation)
        {
            case DeviceOrientation.LandscapeLeft:
            case DeviceOrientation.FaceUp:
            case DeviceOrientation.FaceDown:
            case DeviceOrientation.Unknown:
                return acc.y;

            case DeviceOrientation.LandscapeRight:
                return -acc.y;

            case DeviceOrientation.Portrait:
                return acc.x;

            case DeviceOrientation.PortraitUpsideDown:
                return -acc.x;

            default:
                return acc.y;
        }
    }

    [ContextMenu("Recalibrar base para rotação local atual")]
    public void RecalibrarBase()
    {
        baseRotationLocal = pendulo.localRotation;
        currentAngle = 0f;
    }
}
