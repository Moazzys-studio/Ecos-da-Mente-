using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

[RequireComponent(typeof(Rigidbody))]
public class eco_movimento : MonoBehaviour
{
        [Header("Velocidade vertical")]
    public float velocidadeVertical = 10f;
    public float maxVelocidadeVertical = 15f;

    [Header("Detecção de colisão acima/baixo")]
    public LayerMask camadasSolo;
    public float distanciaChecagem = 0.4f;

    [Header("Rotação suave")]
    public float atrasoRotacao = 0.1f;
    public float duracaoRotacao = 0.25f;

    [Header("Novo Input System")]
    public InputActionReference acaoAlternarRef;

    [Header("Animações")]
    public string animacaoCaindo = "Caindo";
    public float tempoMinimoCaindo = 0.5f;

    [Header("Gravidade personalizada")]
    public float gravidade = 20f;

    private Rigidbody _rb;
    private Animator _anim;
    private bool _subindo = false;
    private Coroutine _rotacaoCo;
    private bool _estaNoChao;

    private InputAction _acaoAlternarFallback;
    private Vector3 posicaoInicial; // <-- Guarda a posição inicial do player

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.freezeRotation = true;

        _anim = GetComponent<Animator>();

        if (!EnhancedTouchSupport.enabled)
            EnhancedTouchSupport.Enable();

        if (acaoAlternarRef == null)
        {
            _acaoAlternarFallback = new InputAction("AlternarMover");
            _acaoAlternarFallback.AddBinding("<Pointer>/press");
            _acaoAlternarFallback.AddBinding("<Keyboard>/space");
        }
    }

    void Start()
    {
        // Guarda a posição inicial do jogador
        posicaoInicial = transform.position;
    }

    void OnEnable()
    {
        var action = acaoAlternarRef != null ? acaoAlternarRef.action : _acaoAlternarFallback;
        if (action != null)
        {
            action.Enable();
            action.started += OnAlternarStarted;
        }
    }

    void OnDisable()
    {
        var action = acaoAlternarRef != null ? acaoAlternarRef.action : _acaoAlternarFallback;
        if (action != null)
        {
            action.started -= OnAlternarStarted;
            if (acaoAlternarRef == null) action.Disable();
        }
    }

    private void OnAlternarStarted(InputAction.CallbackContext ctx)
    {
        _subindo = !_subindo;
        AplicarVelocidadeVertical();
        IniciarRotacaoSuave();
    }

    private void AplicarVelocidadeVertical()
    {
        float vy = _subindo ? velocidadeVertical : -velocidadeVertical;

        Vector3 v = _rb.velocity;
        v.x = 0f;
        v.z = 0f;
        v.y = Mathf.Clamp(vy, -maxVelocidadeVertical, maxVelocidadeVertical);
        _rb.velocity = v;
    }

    private void IniciarRotacaoSuave()
    {
        if (_rotacaoCo != null) StopCoroutine(_rotacaoCo);
        _rotacaoCo = StartCoroutine(RotacionarSuave());
    }

    private IEnumerator RotacionarSuave()
    {
        yield return new WaitForSeconds(atrasoRotacao);

        if (_anim != null)
            _anim.SetBool(animacaoCaindo, true);

        float alvoZ = _subindo ? 180f : 0f;
        Quaternion rotInicial = transform.rotation;
        Quaternion rotFinal = Quaternion.Euler(0f, 90f, alvoZ);

        float t = 0f;
        while (t < duracaoRotacao)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Lerp(rotInicial, rotFinal, t / duracaoRotacao);
            yield return null;
        }

        transform.rotation = rotFinal;

        yield return new WaitForSeconds(tempoMinimoCaindo);

        if (_anim != null)
            _anim.SetBool(animacaoCaindo, false);
    }

    void FixedUpdate()
    {
        bool noChaoAgora = EstaEncostadoAbaixo();

        if (!_estaNoChao && noChaoAgora)
        {
            if (_anim != null)
                _anim.SetBool(animacaoCaindo, false);
        }

        _estaNoChao = noChaoAgora;

        if (noChaoAgora && !_subindo)
        {
            var v = _rb.velocity; v.y = 0f; _rb.velocity = v;
        }
        else if (_subindo && EstaEncostadoAcima())
        {
            var v = _rb.velocity; v.y = 0f; _rb.velocity = v;
        }
        else
        {
            if (!_subindo && !noChaoAgora)
            {
                Vector3 v = _rb.velocity;
                v.y -= gravidade * Time.fixedDeltaTime;
                v.y = Mathf.Clamp(v.y, -maxVelocidadeVertical, maxVelocidadeVertical);
                _rb.velocity = v;
            }
        }
    }

    private bool EstaEncostadoAbaixo()
    {
        return Physics.Raycast(transform.position, Vector3.down, distanciaChecagem, camadasSolo);
    }

    private bool EstaEncostadoAcima()
    {
        return Physics.Raycast(transform.position, Vector3.up, distanciaChecagem, camadasSolo);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("morre"))
    {
        Eco_Streamer_Variaveis.vida_ecoStreamer--;
        // Reseta posição e velocidade
        transform.position = posicaoInicial;
        _rb.velocity = Vector3.zero;
        _subindo = false;

        // Garante que o personagem volte com rotação padrão (em pé)
        transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        if (_anim != null)
            _anim.SetBool(animacaoCaindo, false);
    }
    }

    private void OnTriggerEnter(Collider other)
    {
       if (other.gameObject.CompareTag("coletavel"))
    {
        // Avisa o controlador de inimigos
        FindObjectOfType<controle_inimigos>().OnCollectiblePicked();

        Destroy(other.gameObject); 
    }

    if (other.gameObject.CompareTag("inimigo"))
    {
        Eco_Streamer_Variaveis.vida_ecoStreamer--;
        Eco_Streamer_Variaveis.ecoStreamer_inimigos--;
        Destroy(other.gameObject);

        // Reseta posição e velocidade
        transform.position = posicaoInicial;
        _rb.velocity = Vector3.zero;
        _subindo = false;

        // Garante que o personagem volte com rotação padrão (em pé)
        transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        if (_anim != null)
            _anim.SetBool(animacaoCaindo, false);
    }
    }
}