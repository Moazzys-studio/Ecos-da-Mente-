using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CameraIgnoreWallOnEnter : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Objetos de parede que devem ser ignorados (sumir) quando o player estiver dentro da sala.")]
    [SerializeField] private GameObject[] paredesAlvo;

    [Tooltip("Tag do objeto que dispara (ex.: Player)")]
    [SerializeField] private string tagDisparador = "Player";

    [Header("Layers")]
    [Tooltip("Nome da layer que NÃO é renderizada pela Main Camera (desmarcada no Culling Mask).")]
    [SerializeField] private string layerQuandoDentro = "HiddenInsideRoom";

    [Tooltip("Se verdadeiro, troca a layer de TODOS os filhos (recursivo).")]
    [SerializeField] private bool aplicarRecursivo = true;

    // Guarda a layer original de cada objeto para restaurar
    private readonly Dictionary<GameObject, int> _layersOriginais = new();

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void Awake()
    {
        // Memoriza layers originais
        _layersOriginais.Clear();
        foreach (var go in paredesAlvo)
        {
            if (go == null) continue;
            if (!_layersOriginais.ContainsKey(go))
                _layersOriginais.Add(go, go.layer);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagDisparador)) return;
        TrocarParaLayer(paredesAlvo, layerQuandoDentro, aplicarRecursivo);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagDisparador)) return;
        RestaurarLayersOriginais();
    }

    private void RestaurarLayersOriginais()
    {
        foreach (var kvp in _layersOriginais)
        {
            if (kvp.Key == null) continue;
            SetLayer(kvp.Key, kvp.Value, aplicarRecursivo);
        }
    }

    private void TrocarParaLayer(GameObject[] alvos, string layerNome, bool recursivo)
    {
        int layer = LayerMask.NameToLayer(layerNome);
        if (layer < 0)
        {
            Debug.LogError($"Layer '{layerNome}' não existe. Crie a layer nas Tags & Layers.");
            return;
        }

        foreach (var go in alvos)
        {
            if (go == null) continue;
            SetLayer(go, layer, recursivo);
        }
    }

    private void SetLayer(GameObject go, int layer, bool recursivo)
    {
        if (!recursivo)
        {
            go.layer = layer;
            return;
        }

        var stack = new Stack<Transform>();
        stack.Push(go.transform);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
        }
    }
}
