using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public struct FaixaFloat
{
    public float min;
    public float max;

    public FaixaFloat(float min, float max)
    {
        this.min = min;
        this.max = max;
    }

    public float Aleatorio() => Random.Range(min, max);

    public void Normalizar()
    {
        if (max < min) (min, max) = (max, min);
    }
}

[DisallowMultipleComponent]
public class NPCSocializacao : MonoBehaviour
{
    [Header("Raio/Detecção")]
    [Tooltip("Raio de detecção para socializar (m).")]
    public float raioSocial = 1.6f;

    [Tooltip("Camadas que contêm outros NPCs com este script.")]
    public LayerMask camadaNPC = ~0;

    [Tooltip("Intervalo entre checagens de vizinhos (s).")]
    public FaixaFloat intervaloChecagem = new FaixaFloat(0.6f, 1.0f);

    [Header("Conversa")]
    [Tooltip("Chance de iniciar conversa quando um par válido é encontrado (0-1).")]
    [Range(0f, 1f)] public float chanceConversa = 0.65f;

    [Tooltip("Duração da conversa (s).")]
    public FaixaFloat duracaoConversa = new FaixaFloat(2.5f, 5.0f);

    [Tooltip("Cooldown após conversar (s).")]
    public FaixaFloat cooldownAposConversa = new FaixaFloat(3.0f, 6.0f);

    [Tooltip("Velocidade de rotação para encarar o parceiro (graus/seg).")]
    public float velocidadeRotacao = 540f;

    [Header("Desync de Animação")]
    [Tooltip("Define um offset randômico (0-1) em 'ConversaOffset' no Animator (opcional).")]
    public bool usarConversaOffset = true;

    [Header("Integração (opcional)")]
    [Tooltip("Se houver NPCTrabalho, pausamos o comportamento durante a conversa.")]
    public NPCTrabalho npcTrabalho; // opcional (auto-preenchido no Reset/Awake)

    [Tooltip("Animator (bool Conversando, int ConversaModo, float ConversaOffset opcional).")]
    public Animator animator;

    [Header("Gizmos")]
    public bool desenharGizmo = true;
    public Color gizmoCor = new Color(0.2f, 0.75f, 1f, 0.25f);

    // ---- runtime ----
    private NavMeshAgent _agent;
    private bool _conversando;
    private bool _emCooldown;
    private float _cooldownRestante;

    private NPCSocializacao _parceiroAtual;
    private int _minhaOrdem;     // 1 = A, 2 = B
    private float _duracaoAtual;

    private Coroutine _rotinaChecagem;

    [Header("Stamina durante conversa")]
    [Tooltip("Regen extra de stamina por segundo enquanto conversa.")]
    public float bonusRegenConversaPorSegundo = 2.0f;


    private void Reset()
    {
        npcTrabalho = GetComponent<NPCTrabalho>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!npcTrabalho) npcTrabalho = GetComponent<NPCTrabalho>();
    }

    private void OnEnable()
    {
        if (_rotinaChecagem == null)
            _rotinaChecagem = StartCoroutine(RotinaChecagem());
    }

    private void OnDisable()
    {
        if (_rotinaChecagem != null)
        {
            StopCoroutine(_rotinaChecagem);
            _rotinaChecagem = null;
        }
    }

    private void OnValidate()
    {
        intervaloChecagem.Normalizar();
        duracaoConversa.Normalizar();
        cooldownAposConversa.Normalizar();

        raioSocial = Mathf.Max(0.1f, raioSocial);
        velocidadeRotacao = Mathf.Max(0f, velocidadeRotacao);
    }

    private IEnumerator RotinaChecagem()
    {
        while (true)
        {
            if (_emCooldown)
            {
                _cooldownRestante -= Time.deltaTime;
                if (_cooldownRestante <= 0f) _emCooldown = false;
            }

            if (!_conversando && !_emCooldown)
            {
                TentarEncontrarParceiro();
            }

            yield return new WaitForSeconds(intervaloChecagem.Aleatorio());
        }
    }

    private void TentarEncontrarParceiro()
    {
        if (_conversando) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, raioSocial, camadaNPC, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        foreach (var h in hits)
        {
            if (h.attachedRigidbody && h.attachedRigidbody.gameObject == gameObject) continue;

            NPCSocializacao outro = h.GetComponentInParent<NPCSocializacao>();
            if (outro == null || outro == this) continue;
            if (!outro.isActiveAndEnabled) continue;
            if (outro._conversando || outro._emCooldown) continue;

            if (Random.value > chanceConversa) continue;

            // Handshake simples: o de menor InstanceID propõe
            if (GetInstanceID() < outro.GetInstanceID())
            {
                if (outro.PodeAceitarConvite(this))
                {
                    IniciarConversa(outro, iniciador: true);
                }
            }

            if (_conversando) break; // só um par por checagem
        }
    }

    private bool PodeAceitarConvite(NPCSocializacao deQuem)
    {
        if (_conversando || _emCooldown) return false;

        // Distância ainda válida? (com leve margem)
        if (Vector3.SqrMagnitude(transform.position - deQuem.transform.position) >
            raioSocial * raioSocial * 1.44f) return false;

        return true;
    }

    private void IniciarConversa(NPCSocializacao parceiro, bool iniciador)
    {
        if (_conversando) return;

        _parceiroAtual = parceiro;
        _conversando = true;
        _minhaOrdem = iniciador ? 1 : 2;

        parceiro.ConfirmarEntradaDeConversa(this, iniciador: false);

        if (_agent) _agent.isStopped = true;
        if (npcTrabalho) npcTrabalho.enabled = false;

        _duracaoAtual = duracaoConversa.Aleatorio();

        if (animator)
        {
            animator.SetBool("Conversando", true);
            animator.SetInteger("ConversaModo", _minhaOrdem);
            if (usarConversaOffset) animator.SetFloat("ConversaOffset", Random.value);
            animator.SetBool("IsWalking", false);
        }

        if (parceiro._agent) parceiro._agent.isStopped = true;
        if (parceiro.npcTrabalho) parceiro.npcTrabalho.enabled = false;
        if (parceiro.animator)
        {
            parceiro.animator.SetBool("Conversando", true);
            parceiro.animator.SetInteger("ConversaModo", 3 - _minhaOrdem); // inverso (1<->2)
            if (usarConversaOffset) parceiro.animator.SetFloat("ConversaOffset", Random.value);
            parceiro.animator.SetBool("IsWalking", false);
        }

        StartCoroutine(RotinaConversa());
    }

    private void ConfirmarEntradaDeConversa(NPCSocializacao parceiro, bool iniciador)
    {
        _parceiroAtual = parceiro;
        _conversando = true;
        _minhaOrdem = iniciador ? 1 : 2;

        if (_agent) _agent.isStopped = true;
        if (npcTrabalho) npcTrabalho.enabled = false;

        if (animator)
        {
            animator.SetBool("Conversando", true);
            animator.SetInteger("ConversaModo", _minhaOrdem);
            if (usarConversaOffset) animator.SetFloat("ConversaOffset", Random.value);
            animator.SetBool("IsWalking", false);
        }
    }

    private IEnumerator RotinaConversa()
    {
        float t = 0f;

        while (t < _duracaoAtual && _conversando && _parceiroAtual != null)
        {
            // olha para o parceiro (apenas Y)
            Vector3 dir = _parceiroAtual.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion alvo = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, alvo, velocidadeRotacao * Time.deltaTime);
            }

            // garante que o parceiro olhe também
            Vector3 dir2 = transform.position - _parceiroAtual.transform.position;
            dir2.y = 0f;
            if (dir2.sqrMagnitude > 0.0001f)
            {
                Quaternion alvo2 = Quaternion.LookRotation(dir2.normalized, Vector3.up);
                _parceiroAtual.transform.rotation = Quaternion.RotateTowards(_parceiroAtual.transform.rotation, alvo2, velocidadeRotacao * Time.deltaTime);
            }

            t += Time.deltaTime;
            yield return null;

            // recuperar um pouco de stamina enquanto conversa
            if (npcTrabalho && bonusRegenConversaPorSegundo > 0f)
            {
                npcTrabalho.AdicionarStamina(bonusRegenConversaPorSegundo * Time.deltaTime);
            }
            if (_parceiroAtual && _parceiroAtual.npcTrabalho && bonusRegenConversaPorSegundo > 0f)
            {
                _parceiroAtual.npcTrabalho.AdicionarStamina(bonusRegenConversaPorSegundo * Time.deltaTime);
            }

        }

        EncerrarConversa();
    }

    private void EncerrarConversa()
    {
        if (!_conversando) return;

        if (_parceiroAtual) _parceiroAtual.FimConversaParceiro();
        FimConversaLocal();
    }

    private void FimConversaParceiro()
    {
        FimConversaLocal();
    }

    private void FimConversaLocal()
    {
        _conversando = false;

        if (animator)
        {
            animator.SetBool("Conversando", false);
            // opcional: animator.SetInteger("ConversaModo", 0);
        }

        if (_agent) _agent.isStopped = false;
        if (npcTrabalho) npcTrabalho.enabled = true;

        _emCooldown = true;
        _cooldownRestante = cooldownAposConversa.Aleatorio();

        _parceiroAtual = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!desenharGizmo) return;
        Color prev = Gizmos.color;
        Gizmos.color = gizmoCor;
        Gizmos.DrawSphere(transform.position, Mathf.Max(0.05f, raioSocial));
        Gizmos.color = prev;
    }
}
