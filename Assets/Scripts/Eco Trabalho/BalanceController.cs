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
    private bool modoWaveformDisponivel = false;   // createWaveform com amplitudes
    private bool hasAmplitudeControl = false;      // suporte real de amplitude
    private int sdkInt = 0;
#endif

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

                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    sdkInt = version.GetStatic<int>("SDK_INT");
                }

                // Android 12+ prefere VibratorManager
                if (sdkInt >= 31)
                {
                    AndroidJavaObject vibManager =
                        activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                    if (vibManager != null)
                        vibrator = vibManager.Call<AndroidJavaObject>("getDefaultVibrator");
                    if (vibrator == null) // fallback
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                else
                {
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (vibrator != null)
                    Debug.Log("[BalanceController] Vibrator inicializado corretamente.");
                else
                    Debug.LogWarning("[BalanceController] Vibrator retornou null.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BalanceController] Falha ao inicializar Vibrator: " + e.Message);
        }

        // Capacidades do vibrador
        try
        {
            if (vibrator != null)
            {
                try { hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl"); } catch { hasAmplitudeControl = false; }
                // createWaveform(long[], int[], int) existe desde API 26; assume disponível se temos vibrator
                modoWaveformDisponivel = (sdkInt >= 26);
                Debug.Log($"[BalanceController] SDK={sdkInt} hasAmp={hasAmplitudeControl} waveform={modoWaveformDisponivel}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BalanceController] Capabilities check failed: " + e.Message);
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

        float raw = LerTiltParaLandscapeLeft() - neutralOffset;
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

        // Landscape Left → eixo horizontal principal é X
        float eixo = eixoInvertido ? acc.x : -acc.x;
        return eixo;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void DetectarEixo()
    {
        try
        {
            using (var build = new AndroidJavaClass("android.os.Build"))
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                string modelo = build.GetStatic<string>("MODEL") ?? "Unknown";
                string versao = version.GetStatic<string>("RELEASE") ?? "Unknown";

                if (versao.StartsWith("15") || versao.StartsWith("16") ||
                    modelo.ToLower().Contains("edge") || modelo.ToLower().Contains("s24") ||
                    modelo.ToLower().Contains("motorola"))
                {
                    eixoInvertido = true;
                    Debug.Log($"[BalanceController] Eixo ajustado (X) para {modelo} / Android {versao}");
                }

                // Ajuste fino específico (se quiser manter)
                if (modelo.ToLower().Contains("edge 60"))
                {
                    neutralOffset = 0.06f;
                    Debug.Log("[BalanceController] Offset neutro ajustado +0.06f para Motorola Edge 60 Pro");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BalanceController] Falha ao detectar modelo Android: {e.Message}");
        }
    }

    // --- Vibração contínua com intensidade variável (com fallback PWM) ---
    private float ultimaIntensidade = 0f;
    private bool vibrando = false;
    private bool usandoWaveform = false;
    private float tempoUltimaAtualizacao = 0f;

    // parâmetros do loop tátil
    private const float MIN_DELTA_INTENSIDADE = 0.06f;   // mudança mínima para atualizar
    private const float MIN_INTERVALO_REAPLICAR = 0.18f; // s — mantém “contínuo”, sem spam

    private void AtualizarVibracao(float angulo)
    {
        if (vibrator == null) return;

        float intensidade = Mathf.Clamp01(Mathf.Abs(angulo) / maxAngle);

        // zona morta para vibração
        if (intensidade <= 0.05f)
        {
            if (vibrando)
            {
                try { vibrator.Call("cancel"); } catch { }
                vibrando = false;
                usandoWaveform = false;
            }
            ultimaIntensidade = 0f;
            return;
        }

        // aplica somente quando muda o suficiente ou passou um período
        if (Mathf.Abs(intensidade - ultimaIntensidade) < MIN_DELTA_INTENSIDADE &&
            (Time.time - tempoUltimaAtualizacao) < MIN_INTERVALO_REAPLICAR)
        {
            return;
        }

        try
        {
            bool hasVibrator = vibrator.Call<bool>("hasVibrator");
            if (!hasVibrator) return;

            if (modoWaveformDisponivel)
            {
                if (hasAmplitudeControl)
                {
                    // Waveform com amplitude dinâmica (contínuo)
                    // Padrão: [0ms start, vibra Dv ms, pausa Dp ms], amplitudes [0, amp, 0], repete a partir do índice 0.
                    long vibraMs = Mathf.RoundToInt(Mathf.Lerp(80f, 160f, intensidade));  // pulsos mais longos com mais inclinação
                    long pausaMs = Mathf.RoundToInt(Mathf.Lerp(80f, 30f, intensidade));   // pausas menores com mais inclinação
                    int amp = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(60f, 255f, intensidade)), 1, 255);

                    using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        long[] timings = new long[] { 0, vibraMs, pausaMs };
                        int[] amps = new int[] { 0, amp, 0 };

                        AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createWaveform", timings, amps, 0 // repeatIndex = 0 (loop)
                        );

                        // Alguns devices suportam hint (nem todos)
                        try { effect.Call("setUsageHint", 2); } catch { /* ignore */ }

                        vibrator.Call("vibrate", effect);
                    }

                    usandoWaveform = true;
                    vibrando = true;
                }
                else
                {
                    // Sem amplitude control real → PWM com oneShot em loop (curto)
                    VibrarOneShotPWM(intensidade);
                }
            }
            else
            {
                // API antiga → PWM por oneShot
                VibrarOneShotPWM(intensidade);
            }

            ultimaIntensidade = intensidade;
            tempoUltimaAtualizacao = Time.time;
        }
        catch (System.Exception)
        {
            // Último fallback
            VibrarOneShotPWM(intensidade);
            ultimaIntensidade = intensidade;
            tempoUltimaAtualizacao = Time.time;
        }
    }

    // PWM por oneShot para simular “força”: duração/pausa variam conforme intensidade
    private void VibrarOneShotPWM(float intensidade)
    {
        try
        {
            long duracao = Mathf.RoundToInt(Mathf.Lerp(40f, 160f, intensidade));
            // dispara um pulso; na próxima atualização (MIN_INTERVALO_REAPLICAR) chamamos de novo
            int amplitude = hasAmplitudeControl
                ? Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(80f, 255f, intensidade)), 1, 255)
                : -1; // default amplitude

            using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                int defaultAmp = vibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE");
                AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot", duracao, (hasAmplitudeControl ? amplitude : defaultAmp)
                );
                vibrator.Call("vibrate", effect);
            }

            vibrando = true;
            usandoWaveform = false; // estamos em modo PWM/oneShot
        }
        catch
        {
            // Ultimate fallback simples
            try { vibrator.Call("vibrate", 100); } catch { }
            vibrando = true;
            usandoWaveform = false;
        }
    }
#else
    private void AtualizarVibracao(float angulo)
    {
        float intensidade = Mathf.Clamp01(Mathf.Abs(angulo) / maxAngle);
        if (intensidade > 0.05f)
            Debug.Log($"[Simulação Vibracao] intensidade={intensidade:F2}");
    }
#endif

    [ContextMenu("Recalibrar base para rotação local atual")]
    public void RecalibrarBase()
    {
        baseRotationLocal = pendulo.localRotation;
        currentAngle = 0f;
        StartCoroutine(CalibrarSensor());
    }
}
