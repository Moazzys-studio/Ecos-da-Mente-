using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Entrada de balanço com inicialização universal para Android (Samsung / Motorola / Xiaomi)
/// e fallback para PC (teclado). Usa: Accelerometer (novo Input System) → GravitySensor →
/// Input.acceleration (legado) → Gyro.attitude (roll) como último recurso.
/// </summary>
[DisallowMultipleComponent]
public class BalanceInput : MonoBehaviour
{
    // ===================== CONFIG GERAL =====================
    [Header("Inclinação / Normalização")]
    [Tooltip("Ângulo máximo (graus) usado para normalizar a saída em [-1..+1].")]
    [SerializeField, Min(1f)] private float maxAngulo = 25f;
    public float MaxAngle => maxAngulo;

    [Header("Pré-processamento")]
    [Tooltip("Zona morta (fração). 0.05 = 5% do total.")]
    [SerializeField, Range(0f, 0.5f)] private float zonaMortaFracao = 0.05f;

    [Tooltip("Troque para -1 se a direção estiver invertida.")]
    [SerializeField] private float sinalInput = 1f;

    [Tooltip("Ganho aplicado depois da normalização.")]
    [SerializeField, Min(0f)] private float ganhoInput = 1f;

    [Header("Filtro Passa-Baixa (opcional)")]
    [SerializeField] private bool passaBaixaAtivo = false;
    [SerializeField, Min(0.001f), Tooltip("Half-life (s) do filtro de suavização.")]
    private float passaBaixaHalfLife = 0.10f;

    [Header("Curva de Sensibilidade (opcional)")]
    [SerializeField] private bool curvaSensibilidadeAtiva = false;
    [SerializeField] private AnimationCurve curvaSensibilidade = AnimationCurve.Linear(0, 0, 1, 1);

    // ===================== DESKTOP =====================
    [Header("Desktop (Editor/PC)")]
    [SerializeField] private bool desktopHabilitado = true;
    [SerializeField, Min(0f)] private float desktopStepPorSegundo = 1.0f;
    [SerializeField, Min(0f)] private float desktopDecayPorSegundo = 0.8f;
    [SerializeField, Range(0.1f, 1.0f)] private float desktopMaxTiltVirtual = 1.0f;

    // ===================== ANDROID / SENSORES =====================
    [Header("Android / Sensores")]
    [Tooltip("Força o uso do acelerômetro no Android mesmo se desktop estiver ativo.")]
    [SerializeField] private bool forcarAcelerometroNoAndroid = true;

    [Tooltip("Se o Accelerometer do novo Input System não existir, cai para Input.acceleration.")]
    [SerializeField] private bool usarLegadoSeNovoIndisponivel = true;

    [Tooltip("Usa o giroscópio como último fallback (roll do attitude).")]
    [SerializeField] private bool usarGiroscopioComoFallback = true;

    [Tooltip("Ganho extra pós-normalização para o sensor do Android.")]
    [SerializeField] private float ganhoSensor = 1.0f;

    [Tooltip("Executa calibração automática ao iniciar (offset neutro).")]
    [SerializeField] private bool calibrarAoIniciar = true;

    [Tooltip("Tempo (s) para amostrar o offset neutro.")]
    [SerializeField, Min(0.05f)] private float tempoCalibracao = 0.5f;

    [Header("Perfis por fabricante (opcional)")]
    [Tooltip("Aplica inversão adicional de eixo em Samsung.")]
    [SerializeField] private bool samsungFlipEixo = false;

    [Tooltip("Aplica inversão adicional de eixo em Motorola.")]
    [SerializeField] private bool motorolaFlipEixo = false;

    [Tooltip("Aplica inversão adicional de eixo em Xiaomi.")]
    [SerializeField] private bool xiaomiFlipEixo = false;

    // ===================== ESTADO / SAÍDAS =====================
    /// <summary>Ângulo atual em graus (clamp em ±MaxAngle), útil para debug/overlay.</summary>
    public float CurrentAngleDeg { get; private set; }

    float _offsetNeutro;      // offset após calibração (Android)
    float _desktopTilt;       // acumulador do modo desktop (−1..+1)

#if UNITY_ANDROID && !UNITY_EDITOR
    int _zerosSeguidos = 0;   // diagnóstico: leituras zeradas seguidas
#endif

    // ===================== CICLO DE VIDA =====================
    void Awake()
    {
        // Frame rate razoável para suavidade de leitura
        if (Application.targetFrameRate < 60) Application.targetFrameRate = 60;

        // Compensação de sensores do Unity (ajusta eixos conforme rotação telas)
        Input.compensateSensors = true;

        // Habilita giroscópio (fallback comum que resolve em vários Samsung/Motorola/Xiaomi)
        if (!Input.gyro.enabled) Input.gyro.enabled = true;

        // Habilita explicitamente o Accelerometer do novo Input System (se existir)
        TryEnableAccelerometer();

        // Detecta fabricante para aplicar flip opcional (pode ajustar no inspetor)
        string model = SystemInfo.deviceModel?.ToLower() ?? "";
        if (model.Contains("samsung")) samsungFlipEixo = samsungFlipEixo || false; // deixe como está se já marcado
        if (model.Contains("motorola") || model.Contains("moto")) motorolaFlipEixo = motorolaFlipEixo || false;
        if (model.Contains("xiaomi") || model.Contains("redmi") || model.Contains("mi ")) xiaomiFlipEixo = xiaomiFlipEixo || false;

        if (calibrarAoIniciar)
            StartCoroutine(CalibrarSensorUniversal());
    }

    void OnEnable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        InputSystem.onDeviceChange += OnDeviceChange;
#endif
        TryEnableAccelerometer();
    }

    void OnDisable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        InputSystem.onDeviceChange -= OnDeviceChange;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnDeviceChange(InputDevice dev, InputDeviceChange change)
    {
        if (dev is Accelerometer && (change == InputDeviceChange.Added || change == InputDeviceChange.Enabled))
            TryEnableAccelerometer();
    }
#endif

    private void TryEnableAccelerometer()
    {
        try
        {
            if (Accelerometer.current != null && !Accelerometer.current.enabled)
            {
                InputSystem.EnableDevice(Accelerometer.current);
                Debug.Log("[BalanceInput] Accelerometer habilitado (Input System).");
            }
        }
        catch { /* fallback cuidará */ }
    }

    // ===================== CALIBRAÇÃO =====================
    IEnumerator CalibrarSensorUniversal()
    {
        // Espera 1 frame para estabilizar orientação
        yield return null;
        yield return new WaitForEndOfFrame();

        // Aguarda Landscape se possível (timeout curto)
        float timeout = 0.8f;
        while (timeout > 0f &&
               Screen.orientation != ScreenOrientation.LandscapeLeft &&
               Screen.orientation != ScreenOrientation.LandscapeRight)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // Amostra offset médio
        float soma = 0f; int cont = 0;
        float dur = Mathf.Max(0.1f, tempoCalibracao);
        while (dur > 0f)
        {
            soma += LerTiltNormalizadoUniversal();
            cont++;
            dur -= Time.deltaTime;
            yield return null;
        }

        _offsetNeutro = (cont > 0) ? soma / cont : 0f;
        Debug.Log($"[BalanceInput] Calibração ok. Offset neutro = {_offsetNeutro:F3}, orient={Screen.orientation}, modelo={SystemInfo.deviceModel}");
    }

    [ContextMenu("Recalibrar agora")]
    public void RecalibrarAgora()
    {
        StopAllCoroutines();
        StartCoroutine(CalibrarSensorUniversal());
    }

    // ===================== API PRINCIPAL =====================
    /// <summary>
    /// Processa o input e retorna:
    /// controlSignal ∈ [-1..+1], penaltyKickDir ∈ {-1,0,+1} (mantido p/ compatibilidade)
    /// </summary>
    public (float controlSignal, float penaltyKickDir) ComputeControl(float dt)
    {
        float alvoNorm = 0f;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (forcarAcelerometroNoAndroid || !desktopHabilitado)
        {
            float tilt = LerTiltNormalizadoUniversal() - _offsetNeutro;
            alvoNorm = Mathf.Clamp(tilt * ganhoSensor, -1f, 1f);

            if (Mathf.Approximately(tilt, 0f)) _zerosSeguidos++;
            else _zerosSeguidos = 0;

            if (_zerosSeguidos > 120) // ~2 s a 60 fps
            {
                Debug.LogWarning("[BalanceInput] Leituras ~0 por muito tempo. Checar: orientação travada? accelerometer habilitado? gyro ativo?");
                _zerosSeguidos = 0;
            }
        }
#endif

#if UNITY_EDITOR || UNITY_STANDALONE
        if (desktopHabilitado)
        {
            float dir = 0f;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) dir -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) dir += 1f;
            }

            _desktopTilt += dir * desktopStepPorSegundo * dt;
            if (Mathf.Approximately(dir, 0f))
                _desktopTilt = Mathf.MoveTowards(_desktopTilt, 0f, desktopDecayPorSegundo * dt);

            _desktopTilt = Mathf.Clamp(_desktopTilt, -desktopMaxTiltVirtual, desktopMaxTiltVirtual);
            float use = _desktopTilt;

            if (Mathf.Abs(use) < zonaMortaFracao) use = 0f;

            if (curvaSensibilidadeAtiva && curvaSensibilidade != null)
            {
                float s = Mathf.Sign(use);
                float m = Mathf.Clamp01(Mathf.Abs(use));
                float g = Mathf.Clamp01(curvaSensibilidade.Evaluate(m));
                use = s * g;
            }

            alvoNorm = use;
        }
#endif

        // Pré-processamento comum
        float control = Mathf.Clamp(alvoNorm * ganhoInput * Mathf.Sign(sinalInput), -1f, 1f);
        float angAlvoGraus = control * maxAngulo;

        if (passaBaixaAtivo)
        {
            float lambda = Mathf.Log(2f) / Mathf.Max(0.001f, passaBaixaHalfLife);
            float a = 1f - Mathf.Exp(-lambda * dt);
            CurrentAngleDeg = Mathf.Lerp(CurrentAngleDeg, angAlvoGraus, a);
        }
        else
        {
            CurrentAngleDeg = angAlvoGraus;
        }

        // Mantemos penaltyKickDir = 0 (se você usava, pode reimplementar rápido aqui)
        return (control, 0f);
    }

    // ===================== LEITURA UNIVERSAL =====================
    /// <summary>
    /// Lê tilt lateral normalizado para um referencial Landscape "canônico".
    /// Ordem: Accelerometer (novo) → GravitySensor → Input.acceleration (legado) → Gyro.roll.
    /// Aplica flips opcionais por fabricante e corrige LandscapeRight.
    /// </summary>
    private float LerTiltNormalizadoUniversal()
    {
        // 1) Novo Input System: Accelerometer
        Vector3 a = Vector3.zero;
        bool temAccelNovo = (Accelerometer.current != null && Accelerometer.current.enabled);

        if (temAccelNovo)
        {
            a = Accelerometer.current.acceleration.ReadValue();
        }
        else
        {
            // 2) GravitySensor (se existir): menos ruído (remove componente linear)
#if UNITY_ANDROID && !UNITY_EDITOR
            if (UnityEngine.InputSystem.GravitySensor.current != null &&
                UnityEngine.InputSystem.GravitySensor.current.enabled)
            {
                a = UnityEngine.InputSystem.GravitySensor.current.gravity.ReadValue();
            }
            else
#endif
            {
                // 3) Legado
                if (usarLegadoSeNovoIndisponivel)
                    a = Input.acceleration;
            }
        }

        float lateral;
        ScreenOrientation o = Screen.orientation;

        // Se não temos nada concreto (alguns devices), tenta gyro como último recurso
        if (usarGiroscopioComoFallback && a == Vector3.zero && Input.gyro.enabled)
        {
            // Roll (em radianos) do quaternion de atitude
            // Referência: roll sobre o eixo Z no espaço do dispositivo
            Quaternion q = Input.gyro.attitude;
            // Converter para euler “screen space”: Unity no Android pode precisar ajustar
            Vector3 euler = q.eulerAngles;
            float roll = euler.z; // graus (0..360)
            if (roll > 180f) roll -= 360f; // (-180..+180)

            float norm = Mathf.Clamp(roll / maxAngulo, -1f, 1f);
            lateral = norm;
        }
        else
        {
            // Mapeia aceleração para eixo lateral conforme orientação
            switch (o)
            {
                case ScreenOrientation.LandscapeLeft:
                    lateral = a.x;
                    break;
                case ScreenOrientation.LandscapeRight:
                    lateral = -a.x; // inverte para ficar no mesmo referencial
                    break;
                case ScreenOrientation.Portrait:
                    lateral = -a.y; // gira 90°
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    lateral = a.y;  // gira 90° invertido
                    break;
                default:
                    lateral = a.x;  // assume LandscapeLeft
                    break;
            }

            // Flips por fabricante (se marcado no inspetor)
            if (samsungFlipEixo && SystemInfo.deviceModel.ToLower().Contains("samsung"))
                lateral = -lateral;
            if (motorolaFlipEixo && (SystemInfo.deviceModel.ToLower().Contains("motorola") || SystemInfo.deviceModel.ToLower().Contains("moto")))
                lateral = -lateral;
            if (xiaomiFlipEixo && (SystemInfo.deviceModel.ToLower().Contains("xiaomi") || SystemInfo.deviceModel.ToLower().Contains("redmi") || SystemInfo.deviceModel.ToLower().Contains("mi ")))
                lateral = -lateral;

            lateral = Mathf.Clamp(lateral, -1f, 1f);
        }

        return lateral;
    }
}
