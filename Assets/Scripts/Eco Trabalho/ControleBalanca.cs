using UnityEngine;
using UnityEngine.InputSystem;

public class ControleBalanca : MonoBehaviour
{
    [Header("Referências")]
    public Transform balanca;

    [Header("UI da Barra de Equilíbrio")]
    public RectTransform barraEquilibrio;     // fundo
    public RectTransform ponteiroEquilibrio;  // ponteiro que mexe

    [Header("Limites da balança (X)")]
    public float limiteMin = -116.353f;
    public float limiteMax = -65.461f;

    [Header("Sensibilidade")]
    public float sensibilidadeAcelerometro = 80f;
    public float sensibilidadeTeclado = 120f;

    [Header("Pesos (globais)")]
    public static float pesoEsquerda = 0f;
    public static float pesoDireita = 0f;

    [Header("Configuração de vibração")]
    public float margemLimite = 2f;
    public float intervaloVibracao = 0.15f;

    // -----------------------------
    // VARIÁVEIS GLOBAIS IMPORTANTES
    // -----------------------------
    public static float rotX_Global;  // ângulo atual visível pelo SistemaTurnos
    public static int vidas = 3;      // vidas do jogador

    private float rotX;
    private float origY;
    private float origZ;

    private float timerVibracao = 0f;
    private bool dentroDoLimite = false;

    void Start()
    {
        if (balanca == null)
        {
            Debug.LogError("Arraste a balança no Inspector!");
            enabled = false;
            return;
        }

        Vector3 r = balanca.localEulerAngles;
        origY = r.y;
        origZ = r.z;

        rotX = -90f;  
        balanca.localEulerAngles = new Vector3(rotX, origY, origZ);

        rotX_Global = rotX; // salva global

        if (Accelerometer.current != null)
            InputSystem.EnableDevice(Accelerometer.current);
    }

    void Update()
    {
        float acelInput = 0f;

        var acc = Accelerometer.current;
        if (acc != null)
        {
            acelInput = acc.acceleration.ReadValue().x;
            acelInput = Mathf.Clamp(acelInput, -1f, 1f);
        }

        float teclado = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) teclado = -1f;
            if (Keyboard.current.dKey.isPressed) teclado = 1f;
        }

        float diferencaPeso = pesoDireita - pesoEsquerda;

        float movimento =
            (acelInput * sensibilidadeAcelerometro) +
            (teclado * sensibilidadeTeclado) +
            (diferencaPeso);

        rotX += movimento * Time.deltaTime;

        rotX = Mathf.Clamp(rotX, limiteMin, limiteMax);

        // 🔥 SALVA ANGULO GLOBAL PARA O SISTEMATURNOS
        rotX_Global = rotX;

        balanca.localEulerAngles = new Vector3(rotX, origY, origZ);

        AtualizarPonteiroEquilibrio();
        VerificarVibracao();

        // DEBUG OPCIONAL
        // Debug.Log("Ângulo atual da balança: " + rotX_Global);
    }

    //-----------------------------
    //    UI – BARRA DE EQUILÍBRIO
    //-----------------------------
    void AtualizarPonteiroEquilibrio()
    {
        if (barraEquilibrio == null || ponteiroEquilibrio == null)
            return;

        float t = Mathf.InverseLerp(limiteMin, limiteMax, rotX);

        float halfWidth = barraEquilibrio.rect.width / 2f;

        float posX = Mathf.Lerp(-halfWidth, halfWidth, t);

        Vector2 anchored = ponteiroEquilibrio.anchoredPosition;
        anchored.x = posX;
        ponteiroEquilibrio.anchoredPosition = anchored;
    }

    //-----------------------------
    //        VIBRAÇÃO
    //-----------------------------
    void VerificarVibracao()
    {
        bool estaNoLimite =
            rotX <= limiteMin + margemLimite ||
            rotX >= limiteMax - margemLimite;

        if (estaNoLimite)
        {
            dentroDoLimite = true;

            timerVibracao -= Time.deltaTime;

            if (timerVibracao <= 0f)
            {
                VibracaoManager.Instancia?.VibracaoForte();
                timerVibracao = intervaloVibracao;
            }
        }
        else
        {
            if (dentroDoLimite)
            {
                VibracaoManager.Instancia?.Parar();
                dentroDoLimite = false;
            }
        }
    }
}