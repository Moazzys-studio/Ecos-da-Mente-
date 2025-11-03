using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PortaTrigger : MonoBehaviour
{
    public enum ModoLado { Markers, Axis }
    public enum AxisLocal { X, Y, Z }

    [Header("Modo de detecção")]
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
    [Tooltip("Profundidade mínima (m) além do plano para confirmar DENTRO.")]
    [SerializeField] private float confirmInsideDepth = 0.20f;
    [Tooltip("Travar DENTRO até sair do trigger.")]
    [SerializeField] private bool latchInsideUntilExit = true;

    [Header("Player / Porta")]
    [SerializeField] private string tagPlayer = "Player";
    [SerializeField] private PortaController porta;

    // Estado
    private bool insideLatched = false;   // quando true, não volta a FORA no Stay
    private bool ultimoOutside = true;    // último lado conhecido (true=fora, false=dentro)

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void OnValidate()
    {
        if (porta == null) porta = GetComponentInParent<PortaController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;

        bool isOutside = IsOutside(other.transform.position, out float signedDist);
        ultimoOutside = isOutside;

        // abre para o lado certo
        porta.NotifyEnter(isOutside);

        // feedback visual imediato: tocou no vão -> se veio de fora, já começa a esconder
        porta.SetPlayerDentroDaSala(!isOutside);

        // se já entrou o suficiente, trava "dentro"
        if (!isOutside && signedDist <= -confirmInsideDepth)
            insideLatched = true;

        // Debug.Log($"ENTER | outside={isOutside} | s={signedDist:0.000}");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;

        bool isOutside = IsOutside(other.transform.position, out float signedDist);

        if (latchInsideUntilExit)
        {
            // Só permite mudar para DENTRO; nunca volta para FORA enquanto estiver no trigger
            if (!insideLatched && !isOutside && signedDist <= -confirmInsideDepth)
            {
                insideLatched = true;
                porta.SetPlayerDentroDaSala(true); // garante invisível
            }
        }
        else
        {
            // Sem latch: ainda assim pedimos profundidade mínima para considerar DENTRO
            bool insideNow = (!isOutside && signedDist <= -confirmInsideDepth);
            porta.SetPlayerDentroDaSala(insideNow);
        }

        ultimoOutside = isOutside;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;

        // Recalcula de qual lado você está AO SAIR
        bool isOutsideExit = IsOutside(other.transform.position, out float signedDistExit);

        // **Regra nova**:
        // - Saiu para FORA  -> mostra (visível=true)
        // - Saiu para DENTRO -> mantém invisível (visível=false)
        porta.SetPlayerDentroDaSala(!isOutsideExit ? true : false); 
        // (equivalente: if (isOutsideExit) show; else hide)

        porta.NotifyExit();          // agenda fechamento etc.
        insideLatched = false;
        ultimoOutside = isOutsideExit;

        // Debug.Log($"EXIT | outsideExit={isOutsideExit} | sExit={signedDistExit:0.000}");
    }

    // -------- cálculo do lado e distância assinada ao plano --------
    private bool IsOutside(Vector3 posWorld, out float signed)
    {
        Vector3 n;     // normal apontando de DENTRO -> FORA
        Vector3 M;     // ponto no plano médio

        if (modo == ModoLado.Markers && pontoFora != null && pontoDentro != null)
        {
            n = (pontoFora.position - pontoDentro.position).normalized;
            M = (pontoFora.position + pontoDentro.position) * 0.5f;
            signed = Vector3.Dot(n, posWorld - M);
            return signed > 0f; // >0 FORA, <0 DENTRO
        }
        else
        {
            Vector3 axis = (foraAxis == AxisLocal.X ? Vector3.right
                         : (foraAxis == AxisLocal.Y ? Vector3.up : Vector3.forward));
            if (inverterAxis) axis = -axis;

            Vector3 localPos = transform.InverseTransformPoint(posWorld);
            n = axis.normalized;
            M = Vector3.zero;

            signed = Vector3.Dot(n, localPos - M);
            return signed > 0f;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0,1,1,0.9f);
        if (GetComponent<Collider>() is BoxCollider b)
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

            // faixa de confirmação de "dentro"
            Gizmos.color = new Color(1,0.6f,0,0.6f);
            Gizmos.DrawLine(M - n * confirmInsideDepth, M - n * (confirmInsideDepth + 0.2f));
        }
    }
#endif
}
