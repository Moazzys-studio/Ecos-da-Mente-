using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem; // Novo Input System

[RequireComponent(typeof(Collider))]
public class PortaSaidaTrigger : MonoBehaviour
{
    public enum ModoLado { Markers, Axis }
    public enum AxisLocal { X, Y, Z }

    [Header("Modo de detecção do lado")]
    [SerializeField] private ModoLado modo = ModoLado.Markers;

    [Header("Markers (recomendado)")]
    [Tooltip("Empty FORA do vão (world).")]
    [SerializeField] private Transform pontoFora;
    [Tooltip("Empty DENTRO do vão (world).")]
    [SerializeField] private Transform pontoDentro;

    [Header("Axis (alternativo)")]
    [SerializeField] private AxisLocal foraAxis = AxisLocal.Z;
    [SerializeField] private bool inverterAxis = false;

    [Header("Estabilidade")]
    [Tooltip("Profundidade além do plano para confirmar DENTRO.")]
    [SerializeField, Min(0f)] private float confirmInsideDepth = 0.20f;

    [Header("Clique/Toque para abrir (Novo Input System)")]
    [Tooltip("Câmera usada no raycast de clique/toque. Se nula, tenta Camera.main.")]
    [SerializeField] private Camera cameraRay;
    [Tooltip("Camadas permitidas no clique.")]
    [SerializeField] private LayerMask clickMask = ~0;
    [Tooltip("Se marcado, aceita clique em QUALQUER collider que seja filho do PortaController.")]
    [SerializeField] private bool aceitarCliqueEmFilhosDaPorta = true;
    [Tooltip("Colliders específicos que podem ser clicados (opcional). Se vazio, usa a regra acima.")]
    [SerializeField] private Collider[] collidersValidos;

    [Header("Player / Porta")]
    [SerializeField] private string tagPlayer = "Player";
    [SerializeField] private PortaController porta;

    [Header("Comportamento")]
    [Tooltip("Só permite abrir se o player estiver dentro do trigger (no vão).")]
    [SerializeField] private bool exigirPlayerNoVao = true;

    [Header("Animação")]
    [Tooltip("Animator da porta (ou do conjunto visual) que terá o bool NaSaida ligado enquanto o player estiver no trigger.")]
    [SerializeField] private Animator animatorDaPorta;
    [Tooltip("Nome do parâmetro bool no Animator.")]
    [SerializeField] private string parametroBoolNaSaida = "NaSaida";

    [Header("Eventos de passagem")]
    public UnityEvent OnQualquerPassagem, OnPassagemParaDentro, OnPassagemParaFora;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    // Estado
    private bool playerNoVao = false;
    private bool ultimoOutside = true;

    // Input
    private InputAction _pressAction;

    // Cache do hash do parâmetro
    private int _hashNaSaida;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void OnValidate()
    {
        if (!porta) porta = GetComponentInParent<PortaController>();
        if (!cameraRay) cameraRay = Camera.main;
    }

    private void Awake()
    {
        _hashNaSaida = Animator.StringToHash(string.IsNullOrWhiteSpace(parametroBoolNaSaida) ? "NaSaida" : parametroBoolNaSaida);
        if (!animatorDaPorta)
        {
            animatorDaPorta = GetComponent<Animator>();
            if (!animatorDaPorta) animatorDaPorta = GetComponentInParent<Animator>();
        }
    }

    private void OnEnable()
    {
        _pressAction = new InputAction("Press", binding: "<Pointer>/press");
        _pressAction.AddBinding("<Touchscreen>/primaryTouch/press");
        _pressAction.started += OnPressStarted; // começou clique/toque
        _pressAction.Enable();
    }

    private void OnDisable()
    {
        if (_pressAction != null)
        {
            _pressAction.started -= OnPressStarted;
            _pressAction.Disable();
            _pressAction.Dispose();
            _pressAction = null;
        }
    }

    private void SetNaSaida(bool valor)
    {
        if (animatorDaPorta)
        {
            animatorDaPorta.SetBool(_hashNaSaida, valor);
            if (logDebug) Debug.Log($"[PortaSaidaTrigger] Animator.SetBool({parametroBoolNaSaida},{valor})");
        }
        else if (logDebug)
        {
            Debug.Log("[PortaSaidaTrigger] Animator não atribuído (ignorado).");
        }
    }

    private void OnPressStarted(InputAction.CallbackContext ctx)
    {
        if (porta == null)
        {
            if (logDebug) Debug.LogWarning("[PortaSaidaTrigger] PortaController não atribuído.");
            return;
        }
        if (exigirPlayerNoVao && !playerNoVao)
        {
            if (logDebug) Debug.Log("[PortaSaidaTrigger] Ignorado: player não está no vão.");
            return;
        }

        Camera cam = cameraRay ? cameraRay : Camera.main;
        if (cam == null)
        {
            if (logDebug) Debug.LogWarning("[PortaSaidaTrigger] cameraRay nula e Camera.main não encontrada.");
            return;
        }

        Vector2 screenPos;
        if (Touchscreen.current?.primaryTouch.press.isPressed == true)
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null)
            screenPos = Mouse.current.position.ReadValue();
        else
            return;

        var ray = cam.ScreenPointToRay(screenPos);

        // 1) Raycast simples
        if (Physics.Raycast(ray, out var hit, 1000f, clickMask, QueryTriggerInteraction.Collide))
        {
            if (CliqueAceito(hit.collider))
            {
                AbrirConformeLado();
                return;
            }
        }

        // 2) RaycastAll
        var all = Physics.RaycastAll(ray, 1000f, clickMask, QueryTriggerInteraction.Collide);
        foreach (var h in all)
        {
            if (CliqueAceito(h.collider))
            {
                AbrirConformeLado();
                return;
            }
        }

        // 3) SphereCast pra porta fina
        if (Physics.SphereCast(ray, 0.12f, out var hs, 1000f, clickMask, QueryTriggerInteraction.Collide))
        {
            if (CliqueAceito(hs.collider))
            {
                AbrirConformeLado();
                return;
            }
        }

        if (logDebug) Debug.Log("[PortaSaidaTrigger] Nenhum collider aceitável sob o clique/toque.");
    }

    private void AbrirConformeLado()
    {
        porta.NotifyEnter(ultimoOutside);          // abre pro lado correto
        porta.SetPlayerDentroDaSala(!ultimoOutside); // oculta/mostra paredes conforme lado
        if (logDebug) Debug.Log($"[PortaSaidaTrigger] ABRIU por clique. LadoOutside={ultimoOutside}");
    }

    private bool CliqueAceito(Collider col)
    {
        if (col == null) return false;

        // Lista explícita tem prioridade
        if (collidersValidos != null && collidersValidos.Length > 0)
        {
            foreach (var c in collidersValidos) if (c == col) return true;
            return false;
        }

        // Se aceitar filhos da porta, vale qualquer collider dentro da hierarquia da porta
        if (aceitarCliqueEmFilhosDaPorta && porta != null)
        {
            if (col.transform == porta.transform || col.transform.IsChildOf(porta.transform))
                return true;
        }

        // Fallback: aceita clique no próprio trigger ou filhos dele
        return (col.transform == transform || col.transform.IsChildOf(transform));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagPlayer)) return;

        playerNoVao = true;
        ultimoOutside = IsOutside(other.transform.position, out _);

        // Liga a animação enquanto está na saída
        SetNaSaida(true);

        if (logDebug) Debug.Log($"[PortaSaidaTrigger] Player entrou no vão. Outside={ultimoOutside}");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(tagPlayer)) return;
        ultimoOutside = IsOutside(other.transform.position, out _);

        // Garante que permaneça true enquanto dentro (se algum outro sistema desligar)
        if (!playerNoVao) { playerNoVao = true; SetNaSaida(true); }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagPlayer)) return;

        playerNoVao = false;
        bool isOutsideExit = IsOutside(other.transform.position, out _);

        // Desliga a animação ao sair
        SetNaSaida(false);

        // Mostrar/ocultar conforme lado de saída + fechar
        porta.SetPlayerDentroDaSala(!isOutsideExit);
        porta.NotifyExit();
        porta.ForcarSalaVisivel();


        OnQualquerPassagem?.Invoke();
        if (!isOutsideExit) OnPassagemParaDentro?.Invoke();
        else                OnPassagemParaFora?.Invoke();

        if (logDebug) Debug.Log($"[PortaSaidaTrigger] Player saiu. Outside={isOutsideExit}");
    }

    private bool IsOutside(Vector3 posWorld, out float signed)
    {
        Vector3 n, M;
        if (modo == ModoLado.Markers && pontoFora && pontoDentro)
        {
            n = (pontoFora.position - pontoDentro.position).normalized;
            M = (pontoFora.position + pontoDentro.position) * 0.5f;
            signed = Vector3.Dot(n, posWorld - M);
            return signed > 0f; // >0 FORA, <0 DENTRO
        }
        else
        {
            Vector3 axis = (foraAxis == AxisLocal.X ? Vector3.right :
                           (foraAxis == AxisLocal.Y ? Vector3.up : Vector3.forward));
            if (inverterAxis) axis = -axis;
            Vector3 localPos = transform.InverseTransformPoint(posWorld);
            n = axis.normalized; M = Vector3.zero;
            signed = Vector3.Dot(n, localPos - M);
            return signed > 0f;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0,1,1,0.9f);
        if (TryGetComponent<BoxCollider>(out var b))
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        if (modo == ModoLado.Markers && pontoFora && pontoDentro)
        {
            Vector3 pf = pontoFora.position;
            Vector3 pd = pontoDentro.position;
            Vector3 n  = (pf - pd).normalized;
            Vector3 M  = (pf + pd) * 0.5f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pd, pf);
            Gizmos.DrawSphere(pd, 0.05f); // dentro
            Gizmos.DrawSphere(pf, 0.05f); // fora

            Gizmos.color = new Color(1,0.6f,0,0.6f);
            Gizmos.DrawLine(M - n * confirmInsideDepth, M - n * (confirmInsideDepth + 0.2f));
        }
    }
#endif
}
