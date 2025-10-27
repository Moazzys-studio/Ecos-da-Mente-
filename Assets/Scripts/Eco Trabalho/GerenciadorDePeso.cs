using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lê o nível do "líquido" (0..1) de cada lado, converte a diferença em um
/// <b>viés em graus</b> baseado no MaxAngle e envia para a <see cref="BalanceMecanics"/>.
/// A mecânica transforma esse viés em <b>torque</b> (competição real contra o jogador).
/// </summary>
/// <remarks>
/// Regra de calibração para alcançar ~MaxAngle quando (esq=1, dir=0) ou vice-versa:
/// <c>WeightTorqueK = SpringK / PesoMaximo</c>
/// (ajuste Damping C para controlar as oscilações).
/// </remarks>
[ExecuteAlways]
[AddComponentMenu("Ecos da Mente/Balança/Gerenciador de Peso")]
public class GerenciadorDePeso : MonoBehaviour
{
    /// <summary>Instância global opcional (facilita chamadas estáticas em protótipos).</summary>
    public static GerenciadorDePeso instancia;

    [Header("Referências")]
    [SerializeField, Tooltip("Camada VISUAL/IO (pêndulo, pratos, vibração). Mantém o externalAngleOffset (legado).")]
    private BalanceController balanceController;

    [SerializeField, Tooltip("Camada de FÍSICA/REGRAS. Possui TotalAngle e recebe o viés do peso.")]
    private BalanceMecanics mecanics;

    [SerializeField, Tooltip("Camada de ENTRADA. Fornece MaxAngle (referência de escala para o viés).")]
    private BalanceInput inputRef;

    [SerializeField, Tooltip("Indicador visual do nível do 'líquido' no lado ESQUERDO.")]
    public LiquidoBateria bateriaEsquerda;

    [SerializeField, Tooltip("Indicador visual do nível do 'líquido' no lado DIREITO.")]
    public LiquidoBateria bateriaDireita;

    [SerializeField, Tooltip("Transform do pêndulo (apenas para registrar rotação base no Editor).")]
    private Transform penduloRef;

    [Header("Controle direto do líquido (0 a 1)")]
    [Tooltip("Nível da bateria ESQUERDA (0..1). Em Play é sobrescrito pelo fillLevel do LiquidoBateria.")]
    [Range(0f, 1f)] public float nivelEsquerda = 0.5f;

    [Tooltip("Nível da bateria DIREITA (0..1). Em Play é sobrescrito pelo fillLevel do LiquidoBateria.")]
    [Range(0f, 1f)] public float nivelDireita  = 0.5f;

    [Header("Configuração de Peso")]
    [Tooltip("Escala de 'peso' máximo. Mantém linearidade com o nível do líquido.")]
    [SerializeField, Min(0f)] private float pesoMaximo = 1f;

    [Tooltip("Inércia do peso (suavização). Aumente para resposta mais lenta e estável.")]
    [SerializeField, Min(0f)] private float suavizacaoPeso = 4f;

    [Tooltip("Troque para -1 se o sentido pender invertido em relação ao esperado.")]
    [SerializeField] private float pesoDirectionSign = 1f;

    // ---------- Internos ----------
    [System.NonSerialized] public float pesoEsquerda;
    [System.NonSerialized] public float pesoDireita;
    [System.NonSerialized] public float diferencaSuavizada; // direita - esquerda (em unidades de 'peso')
    [System.NonSerialized] public float offsetPesoDeg;      // viés efetivo enviado (graus)

    private Quaternion baseRotacaoPendulo;
    private bool baseRegistrada = false;

    /// <summary>Garante referências básicas quando possível.</summary>
    private void GarantirReferencias()
    {
        // Se veio só o BalanceController, puxa os outros do mesmo GO
        if (balanceController != null)
        {
            var go = balanceController.gameObject;
            if (mecanics == null) mecanics = go.GetComponent<BalanceMecanics>();
            if (inputRef == null) inputRef = go.GetComponent<BalanceInput>();
            if (penduloRef == null)
            {
                Transform achado = balanceController.transform.Find("Pendulo");
                if (achado) penduloRef = achado;
            }
        }
    }

    /// <summary>Auto-descoberta de refs na cena (fallback leve).</summary>
    private void TentarAutoWire()
    {
        if (balanceController == null) balanceController = FindFirstObjectByType<BalanceController>();
        if (mecanics == null && balanceController != null) mecanics = balanceController.GetComponent<BalanceMecanics>();
        if (inputRef == null && balanceController != null) inputRef = balanceController.GetComponent<BalanceInput>();
    }

    /// <summary>Registra a rotação base do pêndulo para diagnóstico/edição.</summary>
    private void RegistrarRotacaoBase()
    {
        if (penduloRef != null && !baseRegistrada)
        {
            baseRotacaoPendulo = penduloRef.localRotation;
            baseRegistrada = true;
        }
    }

    /// <summary>Checagem de integridade das refs mínimas.</summary>
    private bool ReferenciasValidas()
    {
        return balanceController != null && mecanics != null && inputRef != null
               && bateriaEsquerda != null && bateriaDireita != null;
    }

    private void Awake()
    {
        if (instancia == null) instancia = this;
    }

    private void Start()
    {
        GarantirReferencias();
        RegistrarRotacaoBase();

        // Espelha visuais nos sliders ao iniciar
        if (bateriaEsquerda) nivelEsquerda = bateriaEsquerda.fillLevel;
        if (bateriaDireita)  nivelDireita  = bateriaDireita.fillLevel;

        offsetPesoDeg = 0f;

        // Zera o viés inicial na mecânica por segurança
        if (mecanics != null) mecanics.SetWeightBias(0f);
    }

    private void Update()
    {
        if (!ReferenciasValidas()) { TentarAutoWire(); if (!ReferenciasValidas()) return; }

        // Em Play lemos os níveis animados; em Edição os sliders dirigem o visual
        if (Application.isPlaying)
        {
            if (bateriaEsquerda) nivelEsquerda = Mathf.Clamp01(bateriaEsquerda.fillLevel);
            if (bateriaDireita)  nivelDireita  = Mathf.Clamp01(bateriaDireita.fillLevel);
        }
        else
        {
            nivelEsquerda = Mathf.Clamp01(nivelEsquerda);
            nivelDireita  = Mathf.Clamp01(nivelDireita);
            if (bateriaEsquerda) bateriaEsquerda.fillLevel = nivelEsquerda;
            if (bateriaDireita)  bateriaDireita.fillLevel  = nivelDireita;
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        AtualizarPesos();
        AplicarPesoNaBalanca();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            GarantirReferencias();
            RegistrarRotacaoBase();

            nivelEsquerda = Mathf.Clamp01(nivelEsquerda);
            nivelDireita  = Mathf.Clamp01(nivelDireita);
            if (bateriaEsquerda) bateriaEsquerda.fillLevel = nivelEsquerda;
            if (bateriaDireita)  bateriaDireita.fillLevel  = nivelDireita;

            AtualizarPesos();
            SceneView.RepaintAll();
        }
    }
#endif

    /// <summary>Converte níveis (0..1) em pesos lineares e suaviza a diferença.</summary>
    private void AtualizarPesos()
    {
        // Linearidade simples: peso = nível * escala
        pesoEsquerda = nivelEsquerda * pesoMaximo;
        pesoDireita  = nivelDireita  * pesoMaximo;

        // Diferença positiva => pende à DIREITA
        float difBruta = pesoDireita - pesoEsquerda;

        // Suavização da diferença (inércia visual/física)
        float k = Application.isPlaying ? Time.deltaTime * Mathf.Max(0.01f, suavizacaoPeso) : 1f;
        diferencaSuavizada = Mathf.Lerp(diferencaSuavizada, difBruta, k);
    }

    /// <summary>Mapeia diferença para viés em graus e envia para a mecânica.</summary>
    private void AplicarPesoNaBalanca()
    {
        if (!ReferenciasValidas()) return;

        // Converte (−PesoMax..+PesoMax) → (−MaxAngle..+MaxAngle)
        float maxRef = (inputRef != null) ? inputRef.MaxAngle : 25f;
        float biasAlvoDeg = pesoDirectionSign * diferencaSuavizada * maxRef;

        // Suaviza o viés angular final
        float k = Application.isPlaying ? Time.deltaTime * Mathf.Max(0.01f, suavizacaoPeso) : 1f;
        offsetPesoDeg = Mathf.Lerp(offsetPesoDeg, biasAlvoDeg, k);

        // Envia para a mecânica com clamp por segurança
        if (mecanics != null)
            mecanics.SetWeightBias(Mathf.Clamp(offsetPesoDeg, -maxRef, maxRef));
    }

    // ---------- Utilidades públicas ----------

    /// <summary>Define imediatamente os níveis de líquido (0..1) e aplica.</summary>
    public void SetNiveis(float esquerdo01, float direito01)
    {
        nivelEsquerda = Mathf.Clamp01(esquerdo01);
        nivelDireita  = Mathf.Clamp01(direito01);
        AtualizarPesos();
        AplicarPesoNaBalanca();
    }

#if UNITY_EDITOR
    // ----------------- Custom Editor helpers -----------------
    [ContextMenu("Teste/Forçar Peso ESQUERDA (1,0)")]
    private void TestePesoEsquerda() => SetNiveis(1f, 0f);

    [ContextMenu("Teste/Forçar Peso DIREITA (0,1)")]
    private void TestePesoDireita() => SetNiveis(0f, 1f);

    [ContextMenu("Teste/Forçar Peso NEUTRO (0.5,0.5)")]
    private void TestePesoNeutro() => SetNiveis(0.5f, 0.5f);
#endif
}

#if UNITY_EDITOR
/// <summary>Inspector com dicas, auto-calibração e botões de teste.</summary>
[CustomEditor(typeof(GerenciadorDePeso))]
public class GerenciadorDePesoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var g = (GerenciadorDePeso)target;
        var mecanics = GetMecanics(g);
        var maxAngle = GetMaxAngle(g);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Ajuda & Diagnóstico", EditorStyles.boldLabel);

        if (mecanics != null)
        {
            float springK = GetFloat(mecanics, "springK", 18f);
            float weightK = GetFloat(mecanics, "weightTorqueK", 6f);
            float damping = GetFloat(mecanics, "dampingC", 7f);

            float pesoMax = GetPrivate(g, "pesoMaximo", 1f);
            float sugestaoW = (pesoMax <= 0f) ? 0f : springK / pesoMax;

            EditorGUILayout.HelpBox(
                $"Regra para bater ~MaxAngle ({maxAngle:0.#}°) quando Δnível=1.0:\n" +
                $"WeightTorqueK = SpringK / PesoMáximo → {springK:0.##} / {pesoMax:0.##} = {sugestaoW:0.##}",
                MessageType.Info
            );

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Aplicar Auto-Calibração"))
            {
                SetFloat(mecanics, "weightTorqueK", sugestaoW);
                EditorUtility.SetDirty(mecanics);
            }
            if (GUILayout.Button("Teste ESQ (1,0)")) g.SetNiveis(1f, 0f);
            if (GUILayout.Button("Teste DIR (0,1)")) g.SetNiveis(0f, 1f);
            if (GUILayout.Button("Neutro (0.5,0.5)")) g.SetNiveis(0.5f, 0.5f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"SpringK: {springK:0.##} | WeightTorqueK: {weightK:0.##} | DampingC: {damping:0.##}");
        }
        else
        {
            EditorGUILayout.HelpBox("BalanceMecanics não encontrado para calcular a sugestão.", MessageType.Warning);
        }
    }

    // ---- Helpers para pegar campos privados sem expor a API pública ----
    private BalanceMecanics GetMecanics(GerenciadorDePeso g)
    {
        var f = typeof(GerenciadorDePeso).GetField("mecanics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (BalanceMecanics)f?.GetValue(g);
    }
    private float GetMaxAngle(GerenciadorDePeso g)
    {
        var fInput = typeof(GerenciadorDePeso).GetField("inputRef", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var input = (BalanceInput)fInput?.GetValue(g);
        return input ? input.MaxAngle : 25f;
    }
    private float GetPrivate(object obj, string name, float fallback)
    {
        var f = obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null ? (float)f.GetValue(obj) : fallback;
    }
    private float GetFloat(object obj, string name, float fallback)
    {
        var f = obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null ? (float)f.GetValue(obj) : fallback;
    }
    private void SetFloat(object obj, string name, float value)
    {
        var f = obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(obj, value);
    }
}
#endif
