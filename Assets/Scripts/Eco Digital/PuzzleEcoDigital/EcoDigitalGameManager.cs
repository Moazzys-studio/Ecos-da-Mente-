using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EcoDigitalGameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject painelInicio;
    [SerializeField] private bool mostrarNoStart = false;

    [Header("Câmera / Animator")]
    [Tooltip("Animator que controla as animações de câmera (ex.: Animator da Virtual Camera).")]
    [SerializeField] private Animator animatorCamera;

    [Tooltip("Nome do parâmetro Trigger no Animator (ex.: 'CameraJogo7'). Usado se o modo for Trigger.")]
    [SerializeField] private string nomeTriggerCamera = "CameraJogo7";

    // ===== Entrada Inicial da Câmera =====
    public enum ModoEntradaCamera { Trigger, PlayState, CrossFadeState }

    [Header("Entrada Inicial da Câmera")]
    [Tooltip("Se marcado, ao iniciar o jogo (Start) já pulamos para a câmera 'inicial'.")]
    [SerializeField] private bool pularParaCameraInicialNoStart = true;

    [Tooltip("Como pular para a câmera inicial: por Trigger, Play direto no State, ou CrossFade.")]
    [SerializeField] private ModoEntradaCamera modoEntradaInicial = ModoEntradaCamera.Trigger;

    [Tooltip("Nome do estado da câmera (ex.: 'CameraJogo7'). Usado se o modo for PlayState ou CrossFadeState.")]
    [SerializeField] private string nomeEstadoCameraInicial = "CameraJogo7";

    [Tooltip("Duração do CrossFade (s). Válido apenas no modo CrossFadeState.")]
    [SerializeField, Min(0f)] private float crossFadeDuration = 0.15f;

    // ===== Disparo / Alvos =====
    [Header("Disparo / Alvos")]
    [Tooltip("Transform do Eco (alvo).")]
    [SerializeField] private Transform eco;

    [Tooltip("Spawners (4 no seu caso). Ordem livre, mas lembre qual é qual).")]
    [SerializeField] private Transform[] spawners = new Transform[4];

    [Tooltip("Yaw extra por spawner (graus). Ex.: [-45, +45, 0, 0].")]
    [SerializeField] private float[] yawExtraPorSpawner = new float[4];

    [Tooltip("Se quiser alternância por turno, liste aqui os índices dos spawners que participarão do turno atual (ex.: 0 e 1). Se vazio, usa o spawnerIndex da regra.")]
    [SerializeField] private int[] spawnersDoTurno = new int[] { 0, 1 };

    [Tooltip("Prefab do projétil (global/opcional). Será usado se a regra não tiver sua própria lista.")]
    [SerializeField] private GameObject prefabProjetil;

    public enum ModoSelecaoPrefab
    {
        Primeiro,
        AleatorioCadaTiro,
        RoundRobin
    }

    [System.Serializable]
    public struct RegraDeTiroPorEstado
    {
        [Tooltip("Nome do estado no Animator (pode ser só o nome do State). Ex.: 'CameraJogo6'")]
        public string stateName;

        [Tooltip("Índice do spawner que dispara (0 a N-1). Só é usado se 'spawnersDoTurno' estiver vazio.")]
        public int spawnerIndex;

        [Tooltip("Quantidade de tiros ao entrar nesse estado.")]
        public int quantidadeTiros;

        [Tooltip("Tempo entre cada tiro (s).")]
        public float intervaloEntreTiros;

        [Tooltip("Velocidade do projétil (m/s).")]
        public float velocidadeProjetil;

        [Header("Prefabs desta seção")]
        [Tooltip("Se vazio/nulo, usa o prefab global (prefabProjetil).")]
        public GameObject[] prefabsProjetil;

        [Tooltip("Como escolher o prefab desta seção.")]
        public ModoSelecaoPrefab modoSelecao;
    }

    [Header("Regras por Estado")]
    [Tooltip("Mapeie aqui: Estado da câmera -> Quem atira? Quantos tiros? Velocidade? Quais prefabs?")]
    [SerializeField]
    private RegraDeTiroPorEstado[] regras = new RegraDeTiroPorEstado[] { };

    [Header("Eventos")]
    public UnityEvent OnPainelInicioMostrado;

    // ===== controle interno =====
    private int _lastFullPathHash = 0;

    // round-robin: índice atual por regra (para lista de prefabs)
    private int[] _rrIndex;

    // alternância de spawner por turno
    private int _cursorAlternanciaSpawner = 0;

    private void Start()
    {
        if (painelInicio != null)
            painelInicio.SetActive(mostrarNoStart);

        // inicializa round-robin por regra
        _rrIndex = (regras != null && regras.Length > 0) ? new int[regras.Length] : new int[0];

        // Pular para câmera inicial se configurado
        if (pularParaCameraInicialNoStart)
        {
            PularParaCameraInicial();
        }
    }

    private void Update()
    {
        // Monitora entrada em novos estados de câmera (Layer 0).
        if (animatorCamera == null) return;

        AnimatorStateInfo s = animatorCamera.GetCurrentAnimatorStateInfo(0);
        int currentHash = s.fullPathHash;

        if (currentHash != _lastFullPathHash)
        {
            _lastFullPathHash = currentHash;
            // Entrou em um novo estado → aplica regras
            AplicarRegrasParaEstadoAtual(s);
        }
    }

    /// <summary>Mostra o painel de início do jogo (idempotente).</summary>
    public void MostrarPainelInicio()
    {
        if (painelInicio == null) return;

        if (!painelInicio.activeSelf)
        {
            painelInicio.SetActive(true);
            OnPainelInicioMostrado?.Invoke();
        }
    }

    /// <summary>Esconder painel.</summary>
    public void EsconderPainelInicio()
    {
        if (painelInicio == null) return;
        if (painelInicio.activeSelf) painelInicio.SetActive(false);
    }

    /// <summary>Chamado pelo botão "Jogar". Fecha o painel e posiciona câmera.</summary>
    public void IniciarJogo()
    {
        EsconderPainelInicio();
        PularParaCameraInicial();
    }

    // ===== APIs para pular para câmera =====

    /// <summary>Pula para a câmera inicial segundo o modo configurado.</summary>
    public void PularParaCameraInicial()
    {
        if (animatorCamera == null)
            animatorCamera = GetComponent<Animator>() ?? FindFirstObjectByType<Animator>();

        if (animatorCamera == null)
        {
            Debug.LogWarning("[EcoDigitalGameManager] Animator da câmera não encontrado.");
            return;
        }

        switch (modoEntradaInicial)
        {
            case ModoEntradaCamera.Trigger:
                PularParaCameraPorTrigger(nomeTriggerCamera);
                break;

            case ModoEntradaCamera.PlayState:
                PularParaCameraPorEstado(nomeEstadoCameraInicial, usarCrossFade:false, 0f);
                break;

            case ModoEntradaCamera.CrossFadeState:
                PularParaCameraPorEstado(nomeEstadoCameraInicial, usarCrossFade:true, crossFadeDuration);
                break;
        }
    }

    /// <summary>Aciona um Trigger no Animator para trocar a câmera.</summary>
    public void PularParaCameraPorTrigger(string triggerName)
    {
        if (animatorCamera == null) return;

        if (HasTrigger(animatorCamera, triggerName))
        {
            animatorCamera.ResetTrigger(triggerName);
            animatorCamera.SetTrigger(triggerName);
        }
        else
        {
            Debug.LogWarning($"[EcoDigitalGameManager] Trigger '{triggerName}' não existe no Animator.");
        }
    }

    /// <summary>Força a transição para um estado de câmera (Play direto ou CrossFade).</summary>
    public void PularParaCameraPorEstado(string stateName, bool usarCrossFade, float duration)
    {
        if (animatorCamera == null) return;
        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogWarning("[EcoDigitalGameManager] Nome de estado vazio ao tentar pular de câmera.");
            return;
        }

        int layer = 0;
        if (usarCrossFade)
            animatorCamera.CrossFade(stateName, Mathf.Max(0f, duration), layer, 0f);
        else
            animatorCamera.Play(stateName, layer, 0f);
    }

    // ======== Regras / Disparo ========
    private void AplicarRegrasParaEstadoAtual(AnimatorStateInfo stateInfo)
    {
        if (regras == null || regras.Length == 0) return;

        // Coleta índices das regras cujo nome bate com o estado atual
        List<int> idxRegrasAlvo = new List<int>();
        for (int i = 0; i < regras.Length; i++)
        {
            var r = regras[i];
            if (string.IsNullOrWhiteSpace(r.stateName)) continue;
            if (stateInfo.IsName(r.stateName) || stateInfo.IsName("Base Layer." + r.stateName))
                idxRegrasAlvo.Add(i);
        }
        if (idxRegrasAlvo.Count == 0) return;

        foreach (int idx in idxRegrasAlvo)
        {
            var regra = regras[idx];

            // Validação mínima do Eco e dos arrays
            if (eco == null)
            {
                Debug.LogWarning("[EcoDigitalGameManager] Transform do Eco não atribuído.");
                continue;
            }
            if (spawners == null || spawners.Length == 0)
            {
                Debug.LogWarning("[EcoDigitalGameManager] Nenhum spawner atribuído.");
                continue;
            }

            // Valida prefabs (regra ou global)
            bool temLista = (regra.prefabsProjetil != null && regra.prefabsProjetil.Length > 0);
            if (!temLista && prefabProjetil == null)
            {
                Debug.LogWarning($"[EcoDigitalGameManager] Nenhum prefab de projétil definido (regra '{regra.stateName}' sem lista e prefab global vazio).");
                continue;
            }

            StartCoroutine(SequenciaDeTiros(idx,
                Mathf.Max(1, regra.quantidadeTiros),
                Mathf.Max(0f, regra.intervaloEntreTiros),
                Mathf.Max(0.1f, regra.velocidadeProjetil)));
        }
    }

    private IEnumerator SequenciaDeTiros(int regraIndex, int quantidade, float intervalo, float velocidade)
    {
        for (int shot = 0; shot < quantidade; shot++)
        {
            // Escolhe spawner para ESTE tiro:
            int idxSpawner = SelecionarSpawnerParaTiro(regraIndex);
            if (idxSpawner < 0 || idxSpawner >= spawners.Length || spawners[idxSpawner] == null)
            {
                Debug.LogWarning($"[EcoDigitalGameManager] Spawner inválido no tiro {shot} (idx {idxSpawner}).");
                yield break;
            }
            Transform spawner = spawners[idxSpawner];

            // Direção “congelada” no instante do disparo (reto, sem perseguir).
            Vector3 dir = (eco.position - spawner.position);
            dir.y = 0f; // trava no plano XZ (2.5D). Remova se quiser 3D.
            Vector3 dirNorm = dir.sqrMagnitude > 0.0001f ? dir.normalized : spawner.forward;

            // Seleciona o prefab conforme a regra
            GameObject prefab = SelecionarPrefabParaTiro(regraIndex, shot);
            if (prefab == null)
            {
                Debug.LogWarning("[EcoDigitalGameManager] Prefab nulo. Pulando tiro.");
            }
            else
            {
                GameObject go = Instantiate(prefab, spawner.position, Quaternion.identity);

                // 1) Lança o projétil (mantém sua física/direção normal)
                var tiro = go.GetComponent<EcoTiroProjetil>();
                if (tiro != null)
                {
                    tiro.Lancar(dirNorm, velocidade); // mantém assinatura atual
                }

                // 2) Ajusta a orientação VISUAL com yaw extra do spawner (sem afetar a velocidade)
                float yawExtra = ObterYawExtra(idxSpawner);
                if (Mathf.Abs(yawExtra) > 0.01f)
                {
                    // Olha para a direção e aplica yaw adicional
                    go.transform.rotation = Quaternion.LookRotation(dirNorm, Vector3.up) * Quaternion.Euler(0f, yawExtra, 0f);
                }
                else
                {
                    // Garantir que olhe para o Eco mesmo sem yaw extra
                    go.transform.rotation = Quaternion.LookRotation(dirNorm, Vector3.up);
                }
            }

            if (intervalo > 0f && shot < quantidade - 1)
                yield return new WaitForSeconds(intervalo);
        }
    }

    private int SelecionarSpawnerParaTiro(int regraIndex)
    {
        // Se a lista de spawnersDoTurno NÃO estiver vazia, alterna entre eles a cada tiro
        if (spawnersDoTurno != null && spawnersDoTurno.Length > 0)
        {
            int idx = spawnersDoTurno[_cursorAlternanciaSpawner % spawnersDoTurno.Length];
            _cursorAlternanciaSpawner++;
            return Mathf.Clamp(idx, 0, spawners.Length - 1);
        }

        // Caso contrário, usa o spawnerIndex da regra (com clamp)
        var regra = regras[regraIndex];
        return Mathf.Clamp(regra.spawnerIndex, 0, Mathf.Max(0, spawners.Length - 1));
    }

    private float ObterYawExtra(int idxSpawner)
    {
        if (yawExtraPorSpawner == null || yawExtraPorSpawner.Length == 0) return 0f;
        if (idxSpawner < 0 || idxSpawner >= yawExtraPorSpawner.Length) return 0f;
        return yawExtraPorSpawner[idxSpawner];
    }

    private GameObject SelecionarPrefabParaTiro(int regraIndex, int shotNumber)
    {
        if (regras == null || regraIndex < 0 || regraIndex >= regras.Length) return prefabProjetil;

        var regra = regras[regraIndex];
        var lista = regra.prefabsProjetil;
        bool temLista = (lista != null && lista.Length > 0);

        if (!temLista) return prefabProjetil;

        switch (regra.modoSelecao)
        {
            case ModoSelecaoPrefab.Primeiro:
                return lista[0];

            case ModoSelecaoPrefab.AleatorioCadaTiro:
                return lista[Random.Range(0, lista.Length)];

            case ModoSelecaoPrefab.RoundRobin:
                if (_rrIndex == null || _rrIndex.Length != regras.Length)
                    _rrIndex = new int[regras.Length];
                int idx = _rrIndex[regraIndex] % Mathf.Max(1, lista.Length);
                _rrIndex[regraIndex] = (idx + 1) % lista.Length;
                return lista[idx];

            default:
                return lista[0];
        }
    }

    // ===== util =====
    private static bool HasTrigger(Animator anim, string triggerName)
    {
        if (anim == null || string.IsNullOrEmpty(triggerName)) return false;
        foreach (var p in anim.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
                return true;
        return false;
    }

    private static string FormatarParametros(Animator anim, string cabecalho)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine(cabecalho);
        if (anim == null) { sb.AppendLine("- (Animator nulo)"); return sb.ToString(); }

        foreach (var p in anim.parameters)
            sb.AppendLine($"- {p.name} ({p.type})");
        return sb.ToString();
    }
}
