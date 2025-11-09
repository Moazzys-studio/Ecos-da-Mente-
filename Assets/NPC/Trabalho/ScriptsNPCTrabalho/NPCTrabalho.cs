using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class NPCTrabalho : MonoBehaviour
{
    // =========================
    //        ESTADOS
    // =========================
    public enum Estado { Idle = 0, Andando = 1 }
    public enum PadraoCaminhada { Horario = 0, AntiHorario = 1, VaiEVem = 2, PulaUm = 3 }

    // =========================
    //    CONFIG INSPECTOR
    // =========================
    [Header("Referências (Corredor)")]
    [Tooltip("Quatro pontos de rota no corredor, na ordem que você considera 'horário'.")]
    public Transform[] pontosCorredor = new Transform[4];

    [Header("Movimentação")]
    [Tooltip("Velocidade base do agente (sofre variação aleatória por NPC).")]
    public float velocidadeBase = 2.8f;

    [Tooltip("Variação percentual aleatória aplicada à velocidade base (0.12 = ±12%).")]
    [Range(0f, 0.5f)] public float variacaoVelocidade = 0.12f;

    [Tooltip("Multiplicador aplicado quando o NPC está cansado.")]
    [Range(0.2f, 1f)] public float multiplicadorCansado = 0.6f;

    [Tooltip("Distância para considerar chegada.")]
    public float pararAoAproximar = 0.25f;

    [Header("Idle (pausas entre deslocamentos)")]
    public float idleMin = 0.3f;
    public float idleMax = 1.2f;

    [Header("Comportamento de Corredor")]
    [Tooltip("Se verdadeiro, este NPC segue o comportamento de corredor e entra na contagem global.")]
    public bool usarComportamentoCorredor = true;

    [Tooltip("Se marcado, o padrão de caminhada será sorteado ao iniciar.")]
    public bool padraoAleatorio = true;

    [Tooltip("Padrão usado se 'padraoAleatorio' estiver desmarcado.")]
    public PadraoCaminhada padraoFixo = PadraoCaminhada.Horario;

    [Header("Animação (Opcional)")]
    [Tooltip("Animator com parâmetros: bool IsWalking, float Velocidade, bool Cansado.")]
    public Animator animator;

    [Header("STAMINA")]
    [Tooltip("Stamina máxima do NPC.")]
    public float staminaMax = 100f;
    public bool EstaCansado => _estaCansado;

    [Tooltip("Stamina inicial (se  <0, será sorteada entre 60% e 100%).")]
    public float staminaInicial = -1f;

    [Tooltip("Dreno base por segundo enquanto anda (antes dos multiplicadores).")]
    public float drainBasePorSegundo = 1.2f;

    [Tooltip("Quanto a velocidade impacta o dreno (ex.: 0.5 → +50% de dreno a 100% da vel. base).")]
    [Range(0f, 2f)] public float impactoVelocidadeNoDreno = 0.6f;

    [Tooltip("Regen por segundo quando em Idle.")]
    public float regenPorSegundo = 4f;

    [Tooltip("Histerese para sair do estado 'cansado' (ex.: 0.35 → precisa passar de 35%).")]
    [Range(0.05f, 0.9f)] public float limiarSairCansado = 0.35f;

    [Tooltip("Limiar para entrar em 'cansado' (ex.: 0.1 → <=10% entra cansado).")]
    [Range(0.01f, 0.5f)] public float limiarEntrarCansado = 0.12f;

    [Header("UI World-Space (por NPC)")]
    [Tooltip("Canvas em World Space como filho do NPC.")]
    public Canvas canvasWorld;

    [Tooltip("Painel que mostra as infos (ativado ao hover/toque).")]
    public GameObject painelInfo;

    [Tooltip("Barra de stamina (Image com Fill).")]
    public Image barraStamina;

    [Tooltip("Texto de estado (TMP).")]
    public TextMeshProUGUI textoEstado;

    [Tooltip("Altura do canvas sobre a cabeça.")]
    public float alturaCanvas = 2.0f;

    [Tooltip("Tempo que o painel fica visível após toque no mobile.")]
    public float tempoVisivelAposToque = 2.5f;

    [Header("Depuração")]
    public bool desenharGizmos = true;

    // =========================
    //     ESTÁTICOS ÚTEIS
    // =========================
    private static int _ativosNoCorredor = 0;
    public static int AtivosNoCorredor => _ativosNoCorredor;

    // =========================
    //       RUNTIME
    // =========================
    private NavMeshAgent _agent;
    private Estado _estadoAtual = Estado.Idle;

    private PadraoCaminhada _padraoEscolhido;
    private int _indiceAtual = 0;
    private int _indiceAlvo = 0;

    // Vai-e-vem
    private int _vaiVemA = 0;
    private int _vaiVemB = 1;
    private bool _indoParaB = true;

    private Coroutine _rotinaEstado;
    private System.Random _rng;

    // Velocidades
    private float _velocidadeAlocada;         // velocidade sorteada para este NPC
    private float _velocidadeNormal;          // cache enquanto descansado
    private float _velocidadeCansado;         // cache enquanto cansado

    // Stamina
    private float _stamina;
    private bool _estaCansado;

    // Desincronização
    private float _multDrain;   // multiplicador aleatório de dreno
    private float _multRegen;   // multiplicador aleatório de regen

    // UI/Interação
    private bool _mouseDentro;                // PC hover
    private bool _selecionadoMobile;          // Android toque
    private float _timerVisibilidadeMobile;

    private Camera _cam;

    private void OnEnable()
    {
        if (usarComportamentoCorredor)
            _ativosNoCorredor++;
    }

    private void OnDisable()
    {
        if (usarComportamentoCorredor)
            _ativosNoCorredor = Mathf.Max(0, _ativosNoCorredor - 1);
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.stoppingDistance = pararAoAproximar;
        _agent.updateRotation = true;
        _agent.updateUpAxis = true;

        _cam = Camera.main;

        int seed = (GetInstanceID() ^ System.Environment.TickCount) & 0x7FFFFFFF;
        _rng = new System.Random(seed);

        // velocidade levemente diferente por NPC
        float fator = 1f + (float)((_rng.NextDouble() * 2.0 - 1.0) * variacaoVelocidade);
        _velocidadeAlocada = Mathf.Max(0.1f, velocidadeBase * fator);
        _velocidadeNormal = _velocidadeAlocada;
        _velocidadeCansado = Mathf.Max(0.1f, _velocidadeAlocada * multiplicadorCansado);
        _agent.speed = _velocidadeNormal;
        _agent.avoidancePriority = _rng.Next(20, 80);

        // multipliers de dreno/regen diferentes por NPC
        _multDrain = Mathf.Lerp(0.85f, 1.15f, (float)_rng.NextDouble());
        _multRegen = Mathf.Lerp(0.85f, 1.15f, (float)_rng.NextDouble());

        // stamina inicial
        if (staminaInicial >= 0f)
            _stamina = Mathf.Clamp(staminaInicial, 0f, staminaMax);
        else
            _stamina = Mathf.Lerp(staminaMax * 0.6f, staminaMax, (float)_rng.NextDouble());

        // UI inicial
        if (canvasWorld)
        {
            canvasWorld.renderMode = RenderMode.WorldSpace;
            PositionCanvas();
        }
        SafeSetPainel(false);
        AtualizarUI();
    }

    private void Start()
    {
        if (pontosCorredor == null || pontosCorredor.Length < 4)
        {
            Debug.LogWarning($"[{name}] Configure 4 pontos no corredor para o NPCTrabalho.");
            enabled = false;
            return;
        }

        if (!usarComportamentoCorredor) return;

        _padraoEscolhido = padraoAleatorio ? (PadraoCaminhada)_rng.Next(0, 4) : padraoFixo;
        _indiceAtual = _rng.Next(0, 4);

        switch (_padraoEscolhido)
        {
            case PadraoCaminhada.Horario:
                _indiceAlvo = ProximoHorario(_indiceAtual);
                break;
            case PadraoCaminhada.AntiHorario:
                _indiceAlvo = ProximoAntiHorario(_indiceAtual);
                break;
            case PadraoCaminhada.VaiEVem:
                _vaiVemA = _indiceAtual;
                _vaiVemB = PegarOutroIndice(_vaiVemA);
                _indoParaB = true;
                _indiceAlvo = _vaiVemB;
                break;
            case PadraoCaminhada.PulaUm:
                _indiceAlvo = PulaSempreUm(_indiceAtual);
                break;
        }

        TrocarEstado(Estado.Andando);
    }

    private void Update()
    {
        if (!usarComportamentoCorredor) return;

        // ====== MOVIMENTO / FSM ======
        if (_estadoAtual == Estado.Andando)
        {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                _indiceAtual = _indiceAlvo;
                TrocarEstado(Estado.Idle);
            }
        }

        // ====== STAMINA ======
        AtualizarStamina(Time.deltaTime);
        AplicarEstadoCansadoSeNecessario();

        // ====== ANIMAÇÃO ======
        if (animator)
        {
            float velPlanar = new Vector3(_agent.velocity.x, 0f, _agent.velocity.z).magnitude;
            animator.SetBool("IsWalking", velPlanar > 0.05f && _estadoAtual == Estado.Andando);
            animator.SetFloat("Velocidade", velPlanar);
            animator.SetBool("Cansado", _estaCansado);
        }

        // ====== UI/Interação ======
        if (Application.isMobilePlatform)
        {
            HandleToqueMobile();
        }
        AtualizarUI();
    }

    private void LateUpdate()
    {
        PositionCanvas();
    }

    // =========================
    //       STAMINA LOGIC
    // =========================
    private void AtualizarStamina(float dt)
    {
        if (_estadoAtual == Estado.Andando)
        {
            // Dreno base * multiplicadores * influência da velocidade
            float fatorVel = 1f + impactoVelocidadeNoDreno * Mathf.Clamp01(_agent.speed / Mathf.Max(0.01f, _velocidadeNormal));
            float drain = drainBasePorSegundo * _multDrain * fatorVel;
            _stamina = Mathf.Max(0f, _stamina - drain * dt);
        }
        else // Idle
        {
            float regen = regenPorSegundo * _multRegen;
            _stamina = Mathf.Min(staminaMax, _stamina + regen * dt);
        }
    }

    private void AplicarEstadoCansadoSeNecessario()
    {
        float frac = (_stamina / staminaMax);

        // entra cansado
        if (!_estaCansado && frac <= limiarEntrarCansado)
        {
            _estaCansado = true;
            _agent.speed = _velocidadeCansado;
        }

        // sai do cansado
        if (_estaCansado && frac >= limiarSairCansado)
        {
            _estaCansado = false;
            _agent.speed = _velocidadeNormal;
        }

        // Se estiver cansado e em Andando, mantemos — opção: poderíamos forçar pequenos Idles extras
        // (se quiser isso depois, adicionamos micro-pauses aqui).
    }

    public void AdicionarStamina(float quantidade)
    {
        if (quantidade <= 0f) return;
        _stamina = Mathf.Min(staminaMax, _stamina + quantidade);
    }


    // =========================
    //         FSM
    // =========================
    private void TrocarEstado(Estado novo)
    {
        if (_rotinaEstado != null) StopCoroutine(_rotinaEstado);
        _estadoAtual = novo;

        switch (_estadoAtual)
        {
            case Estado.Idle:
                _rotinaEstado = StartCoroutine(RotinaIdle());
                break;
            case Estado.Andando:
                IrParaDestinoAtual();
                break;
        }
    }

    private IEnumerator RotinaIdle()
    {
        float pausa = Mathf.Lerp(idleMin, idleMax, (float)_rng.NextDouble());

        // Se muito cansado, aumenta pausa (desincroniza ainda mais)
        if (_estaCansado) pausa *= Mathf.Lerp(1.15f, 1.6f, (float)_rng.NextDouble());

        yield return new WaitForSeconds(pausa);

        switch (_padraoEscolhido)
        {
            case PadraoCaminhada.Horario:
                _indiceAlvo = ProximoHorario(_indiceAtual);
                break;
            case PadraoCaminhada.AntiHorario:
                _indiceAlvo = ProximoAntiHorario(_indiceAtual);
                break;
            case PadraoCaminhada.VaiEVem:
                _indiceAlvo = _indoParaB ? _vaiVemA : _vaiVemB;
                _indoParaB = !_indoParaB;
                break;
            case PadraoCaminhada.PulaUm:
                _indiceAlvo = PulaSempreUm(_indiceAtual);
                break;
        }

        TrocarEstado(Estado.Andando);
    }

    private void IrParaDestinoAtual()
    {
        Transform alvo = (_indiceAlvo >= 0 && _indiceAlvo < pontosCorredor.Length) ? pontosCorredor[_indiceAlvo] : null;
        if (!alvo)
        {
            Debug.LogWarning($"[{name}] Ponto {_indiceAlvo} não atribuído.");
            return;
        }

        if (!_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }

        _agent.SetDestination(alvo.position);
    }

    // =========================
    //      UI / INTERAÇÃO
    // =========================
    private void PositionCanvas()
    {
        if (!canvasWorld) return;

        // Posiciona acima do NPC
        Vector3 pos = transform.position + Vector3.up * alturaCanvas;
        canvasWorld.transform.position = pos;

        // Billboard para câmera
        if (_cam)
        {
            Vector3 dir = (canvasWorld.transform.position - _cam.transform.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
                canvasWorld.transform.forward = dir;
        }
    }

    private void AtualizarUI()
    {
        // visibilidade
        bool visivel = false;
        if (Application.isMobilePlatform)
        {
            visivel = _selecionadoMobile && (_timerVisibilidadeMobile > 0f);
            if (_timerVisibilidadeMobile > 0f) _timerVisibilidadeMobile -= Time.deltaTime;
            if (_timerVisibilidadeMobile <= 0f) _selecionadoMobile = false;
        }
        else
        {
            visivel = _mouseDentro;
        }
        SafeSetPainel(visivel);

        // barra e texto
        if (barraStamina)
            barraStamina.fillAmount = staminaMax > 0f ? Mathf.Clamp01(_stamina / staminaMax) : 0f;

        if (textoEstado)
            textoEstado.text = ObterEstadoTexto();
    }

    private string ObterEstadoTexto()
    {
        if (_estaCansado) return "Cansado";
        if (_estadoAtual == Estado.Andando) return "Ocupado";
        return "Distraído";
    }

    private void SafeSetPainel(bool ativo)
    {
        if (painelInfo && painelInfo.activeSelf != ativo)
            painelInfo.SetActive(ativo);
    }

    // PC: exige um Collider no NPC (ou no filho com este script no mesmo GO).
    private void OnMouseEnter()
    {
        if (!Application.isMobilePlatform)
            _mouseDentro = true;
    }

    private void OnMouseExit()
    {
        if (!Application.isMobilePlatform)
            _mouseDentro = false;
    }

    private void HandleToqueMobile()
    {
        if (Input.touchCount == 0) return;

        Touch t = Input.GetTouch(0);
        if (t.phase != TouchPhase.Began) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Ray ray = _cam.ScreenPointToRay(t.position);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // Considera este NPC selecionado apenas se o toque foi nele (ou num filho)
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                _selecionadoMobile = true;
                _timerVisibilidadeMobile = tempoVisivelAposToque;
            }
        }
    }

    // =========================
    //   AUXILIARES DE PADRÃO
    // =========================
    private int ProximoHorario(int atual) => (atual + 1) & 3;
    private int ProximoAntiHorario(int atual) => (atual + 3) & 3;
    private int PulaSempreUm(int atual) => (atual + 2) & 3;

    private int PegarOutroIndice(int exceto)
    {
        int v;
        do { v = _rng.Next(0, 4); } while (v == exceto);
        return v;
    }

    // =========================
    //        GIZMOS
    // =========================
    private void OnDrawGizmosSelected()
    {
        if (!desenharGizmos) return;
        if (pontosCorredor == null) return;

        Gizmos.matrix = Matrix4x4.identity;

        for (int i = 0; i < pontosCorredor.Length; i++)
        {
            if (!pontosCorredor[i]) continue;
            Gizmos.DrawWireSphere(pontosCorredor[i].position, 0.15f);
        }

        if (pontosCorredor.Length >= 4 &&
            pontosCorredor[0] && pontosCorredor[1] && pontosCorredor[2] && pontosCorredor[3])
        {
            Gizmos.DrawLine(pontosCorredor[0].position, pontosCorredor[1].position);
            Gizmos.DrawLine(pontosCorredor[1].position, pontosCorredor[2].position);
            Gizmos.DrawLine(pontosCorredor[2].position, pontosCorredor[3].position);
            Gizmos.DrawLine(pontosCorredor[3].position, pontosCorredor[0].position);
        }

        // destino atual (runtime)
        #if UNITY_EDITOR
        if (Application.isPlaying && usarComportamentoCorredor)
        {
            int idx = Mathf.Clamp(_indiceAlvo, 0, pontosCorredor.Length - 1);
            var alvo = pontosCorredor[idx];
            if (alvo) Gizmos.DrawWireCube(alvo.position, Vector3.one * 0.25f);
        }
        #endif
    }

    // =========================
    //   API ESTÁTICA/UTIL
    // =========================
    public static int QuantosNPCsAtivosNoCorredor() => _ativosNoCorredor;
}
