using UnityEngine;

/// <summary>
/// Mecânica da balança (massa–mola–amortecimento) com competição entre:
/// - PESO do líquido (viés em graus vindo do GerenciadorDePeso)
/// - ESFORÇO do jogador (rate control + burst com stamina)
///
/// Notas de design:
/// • O input do jogador é tratado como “controle de velocidade” (deg/s), então
///   segurar o aparelho/tecla não congela a balança – é preciso acompanhar.
/// • O peso usa escala em GRAUS (não normalizada), mantendo a calibração simples:
///   ângulo ≈ (kTorquePeso / kMola) × biasPesoDeg.
/// • Para Δnível=1.0 tender ao MaxAngle em repouso, use kTorquePeso ≈ kMola / PesoMáximo.
/// • Para reverter 1–0, configure (kTorqueBaseJogador + kTorqueBurstJogador) > kTorquePeso.
/// </summary>
[AddComponentMenu("Ecos da Mente/Balança/Balance Mecanics (PT-BR)")]
[RequireComponent(typeof(BalanceController))]
public class BalanceMecanics : MonoBehaviour
{
    // ===================== PARÂMETROS DE DINÂMICA =====================
    [Header("Dinâmica da Balança (massa–mola–amortecimento)")]
    [SerializeField, Tooltip("Se desligado, não integra física; só interpola o ângulo até o viés (peso).")]
    private bool momentumHabilitado = true;

    [SerializeField, Min(0f), Tooltip("Constante da mola que puxa o ângulo de volta ao centro (0°).")]
    private float kMola = 22f;

    [SerializeField, Min(0f), Tooltip("Amortecimento proporcional à velocidade angular (freio).")]
    private float cAmortecimento = 7f;

    // ===================== PESO DO LÍQUIDO =====================
    [Header("Torque do Peso (competição real)")]
    [SerializeField, Min(0f), Tooltip("Ganho do torque do peso em GRAUS (não normalizado).")]
    private float kTorquePeso = 20f;

    [SerializeField, Min(0f), Tooltip("Limite de segurança para o viés do peso (graus).")]
    private float limiteBiasPesoDeg = 25f;

    // ===================== ESFORÇO DO JOGADOR =====================
    [Header("Esforço do Jogador (rate control + burst)")]
    [SerializeField, Min(0f), Tooltip("Velocidade alvo (deg/s) quando o input normalizado é ±1.")]
    private float taxaMaxJogadorDegPorSeg = 40f;

    [SerializeField, Min(0f), Tooltip("Ganho base do servo de velocidade do jogador (sustentável).")]
    private float kTorqueBaseJogador = 12f;

    [SerializeField, Min(0f), Tooltip("Ganho extra temporário (burst) escalado pela stamina.")]
    private float kTorqueBurstJogador = 12f;

    [SerializeField, Min(0f), Tooltip("Stamina máxima (~segundos de burst 'cheio').")]
    private float staminaMaxJogador = 2.0f;

    [SerializeField, Min(0f), Tooltip("Drenagem de stamina por segundo quando há esforço (|input| > zona morta).")]
    private float drenagemStaminaPorSeg = 1.2f;

    [SerializeField, Min(0f), Tooltip("Regeneração de stamina por segundo quando não há esforço.")]
    private float regeneracaoStaminaPorSeg = 0.8f;

    [SerializeField, Range(0f, 2f), Tooltip("Zona morta do input (em graus) para não drenar stamina por micro-movimentos.")]
    private float zonaMortaInputDeg = 0.15f;

    // ===================== FALLBACK SEM MOMENTUM =====================
    [Header("Fallback sem Momentum")]
    [SerializeField, Min(0f), Tooltip("Velocidade de aproximação (MoveTowards) quando o momentum está desligado.")]
    private float velSuavizada = 8f;

    // ===================== PENALIDADE RÁPIDA (OPCIONAL) =====================
    [Header("Penalidade vinda do Input (mobile)")]
    [SerializeField, Min(0f), Tooltip("‘Chute’ instantâneo em graus aplicado por KickPenaltySign(+/-1).")]
    private float penalidadeChuteDeg = 4f;

    // ===================== API PÚBLICA =====================
    /// <summary>Ângulo total atual (graus) – lido pela UI.</summary>
    public float TotalAngle => anguloAtualDeg;

    /// <summary>
    /// Ângulo do input do jogador em graus (preenchido neste script a partir do BalanceInput).
    /// Mantido público para debug/telemetria.
    /// </summary>
    [HideInInspector] public float inputAngleDeg;

    // ===================== ESTADO INTERNO =====================
    private float anguloAtualDeg;              // ângulo da balança (graus)
    private float velAngularDegPorSeg;         // velocidade angular (graus/seg)
    private float biasPesoDeg;                 // viés do líquido (graus)
    private float staminaJogador;              // 0..staminaMaxJogador

    [SerializeField, Tooltip("BalanceInput fornece MaxAngle e o sinal normalizado do jogador.")]
    private BalanceInput inputRef;

    [SerializeField] private BalanceController controller;

    private void Awake()
    {
        staminaJogador = staminaMaxJogador;
        if (inputRef == null) inputRef = GetComponent<BalanceInput>();
        if (controller == null) controller = GetComponent<BalanceController>();
    }

    private void Update()
    {
        // ===================== MODO SEM MOMENTUM =====================
        if (!momentumHabilitado)
        {
            float maxRef = ObterMaxAngle();
            float alvo = Mathf.Clamp(biasPesoDeg, -maxRef, maxRef);       // só segue o peso
            anguloAtualDeg = Mathf.MoveTowards(anguloAtualDeg, alvo, velSuavizada * Time.deltaTime);
            controller.ApplyAngle(anguloAtualDeg, maxRef);
            return;
        }

        // ===================== LEITURA DO INPUT DO JOGADOR =====================
        float maxAngleRef = ObterMaxAngle();

        if (inputRef != null)
        {
            // ComputeControl(dt) devolve (sinal normalizado [-1..1], chute –1/0/+1).
            var (sinal, direcaoChute) = inputRef.ComputeControl(Time.deltaTime);

            // Converte sinal normalizado para GRAUS (–MaxAngle..+MaxAngle).
            inputAngleDeg = Mathf.Clamp(sinal, -1f, 1f) * maxAngleRef;

            if (direcaoChute != 0)
                KickPenaltySign(direcaoChute > 0 ? 1 : -1);
        }

        // ===================== SERVO DE VELOCIDADE (RATE CONTROL) =====================
        // Converte o ângulo de input em uma velocidade alvo (deg/s).
        float taxaAlvo = Mathf.Clamp(
            (inputAngleDeg / Mathf.Max(0.0001f, maxAngleRef)) * taxaMaxJogadorDegPorSeg,
            -taxaMaxJogadorDegPorSeg, taxaMaxJogadorDegPorSeg
        );

        // Ganho efetivo do jogador: base + burst proporcional à stamina.
        float fatorStamina = (staminaMaxJogador > 0f) ? (staminaJogador / staminaMaxJogador) : 0f;
        float ganhoJogador = kTorqueBaseJogador + kTorqueBurstJogador * fatorStamina;

        // Torque do jogador busca (taxaAlvo - velAtual). Quanto maior a diferença, maior o torque.
        float torqueJogador = ganhoJogador * (taxaAlvo - velAngularDegPorSeg);

        // Drena/regenera stamina de acordo com esforço (acima da zona morta).
        bool aplicandoEsforco = Mathf.Abs(inputAngleDeg) > zonaMortaInputDeg;
        float deltaStamina = (aplicandoEsforco ? -drenagemStaminaPorSeg : regeneracaoStaminaPorSeg) * Time.deltaTime;
        staminaJogador = Mathf.Clamp(staminaJogador + deltaStamina, 0f, staminaMaxJogador);

        // ===================== TORQUE DO PESO (EM GRAUS) =====================
        float biasClamped = Mathf.Clamp(biasPesoDeg, -maxAngleRef, maxAngleRef);
        float torquePeso = kTorquePeso * biasClamped;    // escala em GRAUS (não dividir por MaxAngle)

        // ===================== MOLAS + AMORTECIMENTO =====================
        float torqueMola = -kMola * anguloAtualDeg;
        float torqueAmort = -cAmortecimento * velAngularDegPorSeg;

        // ===================== SOMA E INTEGRAÇÃO =====================
        float torqueTotal = torqueMola + torqueAmort + torquePeso + torqueJogador;

        velAngularDegPorSeg += torqueTotal * Time.deltaTime;
        anguloAtualDeg       += velAngularDegPorSeg * Time.deltaTime;

        // ===================== CLAMP & VISUAL =====================
        anguloAtualDeg = Mathf.Clamp(anguloAtualDeg, -maxAngleRef, maxAngleRef);
        controller.ApplyAngle(anguloAtualDeg, maxAngleRef);
    }

    private float ObterMaxAngle() => inputRef ? inputRef.MaxAngle : 25f;

    /// <summary>
    /// Define o viés do líquido (em graus). Chamado pelo GerenciadorDePeso.
    /// O valor é limitado tanto por <see cref="limiteBiasPesoDeg"/> quanto por MaxAngle.
    /// </summary>
    public void SetWeightBias(float biasDeg)
    {
        float maxRef = ObterMaxAngle();
        float limite = Mathf.Min(limiteBiasPesoDeg, maxRef);
        biasPesoDeg = Mathf.Clamp(biasDeg, -limite, limite);
    }

    /// <summary>
    /// Aplica uma penalidade instantânea (empurrão) no ângulo atual: útil para feedbacks rápidos no mobile.
    /// </summary>
    public void KickPenaltySign(int sign)
    {
        float maxRef = ObterMaxAngle();
        anguloAtualDeg = Mathf.Clamp(anguloAtualDeg + Mathf.Sign(sign) * penalidadeChuteDeg, -maxRef, maxRef);
    }

    // ----------------- Propriedades (caso queira ajustar em runtime) -----------------
    public bool MomentumHabilitado { get => momentumHabilitado; set => momentumHabilitado = value; }
    public float KMola { get => kMola; set => kMola = Mathf.Max(0f, value); }
    public float CAmortecimento { get => cAmortecimento; set => cAmortecimento = Mathf.Max(0f, value); }
    public float KTorquePeso { get => kTorquePeso; set => kTorquePeso = Mathf.Max(0f, value); }
    public float LimiteBiasPesoDeg { get => limiteBiasPesoDeg; set => limiteBiasPesoDeg = Mathf.Max(0f, value); }

    // ----------------- Leituras úteis para debug/UX -----------------
    public float AnguloAtualDeg => anguloAtualDeg;
    public float VelAngularDegPorSeg => velAngularDegPorSeg;
    public float BiasPesoDeg => biasPesoDeg;
    public float StaminaJogador => staminaJogador;
}
