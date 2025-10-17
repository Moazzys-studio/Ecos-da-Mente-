using UnityEngine;
using UnityEngine.InputSystem;

public class LiquidoBateria : MonoBehaviour
{
    [SerializeField] private Renderer liquidoRenderer;
    [Range(0,1)] public float fillLevel = 0.5f;
    [Range(0f, 2f)] public float tiltStrength = 0.4f;
    private Material mat;

    void Start()
    {
        mat = liquidoRenderer.material;
        if (Accelerometer.current == null)
            Debug.LogWarning("⚠️ Nenhum acelerômetro detectado — teste no dispositivo físico.");
    }

    void Update()
    {
        mat.SetFloat("_FillLevel", fillLevel);

        if (Accelerometer.current == null) return;

        Vector3 acc = Accelerometer.current.acceleration.ReadValue();

        // Detecta a orientação real do dispositivo
        Vector2 tilt;

        switch (Screen.orientation)
        {
            case ScreenOrientation.LandscapeLeft:
                tilt = new Vector2(acc.y, -acc.x); // rotação 90° anti-horário
                break;

            case ScreenOrientation.LandscapeRight:
                tilt = new Vector2(-acc.y, acc.x); // rotação 90° horário
                break;

            case ScreenOrientation.PortraitUpsideDown:
                tilt = new Vector2(-acc.x, -acc.y); // invertido
                break;

            default: // Portrait normal
                tilt = new Vector2(acc.x, acc.y);
                break;
        }

        // Suaviza e amplifica o efeito
        tilt *= tiltStrength;

        // Envia pro shader (_Tilt.x e _Tilt.y)
        mat.SetVector("_Tilt", new Vector4(tilt.x, tilt.y, 0, 0));
    }
}
