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

    [Tooltip("Parâmetro float que controla o Blend Tree de locomoção normal.")]
    [SerializeField] private string nomeParametroSpeed = "Speed";

    [Tooltip("Parâmetro float usado no Blend Tree do Mesmerize (-1 = esquerda, 0 = idle, 1 = direita).")]
    [SerializeField] private string nomeParametroStrafe = "Strafe";

    [Tooltip("Parâmetro bool que liga o estado Mesmerizado no Animator.")]
    [SerializeField] private string nomeParametroMesmerizado = "Mesmerizado";

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
    public void OnMove(InputValue valor)
    {
        entradaMovimentoRaw = valor.Get<Vector2>();
    }

    // ===================== API MESMERIZE =====================
    public void AtivarMesmerize(Transform alvo)
    {
        estaMesmerizado = true;
        alvoMesmerize   = alvo;

        if (animator != null && !string.IsNullOrEmpty(nomeParametroMesmerizado))
            animator.SetBool(nomeParametroMesmerizado, true);
    }

    public void DesativarMesmerize()
    {
        estaMesmerizado = false;
        alvoMesmerize   = null;

        if (animator != null && !string.IsNullOrEmpty(nomeParametroMesmerizado))
            animator.SetBool(nomeParametroMesmerizado, false);

        // Garante que o Blend Tree volta pro idle normal
        if (animator != null && !string.IsNullOrEmpty(nomeParametroStrafe))
            animator.SetFloat(nomeParametroStrafe, 0f);
    }

    private void FixedUpdate()
    {
        // Ainda respeita o empurrão / knockback
        var gerenciador = FindObjectOfType<GerenciadorAnimacoesEcoDigital>();
        if (gerenciador != null && gerenciador.EstaEmpurrado)
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
                float magReesc = Mathf.InverseLerp(limiarEntrar, 1f, magClamp);
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

        // 4) MOVIMENTO FÍSICO
        Vector3 velocidadeDesejada = direcaoPlanar * (velocidadeMovimento * intensidade);

#if UNITY_600_OR_NEWER
        Vector3 curVel = rb.linearVelocity;
        rb.linearVelocity = new Vector3(velocidadeDesejada.x, curVel.y, velocidadeDesejada.z);
#else
        Vector3 curVel = rb.velocity;
        rb.velocity = new Vector3(velocidadeDesejada.x, curVel.y, velocidadeDesejada.z);
#endif

        // 5) ROTAÇÃO VISUAL (Mesmerize tem prioridade)
        AtualizarRotacaoVisual(direcaoPlanar);

        // 6) ANIMATOR: SPEED
        if (animator != null && !string.IsNullOrEmpty(nomeParametroSpeed))
            animator.SetFloat(nomeParametroSpeed, velocidadeDesejada.magnitude);

        // 7) ANIMATOR: STRAFE (apenas durante Mesmerize)
        AtualizarStrafeMesmerize(velocidadeDesejada);
    }

    private void AtualizarStrafeMesmerize(Vector3 velocidadeDesejada)
    {
        if (animator == null || string.IsNullOrEmpty(nomeParametroStrafe))
            return;

        if (!estaMesmerizado || alvoMesmerize == null)
        {
            // Fora do mesmerize, zera o strafe
            animator.SetFloat(nomeParametroStrafe, 0f);
            return;
        }

        // Direção para o outdoor
        Vector3 dirAlvo = alvoMesmerize.position - transform.position;
        dirAlvo.y = 0f;

        if (dirAlvo.sqrMagnitude < 0.0001f)
        {
            animator.SetFloat(nomeParametroStrafe, 0f);
            return;
        }

        dirAlvo.Normalize();

        // Eixo "direita" relativo ao outdoor (cross up x forward)
        Vector3 direita = Vector3.Cross(Vector3.up, dirAlvo);

        // Componente lateral da velocidade
        float lateral = Vector3.Dot(velocidadeDesejada, direita);

        // Normaliza pra -1..1 usando a velocidadeMovimento como referência
        float strafe = 0f;
        if (velocidadeMovimento > 0.01f)
            strafe = Mathf.Clamp(lateral / velocidadeMovimento, -1f, 1f);

        animator.SetFloat(nomeParametroStrafe, strafe);
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
                Quaternion rotBase     = Quaternion.LookRotation(dir.normalized, Vector3.up);
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
