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
    [Tooltip("Se verdadeiro, o Eco está sob efeito de Mesmerize.")]
    public bool estaMesmerizado = false;

    [Tooltip("Alvo que o Eco deve olhar enquanto mesmerizado.")]
    public Transform alvoMesmerize;

    [Tooltip("Velocidade de rotação para alinhar com o alvo do Mesmerize.")]
    [SerializeField] private float velocidadeRotacaoMesmerize = 6f;

    // ===================== ANIMAÇÕES =====================
    [Header("Animações (Gerenciador)")]
    [SerializeField] private GerenciadorAnimacoesEcoDigital gerenciadorAnimacoes;

    // ===================== INPUT SYSTEM =====================
    [Header("Input System (Novo)")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string nomeActionMap = "Player";
    [SerializeField] private string nomeActionMove = "Move";
    [SerializeField] private string nomeActionSprint = "Sprint";

    // ===================== MOBILE / JOYSTICK =====================
    [Header("Mobile / Joystick")]
    public bool usarJoystickMobile = true;
    public FixedJoystick joystickMobile;

    // ===================== PRIVADOS =====================
    private Rigidbody rb;
    private Vector2 entradaMovimentoRaw;
    private Vector2 entradaMovimentoFiltrada;
    private Vector2 entradaMovimentoSuave;
    private Vector2 velSuavizacao;
    private bool dentroDaZonaMorta = true;
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

    // ===================== INPUT CALLBACK =====================
    public void OnMove(InputValue valor)
    {
        if (usarJoystickMobile)
            return;

        entradaMovimentoRaw = valor.Get<Vector2>();
    }

    public void OnSprint(InputValue _) { }

    // ===================== MESMERIZE API =====================
    public void AtivarMesmerize(Transform alvo)
    {
        estaMesmerizado = true;
        alvoMesmerize = alvo;
        gerenciadorAnimacoes?.DefinirMesmerizado(true);
    }

    public void DesativarMesmerize()
    {
        estaMesmerizado = false;
        alvoMesmerize = null;
        gerenciadorAnimacoes?.DefinirMesmerizado(false);
    }

    private void FixedUpdate()
    {
        AtualizarSprintEstado();

        if (gerenciadorAnimacoes != null && gerenciadorAnimacoes.EstaEmpurrado)
            return;

        // ===================== JOYSTICK MOBILE =====================
        if (usarJoystickMobile && joystickMobile != null)
        {
            entradaMovimentoRaw = new Vector2(
                joystickMobile.Horizontal,
                joystickMobile.Vertical
            );
        }

        // ========== DEADZONE / HISTERSE ==========
        Vector2 bruto = entradaMovimentoRaw;
        float mag = bruto.magnitude;

        float limiarEntrar = Mathf.Clamp01(zonaMorta);
        float limiarSair = Mathf.Clamp01(zonaMorta + Mathf.Abs(histereseZonaMorta));

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
                float magReesc = Mathf.InverseLerp(limiarEntrar, 1f, magClamp);
                entradaMovimentoFiltrada = bruto.normalized * magReesc;
            }
            else
            {
                entradaMovimentoFiltrada = bruto;
            }
        }

        // ========== SUAVIZAÇÃO ==========
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
        else entradaMovimentoSuave = entradaMovimentoFiltrada;

        Vector2 unit = entradaMovimentoSuave.sqrMagnitude > 1e-6f
            ? entradaMovimentoSuave.normalized
            : Vector2.zero;

        float intensidade = Mathf.Clamp01(entradaMovimentoSuave.magnitude);

        // ========== DIREÇÃO PLANAR ==========
        Vector3 direcaoPlanar = CalcularDirecaoPlanarNormalizada(unit);

        if (direcaoPlanar.sqrMagnitude >= limiarRotacao * limiarRotacao)
            ultimaDirecaoPlanar = direcaoPlanar;

        // ========== MOVIMENTO FÍSICO ==========
        float velocidadeBase = velocidadeMovimento;
        if (sprintAtivo)
            velocidadeBase *= multiplicadorSprint;

#if UNITY_600_OR_NEWER
        Vector3 curVel = rb.linearVelocity;
        rb.linearVelocity = new Vector3(
            direcaoPlanar.x * velocidadeBase * intensidade,
            curVel.y,
            direcaoPlanar.z * velocidadeBase * intensidade
        );
#else
        Vector3 curVel = rb.velocity;
        rb.velocity = new Vector3(
            direcaoPlanar.x * velocidadeBase * intensidade,
            curVel.y,
            direcaoPlanar.z * velocidadeBase * intensidade
        );
#endif

        // ========== ROTAÇÃO VISUAL ==========
        AtualizarRotacaoVisual(direcaoPlanar);

        // ========== ANIMAÇÕES ==========
        gerenciadorAnimacoes?.AtualizarLocomocao(
            (direcaoPlanar * (velocidadeBase * intensidade)).magnitude,
            sprintAtivo
        );

        gerenciadorAnimacoes?.AtualizarStrafeMesmerize(
            direcaoPlanar * velocidadeBase,
            estaMesmerizado,
            alvoMesmerize,
            transform.position,
            velocidadeMovimento
        );
    }

    private void AtualizarSprintEstado()
    {
        sprintAtivo = false;

        if (playerInput == null || playerInput.actions == null)
            return;

        var action = playerInput.actions[nomeActionSprint];
        if (action == null)
            return;

        sprintAtivo = action.IsPressed();
    }

    private void AtualizarRotacaoVisual(Vector3 direcaoPlanar)
    {
        if (pivoModelo == null) return;

        if (estaMesmerizado && alvoMesmerize != null)
        {
            Vector3 dir = alvoMesmerize.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                Quaternion final = rot * Quaternion.Euler(0f, deslocamentoYawModelo, 0f);

                pivoModelo.rotation = Quaternion.Slerp(
                    pivoModelo.rotation,
                    final,
                    Mathf.Clamp01(velocidadeRotacaoMesmerize * Time.deltaTime)
                );
            }
            return;
        }

        if (direcaoPlanar.sqrMagnitude < limiarRotacao * limiarRotacao)
        {
            if (!girarQuandoParado) return;
            direcaoPlanar = ultimaDirecaoPlanar;
        }

        Quaternion rotBase = Quaternion.LookRotation(direcaoPlanar, Vector3.up);
        Quaternion finalNormal = rotBase * Quaternion.Euler(0f, deslocamentoYawModelo, 0f);

        if (interpolacaoGiro <= 0f)
        {
            pivoModelo.rotation = finalNormal;
        }
        else
        {
            float t = Mathf.Clamp01(interpolacaoGiro * Time.deltaTime);
            pivoModelo.rotation = Quaternion.Slerp(pivoModelo.rotation, finalNormal, t);
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
