using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PortaTrigger : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private PortaController porta;

    [Tooltip("Ponto que representa o LADO DE FORA do vão (Empty posicionado fora).")]
    [SerializeField] private Transform pontoFora;

    [Tooltip("Ponto que representa o LADO DE DENTRO do vão (Empty posicionado dentro).")]
    [SerializeField] private Transform pontoDentro;

    [Header("Histerese / Estabilidade")]
    [Tooltip("Largura da zona neutra (m) em torno do plano médio (evita flip no meio do vão).")]
    [SerializeField] private float zonaNeutra = 0.06f;

    [Header("Player / Física")]
    [Tooltip("Tag do player. Para OnTrigger funcionar, o Player (ou este trigger) precisa ter Rigidbody.")]
    [SerializeField] private string tagPlayer = "Player";

    // Estado atual do lado (null = indefinido)
    private bool? foraAtual = null;

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

        bool isFora = EstaDoLadoDeFora(other.transform.position, usarHisterese:false, out float s);
        foraAtual = isFora;

        // << NOVO >> trava direção no controller
        porta.NotifyEnter(isFora);

        // Atualiza estado de “dentro” (para sumir/voltar paredes)
        porta.SetPlayerDentroDaSala(!isFora);

        Debug.Log($"[PortaTrigger:{name}] ENTER {other.name} | outside={isFora} | s={s:0.000}");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;

        bool isFora = EstaDoLadoDeFora(other.transform.position, usarHisterese:true, out float s);

        if (!foraAtual.HasValue || isFora != foraAtual.Value)
        {
            foraAtual = isFora;
            // Mesmo se flipar no meio, o controller só troca direção se destravar (ninguém no vão)
            porta.SetPlayerDentroDaSala(!isFora);
            Debug.Log($"[PortaTrigger:{name}] STAY flip -> outside={isFora} | s={s:0.000}");
        }

        // Não chamamos mais “abrir de novo” aqui — o controller mantém aberta pela direção travada.
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagPlayer) || porta == null) return;

        foraAtual = null;
        porta.NotifyExit();

        Debug.Log($"[PortaTrigger:{name}] EXIT {other.name}");
    }

    /// <summary>
    /// Retorna true se posMundo está do lado de FORA.
    /// Plano médio em M = (fora+dentro)/2, normal n = (fora - dentro).normalized
    /// s = dot(n, pos - M). s > 0 => FORA; s < 0 => DENTRO.
    /// Com histerese: mantém estado até ultrapassar ±zonaNeutra.
    /// </summary>
    private bool EstaDoLadoDeFora(Vector3 posMundo, bool usarHisterese, out float s)
    {
        if (pontoFora == null || pontoDentro == null)
        {
            // Fallback simples se não configurar os empties
            Vector3 p0 = transform.position;
            Vector3 nFallback = transform.forward.normalized;
            s = Vector3.Dot(nFallback, posMundo - p0);
            bool foraSemHisterese = s > 0f;

            if (usarHisterese && foraAtual.HasValue)
            {
                if (foraAtual.Value) s = Mathf.Max(s, +0.001f); // estava FORA
                else                 s = Mathf.Min(s, -0.001f); // estava DENTRO
                foraSemHisterese = s > 0f;
            }
            return foraSemHisterese;
        }

        Vector3 pf = pontoFora.position;
        Vector3 pd = pontoDentro.position;

        Vector3 n = (pf - pd).normalized; // normal: de dentro -> fora
        Vector3 M = (pf + pd) * 0.5f;     // plano médio

        s = Vector3.Dot(n, posMundo - M);

        if (usarHisterese && foraAtual.HasValue)
        {
            if (foraAtual.Value) // estava FORA
            {
                if (s > -zonaNeutra) s = +0.001f;
            }
            else // estava DENTRO
            {
                if (s < +zonaNeutra) s = -0.001f;
            }
        }

        return s > 0f; // >0 => FORA
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Volume do trigger
        if (GetComponent<Collider>() is BoxCollider b)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        // Linha fora-dentro + plano médio e zona neutra
        if (pontoFora != null && pontoDentro != null)
        {
            Vector3 pf = pontoFora.position;
            Vector3 pd = pontoDentro.position;
            Vector3 nDir = (pf - pd).normalized;
            Vector3 M  = (pf + pd) * 0.5f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pd, pf);
            Gizmos.DrawSphere(pd, 0.04f); // dentro
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(pf, 0.04f); // fora

            Gizmos.color = new Color(0.2f, 1f, 1f, 0.7f);
            Gizmos.DrawLine(M - nDir * 0.4f, M + nDir * 0.4f);

            if (zonaNeutra > 0f)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
                Gizmos.DrawLine(M + nDir * zonaNeutra, M + nDir * (zonaNeutra + 0.2f));
                Gizmos.DrawLine(M - nDir * zonaNeutra, M - nDir * (zonaNeutra + 0.2f));
            }
        }
    }
#endif
}
