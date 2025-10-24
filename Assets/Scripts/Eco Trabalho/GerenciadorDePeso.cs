using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GerenciadorDePeso : MonoBehaviour
{
    public static GerenciadorDePeso instancia;

    [Header("Referências")]
    [SerializeField] private BalanceController balanceController;
    [SerializeField] public LiquidoBateria bateriaEsquerda;
    [SerializeField] public LiquidoBateria bateriaDireita;
    [SerializeField] private Transform penduloRef;

    [Header("Controle direto do líquido (0 a 1)")]
    [Range(0f, 1f)] public float nivelEsquerda = 0.5f;
    [Range(0f, 1f)] public float nivelDireita  = 0.5f;

    [Header("Configuração de Peso")]
    [Tooltip("Peso máximo em 'unidades relativas' (0..1). Mantém linearidade com o nível do líquido.")]
    [SerializeField, Min(0f)] private float pesoMaximo = 1f;
    [Tooltip("Resposta do peso (inércia do torque).")]
    [SerializeField, Min(0f)] private float suavizacaoPeso = 4f;

    [Tooltip("Sinal do lado (troque para -1 se pender invertido).")]
    [SerializeField] private float pesoDirectionSign = 1f;

    // internos
    private float pesoEsquerda;
    private float pesoDireita;
    private float diferencaSuavizada;
    private float offsetPesoDeg; // viés em graus que enviaremos ao Balance (vira torque)

    private Quaternion baseRotacaoPendulo;
    private bool baseRegistrada = false;

    void Awake()
    {
        if (instancia == null) instancia = this;
    }

    void Start()
    {
        GarantirReferencias();
        RegistrarRotacaoBase();
        // Em Start a gente só espelha os valores visuais nos sliders
        if (bateriaEsquerda) nivelEsquerda = bateriaEsquerda.fillLevel;
        if (bateriaDireita)  nivelDireita  = bateriaDireita.fillLevel;

        offsetPesoDeg = 0f;

        if (balanceController != null)
        {
            balanceController.externalAngleOffset = 0f; // legado, mantido zerado
            balanceController.weightBiasDeg = 0f;       // peso vira torque dentro do Balance
        }
    }

    void Update()
    {
        if (!ReferenciasValidas()) return;

        // IMPORTANTE:
        // - Em Play: LE o fillLevel das baterias (animado por Turnos/Animação/etc).
        // - Fora do Play: escreve o fillLevel a partir dos sliders para pré-visualizar.
        if (Application.isPlaying)
        {
            nivelEsquerda = Mathf.Clamp01(bateriaEsquerda.fillLevel);
            nivelDireita  = Mathf.Clamp01(bateriaDireita.fillLevel);
        }
        else
        {
            // Preview no Editor
            nivelEsquerda = Mathf.Clamp01(nivelEsquerda);
            nivelDireita  = Mathf.Clamp01(nivelDireita);
            bateriaEsquerda.fillLevel = nivelEsquerda;
            bateriaDireita.fillLevel  = nivelDireita;
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        AtualizarPesos();
        AplicarPesoNaBalanca();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            GarantirReferencias();
            RegistrarRotacaoBase();

            // No Editor, sliders dirigem a visualização:
            nivelEsquerda = Mathf.Clamp01(nivelEsquerda);
            nivelDireita  = Mathf.Clamp01(nivelDireita);
            if (bateriaEsquerda) bateriaEsquerda.fillLevel = nivelEsquerda;
            if (bateriaDireita)  bateriaDireita.fillLevel  = nivelDireita;

            AtualizarPesos();
            SceneView.RepaintAll();
        }
    }
#endif

    private void GarantirReferencias()
    {
        if (balanceController == null) return;

        if (penduloRef == null)
        {
            Transform achado = balanceController.transform.Find("Pendulo");
            if (achado != null) penduloRef = achado;
        }
    }

    private void RegistrarRotacaoBase()
    {
        if (penduloRef != null && !baseRegistrada)
        {
            baseRotacaoPendulo = penduloRef.localRotation; // neutro (ex.: -90x, 90y, 0z)
            baseRegistrada = true;
        }
    }

    private bool ReferenciasValidas()
    {
        return balanceController != null && bateriaEsquerda != null && bateriaDireita != null;
    }

    private void AtualizarPesos()
    {
        // linear: 0..1
        pesoEsquerda = nivelEsquerda * pesoMaximo;
        pesoDireita  = nivelDireita  * pesoMaximo;

        // diferença positiva => pende à direita
        float difBruta = pesoDireita - pesoEsquerda;

        float k = Application.isPlaying ? Time.deltaTime * Mathf.Max(0.01f, suavizacaoPeso) : 1f;
        diferencaSuavizada = Mathf.Lerp(diferencaSuavizada, difBruta, k);
    }

    private void AplicarPesoNaBalanca()
    {
        if (!ReferenciasValidas()) return;

        // Converte a diferença (−1..+1) para um viés em GRAUS.
        // Esse viés NÃO é somado ao ângulo final — vira TORQUE dentro do BalanceController.
        float biasAlvoDeg = pesoDirectionSign * diferencaSuavizada * balanceController.MaxAngle;

        // Suaviza o viés em graus
        float k = Application.isPlaying ? Time.deltaTime * Mathf.Max(0.01f, suavizacaoPeso) : 1f;
        offsetPesoDeg = Mathf.Lerp(offsetPesoDeg, biasAlvoDeg, k);

        // Envia para o Balance (ele converte em torque com weightTorqueK)
        balanceController.weightBiasDeg =
            Mathf.Clamp(offsetPesoDeg, -balanceController.MaxAngle, balanceController.MaxAngle);

        // Mantém legado desativado
        balanceController.externalAngleOffset = 0f;
    }
}
