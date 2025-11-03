using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PortaTrigger : MonoBehaviour
{
    public enum ModoLado { Markers, Axis }
    public enum AxisLocal { X, Y, Z }

    [Header("Modo de detecção")]
    [SerializeField] private ModoLado modo = ModoLado.Markers;

    [Header("Markers (recomendado)")]
    [Tooltip("Empty posicionado FORA do vão (world).")]
    [SerializeField] private Transform pontoFora;
    [Tooltip("Empty posicionado DENTRO do vão (world).")]
    [SerializeField] private Transform pontoDentro;

    [Header("Axis (alternativo)")]
    [Tooltip("Eixo local que aponta para FORA quando usado o modo Axis.")]
    [SerializeField] private AxisLocal foraAxis = AxisLocal.Z;
    [Tooltip("Se marcado, inverte o lado (FORA vira DENTRO).")]
    [SerializeField] private bool inverterAxis = false;

    [Header("Estabilidade")]
    [Tooltip("Largura da zona neutra em metros (evita flip no meio do vão).")]
    [SerializeField] private float zonaNeutra = 0.12f;
    [Tooltip("Tempo mínimo após trocar de lado sem permitir nova troca (s).")]
    [SerializeField] private float cooldownLado = 0.25f;

    [Header("Player / Física")]
    [SerializeField] private string tagPlayer = "Player";
    [Tooltip("Opcional: distância máxima do centro do trigger para considerar a histerese (0 = desliga).")]
    [SerializeField] private float raioHisterese = 0f;

    [Header("Referência da porta")]
    [SerializeField] private PortaController porta;

    // Estado
    private bool? foraAtual = null;
    private float podeTrocarApos = 0f;

    private Collider _col;

    private void Reset()
    {
        _col = GetComponent<Collider>();
        if (_col) _col.isTrigger = true;
    }

    private void Awake()
    {
        _col = GetComponent<Collider>();
    }

    private void OnValidate()
    {
        if (porta == null) porta = GetComponentInParent<PortaController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;

        bool isFora = CalcularFora(other.transform.position, usarHisterese:false, out float s);
        foraAtual = isFora;
        podeTrocarApos = Time.time + cooldownLado;

        porta.NotifyEnter(isFora);
        porta.SetPlayerDentroDaSala(!isFora);

        Debug.Log($"[PortaTrigger:{name}] ENTER {other.name} | outside={isFora} | s={s:0.000}");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;

        bool isFora = CalcularFora(other.transform.position, usarHisterese:true, out float s);

        // trava lado por um curto período após a última troca
        if (Time.time < podeTrocarApos && foraAtual.HasValue)
        {
            isFora = foraAtual.Value;
        }

        if (!foraAtual.HasValue || isFora != foraAtual.Value)
        {
            // só permite trocar se cooldown passou
            if (Time.time >= podeTrocarApos)
            {
                foraAtual = isFora;
                podeTrocarApos = Time.time + cooldownLado;
                porta.SetPlayerDentroDaSala(!isFora);
                Debug.Log($"[PortaTrigger:{name}] STAY flip -> outside={isFora} | s={s:0.000}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;

        porta.NotifyExit();
        foraAtual = null;

        Debug.Log($"[PortaTrigger:{name}] EXIT {other.name}");
    }

    // ---------- Cálculo do lado ----------
    private bool CalcularFora(Vector3 posWorld, bool usarHisterese, out float s)
    {
        if (modo == ModoLado.Markers && pontoFora != null && pontoDentro != null)
        {
            // Normal definida por markers -> independente de rotação do pai
            Vector3 pf = pontoFora.position;
            Vector3 pd = pontoDentro.position;
            Vector3 n  = (pf - pd).normalized;   // dentro -> fora
            Vector3 M  = (pf + pd) * 0.5f;       // plano médio

            s = Vector3.Dot(n, posWorld - M);    // >0 fora, <0 dentro
            return AplicarHisterese(s, usarHisterese);
        }
        else
        {
            // Axis local da moldura da porta
            Vector3 nLocal = Vector3.forward;
            if (foraAxis == AxisLocal.X) nLocal = Vector3.right;
            else if (foraAxis == AxisLocal.Y) nLocal = Vector3.up;

            if (inverterAxis) nLocal = -nLocal;

            // Converte posição para espaço local da porta
            Vector3 localPos = transform.InverseTransformPoint(posWorld);
            Vector3 n = nLocal.normalized;
            Vector3 M = Vector3.zero; // plano no centro local

            s = Vector3.Dot(n, localPos - M);    // >0 fora, <0 dentro (no espaço local)
            return AplicarHisterese(s, usarHisterese);
        }
    }

    private bool AplicarHisterese(float s, bool usarHisterese)
    {
        if (!usarHisterese || !foraAtual.HasValue)
            return s > 0f;

        // zona neutra ao redor do plano (em unidades do mesmo espaço do cálculo)
        if (foraAtual.Value)   // estava FORA
        {
            if (s < -zonaNeutra) return false; // cruza de vez para dentro
            else return true;                  // mantém FORA na zona neutra
        }
        else                    // estava DENTRO
        {
            if (s > +zonaNeutra) return true;  // cruza de vez para fora
            else return false;                 // mantém DENTRO na zona neutra
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        if (GetComponent<Collider>() is BoxCollider b)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        if (modo == ModoLado.Markers && pontoFora != null && pontoDentro != null)
        {
            Vector3 pf = pontoFora.position;
            Vector3 pd = pontoDentro.position;
            Vector3 n  = (pf - pd).normalized;
            Vector3 M  = (pf + pd) * 0.5f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pd, pf);
            Gizmos.DrawSphere(pd, 0.05f); // dentro
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(pf, 0.05f); // fora

            // zona neutra
            Gizmos.color = new Color(0.2f, 1f, 1f, 0.6f);
            Gizmos.DrawLine(M - n * 0.4f, M + n * 0.4f);
            Gizmos.DrawLine(M + n * zonaNeutra, M + n * (zonaNeutra + 0.2f));
            Gizmos.DrawLine(M - n * zonaNeutra, M - n * (zonaNeutra + 0.2f));
        }
        else
        {
            // Eixo local
            Vector3 n = (foraAxis == AxisLocal.X ? transform.right :
                        (foraAxis == AxisLocal.Y ? transform.up : transform.forward)) * (inverterAxis ? -1f : 1f);

            Vector3 M = transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(M - n * 0.5f, M + n * 0.5f);
            Gizmos.DrawWireSphere(M + n * zonaNeutra, 0.05f);
            Gizmos.DrawWireSphere(M - n * zonaNeutra, 0.05f);
        }
    }
#endif
}
