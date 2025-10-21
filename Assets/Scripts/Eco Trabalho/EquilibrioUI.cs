using UnityEngine;

public class EquilibrioUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private RectTransform tracker;         // o indicador móvel
    [SerializeField] private RectTransform barraEquilibrio; // a barra de fundo
    [SerializeField] private BalanceController balance;     // sua balança

    [Header("Ajustes")]
    [SerializeField, Tooltip("Velocidade do easing do tracker")]
    private float suavizacao = 8f;

    // opcional: marcador do "peso bruto" (sem acelerômetro)
    [SerializeField] private RectTransform marcadorPesoBruto; 

    private float alvoX;
    private float atualX;

    void Update()
    {
        if (tracker == null || barraEquilibrio == null || balance == null) return;

        // largura útil: metade da barra menos metade do tracker
        float halfBar = barraEquilibrio.rect.width * 0.5f;
        float halfTracker = tracker.rect.width * 0.5f;
        float alcance = Mathf.Max(0f, halfBar - halfTracker); // garante que encoste na borda visual

        // 1) valor NORMALIZADO do ângulo TOTAL (acelerômetro + offset de peso)
        float totalNorm = Mathf.Clamp(balance.TotalAngle / balance.MaxAngle, -1f, 1f);

        // posição alvo do tracker (pós-compensação)
        alvoX = totalNorm * alcance;
        atualX = Mathf.Lerp(atualX, alvoX, Time.deltaTime * suavizacao);
        tracker.anchoredPosition = new Vector2(atualX, tracker.anchoredPosition.y);

        // 2) opcional: mostrar também o "peso bruto" (só o offset do líquido, sem acelerômetro)
        if (marcadorPesoBruto != null)
        {
            float brutoNorm = Mathf.Clamp(balance.externalAngleOffset / balance.MaxAngle, -1f, 1f);
            float brutoX = brutoNorm * alcance;
            marcadorPesoBruto.anchoredPosition = new Vector2(brutoX, marcadorPesoBruto.anchoredPosition.y);
        }
    }
}
