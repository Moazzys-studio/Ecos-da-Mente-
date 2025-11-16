using System.Collections;
using UnityEngine;
using TMPro;
using System;


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

    [Header("UI - Não lidas (sempre ativo)")]
    [Tooltip("Painel que SEMPRE fica ativo.")]
    [SerializeField] private GameObject painelNaoLidas;

    [Tooltip("TMP com o texto 'X notificações não lidas'.")]
    [SerializeField] private TextMeshProUGUI textoNaoLidas;

    [Header("UI - Notificação recebida")]
    [Tooltip("Painel que aparece SOMENTE quando chega notificação.")]
    [SerializeField] private GameObject painelRecebida;

    [Tooltip("TMP do painel de recebida ('1 Notificação recebida').")]
    [SerializeField] private TextMeshProUGUI textoRecebida;

    [Tooltip("Quanto tempo (s) o painel 'recebida' fica visível após cada chegada.")]
    [SerializeField, Min(0.1f)] private float tempoExibicaoRecebida = 1.5f;

    public event Action<int> NotificacaoRecebida;


    // =================== ANIMAÇÃO ====================
    [Header("Animação")]
    [Tooltip("Animator da imagem de notificação recebida.")]
    [SerializeField] private Animator animRecebida;

    [Tooltip("Trigger usado para tocar a animação.")]
    [SerializeField] private string triggerAnim = "Play";

    // ----- Estado -----
    private int naoLidas = 0;
    private Coroutine coLoop;
    private bool timerAtivo = false;
    private int versaoExibicao = 0;

    private void Awake()
    {
        naoLidas = 0;
    }

    private void Start()
    {
        if (painelNaoLidas) painelNaoLidas.SetActive(true);
        if (painelRecebida) painelRecebida.SetActive(false);
        AtualizarTextoNaoLidas();

        if (iniciarAutomatico) IniciarMensagensAutomaticas();
    }

    private void OnDisable()
    {
        PararMensagensAutomaticas();
        if (painelRecebida) painelRecebida.SetActive(false);
        timerAtivo = false;
    }

    // ================= API =================

    public void IniciarMensagensAutomaticas()
    {
        PararMensagensAutomaticas();
        coLoop = StartCoroutine(CoLoopRecebimento());
    }

    public void PararMensagensAutomaticas()
    {
        if (coLoop != null)
        {
            StopCoroutine(coLoop);
            coLoop = null;
        }
    }

    public void ReceberNotificacao()
    {
        // 1) Soma +1 nas não lidas
        naoLidas++;
        AtualizarTextoNaoLidas();

        // 2) Mostra painel "Notificação recebida"
        if (painelRecebida) painelRecebida.SetActive(true);
        if (textoRecebida) textoRecebida.text = "1 Notificação recebida";

        // 3) TOCAR A ANIMAÇÃO
        if (animRecebida)
        {
            animRecebida.ResetTrigger(triggerAnim);
            animRecebida.Play(0, 0, 0);
            animRecebida.SetTrigger(triggerAnim);
        }

        // 4) Reinicia o timer de exibição
        versaoExibicao++;
        if (!timerAtivo) StartCoroutine(CoOcultarRecebidaDepois(versaoExibicao));

        NotificacaoRecebida?.Invoke(naoLidas);
    }

    public void MarcarTodasComoLidas()
    {
        naoLidas = 0;
        AtualizarTextoNaoLidas();
    }

    public int ObterNaoLidas() => naoLidas;

    // ================= Coroutines =================

    private IEnumerator CoLoopRecebimento()
    {
        while (true)
        {
            if (usarTimeScale) yield return new WaitForSeconds(intervaloSegundos);
            else yield return new WaitForSecondsRealtime(intervaloSegundos);

            ReceberNotificacao();
        }
    }

    private IEnumerator CoOcultarRecebidaDepois(int versaoLocal)
    {
        timerAtivo = true;

        if (usarTimeScale) yield return new WaitForSeconds(tempoExibicaoRecebida);
        else yield return new WaitForSecondsRealtime(tempoExibicaoRecebida);

        if (versaoLocal == versaoExibicao && painelRecebida)
            painelRecebida.SetActive(false);

        timerAtivo = false;
    }

    // ================= UI =================

    private void AtualizarTextoNaoLidas()
    {
        if (!textoNaoLidas) return;

        if (naoLidas == 0)
            textoNaoLidas.text = "Sem notificações";
        else if (naoLidas == 1)
            textoNaoLidas.text = "1 Notificação pendente";
        else
            textoNaoLidas.text = $"{naoLidas} notificações pendentes";
    }
}