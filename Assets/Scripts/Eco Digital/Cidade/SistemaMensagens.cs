using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Sistema de mensagens do Eco Digital.
/// - Pode gerar notificações automaticamente a cada X segundos.
/// - Mantém um contador TOTAL de notificações recebidas (1,2,3,...).
/// - Dispara um evento toda vez que uma nova notificação chega,
///   passando o total acumulado.
/// 
/// OBS: O nome do evento e da função continuam os mesmos para não
/// quebrar os outros scripts (GestorGatilhos, GerenciadorAnimacoes, etc.).
/// Agora o int significa "total recebidas", não "não lidas".
/// </summary>
[DisallowMultipleComponent]
public class SistemaMensagens : MonoBehaviour
{
    [Header("Geração automática")]
    [Tooltip("Se verdadeiro, recebe 1 notificação a cada 'intervaloSegundos'.")]
    [SerializeField] private bool iniciarAutomatico = true;

    [Tooltip("Intervalo (s) entre notificações.")]
    [SerializeField, Min(0.1f)] private float intervaloSegundos = 5f;

    [Tooltip("Se falso, usa tempo real (WaitForSecondsRealtime).")]
    [SerializeField] private bool usarTimeScale = true;

    /// <summary>
    /// Evento disparado SEMPRE que chega uma nova notificação.
    /// O int passado é o TOTAL de notificações recebidas até agora.
    /// </summary>
    public event Action<int> NotificacaoRecebida;

    // ---- Estado interno ----
    [Tooltip("Total de notificações recebidas desde o início.")]
    [SerializeField] private int totalRecebidas = 0;

    private Coroutine coLoop;

    private void Start()
    {
        if (iniciarAutomatico)
            IniciarMensagensAutomaticas();
    }

    private void OnDisable()
    {
        PararMensagensAutomaticas();
    }

    // ================= API PÚBLICA =================

    /// <summary>
    /// Inicia o recebimento automático: 1 notificação a cada 'intervaloSegundos'.
    /// </summary>
    public void IniciarMensagensAutomaticas()
    {
        PararMensagensAutomaticas();
        coLoop = StartCoroutine(CoLoopRecebimento());
    }

    /// <summary>
    /// Para o recebimento automático.
    /// </summary>
    public void PararMensagensAutomaticas()
    {
        if (coLoop != null)
        {
            StopCoroutine(coLoop);
            coLoop = null;
        }
    }

    /// <summary>
    /// Dispara MANUALMENTE uma notificação (soma +1 no total).
    /// </summary>
    public void ReceberNotificacao()
    {
        totalRecebidas++;
        NotificacaoRecebida?.Invoke(totalRecebidas);
    }

    /// <summary>
    /// Retorna o total de notificações recebidas desde o início.
    /// </summary>
    public int ObterTotalRecebidas() => totalRecebidas;

    // ================= Coroutines =================

    private IEnumerator CoLoopRecebimento()
    {
        while (true)
        {
            if (usarTimeScale)
                yield return new WaitForSeconds(intervaloSegundos);
            else
                yield return new WaitForSecondsRealtime(intervaloSegundos);

            ReceberNotificacao();
        }
    }
}
