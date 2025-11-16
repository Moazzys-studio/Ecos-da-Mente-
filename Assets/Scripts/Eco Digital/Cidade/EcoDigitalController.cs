using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class EcoDigitalController : MonoBehaviour
{
    // ===================== INPUT / CÂMERA =====================
    [Header("Input / Câmera")]
    [Tooltip("Câmera para movimento relativo; se vazio, usa Camera.main.")]
    [SerializeField] private Transform transformCamera;

    [Tooltip("Se verdadeiro, converte o stick para o plano XZ relativo à câmera.")]
    [SerializeField] private bool relativoACamera = true;

    [Tooltip("Velocidade base de deslocamento no plano XZ (m/s).")]
    [SerializeField, Min(0f)] private float velocidadeMovimento = 4.5f;

    [Header("Zona morta / suavização")]
    [Tooltip("Magnitude mínima do input para começar a considerar movimento.")]
    [SerializeField, Range(0f, 1f)] private float zonaMorta = 0.02f;

    [Tooltip("Histerese adicional para sair da zona morta (evita flick).")]
    [SerializeField, Range(0f, 1f)] private float histereseZonaMorta = 0.02f;

    [Tooltip("Se verdadeiro, reescala o input para compensar a zona morta radial.")]
    [SerializeField] private bool compensarZonaMortaRadial = true;

    [Tooltip("Tempo de suavização (SmoothDamp) do input em segundos.")]
    [SerializeField, Range(0f, 0.25f)] private float tempoSuavizacaoInput = 0.06f;

    // ===================== VISUAL / ROTAÇÃO =====================
    [Header("Visual / Rotação")]
    [Tooltip("Transform do modelo visual, usado para girar o Eco. Se vazio, usa o próprio transform.")]
    [SerializeField] private Transform pivoModelo;

    [Tooltip("Offset em graus no Y aplicado à rotação visual (ajuste de modelo).")]
    [SerializeField] private float deslocamentoYawModelo = 0f;

    [Tooltip("Velocidade de interpolação da rotação (quanto maior, mais rápido).")]
    [SerializeField, Range(0f, 40f)] private float interpolacaoGiro = 16f;

    [Tooltip("Limiar mínimo de magnitude da direção para atualizar a rotação.")]
    [SerializeField, Range(0.01f, 0.25f)] private float limiarRotacao = 0.06f;

    [Tooltip("Se verdadeiro, o modelo continua girando mesmo parado.")]
    [SerializeField] private bool girarQuandoParado = false;

    // ===================== CORRIDA / SPRINT =====================
    [Header("Corrida / Sprint")]
    [Tooltip("Multiplicador aplicado à velocidadeMovimento quando está correndo.")]
    [SerializeField, Min(1f)] private float multiplicadorSprint = 1.6f;

    [Tooltip("Indica se o sprint está ativo no frame atual (PC ou mobile).")]
    [SerializeField] private bool sprintAtivo = false;

    // ===================== MESMERIZE =====================
    [Header("Mesmerize")]
    [Tooltip("Se verdadeiro, o Eco está sob efeito de Mesmerize (olhando para um outdoor).")]
    public bool estaMesmerizado = false;

    [Tooltip("Alvo que o Eco deve olhar enquanto mesmerizado (normalmente um Empty na frente do outdoor).")]
    public Transform alvoMesmerize;

    [Tooltip("Velocidade de rotação para alinhar com o alvo do Mesmerize.")]
    [SerializeField] private float velocidadeRotacaoMesmerize = 6f;

    // ===================== ANIMAÇÕES (GERENCIADOR) =====================
    [Header("Animações (Gerenciador)")]
    [Tooltip("Gerenciador responsável por controlar o Animator do Eco Digital.")]
    [SerializeField] private GerenciadorAnimacoesEcoDigital gerenciadorAnimacoes;

    // ===================== INPUT SYSTEM =====================
    [Header("Input System (Novo)")]
    [Tooltip("PlayerInput que usa o InputSystem_Actions. Deve estar no mesmo GameObject.")]
    [SerializeField] private PlayerInput playerInput;

    [Tooltip("Nome do Action Map usado pelo personagem (ex.: 'Player').")]
    [SerializeField] private string nomeActionMap = "Player";

    [Tooltip("Nome da Action de movimento (ex.: 'Move').")]
    [SerializeField] private string nomeActionMove = "Move";

    [Tooltip("Nome da Action de sprint (ex.: 'Sprint').")]
    [SerializeField] private string nomeActionSprint = "Sprint";

    // ===================== PRIVADOS =====================
    private Rigidbody rb;
    private Vector2 entradaMovimentoRaw;       // valor que vem do Input System
    private Vector2 entradaMovimentoFiltrada;  // após deadzone/histerese/compensação
    private Vector2 entradaMovimentoSuave;     // após SmoothDamp
    private Vector2 velSuavizacao;             // estado interno do SmoothDamp
    private bool dentroDaZonaMorta = true;     // para histerese

    private Vector3 ultimaDirecaoPlanar = Vector3.forward;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (transformCamera == null && Camera.main != null)
            transformCamera = Camera.main.transform;

        if (pivoModelo == null) pivoModelo = transform;

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerInput != null && !string.IsNullOrEmpty(nomeActionMap))
            playerInput.defaultActionMap = nomeActionMap;

        if (gerenciadorAnimacoes == null)
        {
#if UNITY_2023_1_OR_NEWER
            gerenciadorAnimacoes = Object.FindFirstObjectByType<GerenciadorAnimacoesEcoDigital>();
#else
            gerenciadorAnimacoes = Object.FindObjectOfType<GerenciadorAnimacoesEcoDigital>();
#endif
        }
    }

    // ========= CALLBACKS DO INPUT SYSTEM =========
    // PlayerInput deve estar configurado para chamar estes métodos pela Action "Move" e "Sprint".
    public void OnMove(InputValue valor)
    {
        // Essa Action é a "Move" do Action Map Player.
        entradaMovimentoRaw = valor.Get<Vector2>();
    }

    public void OnSprint(InputValue _)
    {
        // Intencionalmente vazio.
        // O estado real do sprint é lido direto da Action "Sprint" no FixedUpdate.
    }

    // ===================== API MESMERIZE =====================
    public void AtivarMesmerize(Transform alvo)
    {
        estaMesmerizado = true;
        alvoMesmerize   = alvo;

        if (gerenciadorAnimacoes != null)
            gerenciadorAnimacoes.DefinirMesmerizado(true);
    }

    public void DesativarMesmerize()
    {
        estaMesmerizado = false;
        alvoMesmerize   = null;

        if (gerenciadorAnimacoes != null)
            gerenciadorAnimacoes.DefinirMesmerizado(false);
    }

    private void FixedUpdate()
    {
        AtualizarSprintEstado();

        // Ainda respeita o empurrão / knockback
        if (gerenciadorAnimacoes != null && gerenciadorAnimacoes.EstaEmpurrado)
            return;

        // 1) DEADZONE / HISTERSE
        Vector2 bruto = entradaMovimentoRaw;
        float mag     = bruto.magnitude;

        float limiarEntrar = Mathf.Clamp01(zonaMorta);
        float limiarSair   = Mathf.Clamp01(zonaMorta + Mathf.Abs(histereseZonaMorta));

        if (dentroDaZonaMorta)
        {
            if (mag > limiarSair)
                dentroDaZonaMorta = false;
        }
        else
        {
            if (mag < limiarEntrar)
                dentroDaZonaMorta = true;
        }

        if (dentroDaZonaMorta)
        {
            entradaMovimentoFiltrada = Vector2.zero;
        }
        else
        {
            if (compensarZonaMortaRadial && limiarEntrar > 0f && limiarEntrar < 1f)
            {
                float magClamp = Mathf.Clamp(mag, limiarEntrar, 1f);
                float magReesc = Mathf.InverseLerp(limiarEntrar, limiarSair <= 0f ? 1f : 1f, magClamp);
                entradaMovimentoFiltrada = bruto.normalized * magReesc;
            }
            else
            {
                entradaMovimentoFiltrada = bruto;
            }
        }

        // 2) SUAVIZAÇÃO
        if (tempoSuavizacaoInput > 0f)
        {
            entradaMovimentoSuave = Vector2.SmoothDamp(
                entradaMovimentoSuave,
                entradaMovimentoFiltrada,
                ref velSuavizacao,
                tempoSuavizacaoInput,
                Mathf.Infinity,
                Time.fixedDeltaTime
            );
        }
        else
        {
            entradaMovimentoSuave = entradaMovimentoFiltrada;
        }

        Vector2 unit = entradaMovimentoSuave.sqrMagnitude > 1e-6f
            ? entradaMovimentoSuave.normalized
            : Vector2.zero;

        float intensidade = Mathf.Clamp01(entradaMovimentoSuave.magnitude);

        // 3) DIREÇÃO PLANAR
        Vector3 direcaoPlanar = CalcularDirecaoPlanarNormalizada(unit);

        if (direcaoPlanar.sqrMagnitude >= limiarRotacao * limiarRotacao)
            ultimaDirecaoPlanar = direcaoPlanar;

        // 4) MOVIMENTO FÍSICO (Sprint aplicado aqui)
        float velocidadeBase = velocidadeMovimento;
        if (sprintAtivo)
            velocidadeBase *= multiplicadorSprint;

        Vector3 velocidadeDesejada = direcaoPlanar * (velocidadeBase * intensidade);

#if UNITY_600_OR_NEWER
        Vector3 curVel = rb.linearVelocity;
        rb.linearVelocity = new Vector3(velocidadeDesejada.x, curVel.y, velocidadeDesejada.z);
#else
        Vector3 curVel = rb.velocity;
        rb.velocity = new Vector3(velocidadeDesejada.x, curVel.y, velocidadeDesejada.z);
#endif

        // 5) ROTAÇÃO VISUAL (Mesmerize tem prioridade)
        AtualizarRotacaoVisual(direcaoPlanar);

        // 6) ANIMAÇÕES (tudo via GerenciadorAnimacoes)
        if (gerenciadorAnimacoes != null)
        {
            gerenciadorAnimacoes.AtualizarLocomocao(velocidadeDesejada.magnitude, sprintAtivo);

            gerenciadorAnimacoes.AtualizarStrafeMesmerize(
                velocidadeDesejada,
                estaMesmerizado,
                alvoMesmerize,
                transform.position,
                velocidadeMovimento
            );
        }
    }

    /// <summary>
    /// Lê diretamente o estado da Action "Sprint" pelo PlayerInput.
    /// - PC: Left Shift [Keyboard] no Action Map Player/Sprint.
    /// - Mobile: Touch #1 / Touch Contact? [Touchscreen] no mesmo Sprint.
    /// </summary>
    private void AtualizarSprintEstado()
    {
        sprintAtivo = false;

        if (playerInput == null || playerInput.actions == null)
            return;

        var action = playerInput.actions[nomeActionSprint];
        if (action == null)
            return;

        // IsPressed() olha o estado real dos bindings, independe de interação.
        sprintAtivo = action.IsPressed();
    }

    private void AtualizarRotacaoVisual(Vector3 direcaoPlanar)
    {
        if (pivoModelo == null) return;

        // MESMERIZE: sempre olha para o alvo
        if (estaMesmerizado && alvoMesmerize != null)
        {
            Vector3 dir = alvoMesmerize.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion rotBase      = Quaternion.LookRotation(dir.normalized, Vector3.up);
                Quaternion rotCorrigida = rotBase * Quaternion.Euler(0f, deslocamentoYawModelo, 0f);

                float t = Mathf.Clamp01(velocidadeRotacaoMesmerize * Time.deltaTime);
                pivoModelo.rotation = Quaternion.Slerp(pivoModelo.rotation, rotCorrigida, t);
            }
            return;
        }

        // ROTAÇÃO NORMAL
        Vector3 dirNormal = direcaoPlanar;
        if (dirNormal.sqrMagnitude < limiarRotacao * limiarRotacao)
        {
            if (!girarQuandoParado) return;
            dirNormal = ultimaDirecaoPlanar;
            if (dirNormal.sqrMagnitude < 1e-6f) return;
        }

        Quaternion rotBaseNormal      = Quaternion.LookRotation(dirNormal, Vector3.up);
        Quaternion rotCorrigidaNormal = rotBaseNormal * Quaternion.Euler(0f, deslocamentoYawModelo, 0f);

        if (interpolacaoGiro <= 0f)
        {
            pivoModelo.rotation = rotCorrigidaNormal;
        }
        else
        {
            float t = Mathf.Clamp01(interpolacaoGiro * Time.deltaTime);
            pivoModelo.rotation = Quaternion.Slerp(pivoModelo.rotation, rotCorrigidaNormal, t);
        }
    }

    private Vector3 CalcularDirecaoPlanarNormalizada(Vector2 inputUnitario)
    {
        if (inputUnitario == Vector2.zero) return Vector3.zero;

        if (relativoACamera && transformCamera != null)
        {
            Vector3 frente = Vector3.ProjectOnPlane(transformCamera.forward, Vector3.up).normalized;
            if (frente.sqrMagnitude < 1e-6f) frente = Vector3.forward;

            Vector3 direita = Vector3.Cross(Vector3.up, frente).normalized;
            if (direita.sqrMagnitude < 1e-6f) direita = Vector3.right;

            Vector3 combinado = direita * inputUnitario.x + frente * inputUnitario.y;
            return combinado.sqrMagnitude > 1e-6f ? combinado.normalized : Vector3.zero;
        }
        else
        {
            Vector3 v = new Vector3(inputUnitario.x, 0f, inputUnitario.y);
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }
    }
}
