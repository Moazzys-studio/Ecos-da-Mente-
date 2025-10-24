using UnityEngine;
using UnityEngine.AI;

public class TurnosManager : MonoBehaviour
{
    [Header("NPCs e Destinos")]
    [SerializeField] private GameObject supervisor;
    [SerializeField] private Transform destinoSupervisor;
    [SerializeField] private GameObject helly;
    [SerializeField] private Transform destinoHelly;

    [Header("Configuração de Turnos (marque no Inspector)")]
    [SerializeField] private bool iniciarTurno1;
    [SerializeField] private bool iniciarTurno2;
    [SerializeField] private bool iniciarTurno3;
    [SerializeField] private bool encerrarTurnoAtual;

    [Header("Status de Controle")]
    [SerializeField] private bool turnoEmAndamento = false;
    [SerializeField] private int turnoAtual = 0;

    [Header("Ajustes de Movimento")]
    [SerializeField] private float velocidadeAgente = 3.5f;
    [SerializeField] private float distanciaParada = 0.2f;

    [Header("Animador (bools usadas)")]
    [SerializeField] private string boolCharging = "Charging";

    [Header("Gerenciador de Peso")]
    [SerializeField] private GerenciadorDePeso gerenciadorDePeso;
    [Tooltip("Velocidade para a bateria esquerda encher (0→1 por segundo).")]
    [SerializeField] private float velocidadeCarga = 0.25f;

    // Posições iniciais
    private Vector3 posInicialSupervisor;
    private Vector3 posInicialHelly;

    // Cache de componentes
    private NavMeshAgent agenteSup;
    private Animator     animSup;
    private NavMeshAgent agenteHelly;
    private Animator     animHelly;

    // Estado interno
    private bool supervisorCarregando = false;

    private void Start()
    {
        if (supervisor != null)
        {
            posInicialSupervisor = supervisor.transform.position;
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
    }

    private void Update()
    {
        if (!turnoEmAndamento)
        {
            if (iniciarTurno1) { IniciarTurno(1); iniciarTurno1 = false; }
            else if (iniciarTurno2) { IniciarTurno(2); iniciarTurno2 = false; }
            else if (iniciarTurno3) { IniciarTurno(3); iniciarTurno3 = false; }
        }

        if (encerrarTurnoAtual)
        {
            EncerrarTurno();
            encerrarTurnoAtual = false;
        }

        AtualizarMovimentoNPCs();
        AtualizarCargaSupervisor();
    }

    private void IniciarTurno(int turno)
    {
        turnoAtual = turno;
        turnoEmAndamento = true;
        supervisorCarregando = false;

        if (turno == 1)
        {
            // Começa equilibrado visualmente
            if (gerenciadorDePeso != null)
            {
                gerenciadorDePeso.nivelEsquerda = 0.5f;
                gerenciadorDePeso.nivelDireita  = 0.5f;
            }

            MoverNPC(agenteSup, destinoSupervisor != null ? destinoSupervisor.position : supervisor.transform.position);
        }
        else if (turno == 2)
        {
            MoverNPC(agenteHelly, destinoHelly != null ? destinoHelly.position : helly.transform.position);
        }
        else if (turno == 3)
        {
            if (agenteSup != null)
                MoverNPC(agenteSup, destinoSupervisor != null ? destinoSupervisor.position : supervisor.transform.position);
            if (agenteHelly != null)
                MoverNPC(agenteHelly, destinoHelly != null ? destinoHelly.position : helly.transform.position);
        }
    }

    private void EncerrarTurno()
    {
        turnoEmAndamento = false;
        supervisorCarregando = false;

        // Volta NPCs
        if (agenteSup != null)   MoverNPC(agenteSup, posInicialSupervisor, false);
        if (agenteHelly != null) MoverNPC(agenteHelly, posInicialHelly, false);

        // Reseta animações
        if (animSup != null)   animSup.SetBool(boolCharging, false);
        if (animHelly != null) animHelly.SetBool(boolCharging, false);
    }

    private void MoverNPC(NavMeshAgent agente, Vector3 destino, bool desligarCharging = true)
    {
        if (agente == null) return;

        agente.isStopped = false;
        agente.SetDestination(destino);

        if (desligarCharging)
        {
            Animator a = agente.GetComponent<Animator>();
            if (a != null) a.SetBool(boolCharging, false);
        }
    }

    private void AtualizarMovimentoNPCs()
    {
        // Só precisamos checar o supervisor no Turno 1 por enquanto
        if (agenteSup != null && turnoAtual == 1)
            VerificarChegadaSupervisor();
    }

    private void VerificarChegadaSupervisor()
    {
        if (agenteSup.pathPending) return;

        bool chegou = agenteSup.remainingDistance <= agenteSup.stoppingDistance;
        if (!chegou) return;

        if (!supervisorCarregando)
        {
            // Para de andar e entra no estado de carregar
            agenteSup.isStopped = true;
            animSup?.SetBool(boolCharging, true);
            supervisorCarregando = true;

            // Zera os líquidos para iniciar a “carga” da esquerda 0→100
            if (gerenciadorDePeso != null)
            {
                gerenciadorDePeso.nivelEsquerda = 0f;
                gerenciadorDePeso.nivelDireita  = 0f;
            }
        }
    }

    private void AtualizarCargaSupervisor()
    {
        // Somente no Turno 1 e após o supervisor iniciar a carga
        if (turnoAtual != 1 || !supervisorCarregando || gerenciadorDePeso == null) return;

        // Interpola a bateria esquerda de 0 até 1 (100%)
        gerenciadorDePeso.nivelEsquerda = Mathf.MoveTowards(
            gerenciadorDePeso.nivelEsquerda, 1f, Time.deltaTime * velocidadeCarga
        );

        // Mantém a direita como está (0 no começo), o jogador compensa no pêndulo.
        // O GerenciadorDePeso aplica o peso → externalAngleOffset no BalanceController.

        // Atualização visual imediata (opcional – o Gerenciador já faz no Update)
        if (gerenciadorDePeso.bateriaEsquerda != null)
            gerenciadorDePeso.bateriaEsquerda.fillLevel = gerenciadorDePeso.nivelEsquerda;
        if (gerenciadorDePeso.bateriaDireita != null)
            gerenciadorDePeso.bateriaDireita.fillLevel = gerenciadorDePeso.nivelDireita;
    }
}
