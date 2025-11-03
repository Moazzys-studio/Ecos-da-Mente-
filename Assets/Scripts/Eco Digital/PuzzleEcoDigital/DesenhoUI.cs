using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DesenhoUI : MonoBehaviour
{
    [Header("Canvas/Área de Desenho")]
    [Tooltip("Canvas (Screen Space – Overlay) ou o RectTransform que receberá as linhas.")]
    public RectTransform desenhoArea; // normalmente: o próprio RectTransform do Canvas/Panel

    [Header("Prefab de Linha (UI)")]
    [Tooltip("Prefab com UILineGraphic (Graphic em UI)")]
    public UILineGraphic linhaUIPrefab;

    [Header("Aparência")]
    [Tooltip("Espessura em pixels.")]
    public float espessura = 6f;
    [Tooltip("Cor da linha.")]
    public Color cor = Color.white;

    [Header("Tempo na tela")]
    [Tooltip("Tempo fixo que o traço permanece (s).")]
    public float tempoVisivel = 2.0f;
    [Tooltip("Duração do fade-out após o tempo fixo (s). Se 0, some sem fade.")]
    public float tempoFadeOut = 0.4f;

    [Header("Amostragem")]
    [Tooltip("Distância mínima (pixels) para registrar novo ponto.")]
    public float distanciaMinPx = 3f;

    [Header("Reconhecimento")]
    [Tooltip("Janela entre 2 traços para reconhecer 'X' (s).")]
    public float janelaXSegundos = 0.6f;
    public bool debugLog = false;

    private UILineGraphic _linhaAtual;
    private readonly List<Vector2> _pontosLocais = new();
    private readonly Queue<(float t, List<Vector2> pts)> _bufferTracos = new();
    private const int MAX_TRACOS = 3;

    private Camera _uiCam; // null para Overlay
    private bool _temArea => desenhoArea != null;

    private void Awake()
    {
        if (!_temArea)
        {
            var c = GetComponentInParent<Canvas>();
            if (c != null) desenhoArea = c.transform as RectTransform;
        }

        var canvas = desenhoArea ? desenhoArea.GetComponentInParent<Canvas>() : null;
        _uiCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
    }

    private void Update()
    {
        bool iniciou = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool manteve = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool terminou = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var t = Touchscreen.current.touches[0];
            var ph = t.phase.ReadValue();
            iniciou |= ph == UnityEngine.InputSystem.TouchPhase.Began;
            manteve |= ph == UnityEngine.InputSystem.TouchPhase.Moved || ph == UnityEngine.InputSystem.TouchPhase.Stationary;
            terminou |= ph == UnityEngine.InputSystem.TouchPhase.Ended || ph == UnityEngine.InputSystem.TouchPhase.Canceled;
        }

        if (iniciou) IniciarLinha();
        if (manteve) AdicionarPontoSePreciso();
        if (terminou) FinalizarLinha();
    }

    private Vector2 PegarPosTela()
    {
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            return Touchscreen.current.touches[0].position.ReadValue();
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return Vector2.zero;
    }

    private bool ScreenToLocal(Vector2 screen, out Vector2 local)
    {
        local = Vector2.zero;
        if (!_temArea) return false;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(desenhoArea, screen, _uiCam, out local);
    }

    private void IniciarLinha()
    {
        if (!_temArea || linhaUIPrefab == null)
        {
            Debug.LogWarning("[DesenhoUI] Configure 'desenhoArea' e 'linhaUIPrefab'.");
            return;
        }

        _pontosLocais.Clear();
        _linhaAtual = Instantiate(linhaUIPrefab, desenhoArea);
        _linhaAtual.color = cor;
        _linhaAtual.Thickness = espessura;
        _linhaAtual.raycastTarget = false;

        // Primeiro ponto
        AdicionarPontoSePreciso(true);
    }

    private void AdicionarPontoSePreciso(bool forcar = false)
    {
        if (_linhaAtual == null) return;

        var posTela = PegarPosTela();
        if (!ScreenToLocal(posTela, out var local)) return;

        if (_pontosLocais.Count == 0 || forcar ||
            Vector2.Distance(_pontosLocais[^1], local) >= distanciaMinPx)
        {
            _pontosLocais.Add(local);
            _linhaAtual.SetPoints(_pontosLocais);
        }
    }

    private void FinalizarLinha()
    {
        if (_linhaAtual == null || _pontosLocais.Count < 2)
        {
            if (_linhaAtual != null) Destroy(_linhaAtual.gameObject);
            _linhaAtual = null;
            _pontosLocais.Clear();
            return;
        }

        // Mantém o traço por tempoVisivel e faz fade
        StartCoroutine(FixarEApagar(_linhaAtual, tempoVisivel, tempoFadeOut));

        // Reconhecimento (usa pontos em coordenada de UI; ok para heurísticas)
        var copia = new List<Vector2>(_pontosLocais);
        _pontosLocais.Clear();
        _linhaAtual = null;

        var gesto = ReconhecedorGestosSimples.ReconhecerDeUmTraço(copia);
        if (gesto != TipoGesto.Nenhum)
        {
            if (debugLog) Debug.Log($"[DesenhoUI] 1-traço: {gesto}");
            GestorGestos.I?.AnunciarGesto(gesto);
        }
        else
        {
            // tenta X (dois traços recentes)
            _bufferTracos.Enqueue((Time.time, copia));
            while (_bufferTracos.Count > MAX_TRACOS) _bufferTracos.Dequeue();

            if (_bufferTracos.Count >= 2)
            {
                var arr = _bufferTracos.ToArray();
                var (tB, ptsB) = arr[^1];
                var (tA, ptsA) = arr[^2];

                if (Time.time - tA <= janelaXSegundos &&
                    ReconhecedorGestosSimples.TentaReconhecerX(ptsA, ptsB))
                {
                    if (debugLog) Debug.Log("[DesenhoUI] 2-traços: X");
                    GestorGestos.I?.AnunciarGesto(TipoGesto.X);
                    _bufferTracos.Clear();
                }
            }
        }
    }

    private System.Collections.IEnumerator FixarEApagar(Graphic g, float fixo, float fade)
    {
        if (g == null) yield break;
        if (fixo > 0f) yield return new WaitForSeconds(fixo);

        if (g == null) yield break;

        if (fade > 0f)
        {
            float t = 0f;
            Color c0 = g.color;
            while (t < fade && g != null)
            {
                float a = Mathf.Lerp(1f, 0f, t / fade);
                g.color = new Color(c0.r, c0.g, c0.b, a);
                t += Time.deltaTime;
                yield return null;
            }
        }
        if (g != null) Destroy(g.gameObject);
    }
}
