using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// UI do R.O.B Phone.
/// - Mostra um número simples (1,2,3,...) com total de notificações recebidas.
/// - Quando chega uma notificação nova, toca animação do balão.
/// - Independente disso, o topo (ponto-traço-ponto) faz uma animação a cada X segundos.
/// </summary>
[DisallowMultipleComponent]
public class RobPhoneUI : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Sistema de mensagens que dispara o evento NotificacaoRecebida.")]
    [SerializeField] private SistemaMensagens sistemaMensagens;

    [Tooltip("Texto que mostra o número total de notificações recebidas (1,2,3...).")]
    [SerializeField] private TextMeshProUGUI textoContador;

    [Tooltip("Animator do R.O.B Phone (animações de topo e balão).")]
    [SerializeField] private Animator animator;

    [Header("Parâmetros de animação (Animator)")]
    [Tooltip("Trigger usado para animar o topo (ponto-traço-ponto).")]
    [SerializeField] private string triggerTopoPing = "TopoPing";

    [Tooltip("Trigger usado para animar o balão de mensagem.")]
    [SerializeField] private string triggerBalaoPing = "BalaoPing";

    [Header("Animação automática do topo")]
    [Tooltip("Se verdadeiro, o topo anima sozinho a cada 'intervaloTopoSegundos'.")]
    [SerializeField] private bool animarTopoAutomatico = true;

    [Tooltip("Intervalo em segundos entre animações automáticas do topo.")]
    [SerializeField, Min(0.1f)] private float intervaloTopoSegundos = 3f;

    private Coroutine coTopo;

    private void Reset()
    {
        if (!sistemaMensagens)
            sistemaMensagens = FindObjectOfType<SistemaMensagens>();
    }

    private void OnEnable()
    {
        if (!sistemaMensagens)
            sistemaMensagens = FindObjectOfType<SistemaMensagens>();

        if (sistemaMensagens != null)
            sistemaMensagens.NotificacaoRecebida += OnNotificacaoRecebida;

        if (animarTopoAutomatico && coTopo == null)
            coTopo = StartCoroutine(CoLoopTopo());
    }

    private void OnDisable()
    {
        if (sistemaMensagens != null)
            sistemaMensagens.NotificacaoRecebida -= OnNotificacaoRecebida;

        if (coTopo != null)
        {
            StopCoroutine(coTopo);
            coTopo = null;
        }
    }

    // ================= CALLBACK DA MENSAGEM =================

    private void OnNotificacaoRecebida(int totalRecebidas)
    {
        // Atualiza o número (1,2,3...) no telefone
        if (textoContador != null)
            textoContador.text = totalRecebidas.ToString();

        // Toca animação do balão
        if (animator != null && !string.IsNullOrEmpty(triggerBalaoPing))
            animator.SetTrigger(triggerBalaoPing);
    }

    // ================= LOOP DO TOPO =================

    private IEnumerator CoLoopTopo()
    {
        while (true)
        {
            if (intervaloTopoSegundos > 0f)
                yield return new WaitForSeconds(intervaloTopoSegundos);
            else
                yield return null;

            if (animator != null && !string.IsNullOrEmpty(triggerTopoPing))
                animator.SetTrigger(triggerTopoPing);
        }
    }
}
