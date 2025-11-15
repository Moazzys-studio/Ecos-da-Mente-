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

    [Tooltip("Zona morta radial do analógico (0 = desativado). Recomendado ~0.02.")]
    [SerializeField, Range(0f, 1f)] private float zonaMorta = 0.02f;

    [Tooltip("Histerese para evitar liga/desliga perto da zona morta. Ex.: 0.02.")]
    [SerializeField, Range(0f, 0.25f)] private float histereseZonaMorta = 0.02f;

    [Tooltip("Reescala o input após a zona morta para transição suave.")]
    [SerializeField] private bool compensarZonaMortaRadial = true;

    [Tooltip("Tempo (seg.) do SmoothDamp aplicado ao input; 0 desativa. Ex.: 0.06.")]
    [SerializeField, Range(0f, 0.25f)] private float tempoSuavizacaoInput = 0.06f;

    // ===================== VISUAL / ROTAÇÃO =====================
    [Header("Visual / Rotação")]
    [SerializeField] private Transform pivoModelo;
    [SerializeField] private float deslocamentoYawModelo = 0f;
    [SerializeField, Range(0f, 40f)] private float interpolacaoGiro = 16f;
    [SerializeField, Range(0.01f, 0.25f)] private float limiarRotacao = 0.06f;
    [SerializeField] private bool girarQuandoParado = false;

    // ===================== MESMERIZE =====================
    [Header("Mesmerize")]
    [Tooltip("Se verdadeiro, o Eco está sob efeito de Mesmerize (olhando para um outdoor).")]
    public bool estaMesmerizado = false;

    [Tooltip("Alvo que o Eco deve olhar enquanto mesmerizado (normalmente um Empty na frente do outdoor).")]
    public Transform alvoMesmerize;

    [Tooltip("Velocidade de rotação para alinhar com o alvo do Mesmerize.")]
    [SerializeField] private float velocidadeRotacaoMesmerize = 6f;

    // ===================== ANIMATOR =====================
    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string nomeParametroSpeed = "Speed";

    // ===================== PRIVADOS =====================
    private Rigidbody rb;
    private Vector2 entradaMovimentoRaw;       // valor que vem direto do Input System (teclado/analógico)
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
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    // Input System (Action "Move" como Vector2)
    public void OnMove(InputValue valor) => entradaMovimentoRaw = valor.Get<Vector2>();

    // ===================== API MESMERIZE =====================
    public void AtivarMesmerize(Transform alvo)
    {
        estaMesmerizado = true;
        alvoMesmerize = alvo;
    }

    public void DesativarMesmerize()
    {
        estaMesmerizado = false;
        alvoMesmerize = null;
    }

    private void FixedUpdate()
    {
        var gerenciador = FindObjectOfType<GerenciadorAnimacoesEcoDigital>();
        if (gerenciador != null && gerenciador.EstaEmpurrado)
            return; // bloqueia qualquer movimento

        // 1) Filtra DEADZONE com HISTERese para evitar liga/desliga perto do limiar
        Vector2 bruto = entradaMovimentoRaw;
        float mag = bruto.magnitude;

        float limiarSair = Mathf.Clamp01(zonaMorta + Mathf.Abs(histereseZonaMorta)); // para "sair" da zona morta
        float limiarEntrar = Mathf.Clamp01(zonaMorta);                                // para "entrar" (zerar)

        if (dentroDaZonaMorta)
        {
            // Continua zerado até passar do limiarSair
            if (mag > limiarSair)
            {
                dentroDaZonaMorta = false;
            }
        }
        else
        {
            // Só zera quando cair abaixo do limiarEntrar
            if (mag < limiarEntrar)
            {
                dentroDaZonaMorta = true;
            }
        }

        if (dentroDaZonaMorta)
        {
            entradaMovimentoFiltrada = Vector2.zero;
        }
        else
        {
            if (compensarZonaMortaRadial && limiarEntrar > 0f && limiarEntrar < 1f)
            {
                // Reescalona a magnitude (0..1) mapeando [zonaMorta..1] -> [0..1]
                float magReesc = Mathf.InverseLerp(limiarEntrar, 1f, Mathf.Clamp(mag, limiarEntrar, 1f));
                entradaMovimentoFiltrada = magReesc * bruto.normalized;
            }
            else
            {
                entradaMovimentoFiltrada = bruto; // já normalizamos mais abaixo
            }
        }

        // 2) Suavização do input (remove jitter do stick)
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

        // Garante direção unitária (ou zero)
        Vector2 unit = entradaMovimentoSuave.sqrMagnitude > 1e-6f
            ? entradaMovimentoSuave.normalized
            : Vector2.zero;

        float intensidade = Mathf.Clamp01(entradaMovimentoSuave.magnitude);

        // 3) Converte para direção no plano (considerando câmera se for o caso)
        Vector3 direcaoPlanar = CalcularDirecaoPlanarNormalizada(unit);

        if (direcaoPlanar.sqrMagnitude >= limiarRotacao * limiarRotacao)
            ultimaDirecaoPlanar = direcaoPlanar;

        // 4) Movimento: aplica apenas XZ e preserva Y da física
        Vector3 velocidadeDesejada = direcaoPlanar * (velocidadeMovimento * intensidade);

        #if UNITY_600_OR_NEWER
        Vector3 curVel = rb.linearVelocity;
        rb.linearVelocity = new Vector3(velocidadeDesejada.x, curVel.y, velocidadeDesejada.z);
        #else
        Vector3 curVel = rb.velocity;
        rb.velocity = new Vector3(velocidadeDesejada.x, curVel.y, velocidadeDesejada.z);
        #endif

        // 5) Rotação visual
        AtualizarRotacaoVisual(direcaoPlanar);

        // 6) Animator
        if (animator != null && !string.IsNullOrEmpty(nomeParametroSpeed))
            animator.SetFloat(nomeParametroSpeed, velocidadeDesejada.magnitude);
    }

    private void AtualizarRotacaoVisual(Vector3 direcaoPlanar)
    {
        if (pivoModelo == null) return;

        // ========== MESMERIZE TEM PRIORIDADE ==========
        if (estaMesmerizado && alvoMesmerize != null)
        {
            Vector3 dir = alvoMesmerize.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion rotBase = Quaternion.LookRotation(dir.normalized, Vector3.up);
                Quaternion rotCorrigida = rotBase * Quaternion.Euler(0f, deslocamentoYawModelo, 0f);

                float t = Mathf.Clamp01(velocidadeRotacaoMesmerize * Time.deltaTime);
                pivoModelo.rotation = Quaternion.Slerp(pivoModelo.rotation, rotCorrigida, t);
            }
            return;
        }
        // ==============================================

        Vector3 dirNormal = direcaoPlanar;
        if (dirNormal.sqrMagnitude < limiarRotacao * limiarRotacao)
        {
            if (!girarQuandoParado) return;
            dirNormal = ultimaDirecaoPlanar;
            if (dirNormal.sqrMagnitude < 1e-6f) return;
        }

        Quaternion rotBaseNormal = Quaternion.LookRotation(dirNormal, Vector3.up);
        Quaternion rotCorrigidaNormal = rotBaseNormal * Quaternion.Euler(0f, deslocamentoYawModelo, 0f);

        if (interpolacaoGiro <= 0f)
            pivoModelo.rotation = rotCorrigidaNormal;
        else
            pivoModelo.rotation = Quaternion.Slerp(
                pivoModelo.rotation,
                rotCorrigidaNormal,
                Mathf.Clamp01(interpolacaoGiro * Time.deltaTime)
            );
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
