// PortaSaidaTrigger_NewInputSystem.cs
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem; // Novo Input System

[RequireComponent(typeof(Collider))]
public class PortaSaidaTrigger_NewInputSystem : MonoBehaviour
{
    public enum ModoLado { Markers, Axis }
    public enum AxisLocal { X, Y, Z }

    [Header("Modo de detecção")]
    [SerializeField] private ModoLado modo = ModoLado.Markers;

    [Header("Markers (recomendado)")]
    [SerializeField] private Transform pontoFora;
    [SerializeField] private Transform pontoDentro;

    [Header("Axis (alternativo)")]
    [SerializeField] private AxisLocal foraAxis = AxisLocal.Z;
    [SerializeField] private bool inverterAxis = false;

    [Header("Estabilidade")]
    [SerializeField, Min(0f)] private float confirmInsideDepth = 0.20f;

    [Header("Clique/Toque para abrir (Novo Input System)")]
    [Tooltip("Acione no Editor: Project Settings > Input System Package > Active Input Handling = Input System (ou Both).")]
    [SerializeField] private Camera cameraRay;
    [SerializeField] private LayerMask clickMask = ~0;
    [SerializeField] private Collider alvoCliqueOpcional;

    [Header("Player / Porta")]
    [SerializeField] private string tagPlayer = "Player";
    [SerializeField] private PortaController porta;

    [Header("Eventos de passagem")]
    public UnityEvent OnQualquerPassagem, OnPassagemParaDentro, OnPassagemParaFora;

    // Estado
    private bool playerNoVao = false;
    private bool ultimoOutside = true;

    // Ações de input
    private InputAction _pressAction;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void OnValidate()
    {
        if (!cameraRay) cameraRay = Camera.main;
        if (!porta) porta = GetComponentInParent<PortaController>();
    }

    private void OnEnable()
    {
        // Aceita mouse e toque: press
        _pressAction = new InputAction("Press",
            binding: "<Pointer>/press");
        _pressAction.AddBinding("<Touchscreen>/primaryTouch/press");

        _pressAction.started += OnPressStarted; // começou o clique/toque
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

    private void OnPressStarted(InputAction.CallbackContext ctx)
    {
        if (!playerNoVao || porta == null) return;
        if (cameraRay == null) cameraRay = Camera.main;
        if (cameraRay == null) return;

        Vector2 screenPos;
        if (Touchscreen.current?.primaryTouch.press.isPressed == true)
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null)
            screenPos = Mouse.current.position.ReadValue();
        else
            return;

        var ray = cameraRay.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 1000f, clickMask, QueryTriggerInteraction.Collide))
        {
            bool aceitouClique =
                (alvoCliqueOpcional == null)
                ? (hit.collider && (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)))
                : (hit.collider == alvoCliqueOpcional);

            if (aceitouClique)
            {
                porta.NotifyEnter(ultimoOutside);                 // abre pro lado correto
                porta.SetPlayerDentroDaSala(!ultimoOutside);      // oculta/mostra paredes conforme lado
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;
        playerNoVao = true;
        ultimoOutside = IsOutside(other.transform.position, out _);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(tagPlayer)) return;
        ultimoOutside = IsOutside(other.transform.position, out _);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;
        playerNoVao = false;

        bool isOutsideExit = IsOutside(other.transform.position, out _);
        porta.SetPlayerDentroDaSala(!isOutsideExit);
        porta.NotifyExit();

        OnQualquerPassagem?.Invoke();
        if (!isOutsideExit) OnPassagemParaDentro?.Invoke();
        else                OnPassagemParaFora?.Invoke();
    }

    private bool IsOutside(Vector3 posWorld, out float signed)
    {
        Vector3 n, M;
        if (modo == ModoLado.Markers && pontoFora && pontoDentro)
        {
            n = (pontoFora.position - pontoDentro.position).normalized;
            M = (pontoFora.position + pontoDentro.position) * 0.5f;
            signed = Vector3.Dot(n, posWorld - M);
            return signed > 0f;
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
}
