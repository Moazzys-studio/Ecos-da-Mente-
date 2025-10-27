using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class EquilibrioUI : MonoBehaviour
{
    [Header("Referências (obrigatórias)")]
    [SerializeField] private RectTransform tracker;         // bolinha amarela
    [SerializeField] private RectTransform barraEquilibrio; // fundo/faixa da barra
    [SerializeField] private BalanceMecanics mecanics;      // fornece TotalAngle (graus)
    [SerializeField] private BalanceInput inputRef;         // fornece MaxAngle (graus)

    [Header("Ajustes")]
    [Tooltip("PX extras para não encostar na borda. 0 = vai até o limite útil.")]
    [SerializeField] private float margemPixels = 0f;
    [Tooltip("Suavização do movimento do tracker (só em Play). 0 = sem suavização.")]
    [SerializeField, Min(0f)] private float suavizacao = 10f;
    [Tooltip("Ignorar LayoutGroup no tracker e centralizar anchors/pivot.")]
    [SerializeField] private bool forceFreeMove = true;

    [Header("Debug (somente leitura)")]
    [SerializeField] private float dbg_TotalAngle;
    [SerializeField] private float dbg_MaxAngle;
    [SerializeField] private float dbg_TargetLocalX;

    void Awake()
    {
        AutoWire();
        PrepararTrackerSeNecessario();
    }
    void OnValidate()
    {
        AutoWire();
        PrepararTrackerSeNecessario();
        // Atualiza em edição também
        AtualizarPosicao(immediate:true);
    }
    void Update()
    {
        AtualizarPosicao(immediate: !Application.isPlaying);
    }

    // ---------------- Core ----------------
    void AtualizarPosicao(bool immediate)
    {
        if (tracker == null || barraEquilibrio == null || mecanics == null || inputRef == null)
            return;

        // Larguras reais
        float barW   = barraEquilibrio.rect.width;
        float trackW = tracker.rect.width;

        // Metades
        float halfBar     = barW * 0.5f;
        float halfTracker = trackW * 0.5f;

        // Limites seguros para o CENTRO do tracker dentro da barra
        float margem = Mathf.Max(0f, margemPixels);
        float xMin = -halfBar + halfTracker + margem;
        float xMax =  halfBar - halfTracker - margem;

        // Normalização do ângulo total para [0..1] usando o MaxAngle REAL do jogo
        float maxRef = Mathf.Max(0.0001f, inputRef.MaxAngle);
        float total  = mecanics.TotalAngle;

        // debug
        dbg_TotalAngle = total;
        dbg_MaxAngle   = maxRef;

        float t = Mathf.InverseLerp(-maxRef, +maxRef, total);     // -max -> 0, +max -> 1
        float alvoX = Mathf.Lerp(xMin, xMax, t);                  // posição local desejada
        dbg_TargetLocalX = alvoX;

        // Suavização só em Play
        if (!immediate && suavizacao > 0f)
        {
            float k = 1f - Mathf.Exp(-suavizacao * Time.deltaTime);
            float newX = Mathf.Lerp(tracker.anchoredPosition.x, alvoX, k);
            tracker.anchoredPosition = new Vector2(newX, tracker.anchoredPosition.y);
        }
        else
        {
            tracker.anchoredPosition = new Vector2(alvoX, tracker.anchoredPosition.y);
        }
    }

    // ---------------- Helpers ----------------
    void AutoWire()
    {
        if (mecanics == null) mecanics = FindFirstObjectByType<BalanceMecanics>();
        if (inputRef == null && mecanics != null) inputRef = mecanics.GetComponent<BalanceInput>();
    }

    void PrepararTrackerSeNecessario()
    {
        if (!forceFreeMove || tracker == null) return;

        // Garante que nenhum LayoutGroup domine a posição
        var layout = tracker.GetComponent<LayoutElement>();
        if (layout == null) layout = tracker.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;

        // Anchors/pivot centralizados horizontalmente para mover por anchoredPosition.x
        var aMin = tracker.anchorMin; var aMax = tracker.anchorMax; var p = tracker.pivot;
        aMin.x = aMax.x = 0.5f; p.x = 0.5f;
        tracker.anchorMin = aMin; tracker.anchorMax = aMax; tracker.pivot = p;
    }
}
