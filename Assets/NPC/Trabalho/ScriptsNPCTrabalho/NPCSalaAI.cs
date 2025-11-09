using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCSalaAI : MonoBehaviour
{
    [Header("Referências")]
    public NPCTrabalho npcTrabalho;         // patrulha do corredor (liga/desliga)
    public NPCSocializacao socializacao;    // conversa (já funciona em qualquer lugar)
    public Animator animator;               // opcional

    [Header("Exploração em Sala")]
    [Tooltip("Tempo min/max parado entre pontos dentro da sala.")]
    public Vector2 idleSala = new Vector2(0.6f, 1.4f);

    [Tooltip("Distância para considerar que chegou a um ponto da sala.")]
    public float stopDistSala = 0.25f;

    [Tooltip("Número de alvos aleatórios antes de decidir sair (pode variar).")]
    public Vector2 qtdPontosAntesDeSair = new Vector2(3, 7);

    [Header("Prioridades quando cansado")]
    public bool priorizarQuandoCansado = true;
    public PortaController.TipoSala[] preferidasCansado = new[]
    {
        PortaController.TipoSala.Banheiro,
        PortaController.TipoSala.BreakRoom,
        PortaController.TipoSala.Deposito
    };

    [Header("Sensores")]
    [Tooltip("Salas conhecidas (preencha na cena ou deixe vazio e popularemos em runtime).")]
    public List<SalaPlana> todasAsSalas = new List<SalaPlana>();

    // --- runtime ---
    private NavMeshAgent _agent;
    private SalaPlana _salaAtual;
    private bool _emSala;
    private int _pontosRestantesNaSala;
    private Coroutine _coFluxoSala;

    private void Reset()
    {
        npcTrabalho   = GetComponent<NPCTrabalho>();
        socializacao  = GetComponent<NPCSocializacao>();
        animator      = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (!npcTrabalho)  npcTrabalho = GetComponent<NPCTrabalho>();
        if (!socializacao) socializacao = GetComponent<NPCSocializacao>();

        if (todasAsSalas.Count == 0)
            todasAsSalas.AddRange(FindObjectsOfType<SalaPlana>());
    }

    // ===== Porta: pausa de segurança antes de cruzar =====
    public void PausarAntesDeEntrarSala(float segundos)
    {
        StartCoroutine(CoPausa(segundos));
    }
    private IEnumerator CoPausa(float s)
    {
        bool prev = _agent.isStopped;
        _agent.isStopped = true;
        yield return new WaitForSeconds(s);
        _agent.isStopped = prev;
    }

    // ===== Entrar/Sair de salas (via triggers no chão) =====
    private void OnTriggerEnter(Collider other)
    {
        var sala = other.GetComponent<SalaPlana>();
        if (sala != null)
        {
            _salaAtual = sala;
            EntrouNaSala();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        var sala = other.GetComponent<SalaPlana>();
        if (sala != null && sala == _salaAtual)
        {
            SaiuDaSala();
            _salaAtual = null;
        }
    }

    private void EntrouNaSala()
    {
        if (_emSala) return;
        _emSala = true;

        // Desliga patrulha do corredor para não brigar com o NavMeshAgent
        if (npcTrabalho) npcTrabalho.enabled = false;

        // Define quantos pontos vai explorar antes de sair
        _pontosRestantesNaSala = Mathf.RoundToInt(Random.Range(qtdPontosAntesDeSair.x, qtdPontosAntesDeSair.y));

        if (_coFluxoSala != null) StopCoroutine(_coFluxoSala);
        _coFluxoSala = StartCoroutine(CoExplorarSala());
    }

    private void SaiuDaSala()
    {
        if (!_emSala) return;
        _emSala = false;

        if (_coFluxoSala != null) { StopCoroutine(_coFluxoSala); _coFluxoSala = null; }

        // Reativa patrulha do corredor
        if (npcTrabalho) npcTrabalho.enabled = true;
    }

    private IEnumerator CoExplorarSala()
    {
        while (_emSala && _salaAtual != null)
        {
            // Escolhe um ponto dentro da sala
            Vector3 destino;
            if (!_salaAtual.TrySortearPontoDentro(out destino))
            {
                // fallback: centro do renderer
                destino = _salaAtual.transform.position;
            }

            _agent.SetDestination(destino);

            // Anda até chegar
            while (_emSala && _salaAtual != null && (!_agent.pathPending && _agent.remainingDistance > stopDistSala))
            {
                yield return null;
            }

            // Idle curto entre pontos
            float pausa = Random.Range(idleSala.x, idleSala.y);
            yield return new WaitForSeconds(pausa);

            _pontosRestantesNaSala--;
            if (_pontosRestantesNaSala <= 0)
            {
                // Decide se sai ou fica mais um pouco (leve chance de estender)
                if (Random.value < 0.35f)
                    _pontosRestantesNaSala = Mathf.RoundToInt(Random.Range(qtdPontosAntesDeSair.x, qtdPontosAntesDeSair.y));
                else
                    break; // sai da sala no próximo OnTriggerExit natural
            }
        }
    }

    // ===== Escolha de próxima sala quando cansado =====
    public SalaPlana EscolherSalaDescanso()
    {
        if (!priorizarQuandoCansado || todasAsSalas.Count == 0) return EscolherSalaAleatoria();

        // tenta uma preferida
        var shuffled = new List<SalaPlana>(todasAsSalas);
        Shuffle(shuffled);
        foreach (var s in shuffled)
        {
            foreach (var pref in preferidasCansado)
            {
                if (s.tipoSala == pref) return s;
            }
        }
        return shuffled[0];
    }

    public SalaPlana EscolherSalaAleatoria()
    {
        if (todasAsSalas.Count == 0) return null;
        return todasAsSalas[Random.Range(0, todasAsSalas.Count)];
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
