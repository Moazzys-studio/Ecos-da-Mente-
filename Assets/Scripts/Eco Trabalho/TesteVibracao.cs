using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class TesteVibracao : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator;

    void Start()
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }

        bool hasAmpControl = false;
        try { hasAmpControl = vibrator.Call<bool>("hasAmplitudeControl"); } catch {}

        Debug.Log("=== TESTE VIBRACAO ===");
        Debug.Log("Tem controle de amplitude? " + hasAmpControl);

        // Teste real de 3 intensidades diferentes
        Vibrar(50);   // fraco
        new WaitForSeconds(1f);
        Vibrar(150);  // médio
        new WaitForSeconds(1f);
        Vibrar(255);  // forte
    }

    void Vibrar(int amplitude)
    {
        try
        {
            using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot", 400L, amplitude);
                vibrator.Call("vibrate", effect);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Erro ao vibrar: " + e.Message);
        }
    }
#endif
}
