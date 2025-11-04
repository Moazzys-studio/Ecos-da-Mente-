using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[AddComponentMenu("Ecos/Gestor Gatilhos")]
public class GestorGatilhos : MonoBehaviour
{
    [Header("Barra de Progresso (UI)")]
    [Tooltip("Arraste aqui a Image da barra (Type = Filled).")]
    [SerializeField] private Image barraImage;

    [Tooltip("Valor máximo da barra.")]
    [SerializeField] private int valorMaximo = 100;

    [Tooltip("Valor atual da barra (0..valorMaximo).")]
    [SerializeField, Min(0)] private int valorAtual = 0;

    [Header("Incrementos de Gatilhos")]
    [Tooltip("Quanto o GatilhoPortaSaida adiciona (ex.: 15).")]
    [SerializeField] private int incrementoPortaSaida = 15;

    [Header("Fade de Tela (sobre a câmera/VCam)")]
    [Tooltip("Imagem full-screen preta no Canvas (a cor preta será animada no alfa).")]
    [SerializeField] private Image telaPreta;

    [Tooltip("Duração do fade IN (até preto).")]
    [SerializeField, Min(0f)] private float duracaoFadeIn = 0.25f;

    [Tooltip("Pausa no preto, antes do fade OUT.")]
    [SerializeField, Min(0f)] private float pausaNoPreto = 0.05f;

    [Tooltip("Duração do fade OUT (voltar a ver a cena).")]
    [SerializeField, Min(0f)] private float duracaoFadeOut = 0.25f;

    [Header("Teleporte do Jogador")]
    [Tooltip("Transform do jogador que será teleportado.")]
    [SerializeField] private Transform jogador;

    [Tooltip("Ponto vazio indicando a posição inicial para teleporte.")]
    [SerializeField] private Transform pontoInicial;

    [Tooltip("Se verdadeiro, ao teleportar também aplica a rotação do ponto inicial.")]
    [SerializeField] private bool usarRotacaoDoPontoInicial = false;

    [Header("Eventos")]
    [Tooltip("Chamado sempre que a barra muda (envia o valorAtual normalizado 0..1).")]
    public UnityEvent<float> OnBarraAtualizada;

    [Tooltip("Chamado quando a barra atinge o valor máximo.")]
    public UnityEvent OnBarraCheia;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    // ---- Estado interno ----
    private bool _fazendoFade = false;

    private void Awake()
    {
        if (telaPreta != null)
        {
            var c = telaPreta.color;
            c.a = 0f;
            telaPreta.color = c;
            telaPreta.raycastTarget = true; // bloqueia clique durante o fade
        }
        AtualizarBarraUI();
    }

    // ====== API ======
    public void GatilhoPortaSaida()
    {
        SomarNaBarra(incrementoPortaSaida);
        if (!_fazendoFade)
            StartCoroutine(RotinaFadeTeleport());
        if (logDebug) Debug.Log("[GestorGatilhos] GatilhoPortaSaida acionado.");
    }

    // ====== Barra ======
    private void SomarNaBarra(int delta)
    {
        int anterior = valorAtual;
        valorAtual = Mathf.Clamp(valorAtual + delta, 0, Mathf.Max(1, valorMaximo));
        if (valorAtual != anterior)
        {
            AtualizarBarraUI();
            if (valorAtual >= valorMaximo) OnBarraCheia?.Invoke();
        }
    }

    private void AtualizarBarraUI()
    {
        float norm = (valorMaximo > 0) ? (valorAtual / (float)valorMaximo) : 0f;
        if (barraImage != null)
        {
            if (barraImage.type != Image.Type.Filled) barraImage.type = Image.Type.Filled;
            barraImage.fillAmount = Mathf.Clamp01(norm);
        }
        OnBarraAtualizada?.Invoke(norm);
    }

    // ====== Fade + Teleporte ======
    private IEnumerator RotinaFadeTeleport()
    {
        _fazendoFade = true;
        yield return StartCoroutine(FazerFade(1f, duracaoFadeIn)); // IN
        TeleportarJogadorParaInicio();
        if (pausaNoPreto > 0f) yield return new WaitForSeconds(pausaNoPreto);
        yield return StartCoroutine(FazerFade(0f, duracaoFadeOut)); // OUT
        _fazendoFade = false;
    }

    private IEnumerator FazerFade(float alvoAlpha, float duracao)
    {
        if (telaPreta == null || duracao <= 0f)
        {
            if (telaPreta != null)
            {
                var c2 = telaPreta.color;
                c2.a = Mathf.Clamp01(alvoAlpha);
                telaPreta.color = c2;
            }
            yield break;
        }

        float inicial = telaPreta.color.a;
        float t = 0f;
        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracao);
            var c = telaPreta.color;
            c.a = Mathf.Lerp(inicial, alvoAlpha, k);
            telaPreta.color = c;
            yield return null;
        }
    }

    private void TeleportarJogadorParaInicio()
    {
        if (jogador == null || pontoInicial == null)
        {
            Debug.LogWarning("[GestorGatilhos] Teleporte ignorado: 'jogador' ou 'pontoInicial' não atribuídos.");
            return;
        }

        // Se tiver CharacterController, esse é o padrão seguro:
        var cc = jogador.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            jogador.position = pontoInicial.position;
            if (usarRotacaoDoPontoInicial) jogador.rotation = pontoInicial.rotation;
            cc.enabled = true;
        }
        else
        {
            jogador.position = pontoInicial.position;
            if (usarRotacaoDoPontoInicial) jogador.rotation = pontoInicial.rotation;
        }

        if (logDebug) Debug.Log("[GestorGatilhos] Teleport executado.");
    }
}
