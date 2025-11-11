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

    [Header("Pontos Iniciais (Idle)")]
    [Tooltip("Empty Transform onde o NPC fica em Idle e para onde retorna no fim do turno.")]
    [SerializeField] private Transform inicioSupervisor;
    [SerializeField] private Transform inicioHelly;

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

    [Header("Animator (bools)")]
    [SerializeField] private string boolWalking  = "Walking";
    [SerializeField] private string boolCharging = "Charging";

    [Header("Sistema de Peso")]
    [SerializeField] private GerenciadorDePeso gerenciadorDePeso;
    [SerializeField] private float velocidadeCargaPlena = 0.35f;

    [Header("Encerramento de turno")]
    [SerializeField] private float delayEncerramento = 4f;

    [Header("Turno 3 – Levas (competição)")]
    [SerializeField] private float leva3Dur = 10f;
    [SerializeField] private float l3JitterHz = 6f;

    [Header("Turno 3 – Leva 3 (alternância)")]
    [SerializeField] private float l3IntervaloTroca = 0.25f;
    [SerializeField] private float l3ForcaConst = 1f;

    [Header("Turno 3 - FOV")]
    [SerializeField] private float fovPadrao = 41f;
    [SerializeField] private float fovZoomOut = 63f;
    [SerializeField] private float durAberturaFOV = 1.0f;

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
    private bool fovDinamicoAtivo = false;
    private float distSupInicial = 0f;
    private float distHellyInicial = 0f;
    private bool fovAbrindo = false;
    private float fovAbrirT = 0f;

    private Coroutine rotinaTurno;

    void Start()
    {
        if (supervisor != null)
        {
            agenteSup = supervisor.GetComponent<NavMeshAgent>();
            animSup   = supervisor.GetComponent<Animator>();
            if (agenteSup != null)
            {
                agenteSup.speed = velocidadeAgente;
                agenteSup.stoppingDistance = distanciaParada;
            }
            // Garante Idle inicial
            SetIdle(animSup);
        }

        if (helly != null)
        {
            agenteHelly = helly.GetComponent<NavMeshAgent>();
            animHelly   = helly.GetComponent<Animator>();
            if (agenteHelly != null)
            {
                agenteHelly.speed = velocidadeAgente;
                agenteHelly.stoppingDistance = distanciaParada;
            }
            // Garante Idle inicial
            SetIdle(animHelly);
        }

        // Se não arrastou os pontos iniciais, usa a posição atual como fallback
        if (inicioSupervisor == null && supervisor != null)
        {
            GameObject t = new GameObject("InicioSupervisor (auto)");
            t.transform.position = supervisor.transform.position;
            t.transform.rotation = supervisor.transform.rotation;
            inicioSupervisor = t.transform;
        }
        if (inicioHelly == null && helly != null)
        {
            GameObject t = new GameObject("InicioHelly (auto)");
            t.transform.position = helly.transform.position;
            t.transform.rotation = helly.transform.rotation;
            inicioHelly = t.transform;
        }

        if (gerenciadorDePeso == null)
            gerenciadorDePeso = FindFirstObjectByType<GerenciadorDePeso>();

        if (vcamPrincipal != null) vcamPrincipal.m_Lens.FieldOfView = fovPadrao;

        HideClock();
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

        // Atualiza Walking on/off por velocidade (segurança extra)
        AtualizarWalkingPorVelocidade(agenteSup, animSup);
        AtualizarWalkingPorVelocidade(agenteHelly, animHelly);
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

        // Idle → Walking
        GoTo(agenteSup, destinoSupervisor != null ? destinoSupervisor.position : supervisor.transform.position, animSup);
        yield return WaitAteChegar(agenteSup);

        // Walking → Charging
        SetCharging(animSup, true);

        SetVcamPrioridades(vcamEcoTop: true);
        yield return StartCoroutine(PreencherNivelLado(true, velocidadeCargaPlena));

        ShowClock(duracaoRelogioT1);
        yield return WaitRelogioTerminar();

        // Charging → Walking (voltar)
        SetCharging(animSup, false);
        GoTo(agenteSup, inicioSupervisor.position, animSup);

        yield return WaitDelayAndChegar(agenteSup, delayEncerramento);

        // Walking → Idle ao chegar no ponto inicial
        SetIdle(animSup);
        supervisor.transform.rotation = inicioSupervisor.rotation;

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

        // Idle → Walking
        GoTo(agenteHelly, destinoHelly != null ? destinoHelly.position : helly.transform.position, animHelly);
        yield return WaitAteChegar(agenteHelly);

        // Walking → Charging
        SetCharging(animHelly, true);

        SetVcamPrioridades(vcamEcoTop: true);
        yield return StartCoroutine(PreencherNivelLado(false, velocidadeCargaPlena));

        ShowClock(duracaoRelogioT2);
        yield return WaitRelogioTerminar();

        // Charging → Walking (voltar)
        SetCharging(animHelly, false);
        GoTo(agenteHelly, inicioHelly.position, animHelly);

        yield return WaitDelayAndChegar(agenteHelly, delayEncerramento);

        // Walking → Idle ao chegar no ponto inicial
        SetIdle(animHelly);
        helly.transform.rotation = inicioHelly.rotation;

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

        // Idle → Walking (ambos)
        if (agenteSup)   GoTo(agenteSup, destSup, animSup);
        if (agenteHelly) GoTo(agenteHelly, destHel, animHelly);
        fovDinamicoAtivo = true;

        if (agenteSup)   yield return WaitAteChegar(agenteSup);
        if (agenteHelly) yield return WaitAteChegar(agenteHelly);

        // Walking → Charging
        SetCharging(animSup,   true);
        SetCharging(animHelly, true);
        SetVcamPrioridades(vcamEcoTop: true);

        // Competição de cargas durante o relógio
        ShowClock(duracaoRelogioT3);
        yield return StartCoroutine(CompetirCargasAlternandoTransferindo(duracaoRelogioT3));
        yield return WaitRelogioTerminar();

        // Charging → Walking (voltar)
        SetCharging(animSup,   false);
        SetCharging(animHelly, false);
        if (agenteSup)   GoTo(agenteSup, inicioSupervisor.position, animSup);
        if (agenteHelly) GoTo(agenteHelly, inicioHelly.position, animHelly);

        if (agenteSup)   yield return WaitDelayAndChegar(agenteSup, delayEncerramento);
        if (agenteHelly) yield return WaitDelayAndChegar(agenteHelly, 0f);

        // Walking → Idle
        if (animSup)   SetIdle(animSup);
        if (animHelly) SetIdle(animHelly);
        if (agenteSup)   agenteSup.transform.rotation = inicioSupervisor.rotation;
        if (agenteHelly) helly.transform.rotation     = inicioHelly.rotation;

        turnoEmAndamento = false;
        fovDinamicoAtivo = false;
        fovAbrindo = false;
        if (vcamPrincipal) vcamPrincipal.m_Lens.FieldOfView = fovPadrao;

        OnFimDeJogo();
    }

    // ---------------- Helpers de Movimento / Animação / Câmera ----------------

    private void GoTo(NavMeshAgent agente, Vector3 destino, Animator anim)
    {
        if (agente == null) return;
        agente.isStopped = false;
        agente.ResetPath();
        agente.SetDestination(destino);
        SetWalking(anim, true);   // Idle → Walking
        SetCharging(anim, false); // garante que não está em Charging
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

    private void AtualizarWalkingPorVelocidade(NavMeshAgent agente, Animator anim)
    {
        if (agente == null || anim == null) return;
        // Considera movimento real do agente (para blend suave no Animator se quiser)
        bool andando = !agente.isStopped && agente.velocity.sqrMagnitude > 0.01f;
        anim.SetBool(boolWalking, andando);
    }

    private void SetWalking(Animator anim, bool walking)
    {
        if (anim == null) return;
        anim.SetBool(boolWalking, walking);
    }

    private void SetCharging(Animator anim, bool charging)
    {
        if (anim == null) return;
        anim.SetBool(boolCharging, charging);
        if (charging) anim.SetBool(boolWalking, false); // força sair de Walking
    }

    private void SetIdle(Animator anim)
    {
        if (anim == null) return;
        anim.SetBool(boolWalking,  false);
        anim.SetBool(boolCharging, false);
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

        float bias01 = Mathf.Clamp01(gerenciadorDePeso.nivelEsquerda);

        bool supDominante = (Random.value > 0.5f);
        float trocaTimer  = Mathf.Max(0.05f, l3IntervaloTroca);

        float eventoTimer = 0f;
        float eventoBoost = 1f;
        const float eventoChancePorSegundo = 0.18f;
        const float eventoDur = 0.6f;
        const float eventoMult = 1.8f;

        while (t < dur)
        {
            float dt = Time.deltaTime;

            trocaTimer -= dt;
            if (trocaTimer <= 0f)
            {
                supDominante = !supDominante;
                trocaTimer   += Mathf.Max(0.05f, l3IntervaloTroca);
            }

            float alvo = supDominante ? 1f : 0f;

            if (eventoTimer > 0f)
            {
                eventoTimer -= dt;
                if (eventoTimer <= 0f) eventoBoost = 1f;
            }
            else
            {
                if (Random.value < eventoChancePorSegundo * dt)
                {
                    eventoTimer = eventoDur;
                    eventoBoost = eventoMult;
                }
            }

            float dir = Mathf.Sign(alvo - bias01);
            float step = l3ForcaConst * eventoBoost * dt;
            float jitter = (Random.value * 2f - 1f) * (l3JitterHz * 0.02f) * dt;

            if (Mathf.Abs(alvo - bias01) <= step)
                bias01 = alvo;
            else
                bias01 = Mathf.Clamp01(bias01 + dir * step + jitter);

            if (gerenciadorDePeso.bateriaEsquerda != null)
                gerenciadorDePeso.bateriaEsquerda.fillLevel = bias01;

            if (gerenciadorDePeso.bateriaDireita != null)
                gerenciadorDePeso.bateriaDireita.fillLevel = 1f - bias01;

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

        if (turnoAtual == 3 && fovAbrindo)
        {
            fovAbrirT += Time.deltaTime;
            float k = Mathf.Clamp01(fovAbrirT / Mathf.Max(0.0001f, durAberturaFOV));
            vcamPrincipal.m_Lens.FieldOfView = Mathf.Lerp(fovPadrao, fovZoomOut, k);

            if (k >= 1f) fovAbrindo = false;
            else return;
        }

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
