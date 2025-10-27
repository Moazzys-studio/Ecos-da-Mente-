using System.Collections;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Camada de apresentação/IO do dispositivo:
/// - Aplica rotação no pêndulo (com base local salva).
/// - Restaura rotação dos pratos no LateUpdate.
/// - Garante LandscapeLeft.
/// - Vibração no Android com fallback.
/// </summary>
[DisallowMultipleComponent]
public class BalanceController : MonoBehaviour
{
    [Header("Referências de Cena")]
    [Tooltip("Transform do pêndulo que deve girar no eixo X.")]
    [SerializeField] private Transform pendulo;

    [Tooltip("Prato esquerdo; a rotação global é restaurada todo LateUpdate.")]
    [SerializeField] private Transform pratoEsquerda;

    [Tooltip("Prato direito; a rotação global é restaurada todo LateUpdate.")]
    [SerializeField] private Transform pratoDireita;


    // --- Estado de rotação salvo ---
    private Quaternion baseRotationLocal;
    private Quaternion pratoEsquerdaInicial;
    private Quaternion pratoDireitaInicial;

#if UNITY_ANDROID && !UNITY_EDITOR
    // Vibração
    private AndroidJavaObject vibrator;
    private bool modoWaveformDisponivel = false;
    private bool hasAmplitudeControl = false;
    private int sdkInt = 0;
    private bool vibAtiva = true;

    private float ultimaIntensidade = 0f;
    private bool vibrando = false;
    private bool usandoWaveform = false;
    private float tempoUltimaAtualizacao = 0f;

    private const float MIN_DELTA_INTENSIDADE = 0.06f;
    private const float MIN_INTERVALO_REAPLICAR = 0.18f;
#endif

    private void Awake()
    {
        if (pendulo == null) pendulo = transform;

        baseRotationLocal = pendulo.localRotation;
        if (pratoEsquerda != null) pratoEsquerdaInicial = pratoEsquerda.rotation;
        if (pratoDireita != null) pratoDireitaInicial = pratoDireita.rotation;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Inicialização do Vibrator (preservado do original)
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
#endif
        // Forçar LandscapeLeft
        EnforceLandscapeLeft();
    }

    private void Start()
    {
        // Recalibra base se necessário (mantido equivalente ao original)
        StartCoroutine(_PostStartCalib());
    }

    private IEnumerator _PostStartCalib()
    {
        // nada obrigatório aqui; deixado para simetria com versões antigas
        yield return null;
    }

    /// <summary>
    /// Força a orientação LandscapeLeft e desabilita rotações indesejadas.
    /// </summary>
    public void EnforceLandscapeLeft()
    {
        if (Input.deviceOrientation == DeviceOrientation.Portrait ||
            Input.deviceOrientation == DeviceOrientation.Unknown)
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToLandscapeLeft = true;
    }

    /// <summary>
    /// Aplica o ângulo final vindo da mecânica, rotaciona o pêndulo
    /// e atualiza a vibração de acordo com a intensidade.
    /// </summary>
    public void ApplyAngle(float totalAngleDeg, float referenceMaxAngle)
    {
        pendulo.localRotation = baseRotationLocal * Quaternion.AngleAxis(totalAngleDeg, Vector3.right);
        AtualizarVibracao(totalAngleDeg, referenceMaxAngle);
    }

    private void LateUpdate()
    {
        // Restaura rotação global dos pratos (igual ao original)
        if (pratoEsquerda != null) pratoEsquerda.rotation = pratoEsquerdaInicial;
        if (pratoDireita != null) pratoDireita.rotation = pratoDireitaInicial;
    }

    [ContextMenu("Recalibrar base para rotação local atual")]
    public void RecalibrarBase()
    {
        baseRotationLocal = pendulo.localRotation;
    }

    // ---------------- Vibração ----------------
    private void AtualizarVibracao(float angulo, float refMax)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator == null || !vibAtiva) return;

        float intensidade = Mathf.Clamp01(Mathf.Abs(angulo) / Mathf.Max(0.0001f, refMax));

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
            bool hasVib = vibrator.Call<bool>("hasVibrator");
            if (!hasVib) return;

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
        catch
        {
            VibrarOneShotPWM(intensidade);
            ultimaIntensidade = intensidade;
            tempoUltimaAtualizacao = Time.time;
        }
#else
        // Editor / Standalone: apenas loga a intensidade para depuração
        float intensidade = Mathf.Clamp01(Mathf.Abs(angulo) / Mathf.Max(0.0001f, refMax));
        
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
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

    // Pausa/cancelamento de vibração quando sai da tela, perde foco ou fecha
    private void OnApplicationPause(bool paused)
    {
        vibAtiva = !paused;
        if (paused && vibrator != null)
        {
            try { vibrator.Call("cancel"); } catch { }
        }
    }

    private void OnApplicationFocus(bool focus)
    {
        vibAtiva = focus;
        if (!focus && vibrator != null)
        {
            try { vibrator.Call("cancel"); } catch { }
        }
    }

    private void OnApplicationQuit()
    {
        vibAtiva = false;
        if (vibrator != null)
        {
            try { vibrator.Call("cancel"); } catch { }
        }
    }
#endif
}
