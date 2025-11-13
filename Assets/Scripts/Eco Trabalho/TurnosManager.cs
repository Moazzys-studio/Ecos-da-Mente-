using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public enum DonoTurno
{
    Nenhum = 0,
    Supervisor = 1,
    Helly = 2
}

public class TurnosManager : MonoBehaviour
{
    [Header("NPCs (Controllers)")]
    [SerializeField] private NpcTurnoController supervisorNpc;
    [SerializeField] private NpcTurnoController hellyNpc;

    [Header("Destinos / Pontos Iniciais")]
    [SerializeField] private Transform destinoSupervisor;
    [SerializeField] private Transform destinoHelly;
    [SerializeField] private Transform inicioSupervisor;
    [SerializeField] private Transform inicioHelly;

    [Header("Relógio UI")]
    [SerializeField] private RelogioTurnoUI relogioTurno;
    [SerializeField] private float duracaoRelogioT1 = 12f;
    [SerializeField] private float duracaoRelogioT2 = 12f;
    [SerializeField] private float duracaoRelogioT3 = 8f;

    [Header("Fluxo de início de turno")]
    [Tooltip("Se marcado, o primeiro turno pode começar sozinho (sem camera).")]
    [SerializeField] private bool comecarTurnoAutomatico = false;

    [Tooltip("Se true, os turnos só começam quando a camera mandou o evento.")]
    [SerializeField] private bool iniciarViaEventoDeCamera = true;

    [Header("Tempos do Turno")]
    [Tooltip("Tempo entre trocar para a camera do dono do turno e o NPC começar a andar.")]
    [SerializeField] private float delayAntesMover = 3f;

    [Tooltip("Tempo DEPOIS QUE o NPC COMEÇA A ANDAR até disparar a CameraInterativa.")]
    [SerializeField] private float delayAntesCameraInterativa = 1f;

    [Header("Sistema de Peso/Baterias")]
    [SerializeField] private GerenciadorDePeso gerenciadorDePeso;
    [SerializeField] private float velocidadeCargaPlena = 0.35f;

    [Header("Turno 3 – Competição (alternância)")]
    [SerializeField] private float leva3Dur = 10f;
    [SerializeField] private float l3JitterHz = 6f;
    [SerializeField] private float l3IntervaloTroca = 0.25f;
    [SerializeField] private float l3ForcaConst = 1f;

    [Header("Câmeras dos Turnos")]
    [SerializeField] private CameraTurnoController cameraTurnoController;

    [Header("Skybox Mystic (Shader _Phase)")]
    [Tooltip("Material do skybox que usa o shader MysticBalanceSkyboxURP (com propriedade _Phase).")]
    [SerializeField] private Material skyboxMaterial;

    [Tooltip("Fase inicial (quando o jogo começa).")]
    [SerializeField] private float faseInicio = 0f;

    [Tooltip("Fase da paleta durante o Turno 1 (Supervisor).")]
    [SerializeField] private float faseTurno1 = 1f;

    [Tooltip("Fase da paleta durante o Turno 2 (Helly).")]
    [SerializeField] private float faseTurno2 = 2f;

    [Tooltip("Fase da paleta durante o Turno 3 (embate, dupla).")]
    [SerializeField] private float faseTurno3 = 3f;

    [Tooltip("Duração da interpolação de paleta do skybox (segundos).")]
    [SerializeField] private float duracaoTransicaoSkybox = 1.0f;

    // Estado de turnos
    private bool turnoEmAndamento = false;
    private int  turnoAtual = 0;
    private Coroutine rotinaTurno;

    // Skybox
    private Coroutine rotinaSkybox;

    private void Start()
    {
        if (gerenciadorDePeso == null)
            gerenciadorDePeso = FindFirstObjectByType<GerenciadorDePeso>();

        if (inicioSupervisor == null && supervisorNpc != null)
            inicioSupervisor = supervisorNpc.transform;

        if (inicioHelly == null && hellyNpc != null)
            inicioHelly = hellyNpc.transform;

        if (relogioTurno != null)
            relogioTurno.PararRelogio();

        // Fase inicial do skybox
        if (skyboxMaterial != null)
            skyboxMaterial.SetFloat("_Phase", faseInicio);

        if (comecarTurnoAutomatico && !iniciarViaEventoDeCamera)
            IniciarTurnoInterno(1);
    }

    private void Update()
    {
        // Nada complexo aqui; NPC cuida da própria animação de andar.
    }

    // ======================= CHAMADO PELO SCRIPT DA CÂMERA =======================

    public void IniciarTurnoPorCamera(int turno)
    {
        if (!iniciarViaEventoDeCamera)
        {
            Debug.LogWarning("[TurnosManager] IniciarTurnoPorCamera chamado, mas iniciarViaEventoDeCamera está false.");
        }

        if (turnoEmAndamento)
            return;

        IniciarTurnoInterno(turno);
    }

    // ======================= CONTROLE INTERNO DE TURNOS =======================

    private void IniciarTurnoInterno(int turno)
    {
        if (rotinaTurno != null)
            StopCoroutine(rotinaTurno);

        turnoAtual = turno;
        turnoEmAndamento = true;

        // Atualiza paleta do skybox de acordo com o turno
        switch (turnoAtual)
        {
            case 1:
                TrocarFaseSkybox(faseTurno1);
                break;
            case 2:
                TrocarFaseSkybox(faseTurno2);
                break;
            case 3:
                TrocarFaseSkybox(faseTurno3);
                break;
        }

        // Troca a câmera para o dono do turno
        if (cameraTurnoController != null)
        {
            DonoTurno dono = DonoTurno.Nenhum;

            switch (turnoAtual)
            {
                case 1:
                    dono = DonoTurno.Supervisor;
                    break;

                case 2:
                    dono = DonoTurno.Helly;
                    break;

                case 3:
                    // Para o turno 3 você decide se é Eco/Supervisor/Helly,
                    // aqui deixei Supervisor como exemplo.
                    dono = DonoTurno.Supervisor;
                    break;
            }

            cameraTurnoController.AtivarCameraDoDono(dono);
        }

        // Escolhe qual rotina roda
        switch (turnoAtual)
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

    // ======================= ROTINAS DE TURNO =======================

    // ---- TURNO 1: SUPERVISOR ----
    private IEnumerator RotinaTurno1()
    {
        // 1) Delay antes de o Supervisor começar a andar
        if (delayAntesMover > 0f)
            yield return new WaitForSeconds(delayAntesMover);

        // 2) Começa a andar
        Coroutine moverSup = null;
        if (supervisorNpc != null && destinoSupervisor != null)
            moverSup = StartCoroutine(supervisorNpc.AndarAte(destinoSupervisor));

        // 3) Delay B (enquanto ele já está andando)
        if (delayAntesCameraInterativa > 0f)
            yield return new WaitForSeconds(delayAntesCameraInterativa);

        // 4) Dispara CameraInterativa na câmera do Supervisor
        if (cameraTurnoController != null)
            cameraTurnoController.DispararCameraInterativa(DonoTurno.Supervisor);

        // 5) Garante que ele CHEGOU no destino
        if (moverSup != null)
            yield return moverSup;

        // 6) CHEGOU NO DESTINO → VOLTA PRA CÂMERA DO ECO
        if (cameraTurnoController != null)
            cameraTurnoController.AtivarCameraDoDono(DonoTurno.Nenhum);

        // 7) Agora segue o fluxo normal (carga, relógio, etc)
        if (supervisorNpc != null)
            supervisorNpc.SetCharging(true);

        yield return StartCoroutine(PreencherNivelLado(true, velocidadeCargaPlena));

        // 8) Relógio do turno 1
        if (relogioTurno != null)
        {
            relogioTurno.IniciarRelogio(duracaoRelogioT1);
            while (relogioTurno.EstaAtivo)
                yield return null;
        }

        // 9) Volta Supervisor ao ponto inicial
        if (supervisorNpc != null && inicioSupervisor != null)
        {
            supervisorNpc.SetCharging(false);
            yield return StartCoroutine(supervisorNpc.AndarAte(inicioSupervisor));
            supervisorNpc.AjustarRotacao(inicioSupervisor.rotation);
            supervisorNpc.SetIdle();
        }

        // 10) Fim do turno 1
        turnoEmAndamento = false;

        // 👉 Ao terminar a contagem do relógio do turno 1, começamos o turno 2
        IniciarProximoTurno(2);
    }

    // ---- TURNO 2: HELLY ----
    private IEnumerator RotinaTurno2()
    {
        if (delayAntesMover > 0f)
            yield return new WaitForSeconds(delayAntesMover);

        Coroutine moverHelly = null;
        if (hellyNpc != null && destinoHelly != null)
            moverHelly = StartCoroutine(hellyNpc.AndarAte(destinoHelly));

        if (delayAntesCameraInterativa > 0f)
            yield return new WaitForSeconds(delayAntesCameraInterativa);

        if (cameraTurnoController != null)
            cameraTurnoController.DispararCameraInterativa(DonoTurno.Helly);

        if (moverHelly != null)
            yield return moverHelly;

        // CHEGOU NO DESTINO → VOLTA PRA CÂMERA DO ECO
        if (cameraTurnoController != null)
            cameraTurnoController.AtivarCameraDoDono(DonoTurno.Nenhum);

        if (hellyNpc != null)
            hellyNpc.SetCharging(true);

        yield return StartCoroutine(PreencherNivelLado(false, velocidadeCargaPlena));

        // Relógio do turno 2
        if (relogioTurno != null)
        {
            relogioTurno.IniciarRelogio(duracaoRelogioT2);
            while (relogioTurno.EstaAtivo)
                yield return null;
        }

        if (hellyNpc != null && inicioHelly != null)
        {
            hellyNpc.SetCharging(false);
            yield return StartCoroutine(hellyNpc.AndarAte(inicioHelly));
            hellyNpc.AjustarRotacao(inicioHelly.rotation);
            hellyNpc.SetIdle();
        }

        turnoEmAndamento = false;

        // 👉 Ao terminar a contagem do relógio do turno 2, começamos o turno 3
        IniciarProximoTurno(3);
    }

    // ---- TURNO 3: COMPETIÇÃO ----
    private IEnumerator RotinaTurno3()
    {
        // Supervisor
        Coroutine cSup = null;
        if (supervisorNpc != null && destinoSupervisor != null)
        {
            cSup = StartCoroutine(FluxoMovimentoComCamera(DonoTurno.Supervisor, supervisorNpc, destinoSupervisor));
        }

        // Helly
        Coroutine cHelly = null;
        if (hellyNpc != null && destinoHelly != null)
        {
            cHelly = StartCoroutine(FluxoMovimentoComCamera(DonoTurno.Helly, hellyNpc, destinoHelly));
        }

        if (cSup != null)   yield return cSup;
        if (cHelly != null) yield return cHelly;

        if (supervisorNpc != null) supervisorNpc.SetCharging(true);
        if (hellyNpc != null)      hellyNpc.SetCharging(true);

        if (relogioTurno != null)
        {
            relogioTurno.IniciarRelogio(duracaoRelogioT3);
        }

        yield return StartCoroutine(CompetirCargasAlternandoTransferindo(leva3Dur));

        // Espera o relógio acabar, se estiver rodando
        if (relogioTurno != null)
        {
            while (relogioTurno.EstaAtivo)
                yield return null;
        }

        if (supervisorNpc != null) supervisorNpc.SetCharging(false);
        if (hellyNpc != null)      hellyNpc.SetCharging(false);

        // Volta ao início
        if (supervisorNpc != null && inicioSupervisor != null)
        {
            yield return StartCoroutine(supervisorNpc.AndarAte(inicioSupervisor));
            supervisorNpc.AjustarRotacao(inicioSupervisor.rotation);
            supervisorNpc.SetIdle();
        }

        if (hellyNpc != null && inicioHelly != null)
        {
            yield return StartCoroutine(hellyNpc.AndarAte(inicioHelly));
            hellyNpc.AjustarRotacao(inicioHelly.rotation);
            hellyNpc.SetIdle();
        }

        turnoEmAndamento = false;

        OnFimDeJogo();
    }

    // Fluxo usado no turno 3 (delay A -> andar -> delay B -> CameraInterativa)
    private IEnumerator FluxoMovimentoComCamera(
        DonoTurno dono,
        NpcTurnoController npc,
        Transform destino
    )
    {
        if (npc == null || destino == null)
            yield break;

        // Delay A
        if (delayAntesMover > 0f)
            yield return new WaitForSeconds(delayAntesMover);

        // Começa a andar
        Coroutine mover = StartCoroutine(npc.AndarAte(destino));

        // Delay B
        if (delayAntesCameraInterativa > 0f)
            yield return new WaitForSeconds(delayAntesCameraInterativa);

        // Dispara camera interativa
        if (cameraTurnoController != null)
            cameraTurnoController.DispararCameraInterativa(dono);

        // Garante que ele chegou
        if (mover != null)
            yield return mover;
    }

    // ======================= SKYBOX (FASES) =======================

    private void TrocarFaseSkybox(float faseAlvo)
    {
        if (skyboxMaterial == null)
            return;

        if (rotinaSkybox != null)
            StopCoroutine(rotinaSkybox);

        rotinaSkybox = StartCoroutine(AnimarFaseSkybox(faseAlvo));
    }

    private IEnumerator AnimarFaseSkybox(float faseAlvo)
    {
        float dur = Mathf.Max(0.01f, duracaoTransicaoSkybox);
        float faseInicial = skyboxMaterial.GetFloat("_Phase");
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float fase = Mathf.Lerp(faseInicial, faseAlvo, k);
            skyboxMaterial.SetFloat("_Phase", fase);
            yield return null;
        }

        skyboxMaterial.SetFloat("_Phase", faseAlvo);
    }

    // ======================= PESO / BATERIAS =======================

    private IEnumerator PreencherNivelLado(bool esquerda, float vel)
    {
        if (gerenciadorDePeso == null)
            yield break;

        if (esquerda)
        {
            while (!Mathf.Approximately(gerenciadorDePeso.nivelEsquerda, 1f))
            {
                gerenciadorDePeso.nivelEsquerda =
                    Mathf.MoveTowards(gerenciadorDePeso.nivelEsquerda, 1f, Time.deltaTime * vel);
                AtualizaUIBaterias();
                yield return null;
            }
        }
        else
        {
            while (!Mathf.Approximately(gerenciadorDePeso.nivelDireita, 1f))
            {
                gerenciadorDePeso.nivelDireita =
                    Mathf.MoveTowards(gerenciadorDePeso.nivelDireita, 1f, Time.deltaTime * vel);
                AtualizaUIBaterias();
                yield return null;
            }
        }
    }

    private IEnumerator CompetirCargasAlternandoTransferindo(float dur)
    {
        if (gerenciadorDePeso == null)
            yield break;

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
                if (eventoTimer <= 0f)
                    eventoBoost = 1f;
            }
            else
            {
                if (Random.value < eventoChancePorSegundo * dt)
                {
                    eventoTimer = eventoDur;
                    eventoBoost = eventoMult;
                }
            }

            float dir    = Mathf.Sign(alvo - bias01);
            float step   = l3ForcaConst * eventoBoost * dt;
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
        if (gerenciadorDePeso == null)
            return;

        if (gerenciadorDePeso.bateriaEsquerda != null)
            gerenciadorDePeso.bateriaEsquerda.fillLevel = gerenciadorDePeso.nivelEsquerda;

        if (gerenciadorDePeso.bateriaDireita != null)
            gerenciadorDePeso.bateriaDireita.fillLevel = gerenciadorDePeso.nivelDireita;
    }

    // ======================= FINAL / RESET =======================

    private void IniciarProximoTurno(int proximoTurno)
    {
        // aqui você sempre encadeia: terminou relógio do turno atual -> próximo turno
        // se em algum momento quiser voltar a depender só da câmera,
        // pode colocar um if usando iniciarViaEventoDeCamera.
        IniciarTurnoInterno(proximoTurno);
    }

    private void OnFimDeJogo()
    {
        Debug.Log("[TurnosManager] Fim de jogo (placeholder).");
    }
}
