using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class BalanceController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Transform pendulo;
    [SerializeField] private Transform pratoEsquerda;
    [SerializeField] private Transform pratoDireita;

    [Header("Inclinação")]
    [SerializeField] public float maxAngle = 25f;
    [HideInInspector] public float externalAngleOffset = 0f;
    public float MaxAngle => maxAngle;
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private float deadZone = 0.03f;

    [Header("Ajustes de leitura")]
    [Tooltip("Troque para -1 se o lado estiver invertido")]
    [SerializeField] private float inputSign = 1f;
    [SerializeField] private float inputGain = 1f;

    [Header("Filtro Passa-Baixas (opcional)")]
    [SerializeField] private bool lowPassEnabled = false;
    [SerializeField, Min(0.001f)] private float lowPassHalfLife = 0.10f;

    [Header("Curva de Sensibilidade (opcional)")]
    [SerializeField] private bool sensitivityCurveEnabled = false;
    [SerializeField] private AnimationCurve sensitivityCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

    private Quaternion baseRotationLocal;
    private Quaternion pratoEsquerdaInicial;
    private Quaternion pratoDireitaInicial;

    private float currentAngle;
    private float targetAngle;
    private float filteredInput = 0f;
    private const float LN2 = 0.6931471805599453f;
    public float TotalAngle { get; private set; }

    private bool sensorPronto = false;
    private bool usandoNovoInput = false;
    private bool usandoFallback = false;
    private bool usandoGyro = false;
    private bool eixoInvertido = false;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator;
#endif

    // --- Novo: offset neutro de calibração
    private float neutralOffset = 0f;

    private void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        DetectarEixo();

        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                Debug.Log("[BalanceController] Vibrator inicializado.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BalanceController] Falha ao inicializar Vibrator: " + e.Message);
        }

        try
        {
            if (Accelerometer.current != null)
            {
                InputSystem.EnableDevice(Accelerometer.current);
                usandoNovoInput = true;
                sensorPronto = true;
                Debug.Log("[BalanceController] Usando Accelerometer.current (novo Input System).");
            }
            else if (SystemInfo.supportsAccelerometer)
            {
                usandoFallback = true;
                sensorPronto = true;
                Debug.Log("[BalanceController] Usando Input.acceleration (fallback).");
            }
            else if (SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true;
                usandoGyro = true;
                sensorPronto = true;
                Debug.Log("[BalanceController] Usando giroscópio como fallback final.");
            }
            else
            {
                Debug.LogWarning("[BalanceController] Nenhum sensor disponível!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BalanceController] Falha ao inicializar sensores: " + e.Message);
        }
#endif

        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }

    void Start()
    {
        if (pendulo == null) pendulo = transform;

        baseRotationLocal = pendulo.localRotation;
        if (pratoEsquerda != null) pratoEsquerdaInicial = pratoEsquerda.rotation;
        if (pratoDireita != null) pratoDireitaInicial = pratoDireita.rotation;

        currentAngle = 0f;
        filteredInput = 0f;
        TotalAngle = 0f;
        externalAngleOffset = 0f;

        StartCoroutine(CalibrarSensor());
    }

    // --- Calibra offset inicial do acelerômetro (mantém a balança nivelada)
    private IEnumerator CalibrarSensor()
    {
        yield return new WaitForSeconds(0.3f);

        float soma = 0f;
        int cont = 0;
        float dur = 0.4f;

        while (dur > 0f)
        {
            soma += LerTiltParaLandscapeLeft();
            cont++;
            dur -= Time.deltaTime;
            yield return null;
        }

        neutralOffset = (cont > 0) ? soma / cont : 0f;
        Debug.Log($"[BalanceController] Calibração concluída. Offset neutro = {neutralOffset:F3}");
    }

    void Update()
    {
        if (!sensorPronto) return;

        if (Input.deviceOrientation == DeviceOrientation.Portrait ||
            Input.deviceOrientation == DeviceOrientation.Unknown)
            Screen.orientation = ScreenOrientation.LandscapeLeft;

        float raw = LerTiltParaLandscapeLeft() - neutralOffset; // <-- usa offset calibrado
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

        float normalizedInput = Mathf.InverseLerp(-0.8f, 0.8f, use) * 2f - 1f;
        float processed = Mathf.Sign(normalizedInput) * Mathf.Pow(Mathf.Abs(normalizedInput), 1.2f);
        processed *= inputGain * inputSign;

        targetAngle = Mathf.Clamp(processed * maxAngle, -maxAngle, +maxAngle);
        currentAngle = Mathf.Lerp(currentAngle, targetAngle, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));

        float totalAngle = currentAngle + externalAngleOffset;
        TotalAngle = totalAngle;

        pendulo.localRotation = baseRotationLocal * Quaternion.AngleAxis(totalAngle, Vector3.right);
        AtualizarVibracao(totalAngle);
    }

    void LateUpdate()
    {
        if (pratoEsquerda != null) pratoEsquerda.rotation = pratoEsquerdaInicial;
        if (pratoDireita != null) pratoDireita.rotation = pratoDireitaInicial;
    }

    private float LerTiltParaLandscapeLeft()
    {
        Vector3 acc = Vector3.zero;

        if (usandoNovoInput && Accelerometer.current != null)
            acc = Accelerometer.current.acceleration.ReadValue();
        else if (usandoFallback)
            acc = Input.acceleration;
        else if (usandoGyro)
            acc = Input.gyro.gravity;

        return eixoInvertido ? acc.y : -acc.y;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void DetectarEixo()
    {
        try
        {
            using (var build = new AndroidJavaClass("android.os.Build"))
            {
                string modelo = build.GetStatic<string>("MODEL") ?? "Unknown";
                string versao = build.GetStatic<string>("VERSION_RELEASE") ?? "Unknown";

                if (versao.StartsWith("15") || versao.StartsWith("16") ||
                    modelo.ToLower().Contains("edge") || modelo.ToLower().Contains("s24") ||
                    modelo.ToLower().Contains("motorola"))
                {
                    eixoInvertido = true;
                    Debug.Log($"[BalanceController] Eixo ajustado (Y) para {modelo} / Android {versao}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BalanceController] Falha ao detectar modelo Android: {e.Message}");
        }
    }

    // --- vibração contínua adaptativa (corrigida para resposta mais rápida)
    private float ultimaIntensidade = 0f;
    private bool vibrando = false;
    private float tempoUltimaAtualizacao = 0f;

    private void AtualizarVibracao(float angulo)
    {
        if (vibrator == null) return;

        float intensidade = Mathf.Clamp01(Mathf.Abs(angulo) / maxAngle);

        // só envia atualização se a diferença for perceptível
        if (Mathf.Abs(intensidade - ultimaIntensidade) > 0.05f || Time.time - tempoUltimaAtualizacao > 0.25f)
        {
            if (intensidade <= 0.05f)
            {
                if (vibrando)
                {
                    try { vibrator.Call("cancel"); } catch { }
                    vibrando = false;
                }
                ultimaIntensidade = 0f;
                return;
            }

            long duracao = 80;
            int amplitude = Mathf.RoundToInt(50 + intensidade * 205);

            try
            {
                using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", duracao, amplitude);
                    vibrator.Call("vibrate", effect);
                }
                vibrando = true;
            }
            catch
            {
                vibrator.Call("vibrate", 100);
                vibrando = true;
            }

            ultimaIntensidade = intensidade;
            tempoUltimaAtualizacao = Time.time;
        }
    }
#else
    private void AtualizarVibracao(float angulo)
    {
        float intensidade = Mathf.Clamp01(Mathf.Abs(angulo) / maxAngle);
        if (intensidade > 0.05f)
            Debug.Log($"[Simulação Vibracao Contínua] intensidade={intensidade:F2}");
    }
#endif

    [ContextMenu("Recalibrar base para rotação local atual")]
    public void RecalibrarBase()
    {
        baseRotationLocal = pendulo.localRotation;
        currentAngle = 0f;
        StartCoroutine(CalibrarSensor()); // adiciona recalibração do sensor também
    }
}
