using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

public class TurnosManager : MonoBehaviour
{
    public enum DonoTurno { Nenhum = 0, Supervisor = 1, Helly = 2 }

    [Header("NPCs e Destinos")]
    [SerializeField] private GameObject supervisor;
    [SerializeField] private Transform destinoSupervisor;
    [SerializeField] private GameObject helly;
    [SerializeField] private Transform destinoHelly;

    [Header("Pontos Iniciais (Idle)")]
    [Tooltip("Ponto onde o Supervisor retorna e fica em idle ao final de seu turno.")]
    [SerializeField] private Transform inicioSupervisor;

    [Tooltip("Ponto onde a Helly retorna e fica em idle ao final de seu turno.")]
    [SerializeField] private Transform inicioHelly;

    [Header("Relógio (UI – Image Filled)")]
    [SerializeField] private Image relogioUI;
    [SerializeField] private float duracaoRelogioT1 = 12f;
    [SerializeField] private float duracaoRelogioT2 = 12f;
    [SerializeField] private float duracaoRelogioT3 = 8f;

    [Header("Fluxo de início de turno")]
    [Tooltip("Se marcado, o primeiro turno pode começar sozinho (sem câmera).")]
    [SerializeField] private bool comecarTurnoAutomatico = false;

    [Tooltip("Se true, os turnos só começam quando a câmera chamar o evento.")]
    [SerializeField] private bool iniciarViaEventoDeCamera = true;

    [Header("Ajustes de Movimento")]
    [SerializeField] private float velocidadeAgente = 3.5f;
    [SerializeField] private float distanciaParada = 0.2f;

    [Header("Animator (bools)")]
    [Tooltip("Nome do bool que liga animação de andar.")]
    [SerializeField] private string boolWalking  = "Walking";
    [Tooltip("Nome do bool que liga animação de 'carregando' (parado carregando).")]
    [SerializeField] private string boolCharging = "Charging";

    [Header("Sistema de Peso/Baterias")]
    [SerializeField] private GerenciadorDePeso gerenciadorDePeso;
    [SerializeField] private float velocidadeCargaPlena = 0.35f;

    [Header("Turno 3 – Competição (alternância)")]
    [SerializeField] private float leva3Dur = 10f;
    [SerializeField] private float l3JitterHz = 6f;
    [SerializeField] private float l3IntervaloTroca = 0.25f;
    [SerializeField] private float l3ForcaConst = 1f;

    // Componentes
    private NavMeshAgent agenteSup;
    private Animator     animSup;
    private NavMeshAgent agenteHelly;
    private Animator     animHelly;

    // Estado de turnos
    private bool turnoEmAndamento = false;
    private int  turnoAtual = 0;
    private Coroutine rotinaTurno;

    // Relógio
    private bool  relogioAtivo = false;
    private float relogioDuracao = 0f;
    private float relogioT = 0f;

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
            SetIdle(animHelly);
        }

        // Pivôs "início" automáticos se vazio
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

        HideClock();

        // Se não for via evento de câmera e estiver marcado, pode começar sozinho
        if (comecarTurnoAutomatico && !iniciarViaEventoDeCamera)
        {
            IniciarTurnoInterno(1);
        }
    }

    void Update()
    {
        AtualizarRelogio();
        AtualizarWalkingPorVelocidade(agenteSup,   animSup);
        AtualizarWalkingPorVelocidade(agenteHelly, animHelly);
    }

    // =========================================================
    //      MÉTODOS PÚBLICOS PARA ANIMATION EVENT DA CÂMERA
    // =========================================================

    // Chame esse no Animation Event da ÚLTIMA animação de câmera do turno 1
    public void EventoCamera_IniciarTurno1()
    {
        IniciarTurnoPorCamera(1);
    }

    // Chame esse no Animation Event da ÚLTIMA animação de câmera do turno 2
    public void EventoCamera_IniciarTurno2()
    {
        IniciarTurnoPorCamera(2);
    }

    // Chame esse no Animation Event da ÚLTIMA animação de câmera do turno 3
    public void EventoCamera_IniciarTurno3()
    {
        IniciarTurnoPorCamera(3);
    }

    public void IniciarTurnoPorCamera(int turno)
    {
        if (!iniciarViaEventoDeCamera)
        {
            Debug.LogWarning("[TurnosManager] IniciarTurnoPorCamera chamado, mas 'iniciarViaEventoDeCamera' está desmarcado.");
        }

        if (turnoEmAndamento)
        {
            // já tem turno acontecendo, ignora
            return;
        }

        IniciarTurnoInterno(turno);
    }

    // =========================================================
    //                CONTROLE INTERNO DE TURNOS
    // =========================================================

    private void IniciarTurnoInterno(int turno)
    {
        if (rotinaTurno != null) StopCoroutine(rotinaTurno);

        turnoAtual = turno;
        turnoEmAndamento = true;

        switch (turno)
        {
            case 1:
                rotinaTurno = StartCoroutine(RotinaTurno1());
                break;
            case 2:
                rotinaTurno = StartCoroutine(RotinaTurno2());
                break;
            case 3:
            default:
                rotinaTurno = StartCoroutine(RotinaTurno3());
                break;
        }
    }

    private IEnumerator RotinaTurno1()
    {
        // aqui a câmera JÁ terminou, porque o Animation Event chamou o turno
        // Movimento do Supervisor 3s após início do turno
        StartCoroutine(AgendarMovimento(agenteSup,
            destinoSupervisor ? destinoSupervisor.position : supervisor.transform.position,
            animSup, 3f));

        yield return WaitAteChegar(agenteSup);

        // Carrega lado esquerdo
        SetCharging(animSup, true);
        yield return StartCoroutine(PreencherNivelLado(true, velocidadeCargaPlena));

        // Relógio de tarefa
        ShowClock(duracaoRelogioT1);
        yield return WaitRelogioTerminar();

        // Volta para o início
        SetCharging(animSup, false);
        GoTo(agenteSup, inicioSupervisor.position, animSup);
        yield return WaitDelayAndChegar(agenteSup, 0.4f);
        SetIdle(animSup);
        supervisor.transform.rotation = inicioSupervisor.rotation;

        turnoEmAndamento = false;

        // Se você quiser usar evento de câmera também entre turnos,
        // NÃO chama o 2 aqui automaticamente. Deixa a câmera decidir.
        // Se quiser manter auto, descomente:
        //
        // if (!iniciarViaEventoDeCamera)
        //     IniciarTurnoInterno(2);
    }

    private IEnumerator RotinaTurno2()
    {
        // Helly anda depois da câmera
        StartCoroutine(AgendarMovimento(agenteHelly,
            destinoHelly ? destinoHelly.position : helly.transform.position,
            animHelly, 3f));

        yield return WaitAteChegar(agenteHelly);

        SetCharging(animHelly, true);
        yield return StartCoroutine(PreencherNivelLado(false, velocidadeCargaPlena));

        ShowClock(duracaoRelogioT2);
        yield return WaitRelogioTerminar();

        SetCharging(animHelly, false);
        GoTo(agenteHelly, inicioHelly.position, animHelly);
        yield return WaitDelayAndChegar(agenteHelly, 0.4f);
        SetIdle(animHelly);
        helly.transform.rotation = inicioHelly.rotation;

        turnoEmAndamento = false;

        // Mesmo esquema do turno 1:
        // se quiser que o 3 só comece quando a câmera mandar, deixa assim.
        // Se quiser automático quando terminar o 2 e SEM câmera, descomenta:
        //
        // if (!iniciarViaEventoDeCamera)
        //     IniciarTurnoInterno(3);
    }

    private IEnumerator RotinaTurno3()
    {
        // Ambos iniciam movimento 3s após o início do turno
        StartCoroutine(AgendarMovimento(agenteSup,
            destinoSupervisor ? destinoSupervisor.position : supervisor.transform.position,
            animSup, 3f));
        StartCoroutine(AgendarMovimento(agenteHelly,
            destinoHelly ? destinoHelly.position : helly.transform.position,
            animHelly, 3f));

        if (agenteSup)   yield return WaitAteChegar(agenteSup);
        if (agenteHelly) yield return WaitAteChegar(agenteHelly);

        SetCharging(animSup,   true);
        SetCharging(animHelly, true);

        ShowClock(duracaoRelogioT3);
        yield return StartCoroutine(CompetirCargasAlternandoTransferindo(duracaoRelogioT3));
        yield return WaitRelogioTerminar();

        SetCharging(animSup,   false);
        SetCharging(animHelly, false);

        if (agenteSup)   GoTo(agenteSup, inicioSupervisor.position, animSup);
        if (agenteHelly) GoTo(agenteHelly, inicioHelly.position,    animHelly);

        if (agenteSup)   yield return WaitDelayAndChegar(agenteSup, 0.4f);
        if (agenteHelly) yield return WaitDelayAndChegar(agenteHelly, 0f);

        if (animSup)   SetIdle(animSup);
        if (animHelly) SetIdle(animHelly);

        if (agenteSup)   supervisor.transform.rotation = inicioSupervisor.rotation;
        if (agenteHelly) helly.transform.rotation      = inicioHelly.rotation;

        turnoEmAndamento = false;

        OnFimDeJogo();
    }

    // ------------------------ Agendadores ------------------------

    private IEnumerator AgendarMovimento(NavMeshAgent agente, Vector3 destino, Animator anim, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        GoTo(agente, destino, anim);
    }

    // ------------------------ Relógio ------------------------

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

    // ------------------------ Movimento / Animação ------------------------

    private void GoTo(NavMeshAgent agente, Vector3 destino, Animator anim)
    {
        if (agente == null) return;
        agente.isStopped = false;
        agente.ResetPath();
        agente.SetDestination(destino);
        SetWalking(anim, true);
        SetCharging(anim, false);
    }

    private IEnumerator WaitAteChegar(NavMeshAgent agente)
    {
        if (agente == null) yield break;
        while (agente.pathPending) yield return null;
        while (agente.remainingDistance > agente.stoppingDistance) yield return null;
        yield return null;
    }

    private IEnumerator WaitDelayAndChegar(NavMeshAgent agente, float delay)
    {
        float t = 0f; bool chegou = false;
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
        if (charging) anim.SetBool(boolWalking, false);
    }

    private void SetIdle(Animator anim)
    {
        if (anim == null) return;
        anim.SetBool(boolWalking,  false);
        anim.SetBool(boolCharging, false);
    }

    // ------------------------ Pesos / Baterias ------------------------

    private void ZerarPesos()
    {
        if (gerenciadorDePeso == null) return;
        gerenciadorDePeso.nivelEsquerda = 0f;
        gerenciadorDePeso.nivelDireita  = 0f;
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

            // alternância básica
            trocaTimer -= dt;
            if (trocaTimer <= 0f)
            {
                supDominante = !supDominante;
                trocaTimer   += Mathf.Max(0.05f, l3IntervaloTroca);
            }

            float alvo = supDominante ? 1f : 0f;

            // micro-eventos que intensificam a força por curtos períodos
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

    // ------------------------ Finais/Reset ------------------------

    private void ForcarEncerrarTurno()
    {
        if (rotinaTurno != null) StopCoroutine(rotinaTurno);
        HideClock();

        if (agenteSup != null && inicioSupervisor != null)
        {
            agenteSup.Warp(inicioSupervisor.position);
            agenteSup.isStopped = true;
            supervisor.transform.rotation = inicioSupervisor.rotation;
            SetIdle(animSup);
        }

        if (agenteHelly != null && inicioHelly != null)
        {
            agenteHelly.Warp(inicioHelly.position);
            agenteHelly.isStopped = true;
            helly.transform.rotation = inicioHelly.rotation;
            SetIdle(animHelly);
        }

        turnoEmAndamento = false;
        turnoAtual = 0;
    }

    private void OnFimDeJogo()
    {
        Debug.Log("[TurnosManager] Fim de jogo (placeholder). Dispare sua cena/estado aqui.");
    }
}
