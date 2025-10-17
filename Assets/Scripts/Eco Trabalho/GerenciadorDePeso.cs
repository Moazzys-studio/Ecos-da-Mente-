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
    [SerializeField] private LiquidoBateria bateriaEsquerda;
    [SerializeField] private LiquidoBateria bateriaDireita;
    [SerializeField] private Transform penduloRef;

    [Header("Controle direto do líquido (0 a 1)")]
    [Range(0f, 1f)] public float nivelEsquerda = 0.5f;
    [Range(0f, 1f)] public float nivelDireita = 0.5f;

    [Header("Configuração de Peso")]
    [SerializeField] private float pesoMaximo = 1f;
    [SerializeField] private float suavizacaoPeso = 4f;
    [SerializeField] private float inclinacaoMultiplicador = 10f;

    private float pesoEsquerda;
    private float pesoDireita;
    private float diferencaSuavizada;

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
        SincronizarNiveisComObjetos();
    }

    void Update()
    {
        if (!Application.isPlaying) return; // ⚠️ fora do play, não mexe na rotação

        if (!ReferenciasValidas()) return;

        AtualizarLiquidoVisual();
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
            AtualizarLiquidoVisual();
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
            if (achado != null)
                penduloRef = achado;
        }
    }

    private void RegistrarRotacaoBase()
    {
        if (penduloRef != null && !baseRegistrada)
        {
            baseRotacaoPendulo = penduloRef.localRotation; // grava -90x
            baseRegistrada = true;
        }
    }

    private bool ReferenciasValidas()
    {
        return balanceController != null && penduloRef != null &&
               bateriaEsquerda != null && bateriaDireita != null;
    }

    private void SincronizarNiveisComObjetos()
    {
        if (bateriaEsquerda != null) nivelEsquerda = bateriaEsquerda.fillLevel;
        if (bateriaDireita != null) nivelDireita = bateriaDireita.fillLevel;
    }

    private void AtualizarLiquidoVisual()
    {
        nivelEsquerda = Mathf.Clamp01(nivelEsquerda);
        nivelDireita = Mathf.Clamp01(nivelDireita);

        bateriaEsquerda.fillLevel = nivelEsquerda;
        bateriaDireita.fillLevel = nivelDireita;
    }

    private void AtualizarPesos()
    {
        pesoEsquerda = nivelEsquerda * pesoMaximo;
        pesoDireita = nivelDireita * pesoMaximo;

        float difBruta = pesoDireita - pesoEsquerda;
        float k = Application.isPlaying ? Time.deltaTime * suavizacaoPeso : 1f;
        diferencaSuavizada = Mathf.Lerp(diferencaSuavizada, difBruta, k);
    }

   private void AplicarPesoNaBalanca()
    {
        if (!ReferenciasValidas()) return;

        // Calcula diferença normalizada entre os níveis de líquido
        float pesoEsquerda = nivelEsquerda * pesoMaximo;
        float pesoDireita = nivelDireita * pesoMaximo;
        float diferenca = pesoDireita - pesoEsquerda; // -1 = esquerda cheia, +1 = direita cheia

        // Mapeia diretamente para o MaxAngle
        float torqueLiquido = diferenca * balanceController.MaxAngle;

        // Suaviza a reação (opcional, para simular inércia)
        float alvo = torqueLiquido;
        float atual = balanceController.externalAngleOffset;
        float suavizado = Mathf.Lerp(atual, alvo, Time.deltaTime * suavizacaoPeso);

        // Garante que o torque não excede o limite físico
        balanceController.externalAngleOffset = Mathf.Clamp(suavizado, -balanceController.MaxAngle, balanceController.MaxAngle);
    }

}
