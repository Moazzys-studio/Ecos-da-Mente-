using UnityEngine;

public class VibracaoEquilibrio : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private BalanceController balance;

    [Header("Configuração da vibração")]
    [Tooltip("Tempo mínimo entre vibrações consecutivas (em segundos)")]
    [SerializeField] private float intervaloMinimo = 0.1f;

    [Tooltip("Frequência base (vibrações por segundo quando no máximo desequilíbrio)")]
    [SerializeField] private float frequenciaMaxima = 15f;

    [Tooltip("Multiplicador da intensidade da vibração (0 = desligado, 1 = padrão)")]
    [Range(0f, 1f)]
    [SerializeField] private float intensidade = 1f;

    private float tempoUltimaVibracao = 0f;

    void Update()
    {
        if (balance == null) return;

        // valor normalizado do desequilíbrio (0 = equilíbrio, 1 = extremo)
        float desequilibrio = Mathf.Abs(balance.TotalAngle / balance.MaxAngle);
        desequilibrio = Mathf.Clamp01(desequilibrio);

        // define intervalo entre vibrações com base no desequilíbrio
        float intervalo = Mathf.Lerp(0.5f, intervaloMinimo, desequilibrio);
        float tempoAtual = Time.time;

        // vibra com frequência proporcional ao desequilíbrio
        if (tempoAtual - tempoUltimaVibracao >= intervalo && desequilibrio > 0.05f)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            long duracaoMs = (long)(50 * intensidade * desequilibrio);
            Handheld.Vibrate(); // versão simples
            // alternativa nativa Android API:
            // Vibration.Vibrate(duracaoMs);
#endif
            tempoUltimaVibracao = tempoAtual;
        }
    }
}
