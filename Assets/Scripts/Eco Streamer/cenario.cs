using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cenario : MonoBehaviour
{
   [Header("Cenário")]
    public GameObject[] groundPrefabs;     // prefabs do chão
    public float moveSpeed = 5f;           // velocidade do chão
    public int initialBlocks = 6;          // blocos iniciais na tela
    public float despawnOffset = -15f;     // ponto onde o chão é destruído
    public float spawnOffset = 15f;        // ponto onde o chão começa a aparecer

    [Header("Itens e Obstáculos (opcional)")]
    public GameObject[] itemPrefabs;       // prefabs de itens ou obstáculos
    [Range(0f, 1f)] public float itemSpawnChance = 0.3f; // chance de spawn por bloco
    public Vector2 itemHeightRange = new Vector2(1f, 3f); // altura aleatória dos itens

    private List<GameObject> spawnedBlocks = new List<GameObject>();
    private float nextSpawnX = 0f;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        for (int i = 0; i < initialBlocks; i++)
            SpawnBlock();
    }

    void Update()
    {
        MoveBlocks();
        CheckForRecycle();
    }

    void SpawnBlock()
    {
        GameObject prefab = groundPrefabs[Random.Range(0, groundPrefabs.Length)];
        GameObject block = Instantiate(prefab);

        // --- Pai com collider de Raycast (isTrigger) ---
        Collider parentCol = block.GetComponent<Collider>();
        if (parentCol == null)
            parentCol = block.AddComponent<BoxCollider>();
        parentCol.isTrigger = true; // não empurra o player
        parentCol.gameObject.layer = LayerMask.NameToLayer("Ground"); // Raycast detecta

        // --- Filho Empurrador com collider físico ---
        Transform pushChild = block.transform.Find("Empurrador");
        if (pushChild != null)
        {
            Collider childCol = pushChild.GetComponent<Collider>();
            if (childCol == null)
                childCol = pushChild.gameObject.AddComponent<BoxCollider>();

            childCol.isTrigger = false; // esse é o que empurra
            childCol.gameObject.layer = LayerMask.NameToLayer("Ground");
        }

        // Pega o tamanho do chão pelo Renderer (do filho ou do pai)
        Renderer rend = block.GetComponentInChildren<Renderer>();
        float width = rend.bounds.size.x;
        float yPos = prefab.transform.position.y;

        // Posiciona o bloco na sequência
        block.transform.position = new Vector3(nextSpawnX + width / 2f, yPos, 0f);
        nextSpawnX += width;

        spawnedBlocks.Add(block);

        // Spawn de item opcional
        TrySpawnItem(block, width, yPos);
    }

    void MoveBlocks()
    {
        foreach (GameObject block in spawnedBlocks)
        {
            if (block == null) continue;

            // Move o bloco inteiro
            block.transform.position += Vector3.left * moveSpeed * Time.deltaTime;

            // --- Empurrar apenas a partir do filho ---
            Transform pushChild = block.transform.Find("Empurrador");
            if (pushChild == null) continue;

            Collider childCol = pushChild.GetComponent<Collider>();
            if (childCol == null) continue;

            Vector3 center = childCol.bounds.center;
            Vector3 half = childCol.bounds.extents;

            Collider[] hits = Physics.OverlapBox(center, half);

            foreach (var hit in hits)
            {
                Rigidbody rb = hit.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.MovePosition(rb.position + Vector3.left * moveSpeed * Time.deltaTime);
                }
            }
        }
    }

    void CheckForRecycle()
    {
        if (spawnedBlocks.Count == 0) return;

        GameObject first = spawnedBlocks[0];

        // Verifica se o bloco saiu da tela
        if (first.transform.position.x < despawnOffset)
        {
            Destroy(first);
            spawnedBlocks.RemoveAt(0);
            SpawnBlock();
        }
    }

    void TrySpawnItem(GameObject block, float width, float groundY)
    {
        if (itemPrefabs.Length == 0 || Random.value > itemSpawnChance)
            return;

        GameObject itemPrefab = itemPrefabs[Random.Range(0, itemPrefabs.Length)];

        Vector3 spawnPos = new Vector3(
            block.transform.position.x,
            groundY + Random.Range(itemHeightRange.x, itemHeightRange.y),
            0f
        );

        Instantiate(itemPrefab, spawnPos, Quaternion.identity);
    }

    void OnDrawGizmosSelected()
    {
        // Apenas pra visualizar o limite de destruição
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(despawnOffset, -10f, 0), new Vector3(despawnOffset, 10f, 0));
    }
}
