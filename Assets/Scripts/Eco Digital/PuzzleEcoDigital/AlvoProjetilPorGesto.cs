using UnityEngine;

public class AlvoProjetilPorGesto : MonoBehaviour
{
    [Header("Requisito")]
    [Tooltip("Gesto necessário para destruir este projétil.")]
    public TipoGesto gestoNecessario = TipoGesto.Circulo;

    [Header("Feedback/Destruição")]
    [Tooltip("Efeito ao destruir (opcional).")]
    public GameObject vfxAoDestruir;
    [Tooltip("Destruir imediatamente ao reconhecer gesto?")]
    public bool destruirInstantaneo = true;

    private void OnEnable()
    {
        if (GestorGestos.I != null)
        {
            GestorGestos.I.RegistrarAlvo(this);
            GestorGestos.I.OnGestoReconhecido += OnGesto;
        }
    }

    private void OnDisable()
    {
        if (GestorGestos.I != null)
        {
            GestorGestos.I.OnGestoReconhecido -= OnGesto;
            GestorGestos.I.RemoverAlvo(this);
        }
    }

    private void OnGesto(TipoGesto g)
    {
        if (g == gestoNecessario && destruirInstantaneo)
            Destruir();
    }

    public void Destruir()
    {
        if (vfxAoDestruir != null)
            Instantiate(vfxAoDestruir, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
