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

    [Header("Efeito visual")]
    [Tooltip("Particle System filho deste objeto, ativado quando o Eco entra no trigger.")]
    [SerializeField] private ParticleSystem auraMesmerize;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (gestor == null)
            gestor = FindObjectOfType<GestorGatilhosEcoDigital>();

        // Se não for arrastado no Inspector, tenta achar um PS no filho
        if (auraMesmerize == null)
            auraMesmerize = GetComponentInChildren<ParticleSystem>(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagEco)) return;

        var eco = other.GetComponent<EcoDigitalController>();
        if (eco != null && frenteDoOutdoor != null)
            eco.AtivarMesmerize(frenteDoOutdoor);

        gestor?.EntrouZonaOutdoor();

        // Ativa o Particle System
        if (auraMesmerize != null)
        {
            auraMesmerize.gameObject.SetActive(true);
            auraMesmerize.Play(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagEco)) return;

        var eco = other.GetComponent<EcoDigitalController>();
        if (eco != null)
            eco.DesativarMesmerize();

        gestor?.SaiuZonaOutdoor();

        // Desativa o Particle System
        if (auraMesmerize != null)
        {
            auraMesmerize.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            auraMesmerize.gameObject.SetActive(false);
        }
    }
}
