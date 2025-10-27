using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Cinemachine;
using System.Collections;

public class TurnosManager : MonoBehaviour
{
    [Header("NPCs e Destinos")]
    [SerializeField] private GameObject supervisor;
    [SerializeField] private Transform destinoSupervisor;
    [SerializeField] private GameObject helly;
    [SerializeField] private Transform destinoHelly;

    [Header("Câmeras (Cinemachine)")]
    [SerializeField] private CinemachineVirtualCamera vcamPrincipal;
    [SerializeField] private CinemachineVirtualCamera vcamSupervisor;
    [SerializeField] private CinemachineVirtualCamera vcamHelly;
    [SerializeField] private CinemachineVirtualCamera vcamEco;

    [Header("Relógio (UI – Image Filled 360)")]
    [SerializeField] private Image relogioUI;
    [SerializeField] private float duracaoRelogioT1 = 12f;
    [SerializeField] private float duracaoRelogioT2 = 12f;
    [SerializeField] private float duracaoRelogioT3 = 8f;

    [Header("Configuração de Turnos (debug)")]
    [SerializeField] private bool iniciarTurno1;
    [SerializeField] private bool iniciarTurno2;
    [SerializeField] private bool iniciarTurno3;

    [Header("Ajustes de Movimento")]
    [SerializeField] private float velocidadeAgente = 3.5f;
    [SerializeField] private float distanciaParada = 0.2f;

    [Header("Animador (bools usadas)")]
    [SerializeField] private string boolCharging = "Charging";

    [Header("Sistema de Peso")]
    [SerializeField] private GerenciadorDePeso gerenciadorDePeso;
    [SerializeField] private float velocidadeCargaPlena = 0.35f;

    [Header("Encerramento de turno")]
    [SerializeField] private float delayEncerramento = 4f;

    [Header("Turno 3 – Levas (competição)")]
    [SerializeField] private float leva3Dur = 10f;
    [SerializeField] private float l3JitterHz = 6f;      // jitter aleatório (quanto maior, mais nervoso)

    [Header("Turno 3 – Leva 3 (alternância)")]
    [SerializeField] private float l3IntervaloTroca = 0.25f; // alterna a direção a cada X s
    [SerializeField] private float l3ForcaConst = 1f; // força constante aplicada na Leva 3



    [Header("Turno 3 - FOV")]
    [SerializeField] private float fovPadrao = 41f;
    [SerializeField] private float fovZoomOut = 63f;
    [SerializeField] private float durAberturaFOV = 1.0f; // abre 41→63 no começo do T3



    // Estado inicial
    private Vector3 posInicialSupervisor;
    private Quaternion rotInicialSupervisor;
    private Vector3 posInicialHelly;
    private Quaternion rotInicialHelly;

    // Componentes
    private NavMeshAgent agenteSup;
    private Animator     animSup;
    private NavMeshAgent agenteHelly;
    private Animator     animHelly;

    // Estado
    private bool turnoEmAndamento = false;
    private int  turnoAtual = 0;

    // Relógio
    private bool  relogioAtivo = false;
    private float relogioDuracao = 0f;
    private float relogioT = 0f;

    // FOV T3
    private bool fovDinamicoAtivo = false; // fechamento 63→41
    private float distSupInicial = 0f;
    private float distHellyInicial = 0f;
    private bool fovAbrindo = false;   // abertura 41→63
    private float fovAbrirT = 0f;

    private Coroutine rotinaTurno;

    void Start()
    {
        if (supervisor != null)
        {
            posInicialSupervisor = supervisor.transform.position;
            rotInicialSupervisor = supervisor.transform.rotation;
            agenteSup = supervisor.GetComponent<NavMeshAgent>();
            animSup   = supervisor.GetComponent<Animator>();
            if (agenteSup != null)
            {
                agenteSup.speed = velocidadeAgente;
                agenteSup.stoppingDistance = distanciaParada;
            }
        }
        if (helly != null)
        {
            posInicialHelly = helly.transform.position;
            rotInicialHelly = helly.transform.rotation;
            agenteHelly = helly.GetComponent<NavMeshAgent>();
            animHelly   = helly.GetComponent<Animator>();
            if (agenteHelly != null)
            {
                agenteHelly.speed = velocidadeAgente;
                agenteHelly.stoppingDistance = distanciaParada;
            }
        }

        if (gerenciadorDePeso == null)
            gerenciadorDePeso = FindFirstObjectByType<GerenciadorDePeso>();

        if (vcamPrincipal != null) vcamPrincipal.m_Lens.FieldOfView = fovPadrao;

        HideClock(); // relógio e pai ocultos
    }

    void Update()
    {
        if (!turnoEmAndamento)
        {
            if (iniciarTurno1) { iniciarTurno1 = false; IniciarTurno(1); }
            else if (iniciarTurno2) { iniciarTurno2 = false; IniciarTurno(2); }
            else if (iniciarTurno3) { iniciarTurno3 = false; IniciarTurno(3); }
        }

        AtualizarFOVTurno3();
        AtualizarRelogio();
    }

    // ---------------- Turnos ----------------

    private void IniciarTurno(int turno)
    {
        if (rotinaTurno != null) StopCoroutine(rotinaTurno);
        turnoAtual = turno;
        turnoEmAndamento = true;

        SetVcamPrioridades(vcamEcoTop: true);

        switch (turno)
        {
            case 1: rotinaTurno = StartCoroutine(RotinaTurno1()); break;
            case 2: rotinaTurno = StartCoroutine(RotinaTurno2()); break;
            case 3: rotinaTurno = StartCoroutine(RotinaTurno3()); break;
        }
    }

    private IEnumerator RotinaTurno1()
    {
        ZerarPesos();

        if (vcamSupervisor != null && supervisor != null)
        {
            vcamSupervisor.Follow = supervisor.transform;
            vcamSupervisor.LookAt = supervisor.transform;
            SetVcamPrioridades(supervisorTop: true);
        }

        MoverNPC(agenteSup, destinoSupervisor != null ? destinoSupervisor.position : supervisor.transform.position);
        yield return WaitAteChegar(agenteSup);

        agenteSup.isStopped = true; animSup?.SetBool(boolCharging, true);
        SetVcamPrioridades(vcamEcoTop: true);
        yield return StartCoroutine(PreencherNivelLado(true, velocidadeCargaPlena));

        ShowClock(duracaoRelogioT1);
        yield return WaitRelogioTerminar();

        // volta andando imediatamente
        animSup?.SetBool(boolCharging, false);
        MoverNPC(agenteSup, posInicialSupervisor);
        yield return WaitDelayAndChegar(agenteSup, delayEncerramento);
        if (agenteSup != null) agenteSup.transform.rotation = rotInicialSupervisor;

        turnoEmAndamento = false;
        IniciarTurno(2);
    }

    private IEnumerator RotinaTurno2()
    {
        ZerarPesos();

        if (vcamHelly != null && helly != null)
        {
            vcamHelly.Follow = helly.transform;
            vcamHelly.LookAt = helly.transform;
            SetVcamPrioridades(hellyTop: true);
        }

        MoverNPC(agenteHelly, destinoHelly != null ? destinoHelly.position : helly.transform.position);
        yield return WaitAteChegar(agenteHelly);

        agenteHelly.isStopped = true; animHelly?.SetBool(boolCharging, true);
        SetVcamPrioridades(vcamEcoTop: true);
        yield return StartCoroutine(PreencherNivelLado(false, velocidadeCargaPlena));

        ShowClock(duracaoRelogioT2);
        yield return WaitRelogioTerminar();

        // volta andando imediatamente
        animHelly?.SetBool(boolCharging, false);
        MoverNPC(agenteHelly, posInicialHelly);
        yield return WaitDelayAndChegar(agenteHelly, delayEncerramento);
        if (agenteHelly != null) agenteHelly.transform.rotation = rotInicialHelly;

        turnoEmAndamento = false;
        IniciarTurno(3);
    }

    private IEnumerator RotinaTurno3()
{

    // FOV abre 41→63 enquanto caminham
    if (vcamPrincipal != null) vcamPrincipal.m_Lens.FieldOfView = fovPadrao;
    fovAbrindo = true; fovAbrirT = 0f;

    Vector3 destSup = (destinoSupervisor ? destinoSupervisor.position : supervisor.transform.position);
    Vector3 destHel = (destinoHelly     ? destinoHelly.position     : helly.transform.position);
    distSupInicial   = (agenteSup   ? Vector3.Distance(agenteSup.transform.position,   destSup) : 0f);
    distHellyInicial = (agenteHelly ? Vector3.Distance(agenteHelly.transform.position, destHel) : 0f);

    if (agenteSup)   MoverNPC(agenteSup, destSup);
    if (agenteHelly) MoverNPC(agenteHelly, destHel);
    fovDinamicoAtivo = true;

    if (agenteSup)   yield return WaitAteChegar(agenteSup);
    if (agenteHelly) yield return WaitAteChegar(agenteHelly);

    // entra em "carga" e começa o turno (sem lev as)
    if (animSup)   animSup.SetBool(boolCharging, true);
    if (animHelly) animHelly.SetBool(boolCharging, true);
    SetVcamPrioridades(vcamEcoTop: true);



    // alternância roda pelo tempo do relógio
    yield return StartCoroutine(CompetirCargasAlternandoTransferindo(duracaoRelogioT3));
    
    // relógio aparece AGORA e dura o turno todo
    ShowClock(duracaoRelogioT3);

    // espera o relógio terminar (caso termine 1 frame depois)
    yield return WaitRelogioTerminar();

    // encerra: volta andando
    if (animSup)   animSup.SetBool(boolCharging, false);
    if (animHelly) animHelly.SetBool(boolCharging, false);
    if (agenteSup)   MoverNPC(agenteSup, posInicialSupervisor);
    if (agenteHelly) MoverNPC(agenteHelly, posInicialHelly);

    if (agenteSup)   yield return WaitDelayAndChegar(agenteSup, delayEncerramento);
    if (agenteHelly) yield return WaitDelayAndChegar(agenteHelly, 0f);

    if (agenteSup)   agenteSup.transform.rotation = rotInicialSupervisor;
    if (agenteHelly) agenteHelly.transform.rotation = rotInicialHelly;

    // fim
    turnoEmAndamento = false;
    fovDinamicoAtivo = false;
    fovAbrindo = false;
    if (vcamPrincipal) vcamPrincipal.m_Lens.FieldOfView = fovPadrao;

    OnFimDeJogo();
}


    // ---------------- Helpers de Movimento / Câmera ----------------

    private void MoverNPC(NavMeshAgent agente, Vector3 destino)
    {
        if (agente == null) return;
        agente.isStopped = false;
        agente.ResetPath();
        agente.SetDestination(destino);
    }

    private IEnumerator WaitAteChegar(NavMeshAgent agente)
    {
        if (agente == null) yield break;
        while (agente.pathPending) yield return null;
        while (agente.remainingDistance > agente.stoppingDistance) yield return null;
        yield return null;
    }

    // espera ao mesmo tempo: (tempo >= delay) E (agente chegou)
    private IEnumerator WaitDelayAndChegar(NavMeshAgent agente, float delay)
    {
        float t = 0f;
        bool chegou = false;
        while (true)
        {
            if (!chegou)
            {
                if (agente != null && !agente.pathPending && agente.remainingDistance <= agente.stoppingDistance)
                    chegou = true;
            }
            if (t >= delay && chegou) break;
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void SetVcamPrioridades(bool vcamEcoTop = false, bool supervisorTop = false, bool hellyTop = false)
    {
        const int LOW = 10;
        const int HIGH = 20;

        if (vcamEco != null)        vcamEco.Priority        = vcamEcoTop     ? HIGH : LOW;
        if (vcamSupervisor != null) vcamSupervisor.Priority = supervisorTop   ? HIGH : LOW;
        if (vcamHelly != null)      vcamHelly.Priority      = hellyTop       ? HIGH : LOW;
        if (vcamPrincipal != null)  vcamPrincipal.Priority  = HIGH;
    }

    // ---------------- Relógio ----------------

    private void ShowClock(float dur)
    {
        relogioDuracao = Mathf.Max(0.01f, dur);
        relogioT = 0f;
        relogioAtivo = true;

        if (relogioUI != null)
        {
            if (relogioUI.transform.parent != null)
                relogioUI.transform.parent.gameObject.SetActive(true);

            relogioUI.enabled = true;
            relogioUI.fillAmount = 1f;
        }
    }

    private void HideClock()
    {
        relogioAtivo = false;
        if (relogioUI != null)
        {
            relogioUI.enabled = false;
            relogioUI.fillAmount = 0f;
            if (relogioUI.transform.parent != null)
                relogioUI.transform.parent.gameObject.SetActive(false);
        }
    }

    private void AtualizarRelogio()
    {
        if (!relogioAtivo || relogioUI == null) return;

        relogioT += Time.deltaTime;
        float frac = Mathf.Clamp01(relogioT / relogioDuracao);
        relogioUI.fillAmount = 1f - frac;

        if (frac >= 1f) HideClock();
    }

    private IEnumerator WaitRelogioTerminar()
    {
        while (relogioAtivo) yield return null;
    }

    // ---------------- Pesos / Baterias ----------------

    private void ZerarPesos()
    {
        if (gerenciadorDePeso == null) return;
        gerenciadorDePeso.nivelEsquerda = 0f;
        gerenciadorDePeso.nivelDireita = 0f;
        AtualizaUIBaterias();
    }
    
    // No T3 não zeramos: garantimos um estado inicial jogável.
// - Se os dois estiverem quase vazios, dá um baseline (evita "0/0").
// - Se os dois estiverem lotados, baixa para abrir espaço (evita "1/1" que trava).




    private IEnumerator PreencherNivelLado(bool esquerda, float vel)
    {
        if (gerenciadorDePeso == null) yield break;

        if (esquerda)
        {
            while (!Mathf.Approximately(gerenciadorDePeso.nivelEsquerda, 1f))
            {
                gerenciadorDePeso.nivelEsquerda = Mathf.MoveTowards(gerenciadorDePeso.nivelEsquerda, 1f, Time.deltaTime * vel);
                AtualizaUIBaterias();
                yield return null;
            }
        }
        else
        {
            while (!Mathf.Approximately(gerenciadorDePeso.nivelDireita, 1f))
            {
                gerenciadorDePeso.nivelDireita = Mathf.MoveTowards(gerenciadorDePeso.nivelDireita, 1f, Time.deltaTime * vel);
                AtualizaUIBaterias();
                yield return null;
            }
        }
    }




private IEnumerator CompetirCargasAlternandoTransferindo(float dur)
{
    if (gerenciadorDePeso == null) yield break;

    float t = 0f;

    // bias único 0..1 que representa a "carga total" (um só valor)
    // esq = bias01, dir = 1 - bias01
    float bias01 = Mathf.Clamp01(gerenciadorDePeso.nivelEsquerda);

    // começa aleatório: true = SUP domina (vai para 1), false = HELLY domina (vai para 0)
    bool supDominante = (Random.value > 0.5f);
    float trocaTimer  = Mathf.Max(0.05f, l3IntervaloTroca);

    // evento simples de imprevisibilidade
    float eventoTimer = 0f;              // >0 => evento ativo
    float eventoBoost = 1f;              // multiplicador momentâneo
    const float eventoChancePorSegundo = 0.18f;
    const float eventoDur = 0.6f;
    const float eventoMult = 1.8f;

    while (t < dur)
    {
        float dt = Time.deltaTime;

        // alterna lado 8↔80 no intervalo fixo
        trocaTimer -= dt;
        if (trocaTimer <= 0f)
        {
            supDominante = !supDominante;
            trocaTimer   += Mathf.Max(0.05f, l3IntervaloTroca);
        }

        // alvo do bias: 1 (SUP) ou 0 (HELLY)
        float alvo = supDominante ? 1f : 0f;

        // imprevisibilidade: às vezes dá um "gás" pro sentido atual
        if (eventoTimer > 0f)
        {
            eventoTimer -= dt;
            if (eventoTimer <= 0f) eventoBoost = 1f;
        }
        else
        {
            // chance por segundo
            if (Random.value < eventoChancePorSegundo * dt)
            {
                eventoTimer = eventoDur;
                eventoBoost = eventoMult; // favorece o sentido atual por um tempinho
            }
        }

        // força para aproximar do alvo (com pequeno jitter pra não ficar robô)
        float dir = Mathf.Sign(alvo - bias01); // -1, 0 ou +1
        float step = l3ForcaConst * eventoBoost * dt;

        // jitter com média ~0 (escala baixa)
        float jitter = (Random.value * 2f - 1f) * (l3JitterHz * 0.02f) * dt;

        // aproxima do alvo
        if (Mathf.Abs(alvo - bias01) <= step)
            bias01 = alvo;
        else
            bias01 = Mathf.Clamp01(bias01 + dir * step + jitter);

        // 1) Dirige as BATERIAS (o Gerenciador lê isso todo frame em Play)
        if (gerenciadorDePeso.bateriaEsquerda != null)
            gerenciadorDePeso.bateriaEsquerda.fillLevel = bias01;

        if (gerenciadorDePeso.bateriaDireita != null)
            gerenciadorDePeso.bateriaDireita.fillLevel = 1f - bias01;

        // 2) Aplica já neste frame (mesmo valor) para o torque/viés entrar na mecânica
        gerenciadorDePeso.SetNiveis(bias01, 1f - bias01);


        t += dt;
        yield return null;
    }
}


    private void AtualizaUIBaterias()
    {
        if (gerenciadorDePeso == null) return;

        if (gerenciadorDePeso.bateriaEsquerda != null)
            gerenciadorDePeso.bateriaEsquerda.fillLevel = gerenciadorDePeso.nivelEsquerda;

        if (gerenciadorDePeso.bateriaDireita != null)
            gerenciadorDePeso.bateriaDireita.fillLevel = gerenciadorDePeso.nivelDireita;
    }

    // ---------------- FOV Turno 3 ----------------

    private void AtualizarFOVTurno3()
    {
        if (vcamPrincipal == null) return;

        // 1) Abertura 41→63 no início do T3
        if (turnoAtual == 3 && fovAbrindo)
        {
            fovAbrirT += Time.deltaTime;
            float k = Mathf.Clamp01(fovAbrirT / Mathf.Max(0.0001f, durAberturaFOV));
            vcamPrincipal.m_Lens.FieldOfView = Mathf.Lerp(fovPadrao, fovZoomOut, k);

            if (k >= 1f) fovAbrindo = false; // terminou abrir
            else return; // enquanto abre, não fecha
        }

        // 2) Fechamento 63→41 acontece DEPOIS da abertura, enquanto andam até os pontos finais
        if (!fovDinamicoAtivo || turnoAtual != 3) return;

        float progSup = 1f;
        float progHel = 1f;

        if (agenteSup != null && destinoSupervisor != null && distSupInicial > 0.01f)
            progSup = 1f - Mathf.Clamp01(agenteSup.remainingDistance / distSupInicial);

        if (agenteHelly != null && destinoHelly != null && distHellyInicial > 0.01f)
            progHel = 1f - Mathf.Clamp01(agenteHelly.remainingDistance / distHellyInicial);

        float progresso = Mathf.Min(progSup, progHel);
        vcamPrincipal.m_Lens.FieldOfView = Mathf.Lerp(fovZoomOut, fovPadrao, progresso);
    }

    // ---------------- Fim de jogo (placeholder) ----------------
    private void OnFimDeJogo()
    {
        Debug.Log("[TurnosManager] Fim de jogo (placeholder). Dispare sua cena/estado aqui.");
    }
}
