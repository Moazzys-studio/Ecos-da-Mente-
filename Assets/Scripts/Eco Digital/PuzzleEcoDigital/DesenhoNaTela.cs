using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class Desenho2DNaCamera : MonoBehaviour
{
    [Header("Entrada")]
    public bool habilitado = true;

    [Header("Câmera / Profundidade")]
    [Tooltip("Câmera usada para converter Screen->World.")]
    public Camera cam;
    [Tooltip("Distância da câmera onde a linha será desenhada (em unidades de mundo).")]
    public float zOffsetFromCamera = 0.5f;

    [Header("LineRenderer")]
    [Tooltip("Prefab do LineRenderer (material Unlit/Color).")]
    public LineRenderer linePrefab;
    [Tooltip("Largura do traço em unidades de mundo.")]
    public float larguraMundo = 0.05f;
    [Tooltip("Força ZTest Always via material (precisa de shader compatível).")]
    public bool forcarZTestAlways = false;
    [Tooltip("Sorting Layer e Order para desenhar por cima.")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 5000;

    [Header("Tempo na tela")]
    public float tempoVisivel = 2.0f;
    public float tempoFadeOut = 0.4f;

    [Header("Amostragem")]
    [Tooltip("Distância mínima em pixels para gravar novo ponto.")]
    public float distanciaMinPx = 3f;

    [Header("Reconhecimento")]
    public float janelaXSegundos = 0.6f;
    public bool debugLog = false;

    private LineRenderer _linhaAtual;
    private readonly List<Vector2> _ptsTela = new();
    private readonly Queue<(float t, List<Vector2> pontos)> _bufferTracos = new();
    private const int MAX_TRACOS = 3;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (!habilitado || cam == null) return;

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

    Vector2 PegarPosTela()
    {
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            return Touchscreen.current.touches[0].position.ReadValue();
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return Vector2.zero;
    }

    void IniciarLinha()
    {
        if (linePrefab == null)
        {
            Debug.LogWarning("[Desenho2DNaCamera] linePrefab não atribuído.");
            return;
        }

        _ptsTela.Clear();
        _linhaAtual = Instantiate(linePrefab, Vector3.zero, Quaternion.identity);
        _linhaAtual.useWorldSpace = true;                 // (importante)
        _linhaAtual.positionCount = 0;
        _linhaAtual.widthMultiplier = larguraMundo;

        var rend = _linhaAtual.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sortingLayerName = sortingLayerName;
            rend.sortingOrder = sortingOrder;
            if (forcarZTestAlways && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_ZTest"))
                rend.sharedMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        AdicionarPontoSePreciso(true);
    }

    void AdicionarPontoSePreciso(bool forcar = false)
    {
        if (_linhaAtual == null) return;

        var p = PegarPosTela();
        if (_ptsTela.Count == 0 || forcar || Vector2.Distance(_ptsTela[^1], p) >= distanciaMinPx)
        {
            _ptsTela.Add(p);
            AtualizarWorldPositions();
        }
    }

    void AtualizarWorldPositions()
    {
        if (_linhaAtual == null) return;

        _linhaAtual.positionCount = _ptsTela.Count;
        float z = cam.nearClipPlane + Mathf.Max(0.001f, zOffsetFromCamera);

        for (int i = 0; i < _ptsTela.Count; i++)
        {
            Vector2 s = _ptsTela[i];
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(s.x, s.y, z));
            _linhaAtual.SetPosition(i, w);
        }
    }

    void FinalizarLinha()
    {
        if (_linhaAtual == null || _ptsTela.Count < 2)
        {
            if (_linhaAtual != null) Destroy(_linhaAtual.gameObject);
            _linhaAtual = null;
            _ptsTela.Clear();
            return;
        }

        // Mantém na tela e apaga com fade
        StartCoroutine(ManterEApagar(_linhaAtual, tempoVisivel, tempoFadeOut));
        _linhaAtual = null;

        // Reconhecimento 1-traço
        var copia = new List<Vector2>(_ptsTela);
        _ptsTela.Clear();

        var gesto = ReconhecedorGestosSimples.ReconhecerDeUmTraço(copia);
        if (gesto != TipoGesto.Nenhum)
        {
            if (debugLog) Debug.Log($"[Desenho2DNaCamera] 1-traço: {gesto}");
            GestorGestos.I?.AnunciarGesto(gesto);
        }
        else
        {
            // Tenta X com 2 traços
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
                    if (debugLog) Debug.Log("[Desenho2DNaCamera] 2-traços: X");
                    GestorGestos.I?.AnunciarGesto(TipoGesto.X);
                    _bufferTracos.Clear();
                }
            }
        }
    }

    System.Collections.IEnumerator ManterEApagar(LineRenderer lr, float fixo, float fade)
    {
        if (lr == null) yield break;
        if (fixo > 0f) yield return new WaitForSeconds(fixo);

        if (fade > 0f)
        {
            float t = 0f;
            var mat = lr.material;
            Color c = mat.HasProperty("_Color") ? mat.color : Color.white;
            float w0 = lr.widthMultiplier;

            while (t < fade && lr != null)
            {
                float a = Mathf.Lerp(1f, 0f, t / fade);
                if (mat.HasProperty("_Color")) { c.a = a; mat.color = c; }
                lr.widthMultiplier = Mathf.Lerp(w0, 0f, t / fade);
                t += Time.deltaTime;
                yield return null;
            }
        }

        if (lr != null) Destroy(lr.gameObject);
    }
}
