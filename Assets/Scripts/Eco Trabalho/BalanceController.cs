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
    [HideInInspector] public float externalAngleOffset = 0f; // legado (não usado para somar ângulo)
    public float MaxAngle => maxAngle;
    [SerializeField] private float smoothSpeed = 8f;

    [Tooltip("Zona morta do sinal bruto (acelerômetro/teclado normalizado)")]
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

    [Header("Dinâmica da Balança (massa-mola-amortecimento)")]
    [SerializeField] private bool enableMomentum = true;
    [SerializeField] private float springK = 30f;   // torque do jogador em direção ao target
    [SerializeField] private float dampingC = 8f;   // amortecimento

    [Header("Torque do Peso (competição real)")]
    [Tooltip("Ganho de torque do peso (deg -> torque). Aumente para a carga ‘puxar’ mais.")]
    [SerializeField] private float weightTorqueK = 1.2f;
    [Tooltip("Limita o efeito do peso em graus equivalentes (após somas internas).")]
    [SerializeField] private float weightBiasClampDeg = 25f;

    [Header("Desktop Input (Editor/PC)")]
    [SerializeField] private bool enableDesktopInput = true;
    [SerializeField] private float desktopStepPerSecond = 0.9f;
    [SerializeField] private float desktopDecayPerSecond = 0.7f;
    [SerializeField] private float desktopMaxVirtualTilt = 1.0f;

    [Header("Penalidade de ajuste rápido (Mobile)")]
    [SerializeField] private bool quickBalancePenalty = true;
    [Tooltip("Variação do sinal (normalizado) por segundo que dispara penalidade")]
    [SerializeField] private float inputRateThreshold = 1.5f;
    [Tooltip("Grau do 'tranco' aplicado contra a correção")]
    [SerializeField] private float penaltyKickDeg = 4f;
    [Tooltip("Cooldown em segundos entre kicks")]
    [SerializeField] private float penaltyCooldown = 0.20f;

    private Quaternion baseRotationLocal;
    private Quaternion pratoEsquerdaInicial;
    private Quaternion pratoDireitaInicial;

    private float currentAngle;     // estado (graus)
    private float targetAngle;      // alvo do jogador (graus)
    private float angleVel;         // vel. angular (graus/s)
    private float filteredInput = 0f;
    private const float LN2 = 0.6931471805599453f;
    public float TotalAngle { get; private set; }

    private bool sensorPronto = false;
    private bool usandoNovoInput = false;
    private bool usandoFallback = false;
    private bool usandoGyro = false;
    private bool eixoInvertido = false;

    private float neutralOffset = 0f;

    // Controle “virtual” do PC
    private float desktopTilt = 0f;

    // Penalidade
    private float prevProcessed = 0f;
    private float penaltyTimer = 0f;

    // === NOVO: viés de peso em graus (escrito pelo GerenciadorDePeso) ===
    [HideInInspector] public float weightBiasDeg = 0f; // (+) puxa para um lado, (-) para o outro

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator;
    private bool modoWaveformDisponivel = false;
    private bool hasAmplitudeControl = false;
    private int sdkInt = 0;
    private bool vibAtiva = true;
#endif

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

                if (sdkInt >= 31)
                {
                    AndroidJavaObject vibManager =
                        activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                    if (vibManager != null)
                        vibrator = vibManager.Call<AndroidJavaObject>("getDefaultVibrator");
                    if (vibrator == null)
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

        try
        {
            if (vibrator != null)
            {
                try { hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl"); } catch { hasAmplitudeControl = false; }
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
        angleVel = 0f;
        filteredInput = 0f;
        TotalAngle = 0f;
        externalAngleOffset = 0f;
        weightBiasDeg = 0f;

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
        if (Input.deviceOrientation == DeviceOrientation.Portrait ||
            Input.deviceOrientation == DeviceOrientation.Unknown)
            Screen.orientation = ScreenOrientation.LandscapeLeft;

        float dt = Mathf.Max(Time.deltaTime, 0.00001f);

        // -------- 1) Sinal de controle do jogador (-1..+1) --------
        float controlSignal = 0f;
        bool isMobile = Application.isMobilePlatform;

        if (isMobile && sensorPronto)
        {
            float raw = LerTiltParaLandscapeLeft() - neutralOffset;
            float use = raw;

            if (lowPassEnabled)
            {
                float alpha = 1f - Mathf.Exp(-LN2 * dt / Mathf.Max(0.0001f, lowPassHalfLife));
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
            controlSignal = Mathf.Sign(normalizedInput) * Mathf.Pow(Mathf.Abs(normalizedInput), 1.2f);
            controlSignal *= inputGain * inputSign;
        }
        else if (enableDesktopInput)
        {
            float dir = 0f;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) dir -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) dir += 1f;
            }

            desktopTilt += dir * desktopStepPerSecond * dt;
            if (Mathf.Approximately(dir, 0f))
                desktopTilt = Mathf.MoveTowards(desktopTilt, 0f, desktopDecayPerSecond * dt);

            desktopTilt = Mathf.Clamp(desktopTilt, -desktopMaxVirtualTilt, desktopMaxVirtualTilt);

            float use = desktopTilt;
            if (Mathf.Abs(use) < deadZone) use = 0f;
            if (sensitivityCurveEnabled && sensitivityCurve != null)
            {
                float clamped = Mathf.Clamp(use, -1f, 1f);
                use = Mathf.Clamp(sensitivityCurve.Evaluate(clamped), -1f, 1f);
            }
            controlSignal = use * inputGain * inputSign;
        }
        else
        {
            controlSignal = 0f;
        }

        // -------- 2) Converte controle do jogador para alvo em graus --------
        targetAngle = Mathf.Clamp(controlSignal * maxAngle, -maxAngle, +maxAngle);

        // -------- 3) Penalidade rápida (opcional) --------
        if (isMobile && quickBalancePenalty)
        {
            float rate = Mathf.Abs((controlSignal - prevProcessed) / dt);
            if (penaltyTimer > 0f) penaltyTimer -= dt;

            if (rate > inputRateThreshold && penaltyTimer <= 0f)
            {
                float kickDir = Mathf.Sign(prevProcessed - controlSignal);
                weightBiasDeg = Mathf.Clamp(
                    weightBiasDeg + kickDir * penaltyKickDeg,
                    -weightBiasClampDeg, +weightBiasClampDeg
                );
                penaltyTimer = penaltyCooldown;
            }
        }
        prevProcessed = controlSignal;

        // -------- 4) Dinâmica: torques do jogador + do peso --------
        float clampedWeightBias = Mathf.Clamp(weightBiasDeg, -weightBiasClampDeg, +weightBiasClampDeg);

        if (enableMomentum)
        {
            // Torque total = springK*(target - current) + weightTorqueK*(weightBiasDeg) - damping*vel
            float torquePlayer = springK * (targetAngle - currentAngle);
            float torqueWeight = weightTorqueK * clampedWeightBias;   // torque constante do peso
            float accel = torquePlayer + torqueWeight - dampingC * angleVel;

            angleVel += accel * dt;
            currentAngle += angleVel * dt;
        }
        else
        {
            // fallback simples pro jogador
            currentAngle = Mathf.Lerp(currentAngle, targetAngle, 1f - Mathf.Exp(-smoothSpeed * dt));
            // ainda aplicamos leve drift do peso para não “morrer”
            currentAngle += Mathf.Clamp(weightTorqueK * clampedWeightBias, -maxAngle, maxAngle) * dt * 0.02f;
        }

        // -------- 5) Clamp de segurança e rotação --------
        currentAngle = Mathf.Clamp(currentAngle, -maxAngle, +maxAngle);
        TotalAngle = currentAngle;

        // pêndulo neutro em X = -90: rotaciona relativo ao baseRotationLocal
        pendulo.localRotation = baseRotationLocal * Quaternion.AngleAxis(TotalAngle, Vector3.right);

        // -------- 6) Vibração (Android) --------
        AtualizarVibracao(TotalAngle);
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

    private const float MIN_DELTA_INTENSIDADE = 0.06f;
    private const float MIN_INTERVALO_REAPLICAR = 0.18f;

    private void AtualizarVibracao(float angulo)
    {
        if (!vibAtiva) return;
        if (vibrator == null) return;

        float intensidade = Mathf.Clamp01(Mathf.Abs(angulo) / maxAngle);

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
                    long vibraMs = Mathf.RoundToInt(Mathf.Lerp(80f, 160f, intensidade));
                    long pausaMs = Mathf.RoundToInt(Mathf.Lerp(80f, 30f, intensidade));
                    int amp = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(60f, 255f, intensidade)), 1, 255);

                    using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        long[] timings = new long[] { 0, vibraMs, pausaMs };
                        int[] amps = new int[] { 0, amp, 0 };

                        AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createWaveform", timings, amps, 0
                        );
                        try { effect.Call("setUsageHint", 2); } catch { }
                        vibrator.Call("vibrate", effect);
                    }

                    usandoWaveform = true;
                    vibrando = true;
                }
                else
                {
                    VibrarOneShotPWM(intensidade);
                }
            }
            else
            {
                VibrarOneShotPWM(intensidade);
            }

            ultimaIntensidade = intensidade;
            tempoUltimaAtualizacao = Time.time;
        }
        catch (System.Exception)
        {
            VibrarOneShotPWM(intensidade);
            ultimaIntensidade = intensidade;
            tempoUltimaAtualizacao = Time.time;
        }
    }

    private void VibrarOneShotPWM(float intensidade)
    {
        try
        {
            long duracao = Mathf.RoundToInt(Mathf.Lerp(40f, 160f, intensidade));
            int amplitude = hasAmplitudeControl
                ? Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(80f, 255f, intensidade)), 1, 255)
                : -1;

            using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                int defaultAmp = vibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE");
                AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot", duracao, (hasAmplitudeControl ? amplitude : defaultAmp)
                );
                vibrator.Call("vibrate", effect);
            }

            vibrando = true;
            usandoWaveform = false;
        }
        catch
        {
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
        angleVel = 0f;
        StartCoroutine(CalibrarSensor());
    }

    // pausa/cancelamento de vibração quando sai da tela, perde foco ou fecha
    void OnApplicationPause(bool paused)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        vibAtiva = !paused;
        if (paused && vibrator != null)
        {
            try { vibrator.Call("cancel"); } catch { }
        }
#endif
    }

    void OnApplicationFocus(bool focus)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        vibAtiva = focus;
        if (!focus && vibrator != null)
        {
            try { vibrator.Call("cancel"); } catch { }
        }
#endif
    }

    void OnApplicationQuit()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        vibAtiva = false;
        if (vibrator != null)
        {
            try { vibrator.Call("cancel"); } catch { }
        }
#endif
    }
}
