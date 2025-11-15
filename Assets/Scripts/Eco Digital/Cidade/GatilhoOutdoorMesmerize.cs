using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GatilhoOutdoorMesmerize : MonoBehaviour
{
    [Tooltip("Gestor de gatilhos (ansiedade).")]
    [SerializeField] private GestorGatilhosEcoDigital gestor;

    [Tooltip("Tag do Eco.")]
    [SerializeField] private string tagEco = "Player";

    [Tooltip("Ponto na frente do outdoor para onde o Eco deve olhar.")]
    [SerializeField] private Transform frenteDoOutdoor;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (gestor == null)
            gestor = FindObjectOfType<GestorGatilhosEcoDigital>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagEco)) return;

        var eco = other.GetComponent<EcoDigitalController>();
        if (eco != null && frenteDoOutdoor != null)
            eco.AtivarMesmerize(frenteDoOutdoor);

        gestor?.EntrouZonaOutdoor();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagEco)) return;

        var eco = other.GetComponent<EcoDigitalController>();
        if (eco != null)
            eco.DesativarMesmerize();

        gestor?.SaiuZonaOutdoor();
    }
}
