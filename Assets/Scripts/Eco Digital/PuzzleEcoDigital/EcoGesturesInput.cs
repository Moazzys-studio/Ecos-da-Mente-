using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Responsável por converter o toque/clique do Novo Input System
/// em comandos de desenho para a MecanicaDesenhoNaTela.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class EcoGesturesInput : MonoBehaviour
{
    [Header("Referência da Mecânica de Desenho")]
    [Tooltip("Script responsável por criar e processar o traço desenhado na tela.")]
    [SerializeField] private MecanicaDesenhoNaTela desenho;

    [Header("Configurações de Input Actions")]
    [Tooltip("Nome do Action Map no Input System (ex.: DrawMechanic).")]
    [SerializeField] private string nomeActionMap = "DrawMechanic";

    [Tooltip("Nome da Action responsável por detectar toque/clique.")]
    [SerializeField] private string nomeActionContato = "PointerContact";

    [Tooltip("Nome da Action responsável por fornecer a posição do toque/clique.")]
    [SerializeField] private string nomeActionPosicao = "PointerPosition";

    // Actions do Input System
    private InputAction acaoContato;
    private InputAction acaoPosicao;

    // Estado de desenho
    private bool desenhando = false;
    private PlayerInput playerInput;

    private void Awake()
    {
        if (!desenho)
            desenho = FindFirstObjectByType<MecanicaDesenhoNaTela>();

        playerInput = GetComponent<PlayerInput>();

        // Garante que estamos no Action Map correto
        if (!string.IsNullOrEmpty(nomeActionMap))
            playerInput.SwitchCurrentActionMap(nomeActionMap);

        // Pega as actions criadas no InputActions
        acaoContato  = playerInput.actions[nomeActionContato];
        acaoPosicao  = playerInput.actions[nomeActionPosicao];
    }

    private void OnEnable()
    {
        acaoContato.started  += ComecarContato;
        acaoContato.canceled += FinalizarContato;
        acaoContato.Enable();
        acaoPosicao.Enable();
    }

    private void OnDisable()
    {
        acaoContato.started  -= ComecarContato;
        acaoContato.canceled -= FinalizarContato;
        acaoContato.Disable();
        acaoPosicao.Disable();
    }

    private void Update()
    {
        if (!desenhando || desenho == null)
            return;

        Vector2 pos = acaoPosicao.ReadValue<Vector2>();
        desenho.AdicionarPontoTela(pos);
    }

    private void ComecarContato(InputAction.CallbackContext ctx)
    {
        if (desenho == null) return;

        Vector2 posicao = acaoPosicao.ReadValue<Vector2>();
        desenho.IniciarTraco(posicao);
        desenhando = true;
    }

    private void FinalizarContato(InputAction.CallbackContext ctx)
    {
        if (desenho == null) return;

        desenho.FinalizarTraco();
        desenhando = false;
    }
}
