using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[ExecuteAlways]
public class ChaveUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Referências (arraste do Hierarchy)")]
    [Tooltip("Imagem do trilho (fundo em formato pílula)")]
    [SerializeField] private Image trilho;
    [Tooltip("Imagem do knob (bolinha)")]
    [SerializeField] private Image knob;

    [Header("Estado")]
    [Tooltip("Estado inicial da chave")]
    [SerializeField] private bool ligado = false;

    [Header("Aparência")]
    [Tooltip("Cor do trilho quando LIGADO")]
    [SerializeField] private Color corOnTrilho = new Color(0.23f, 0.64f, 0.56f); // #3AA48E
    [Tooltip("Cor do trilho quando DESLIGADO")]
    [SerializeField] private Color corOffTrilho = new Color(0.91f, 0.42f, 0.42f); // #E76A6A
    [Tooltip("Cor do knob")]
    [SerializeField] private Color corKnob = Color.white;

    [Header("Layout/Animação")]
    [Tooltip("Margem interna horizontal entre knob e borda do trilho (em px)")]
    [SerializeField] private float margem = 4f;
    [Tooltip("Duração da animação (segundos). 0 = instantâneo")]
    [SerializeField, Min(0f)] private float duracao = 0.15f;

    [Header("Eventos")]
    public UnityEvent<bool> aoMudar; // dispara quando a chave liga/desliga

    // cache
    private RectTransform _rtTrilho;
    private RectTransform _rtKnob;
    private Coroutine _anim;
    private Vector2 _posKnobOn;
    private Vector2 _posKnobOff;

    // ============================ Ciclo de vida ============================

    private void Reset()
    {
        // tenta achar automaticamente se o dev criou com nomes padrão
        if (trilho == null)
        {
            var t = transform.Find("Trilho");
            if (t) trilho = t.GetComponent<Image>();
        }
        if (knob == null)
        {
            var k = transform.Find("Knob");
            if (k) knob = k.GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        CacheRefs();
        RecalcularPosicoes();
        AplicarVisual(ligado, true);
    }

    private void OnValidate()
    {
        CacheRefs();
        RecalcularPosicoes();
        AplicarVisual(ligado, true);
    }

    private void OnRectTransformDimensionsChange()
    {
        // se o Canvas Scaler mudar tamanho, recalcule
        RecalcularPosicoes();
        AplicarVisual(ligado, true);
    }

    private void CacheRefs()
    {
        if (trilho) _rtTrilho = trilho.rectTransform;
        if (knob)   _rtKnob   = knob.rectTransform;
    }

    // ============================ Lógica UI ============================

    private void RecalcularPosicoes()
    {
        if (_rtTrilho == null || _rtKnob == null) return;

        float w = _rtTrilho.rect.width;
        float h = _rtTrilho.rect.height;

        // usamos o raio do knob para não encostar na borda
        float raioKnob = _rtKnob.rect.height * 0.5f;
        float xMin = -w * 0.5f + margem + raioKnob;
        float xMax =  w * 0.5f - margem - raioKnob;

        _posKnobOff = new Vector2(xMin, 0f);
        _posKnobOn  = new Vector2(xMax, 0f);
    }

    private void AplicarVisual(bool on, bool instantaneo)
    {
        if (trilho) trilho.color = on ? corOnTrilho : corOffTrilho;
        if (knob)   knob.color   = corKnob;

        if (_rtKnob == null) return;

        if (instantaneo || duracao <= 0f)
        {
            _rtKnob.anchoredPosition = on ? _posKnobOn : _posKnobOff;
            return;
        }

        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimarKnob(on ? _posKnobOn : _posKnobOff));
    }

    private IEnumerator AnimarKnob(Vector2 alvo)
    {
        Vector2 origem = _rtKnob.anchoredPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duracao; // unscaled p/ UI
            float s = Smooth(t);
            _rtKnob.anchoredPosition = Vector2.Lerp(origem, alvo, s);
            yield return null;
        }
        _rtKnob.anchoredPosition = alvo;
        _anim = null;
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x); // SmoothStep
    }

    // ============================ Interação ============================

    /// <summary> Clique/tap alterna o estado. </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        Alternar();
    }

    /// <summary> Liga/desliga via código. </summary>
    public void Definir(bool on, bool dispararEvento = true, bool instantaneo = false)
    {
        if (ligado == on)
        {
            // ainda assim atualiza visual se pedirem instantâneo
            AplicarVisual(on, instantaneo);
            return;
        }

        ligado = on;
        AplicarVisual(ligado, instantaneo);
        if (dispararEvento) aoMudar?.Invoke(ligado);
    }

    /// <summary> Atalho para alternar. </summary>
    public void Alternar(bool dispararEvento = true, bool instantaneo = false)
    {
        Definir(!ligado, dispararEvento, instantaneo);
    }

    // ============================ Getters ============================

    public bool EstaLigado => ligado;
}
