using UnityEngine;

/// <summary>
/// Script genérico para NPC CONECTADO.
/// Coloque este script em cada collider do NPC:
/// - Tipo COLISAO  -> usa OnCollisionEnter (impacto instantâneo)
/// - Tipo AURA     -> usa OnTriggerEnter/Exit (dano por segundo enquanto dentro)
/// 
/// IMPORTANTE:
/// - Collider de COLISAO: isTrigger = false, precisa participar da física.
/// - Collider de AURA:    isTrigger = true, zona de volume.
/// - Eco precisa ter a tag configurada em tagEco.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GatilhoAnsiedadeNpcConectado : MonoBehaviour
{
    public enum TipoGatilho
    {
        Colisao,
        Aura
    }

    [Header("Configuração")]
    [Tooltip("Define se este collider é de COLISÃO ou de AURA.")]
    [SerializeField] private TipoGatilho tipo = TipoGatilho.Colisao;

    [Tooltip("Tag usada pelo Eco (player).")]
    [SerializeField] private string tagEco = "Eco";

    [Header("Referências")]
    [Tooltip("Gestor de gatilhos do Eco Digital. Se não for atribuído, será encontrado na cena.")]
    [SerializeField] private GestorGatilhosEcoDigital gestor;

    private void Awake()
    {
        if (gestor == null)
        {
            gestor = FindObjectOfType<GestorGatilhosEcoDigital>();
            if (gestor == null)
            {
                Debug.LogWarning("[GatilhoAnsiedadeNpcConectado] Nenhum GestorGatilhosEcoDigital encontrado na cena.");
            }
        }
    }

    // ---------- COLISÃO FÍSICA (tipo = Colisao) ----------

    private void OnCollisionEnter(Collision collision)
    {
        if (tipo != TipoGatilho.Colisao) 
            return;

        if (!collision.collider.CompareTag(tagEco))
            return;

        if (gestor != null)
        {
            gestor.RegistrarColisaoNpcConectado();
        }
    }

    // ---------- AURA (TRIGGER) (tipo = Aura) ----------

    private void OnTriggerEnter(Collider other)
    {
        if (tipo != TipoGatilho.Aura) 
            return;

        if (!other.CompareTag(tagEco))
            return;

        if (gestor != null)
        {
            gestor.EntrouAuraNpcConectado();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (tipo != TipoGatilho.Aura) 
            return;

        if (!other.CompareTag(tagEco))
            return;

        if (gestor != null)
        {
            gestor.SaiuAuraNpcConectado();
        }
    }
}
