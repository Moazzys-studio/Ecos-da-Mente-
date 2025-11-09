using UnityEngine;

public class VibracaoManager : MonoBehaviour
{
    // PlayerPrefs
    public const string KEY_VIBRA = "vibration_enabled";

    [Header("Config")]
    [Tooltip("Estado padrão caso ainda não exista PlayerPrefs")]
    [SerializeField] private bool habilitadaPorPadrao = true;

    // Singleton opcional para acesso fácil
    public static VibracaoManager Instancia { get; private set; }

    public bool Habilitada { get; private set; }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;

        // carrega preferencia
        int padrao = habilitadaPorPadrao ? 1 : 0;
        Habilitada = PlayerPrefs.GetInt(KEY_VIBRA, padrao) == 1;
    }

    // ---------------- API pública ----------------

    /// <summary> Liga/desliga vibração e salva no PlayerPrefs. </summary>
    public void DefinirHabilitada(bool habilitar, bool salvar = true)
    {
        Habilitada = habilitar;
        if (salvar)
        {
            PlayerPrefs.SetInt(KEY_VIBRA, Habilitada ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Atalho: alterna o estado.</summary>
    public void Alternar()
    {
        DefinirHabilitada(!Habilitada);
    }

    /// <summary> Vibra por N milissegundos (somente Android build). </summary>
    public void VibrarMs(int milissegundos)
    {
        if (!Habilitada) return;
        #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var context  = activity.Call<AndroidJavaObject>("getApplicationContext");
                var vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator");

                if (vibrator == null) return;

                // SDK int
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdkInt = version.GetStatic<int>("SDK_INT");
                    if (sdkInt >= 26)
                    {
                        // VibrationEffect.createOneShot(milliseconds, DEFAULT_AMPLITUDE)
                        var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                        var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", (long)milissegundos,
                            vibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE")
                        );
                        vibrator.Call("vibrate", effect);
                    }
                    else
                    {
                        vibrator.Call("vibrate", (long)milissegundos);
                    }
                }
            }
        }
        catch { /* silencioso: sem vibração */ }
        #else
        // Outros alvos: no-op
        #endif
    }

    /// <summary> Interrompe vibração contínua/padrões (se algum). </summary>
    public void Parar()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var context  = activity.Call<AndroidJavaObject>("getApplicationContext");
                var vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator != null) vibrator.Call("cancel");
            }
        }
        catch { }
        #endif
    }

    // Presets convenientes (ajuste como quiser)
    public void VibracaoLeve()  => VibrarMs(20);
    public void VibracaoMedia() => VibrarMs(40);
    public void VibracaoForte() => VibrarMs(80);
}
