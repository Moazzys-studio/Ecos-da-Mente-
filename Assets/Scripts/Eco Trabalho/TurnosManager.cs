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

    // Posições iniciais
    [SerializeField] private Vector3 posInicialSupervisor;
    [SerializeField] private Vector3 posInicialHelly;

    // Cache de componentes
    [SerializeField] private NavMeshAgent agenteSup;
    [SerializeField] private Animator animSup;
    [SerializeField] private NavMeshAgent agenteHelly;
    [SerializeField] private Animator animHelly;

    private void Start()
    {
        if (supervisor != null)
        {
            posInicialSupervisor = supervisor.transform.position;
            agenteSup = supervisor.GetComponent<NavMeshAgent>();
            animSup = supervisor.GetComponent<Animator>();
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
            animHelly = helly.GetComponent<Animator>();
            if (agenteHelly != null)
            {
                agenteHelly.speed = velocidadeAgente;
                agenteHelly.stoppingDistance = distanciaParada;
            }
        }
    }

    private void Update()
    {
        if (!turnoEmAndamento)
        {
            if (iniciarTurno1)
            {
                IniciarTurno(1);
                iniciarTurno1 = false;
            }
            else if (iniciarTurno2)
            {
                IniciarTurno(2);
                iniciarTurno2 = false;
            }
            else if (iniciarTurno3)
            {
                IniciarTurno(3);
                iniciarTurno3 = false;
            }
        }

        if (encerrarTurnoAtual)
        {
            EncerrarTurno();
            encerrarTurnoAtual = false;
        }

        AtualizarMovimentoNPCs();
    }

    private void IniciarTurno(int turno)
    {
        turnoAtual = turno;
        turnoEmAndamento = true;

        switch (turno)
        {
            case 1:
                MoverNPC(agenteSup, destinoSupervisor.position);
                break;

            case 2:
                MoverNPC(agenteHelly, destinoHelly.position);
                break;

            case 3:
                MoverNPC(agenteSup, destinoSupervisor.position);
                MoverNPC(agenteHelly, destinoHelly.position);
                break;
        }
    }

    private void EncerrarTurno()
    {
        turnoEmAndamento = false;

        // Volta para posição inicial
        if (agenteSup != null)
            MoverNPC(agenteSup, posInicialSupervisor, false);

        if (agenteHelly != null)
            MoverNPC(agenteHelly, posInicialHelly, false);

        if (animSup != null) animSup.SetBool(boolCharging, false);
        if (animHelly != null) animHelly.SetBool(boolCharging, false);
    }

    private void MoverNPC(NavMeshAgent agente, Vector3 destino, bool ativarCharging = true)
    {
        if (agente == null) return;

        agente.isStopped = false;
        agente.SetDestination(destino);

        Animator anim = agente.GetComponent<Animator>();
        if (anim != null)
            anim.SetBool(boolCharging, false);
    }

    private void AtualizarMovimentoNPCs()
    {
        if (agenteSup != null && turnoAtual != 0)
            VerificarChegada(agenteSup, animSup);

        if (agenteHelly != null && (turnoAtual == 2 || turnoAtual == 3))
            VerificarChegada(agenteHelly, animHelly);
    }

    private void VerificarChegada(NavMeshAgent agente, Animator anim)
    {
        if (agente.remainingDistance <= agente.stoppingDistance && !agente.pathPending)
        {
            if (anim != null)
                anim.SetBool(boolCharging, true);

            agente.isStopped = true;
        }
    }
}
