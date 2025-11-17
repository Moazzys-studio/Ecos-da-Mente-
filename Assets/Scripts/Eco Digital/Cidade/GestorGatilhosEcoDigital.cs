using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestor de ansiedade do Eco Digital.
/// - Mensagens aumentam ansiedade.
/// - Colisão com NPC CONECTADO = aumento instantâneo.
/// - AURA de NPC CONECTADO = aumenta por segundo enquanto dentro.
/// - OUTDOOR = aumenta por segundo enquanto dentro.
/// - ZONA DE CONFORTO = reduz por segundo e pode pausar mensagens.
/// - Quando ansiedade chega no máximo, carrega cena de derrota.
/// - Representação visual: Slider com handle que fica cada vez mais vermelho.
/// </summary>
[DisallowMultipleComponent]
public class GestorGatilhosEcoDigital : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Slider que representa a ansiedade (min=0, max=1).")]
    [SerializeField] private GerenciadorAnimacoesEcoDigital gerenciadorAnimacoes;
    [SerializeField] private Slider sliderAnsiedade;

    [Tooltip("Imagem do handle do slider (emoji/cabecinha ansiosa).")]
    [SerializeField] private Image handleImagem;

    [Header("Referências")]
    [Tooltip("Sistema de mensagens que gera notificações.")]
    [SerializeField] private SistemaMensagens sistemaMensagens;

    [Header("Configuração da Ansiedade")]
    [Tooltip("Valor máximo de ansiedade (100 é o padrão).")]
    [SerializeField, Min(1f)] private float ansiedadeMax = 100f;

    [Tooltip("Velocidade da interpolação visual do slider.")]
    [SerializeField, Min(0.01f)] private float velocidadeInterpolacaoBarra = 5f;

    [Header("Gatilhos - Valores de Ansiedade")]
    [Tooltip("Quanto de ansiedade aumenta ao receber UMA mensagem.")]
    [SerializeField] private float ansiedadePorMensagem = 2f;

    [Tooltip("Quanto de ansiedade aumenta instantaneamente ao colidir com NPC conectado.")]
    [SerializeField] private float ansiedadeColisaoConectado = 10f;

    [Tooltip("Quanto de ansiedade aumenta POR SEGUNDO por cada aura de NPC CONECTADO ativa.")]
    [SerializeField] private float ansiedadePorSegundoAuraConectado = 3f;

    [Tooltip("Quanto de ansiedade aumenta POR SEGUNDO ao estar em área de Outdoor (Mesmerize).")]
    [SerializeField] private float ansiedadePorSegundoOutdoor = 4f;

    [Tooltip("Quanto de ansiedade DIMINUI POR SEGUNDO em Zona de Conforto.")]
    [SerializeField] private float ansiedadePorSegundoConforto = 6f;

    

    [Header("Zonas de Conforto x Sistema de Mensagens")]
    [Tooltip("Se verdadeiro, ao entrar em QUALQUER zona de conforto o sistema de mensagens é pausado, e retomado ao sair de todas.")]
    [SerializeField] private bool pausarMensagensEmConforto = true;

    [Header("Derrota")]
    [Tooltip("Nome da cena de derrota a ser carregada quando ansiedade >= 100%.")]
    [SerializeField] private string nomeCenaDerrota = "CenaDerrotaEcoDigital";

    // ===== ESTADO INTERNO =====

    private float ansiedadeAtual = 0f; // valor real 0..ansiedadeMax

    private int qtdAurasConectado = 0;
    private int qtdZonasOutdoor = 0;
    private int qtdZonasConforto = 0;

    private bool jaDerrotou = false;
    private bool mensagensPausadasEmConforto = false;

    private void Awake()
    {
        if (sliderAnsiedade == null)
            Debug.LogWarning("[GestorGatilhosEcoDigital] Slider de ansiedade não atribuído.");

        if (handleImagem == null)
            Debug.LogWarning("[GestorGatilhosEcoDigital] Handle do slider não atribuído.");

        if (sistemaMensagens == null)
            Debug.LogWarning("[GestorGatilhosEcoDigital] SistemaMensagens não atribuído.");

        if (sliderAnsiedade != null)
        {
            sliderAnsiedade.minValue = 0f;
            sliderAnsiedade.maxValue = 1f;
            sliderAnsiedade.value = 0f;
        }

        AtualizarCorHandle(0f);
    }

    private void OnEnable()
    {
        if (sistemaMensagens != null)
            sistemaMensagens.NotificacaoRecebida += OnNotificacaoRecebida;
    }

    private void OnDisable()
    {
        if (sistemaMensagens != null)
            sistemaMensagens.NotificacaoRecebida -= OnNotificacaoRecebida;
    }

    private void Update()
    {
        if (jaDerrotou) return;

        float delta = Time.deltaTime;

        // 1) Aura de NPC conectado -> ansiedade por segundo
        if (qtdAurasConectado > 0 && ansiedadePorSegundoAuraConectado > 0f)
            ModificarAnsiedade(ansiedadePorSegundoAuraConectado * qtdAurasConectado * delta);

        // 2) Outdoor -> ansiedade por segundo
        if (qtdZonasOutdoor > 0 && ansiedadePorSegundoOutdoor > 0f)
            ModificarAnsiedade(ansiedadePorSegundoOutdoor * qtdZonasOutdoor * delta);

        // 3) Zona de conforto -> reduz por segundo
        if (qtdZonasConforto > 0 && ansiedadePorSegundoConforto > 0f)
            ModificarAnsiedade(-ansiedadePorSegundoConforto * qtdZonasConforto * delta);

        // 4) Interpolação da barra
        AtualizarBarraVisual(delta);

        // ---------------------------------------------
        // troca para a próxima cena ao chegar a 0.80
        // ---------------------------------------------
        if (sliderAnsiedade != null && sliderAnsiedade.value >= 0.80f)
        {
            int indexAtual = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene("PuzzleEcoDigital");
        }
    }

    private void OnNotificacaoRecebida(int naoLidasAtual)
    {
        ModificarAnsiedade(ansiedadePorMensagem);
    }

    private void ModificarAnsiedade(float delta)
    {
        if (jaDerrotou) return;

        ansiedadeAtual += delta;
        ansiedadeAtual = Mathf.Clamp(ansiedadeAtual, 0f, ansiedadeMax);

        if (gerenciadorAnimacoes != null)
            gerenciadorAnimacoes.AtualizarAnsiedade(ansiedadeAtual);

        if (AnsiedadeChegouNoMaximo())
        {
            ansiedadeAtual = ansiedadeMax;
            DispararDerrota();
        }
    }

    private bool AnsiedadeChegouNoMaximo()
    {
        return ansiedadeAtual >= ansiedadeMax - Mathf.Epsilon;
    }

    private void AtualizarBarraVisual(float deltaTime)
    {
        if (sliderAnsiedade == null) return;

        float alvo = ansiedadeAtual / ansiedadeMax;
        float atual = sliderAnsiedade.value;

        float novo = Mathf.Lerp(atual, alvo, velocidadeInterpolacaoBarra * deltaTime);
        sliderAnsiedade.value = novo;

        AtualizarCorHandle(novo);
    }

    private void AtualizarCorHandle(float valorNormalizado)
    {
        if (handleImagem == null) return;

        Color corInicial = new Color(1f, 0.85f, 0.3f);
        Color corFinal = Color.red;

        handleImagem.color = Color.Lerp(corInicial, corFinal, Mathf.Clamp01(valorNormalizado));
    }

    private void DispararDerrota()
    {
        if (jaDerrotou) return;
        jaDerrotou = true;

        Debug.Log("[GestorGatilhosEcoDigital] Ansiedade chegou ao máximo. Carregando cena de derrota...");

        if (!string.IsNullOrEmpty(nomeCenaDerrota))
            SceneManager.LoadScene(nomeCenaDerrota);
        else
            Debug.LogWarning("[GestorGatilhosEcoDigital] Nome da cena de derrota não definido.");
    }

    public void AdicionarAnsiedade(float delta)
    {
        ansiedadeAtual = Mathf.Clamp(ansiedadeAtual + delta, 0f, 100f);

        if (sliderAnsiedade != null)
            sliderAnsiedade.value = ansiedadeAtual;

        if (gerenciadorAnimacoes != null)
            gerenciadorAnimacoes.AtualizarAnsiedade(ansiedadeAtual);
    }

    public void ReduzirAnsiedade(float delta)
    {
        AdicionarAnsiedade(-delta);
    }

    public void RegistrarColisaoNpcConectado()
    {
        ModificarAnsiedade(ansiedadeColisaoConectado);
    }

    public void EntrouAuraNpcConectado()
    {
        qtdAurasConectado = Mathf.Max(0, qtdAurasConectado + 1);
    }

    public void SaiuAuraNpcConectado()
    {
        qtdAurasConectado = Mathf.Max(0, qtdAurasConectado - 1);
    }

    public void EntrouZonaOutdoor()
    {
        qtdZonasOutdoor = Mathf.Max(0, qtdZonasOutdoor + 1);
    }

    public void SaiuZonaOutdoor()
    {
        qtdZonasOutdoor = Mathf.Max(0, qtdZonasOutdoor - 1);
    }

    public void EntrouZonaConforto()
    {
        qtdZonasConforto = Mathf.Max(0, qtdZonasConforto + 1);

        if (pausarMensagensEmConforto && !mensagensPausadasEmConforto && sistemaMensagens != null)
        {
            sistemaMensagens.PararMensagensAutomaticas();
            mensagensPausadasEmConforto = true;
        }
    }

    public void SaiuZonaConforto()
    {
        qtdZonasConforto = Mathf.Max(0, qtdZonasConforto - 1);

        if (pausarMensagensEmConforto && mensagensPausadasEmConforto && qtdZonasConforto == 0 && sistemaMensagens != null)
        {
            sistemaMensagens.IniciarMensagensAutomaticas();
            mensagensPausadasEmConforto = false;
        }
    }

    public float ObterAnsiedadeAtual() => ansiedadeAtual;
    public float ObterAnsiedadeNormalizada() => ansiedadeAtual / ansiedadeMax;
}
