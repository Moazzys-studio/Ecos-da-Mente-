using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AuraNpcConectado : MonoBehaviour
{
    [SerializeField] private GestorGatilhosEcoDigital gestor;
    [SerializeField] private string tagEco = "Eco";

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (gestor == null)
            gestor = FindObjectOfType<GestorGatilhosEcoDigital>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagEco)) return;
        if (gestor == null) return;

        gestor.EntrouAuraNpcConectado();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagEco)) return;
        if (gestor == null) return;

        gestor.SaiuAuraNpcConectado();
    }
}
