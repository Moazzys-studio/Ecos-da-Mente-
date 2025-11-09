using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SalaPlana : MonoBehaviour
{
    public PortaController.TipoSala tipoSala = PortaController.TipoSala.Nenhuma;

    [Tooltip("Se vazio, usa o Renderer.bounds do objeto atual.")]
    public Renderer alvoBounds;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
        if (!alvoBounds) alvoBounds = GetComponent<Renderer>();
    }

    public bool TrySortearPontoDentro(out Vector3 ponto, float margem = 0.15f)
    {
        Bounds b;
        if (alvoBounds) b = alvoBounds.bounds;
        else
        {
            var r = GetComponent<Renderer>();
            if (!r) { ponto = transform.position; return false; }
            b = r.bounds;
        }

        // Sorteia dentro do retângulo do chão com pequena margem
        float x = Random.Range(b.min.x + margem, b.max.x - margem);
        float z = Random.Range(b.min.z + margem, b.max.z - margem);
        float y = b.center.y;

        // Ajusta para o NavMesh próximo
        if (UnityEngine.AI.NavMesh.SamplePosition(new Vector3(x, y, z), out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            ponto = hit.position;
            return true;
        }

        ponto = new Vector3(x, y, z);
        return false;
    }
}
