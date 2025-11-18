using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SalaFader : MonoBehaviour
{
    public enum EffectType { DitherFade, Dissolve }

    [Header("Alvo")]
    [Tooltip("Pai com as paredes/itens que devem sumir/reaparecer.")]
    public Transform raizAlvo;

    [Header("Shader / Parâmetros")]
    public EffectType efeito = EffectType.DitherFade;
    [Tooltip("Nome da layer que fica EXCLUÍDA nas câmeras (ex.: SalaOculta)")]
    public string layerOculta = "SalaOculta";
    [Tooltip("Duração do fade (s).")]
    public float duracao = 0.25f;

    // nomes de propriedades
    static readonly int ID_Fade     = Shader.PropertyToID("_Fade");
    static readonly int ID_Dissolve = Shader.PropertyToID("_Dissolve");

    // cache
    private List<Renderer> rends = new();
    private Dictionary<Transform,int> originalLayers = new();
    private Coroutine co;
    private int alvoLayerOculta = -1;

    void Awake()
    {
        if (!raizAlvo) return;

        alvoLayerOculta = LayerMask.NameToLayer(layerOculta);
        if (alvoLayerOculta < 0)
            Debug.LogWarning($"[SalaFader] Layer '{layerOculta}' não existe. (crie e exclua nas câmeras)");

        // coleta
        var stack = new Stack<Transform>();
        stack.Push(raizAlvo);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (!originalLayers.ContainsKey(t)) originalLayers[t] = t.gameObject.layer;
            var rs = t.GetComponents<Renderer>();
            if (rs != null && rs.Length > 0) rends.AddRange(rs);
            for (int i=0;i<t.childCount;i++) stack.Push(t.GetChild(i));
        }

        // inicia visível (param 1)
        SetParam(1f);
    }

    /// <summary>Faz fade para visível e restaura layer original imediatamente no início.</summary>
    public void FadeIn()
    {
        if (co != null) StopCoroutine(co);
        // volta camada antes do fade, para a parede aparecer
        RestoreLayers();
        co = StartCoroutine(CoFade(1f));
    }

    /// <summary>Faz fade para invisível e, ao terminar, coloca na layer oculta (se existir).</summary>
    public void FadeOut()
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(CoFade(0f, setHiddenAfter:true));
    }

    private IEnumerator CoFade(float alvo, bool setHiddenAfter=false)
    {
        float start = GetParam();
        float t=0f;
        float d = Mathf.Max(0.01f, duracao);

        while (t < d)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f,1f,t/d);
            SetParam(Mathf.Lerp(start, alvo, k));
            yield return null;
        }
        SetParam(alvo);

        if (setHiddenAfter && alvoLayerOculta >= 0)
            SetLayerRecursive(alvoLayerOculta);
        co = null;
    }

    private void SetParam(float v)
    {
        // usa MaterialPropertyBlock pra não instanciar materiais
        var mpb = new MaterialPropertyBlock();
        foreach (var r in rends)
        {
            if (!r) continue;
            r.GetPropertyBlock(mpb);
            if (efeito == EffectType.DitherFade) mpb.SetFloat(ID_Fade, v);
            else                                 mpb.SetFloat(ID_Dissolve, v);
            r.SetPropertyBlock(mpb);
        }
    }

    private float GetParam()
    {
        // Tentativa de leitura (default 1)
        // Muitos shaders não retornam via MPB, então mantemos simples:
        return 1f;
    }

    private void RestoreLayers()
    {
        foreach (var kv in originalLayers)
        {
            if (kv.Key) kv.Key.gameObject.layer = kv.Value;
        }
    }

    private void SetLayerRecursive(int layer)
    {
        var stack = new Stack<Transform>();
        stack.Push(raizAlvo);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (!originalLayers.ContainsKey(t)) originalLayers[t] = t.gameObject.layer;
            t.gameObject.layer = layer;
            for (int i=0;i<t.childCount;i++) stack.Push(t.GetChild(i));
        }
    }
}
