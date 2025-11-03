using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortaController : MonoBehaviour
{
    public enum TipoSala { Nenhuma=0, BreakRoom=1, Supervisor=2, Banheiro=3, Deposito=4, Reuniao=5 }

    [Header("Sala / Ocultação")]
    [SerializeField] private TipoSala tipoDaSala = TipoSala.Nenhuma;
    [SerializeField] private GameObject raizDesrenderizar;
    [SerializeField] private string layerOculta = "SalaOculta";
    [SerializeField] private float delayVisibilidade = 0.20f;

    [Header("Porta (visual)")]
    [SerializeField] private Transform portaVisual;

    [Header("Ângulos (local Euler)")]
    [SerializeField] private Vector3 rotacaoFechada      = new Vector3(-90f, -90f,   0f);
    [SerializeField] private Vector3 rotacaoAbertaFora   = new Vector3(-90f,  15f,   0f);
    [SerializeField] private Vector3 rotacaoAbertaDentro = new Vector3(-90f, -178f,  0f);

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
    [SerializeField, Tooltip("BoxCollider da FOLHA da porta (AABB).")]
    private BoxCollider portaColisorAABB;
    [SerializeField, Tooltip("Layers consideradas parede/batente.")]
    private LayerMask paredeMask;

    // ----- Estado -----
    private Quaternion qFechada, qAbertaFora, qAbertaDentro, alvoRotacao;
    private bool estaAberta=false, estaMovendo=false, bloqueado=false;
    private float tempoFechar=-1f;

    private int contagemOcupantes=0;
    private bool playerDentroDaSala=false;
    private Coroutine coVis;
    private int visToken=0;

    // Direção travada enquanto houver alguém no vão
    private bool direcaoTravadaValida=false;
    private bool ladoTravadoFora=true; // true=abre para FORA; false=para DENTRO

    // Ocultação cache
    private readonly List<Transform> _layerTargets = new();
    private readonly Dictionary<Transform,int> _layersOriginais = new();
    private readonly List<Renderer> _renderers = new();
    private readonly List<SpriteRenderer> _spriteRenderers = new();

    // Áudio
    private float _ultimoSomAbrir=-999f, _ultimoSomFechar=-999f;

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
            audioSource.spatialBlend = 0f; audioSource.dopplerLevel = 0f; audioSource.priority = 10;
        }

        BuildCaches();
        AplicarVisibilidade(true);
        Debug.Log($"[PortaController:{name}] START -> VISÍVEL");
    }

    private void Update()
    {
        if (estaMovendo && portaVisual != null)
        {
            // Próxima rotação candidata
            Quaternion next = Quaternion.Lerp(portaVisual.localRotation, alvoRotacao, Time.deltaTime * velocidadeRotacao);

            // Checagem simples de sobreposição da folha com parede
            if (portaColisorAABB && paredeMask.value != 0)
            {
                if (CausariaSobreposicao(next))
                {
                    // trava aqui (não avança nesse frame)
                    estaMovendo = false;
                    bloqueado   = false;
                }
                else
                {
                    portaVisual.localRotation = next;
                }
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

    // ===== API =====

    /// <summary>Chamado pelo trigger no ENTER, informando o lado de FORA do player.</summary>
    public void NotifyEnter(bool isOutside)
    {
        contagemOcupantes++;
        tempoFechar = -1f;

        if (!direcaoTravadaValida)
        {
            direcaoTravadaValida = true;
            ladoTravadoFora = isOutside;
            // define alvo e abre
            alvoRotacao = ladoTravadoFora ? qAbertaFora : qAbertaDentro;
            TryAbrir(alvoRotacao);
        }
        else
        {
            // já tem direção travada: apenas garante aberta
            if (!estaAberta) TryAbrir(ladoTravadoFora ? qAbertaFora : qAbertaDentro);
        }
    }

    /// <summary>Chamado pelo trigger no EXIT.</summary>
    public void NotifyExit()
    {
        contagemOcupantes = Mathf.Max(0, contagemOcupantes - 1);
        if (contagemOcupantes == 0)
        {
            // destrava direção quando esvaziar
            direcaoTravadaValida = false;
            AgendarFechar();
        }
    }

    /// <summary>Define se o player está DENTRO da sala (true=dentro, false=fora) para ocultação de paredes.</summary>
    public void SetPlayerDentroDaSala(bool dentro)
    {
        if (playerDentroDaSala == dentro) return;
        playerDentroDaSala = dentro;

        bool visivel = !playerDentroDaSala; // dentro -> invisível
        if (coVis != null) StopCoroutine(coVis);
        coVis = StartCoroutine(CoVisibilidadeDelay(visivel, delayVisibilidade));

        // Debug opcional:
        // Debug.Log($"[PortaController:{name}] Dentro={dentro} (vis={visivel} em {delayVisibilidade:0.00}s)");
    }

    // ===== Internos porta =====

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

    // ===== Anti-atravessar parede =====

    private bool CausariaSobreposicao(Quaternion rotCandidate)
    {
        // Calcula centro e tamanho do AABB do BoxCollider em world space sob a rotação candidata.
        // Obs: é uma aproximação simples (usa localToWorldMatrix do colisor).
        var t = portaColisorAABB.transform;
        Vector3 worldCenter = t.TransformPoint(portaColisorAABB.center);
        Vector3 half = portaColisorAABB.size * 0.5f;
        Vector3 worldHalf = Vector3.Scale(half, t.lossyScale);

        // Para “simular” a rotação candidata, aplicamos uma matriz temporária
        // no cálculo do OverlapBox via Quaternion rotCandidate * (rot do pai relativo).
        Quaternion worldRot = rotCandidate; // suficiente para maioria dos casos (pivot na folha)

        Collider[] hits = Physics.OverlapBox(worldCenter, worldHalf, worldRot, paredeMask, QueryTriggerInteraction.Ignore);
        return hits != null && hits.Length > 0;
    }

    // ===== Ocultação =====

    private IEnumerator CoVisibilidadeDelay(bool visivel, float delay)
    {
        int token = ++visToken;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (token != visToken) yield break;

        AplicarVisibilidade(visivel);
        // Debug.Log($"[PortaController:{name}] APLICADO -> {(visivel ? "VISÍVEL" : "INVISÍVEL")}");
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
}
