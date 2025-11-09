using UnityEngine;

// Requer um Collider (isTrigger) no mesmo GO da porta (o mesmo do PortaTrigger).
[RequireComponent(typeof(Collider))]
public class PortaTriggerNPC : MonoBehaviour
{
    public enum AxisLocal { X, Y, Z }

    [Header("NPC")]
    [SerializeField] private string tagNPC = "NPC";

    [Header("Markers (recomendado, iguais ao PortaTrigger)")]
    [Tooltip("Empty FORA do vão (world).")]
    [SerializeField] private Transform pontoFora;
    [Tooltip("Empty DENTRO do vão (world).")]
    [SerializeField] private Transform pontoDentro;

    [Header("Profundidade para confirmar DENTRO (mesma ideia do PortaTrigger)")]
    [SerializeField] private float confirmInsideDepth = 0.20f;

    [Header("Integração Porta")]
    [SerializeField] private PortaController porta; // arraste a mesma porta do PortaTrigger

    [Header("Entrada Suave")]
    [Tooltip("Pausa o NPC ao tocar no trigger antes de cruzar (s).")]
    [SerializeField] private float pausaAntesDeEntrar = 2.0f;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
        if (!porta) porta = GetComponentInParent<PortaController>();
    }

    private void OnValidate()
    {
        if (!porta) porta = GetComponentInParent<PortaController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagNPC) || porta == null) return;

        // Determina se veio de FORA
        bool isOutside = IsOutside(other.transform.position, out float signed);
        porta.NotifyEnter(isOutside);                 // abre para o lado correto
        porta.SetPlayerDentroDaSala(!isOutside);     // feedback imediato de (in)visibilidade

        // Pede ao NPC para PAUSAR 2s antes de de fato cruzar (evita empurrão)
        var npcAI = other.GetComponentInParent<NPCSalaAI>();
        if (npcAI) npcAI.PausarAntesDeEntrarSala(pausaAntesDeEntrar);
        else
        {
            // fallback: tenta parar o NavMeshAgent genérico
            var ag = other.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
            if (ag) StartCoroutine(CoPausaAgent(ag, pausaAntesDeEntrar));
        }
    }

    private System.Collections.IEnumerator CoPausaAgent(UnityEngine.AI.NavMeshAgent ag, float s)
    {
        if (!ag) yield break;
        bool prev = ag.isStopped;
        ag.isStopped = true;
        yield return new WaitForSeconds(s);
        ag.isStopped = prev;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagNPC) || porta == null) return;

        bool isOutsideExit = IsOutside(other.transform.position, out float signedExit);

        // Saiu para FORA -> mostra; Saiu para DENTRO -> mantém invisível
        porta.SetPlayerDentroDaSala(!isOutsideExit);

        porta.NotifyExit();
    }

    private bool IsOutside(Vector3 posWorld, out float signed)
    {
        if (pontoFora != null && pontoDentro != null)
        {
            Vector3 n = (pontoFora.position - pontoDentro.position).normalized; // de dentro->fora
            Vector3 M = (pontoFora.position + pontoDentro.position) * 0.5f;     // plano
            signed = Vector3.Dot(n, posWorld - M);
            return signed > 0f; // >0 FORA, <0 DENTRO
        }
        // Se markers faltarem, assume sempre FORA como fallback
        signed = 1f;
        return true;
    }
}
