using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortaController : MonoBehaviour
{
    public enum TipoSala { Nenhuma=0, BreakRoom=1, Supervisor=2, Banheiro=3, Deposito=4, Reuniao=5 }

    [Header("Sala / Ocultação")]
    [SerializeField] private TipoSala tipoDaSala = TipoSala.Nenhuma;
    [SerializeField, Tooltip("Pai que contém as paredes/itens da sala que devem sumir.")]
    private GameObject raizDesrenderizar;
    [SerializeField, Tooltip("Layer criada para ocultar (todas as câmeras com essa layer desmarcada).")]
    private string layerOculta = "SalaOculta";
    [SerializeField, Tooltip("Atraso só para ESCONDER (evita piscar na passagem).")]
    private float delayInvisibilidade = 0.08f;

    [Header("Porta (visual)")]
    [SerializeField] private Transform portaVisual;

    [Header("Ângulos (local Euler)")]
    [SerializeField] private Vector3 rotacaoFechada      = new Vector3(-90f, -90f, 0f);
    [SerializeField] private Vector3 rotacaoAbertaFora   = new Vector3(-90f,  15f, 0f);
    [SerializeField] private Vector3 rotacaoAbertaDentro = new Vector3(-90f, -178f,0f);

    [Header("Comportamento")]
    [SerializeField, Tooltip("Mais alto = mais responsivo.")] private float velocidadeRotacao = 8f;
    [SerializeField, Tooltip("Fecha X s após sair todo mundo.")] private float delayFechamento = 1.0f;
    [SerializeField] private float toleranciaAngulo = 0.5f;

    [Header("Áudio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somAbrir;
    [SerializeField] private AudioClip somFechar;
    [SerializeField, Tooltip("Throttle de áudio (s).")] private float minIntervaloSom = 0.08f;
    [SerializeField] private bool padronizarAudioSource = true;

    [Header("Anti-atravessar parede (opcional)")]
    [SerializeField] private bool antiOverlapEnabled = true;
    [SerializeField, Tooltip("BoxCollider da FOLHA da porta (AABB).")]
    private BoxCollider portaColisorAABB;
    [SerializeField, Tooltip("Layers consideradas parede/batente.")]
    private LayerMask paredeMask;
    [SerializeField, Tooltip("Folga do Overlap (m) para evitar falso positivo.")]
    private float overlapSkin = 0.005f;

    [Header("Antispam / Anti-spin")]
    [SerializeField, Tooltip("Sempre fechar totalmente antes de trocar a direção de abertura.")]
    private bool fecharAntesDeTrocar = true;
    [SerializeField, Tooltip("Tempo mínimo (s) com o vão vazio antes de destravar a direção.")]
    private float destravaDirecaoDelay = 0.25f;

    // ----- Estado -----
    private Quaternion qFechada, qAbertaFora, qAbertaDentro, alvoRotacao;
    private bool estaAberta=false, estaMovendo=false, bloqueado=false;
    private float tempoFechar=-1f;

    private int contagemOcupantes=0;           // só para controle da porta
    private bool playerDentroDaSala=false;     // estado lógico atual de “dentro”
    private Coroutine coVis;                   // coroutine de (des)visibilidade
    private int visToken=0;                    // coalescer últimas intenções

    // Direção travada enquanto houver alguém no vão
    private bool direcaoTravadaValida=false;
    private bool ladoTravadoFora=true; // true=abre para FORA; false=para DENTRO
    private Coroutine coDestravaDirecao;

    // Ocultação cache
    private readonly List<Transform> _layerTargets = new();
    private readonly Dictionary<Transform,int> _layersOriginais = new();
    private readonly List<Renderer> _renderers = new();
    private readonly List<SpriteRenderer> _spriteRenderers = new();

    // Áudio
    private float _ultimoSomAbrir = -999f, _ultimoSomFechar = -999f;
    [SerializeField] private SalaFader salaFader; // arraste no inspetor (mesmo raizDesrenderizar)


    private void Start()
    {
        qFechada      = Quaternion.Euler(rotacaoFechada);
        qAbertaFora   = Quaternion.Euler(rotacaoAbertaFora);
        qAbertaDentro = Quaternion.Euler(rotacaoAbertaDentro);

        if (portaVisual != null)
        {
            portaVisual.localRotation = qFechada;
            alvoRotacao = qFechada;
            estaAberta = false;
        }

        if (padronizarAudioSource && audioSource != null)
        {
            audioSource.spatialBlend = 0f;
            audioSource.dopplerLevel = 0f;
            audioSource.priority     = 10;
        }

        BuildCaches();
        AplicarVisibilidade(true); // começa visível
        Debug.Log($"[PortaController:{name}] START -> VISÍVEL");
    }

    private void Update()
    {
        if (estaMovendo && portaVisual != null)
        {
            Quaternion next = Quaternion.Lerp(portaVisual.localRotation, alvoRotacao, Time.deltaTime * velocidadeRotacao);

            if (antiOverlapEnabled && portaColisorAABB && paredeMask.value != 0 && CausariaSobreposicao(next))
            {
                estaMovendo = false;
                bloqueado   = false;
            }
            else
            {
                portaVisual.localRotation = next;
            }

            if (Quaternion.Angle(portaVisual.localRotation, alvoRotacao) <= toleranciaAngulo)
            {
                portaVisual.localRotation = alvoRotacao;
                estaMovendo = false;
                bloqueado   = false;
            }
        }

        if (estaAberta && tempoFechar > 0f && Time.time >= tempoFechar)
        {
            TryFechar();
        }
    }

    // ===== API (chamado pelo Trigger) =====

    /// <summary>ENTER: trava direção (se necessário) e abre. Não persegue o player.</summary>
    public void NotifyEnter(bool isOutside)
    {
        contagemOcupantes++;
        tempoFechar = -1f;
        if (coDestravaDirecao != null) { StopCoroutine(coDestravaDirecao); coDestravaDirecao = null; }

        if (!direcaoTravadaValida)
        {
            if (fecharAntesDeTrocar && estaAberta && !AproximadoDoAlvo(qFechada))
                StartCoroutine(CoFecharEAbrir(isOutside));
            else
            {
                ladoTravadoFora = isOutside;
                alvoRotacao = ladoTravadoFora ? qAbertaFora : qAbertaDentro;
                TryAbrir(alvoRotacao);
            }
            direcaoTravadaValida = true;
        }
        else
        {
            if (!estaAberta) TryAbrir(ladoTravadoFora ? qAbertaFora : qAbertaDentro);
        }
    }

    /// <summary>EXIT: agenda fechamento e (apenas) destrava direção depois de um tempo vazio.</summary>
    public void NotifyExit()
    {
        contagemOcupantes = Mathf.Max(0, contagemOcupantes - 1);
        if (contagemOcupantes == 0)
        {
            AgendarFechar();
            if (coDestravaDirecao != null) StopCoroutine(coDestravaDirecao);
            coDestravaDirecao = StartCoroutine(CoDestravarDirecaoDepois());
        }
    }

    /// <summary>Chamado pelo Trigger quando cruza plano: DENTRO=true (esconde), DENTRO=false (mostra).</summary>
    public void SetPlayerDentroDaSala(bool dentro)
    {
        if (playerDentroDaSala == dentro && coVis == null) return; // já está assim e não há ação pendente

        playerDentroDaSala = dentro;
        bool visivel = !playerDentroDaSala;

        // Regras determinísticas:
        // - Se vamos ESCONDER (visivel=false) -> aplica após pequeno atraso (delayInvisibilidade)
        // - Se vamos MOSTRAR (visivel=true)   -> cancela qualquer hide pendente e mostra já
        if (coVis != null) { StopCoroutine(coVis); coVis = null; }

        if (!visivel)
        {
            coVis = StartCoroutine(CoVisEsconderDepois(delayInvisibilidade));
        }
        else
        {
            AplicarVisibilidade(true);
        }
    }

    // ===== Internos porta =====

    private IEnumerator CoDestravarDirecaoDepois()
    {
        yield return new WaitForSeconds(destravaDirecaoDelay);
        if (contagemOcupantes == 0)
            direcaoTravadaValida = false;
        coDestravaDirecao = null;
    }

    private IEnumerator CoFecharEAbrir(bool novoIsOutside)
    {
        alvoRotacao = qFechada;
        TryFechar();

        float t0 = Time.time;
        while (estaMovendo && Time.time - t0 < 1.0f) yield return null;

        ladoTravadoFora = novoIsOutside;
        alvoRotacao = ladoTravadoFora ? qAbertaFora : qAbertaDentro;
        TryAbrir(alvoRotacao);
    }

    private void AgendarFechar()
    {
        if (!estaAberta) return;
        tempoFechar = Time.time + delayFechamento;
    }

    private void TryAbrir(Quaternion alvoDesejado)
    {
        bool mesmaDirecao = estaAberta && AproximadoDoAlvo(alvoDesejado);

        alvoRotacao = alvoDesejado;
        estaAberta  = true;
        estaMovendo = true;
        bloqueado   = true;
        tempoFechar = -1f;

        TocarSomSeguro(somAbrir, ref _ultimoSomAbrir);
        if (mesmaDirecao) _ultimoSomAbrir = Time.time;
    }

    private void TryFechar()
    {
        if (!estaAberta && AproximadoDoAlvo(qFechada)) return;

        alvoRotacao = qFechada;
        estaAberta  = false;
        estaMovendo = true;
        bloqueado   = true;
        tempoFechar = -1f;

        TocarSomSeguro(somFechar, ref _ultimoSomFechar);
    }

    private bool AproximadoDoAlvo(Quaternion qAlvo)
    {
        if (portaVisual == null) return true;
        return Quaternion.Angle(portaVisual.localRotation, qAlvo) <= toleranciaAngulo;
    }

    private void TocarSomSeguro(AudioClip clip, ref float ultimaVez)
    {
        if (audioSource == null || clip == null) return;
        if (Time.time - ultimaVez < minIntervaloSom) return;
        audioSource.PlayOneShot(clip);
        ultimaVez = Time.time;
    }

    // ===== Anti-Overlap (corrigido) =====
    private bool CausariaSobreposicao(Quaternion rotCandidate)
    {
        Transform t = portaColisorAABB.transform;

        Quaternion oldLocalRot = t.localRotation; // sample local
        t.localRotation = rotCandidate;

        Vector3 worldCenter = t.TransformPoint(portaColisorAABB.center);
        Vector3 half        = portaColisorAABB.size * 0.5f - Vector3.one * Mathf.Max(0f, overlapSkin);
        half = new Vector3(Mathf.Max(0f, half.x), Mathf.Max(0f, half.y), Mathf.Max(0f, half.z));
        Vector3 worldHalf   = Vector3.Scale(half, t.lossyScale);
        Quaternion worldRot = t.rotation;

        t.localRotation = oldLocalRot; // revert sample

        Collider[] hits = Physics.OverlapBox(worldCenter, worldHalf, worldRot, paredeMask, QueryTriggerInteraction.Ignore);
        return hits != null && hits.Length > 0;
    }

    // ===== Ocultação =====

    private IEnumerator CoVisEsconderDepois(float delay)
    {
        int token = ++visToken;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (token != visToken) yield break;

        AplicarVisibilidade(false); // esconder
        coVis = null;
    }

    private void BuildCaches()
    {
        _layerTargets.Clear();
        _layersOriginais.Clear();
        _renderers.Clear();
        _spriteRenderers.Clear();

        if (raizDesrenderizar == null) return;

        var stack = new Stack<Transform>();
        stack.Push(raizDesrenderizar.transform);

        while (stack.Count > 0)
        {
            var tt = stack.Pop();
            _layerTargets.Add(tt);
            if (!_layersOriginais.ContainsKey(tt))
                _layersOriginais[tt] = tt.gameObject.layer;

            var rs = tt.GetComponents<Renderer>();
            if (rs != null && rs.Length > 0) _renderers.AddRange(rs);

            var srs = tt.GetComponents<SpriteRenderer>();
            if (srs != null && srs.Length > 0) _spriteRenderers.AddRange(srs);

            for (int i = 0; i < tt.childCount; i++)
                stack.Push(tt.GetChild(i));
        }
    }

    private void AplicarVisibilidade(bool visivel)
{
    if (salaFader != null)
    {
        if (visivel) salaFader.FadeIn();
        else         salaFader.FadeOut();
        return; // o SalaFader cuida de layer/renderers
    }

    // (fallback antigo, caso não use efeitos)
    AplicarPorLayer(visivel);
    AplicarPorRenderers(visivel);
}

    private void AplicarPorLayer(bool visivel)
    {
        if (_layerTargets.Count == 0) return;

        if (visivel)
        {
            foreach (var t in _layerTargets)
            {
                if (t == null) continue;
                if (_layersOriginais.TryGetValue(t, out var original))
                    t.gameObject.layer = original;
            }
            return;
        }

        int hidden = LayerMask.NameToLayer(layerOculta);
        if (hidden < 0)
        {
            Debug.LogError($"[{name}] Layer '{layerOculta}' não existe. Crie a layer e EXCLUA no Culling Mask de TODAS as câmeras.");
            return;
        }

        foreach (var t in _layerTargets)
        {
            if (t == null) continue;
            t.gameObject.layer = hidden;
        }
    }

    private void AplicarPorRenderers(bool visivel)
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            r.enabled = visivel;
        }
        for (int i = 0; i < _spriteRenderers.Count; i++)
        {
            var sr = _spriteRenderers[i];
            if (sr == null) continue;
            sr.enabled = visivel;
        }
    }

    private void OnDisable()
    {
        // Se desabilitar a porta no editor/jogo, garante que a sala não fica presa invisível
        AplicarVisibilidade(true);
    }


    // Torna a sala visível imediatamente, cancelando qualquer ocultação pendente.
    public void ForcarSalaVisivel()
    {
        playerDentroDaSala = false;  // estado lógico
        if (coVis != null) { StopCoroutine(coVis); coVis = null; }
        visToken++;                  // invalida intents pendentes
        AplicarVisibilidade(true);   // mostra já (usa SalaFader se estiver setado)
    }

#if UNITY_EDITOR
    [ContextMenu("TESTE: Invisível (como DENTRO)")]
    private void _TestOcultar() => AplicarVisibilidade(false);
    [ContextMenu("TESTE: Visível (como FORA)")]
    private void _TestMostrar() => AplicarVisibilidade(true);
#endif
}
