using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cenario : MonoBehaviour
{
    [Header("Cenário")]
    public GameObject[] groundPrefabs;     // Prefabs do chão
    public float moveSpeed = 5f;           // Velocidade do cenário se movendo
    public int initialBlocks = 6;          // Quantos blocos aparecem no início
    public float despawnOffset = -15f;     // Posição onde o bloco é destruído

    [Header("Posição Inicial")]
    [Tooltip("Ajuste manualmente onde o cenário começa na tela (quanto mais negativo, mais à esquerda).")]
    public float startOffset = -8f;        // Posição X inicial configurável no Inspector

    [Header("Buracos (Desafios)")]
    [Range(0f, 1f)]
    public float holeChance; // chance de criar buraco
    public float holeMinSize = 4f;   // Tamanho mínimo do buraco
    public float holeMaxSize = 8f;   // Tamanho máximo do buraco

    private List<GameObject> spawnedBlocks = new List<GameObject>();
    private float nextSpawnX;

    void Start()
    {
        // Começa no valor configurado pelo usuário no Inspector
        nextSpawnX = startOffset;

        // Spawna os blocos iniciais
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
        // Evita buracos nos dois primeiros blocos
        if (spawnedBlocks.Count >= 2 && Random.value < holeChance)
        {
            float holeSize = Random.Range(holeMinSize, holeMaxSize);
            nextSpawnX += holeSize;
        }

        GameObject prefab;

        // Força as duas primeiras a serem sempre a prefab número 4 (índice 3)
        if (spawnedBlocks.Count < 2 && groundPrefabs.Length >= 4)
        {
            prefab = groundPrefabs[3];
        }
        else
        {
            prefab = groundPrefabs[Random.Range(0, groundPrefabs.Length)];
        }

        GameObject block = Instantiate(prefab);

        // Configura Rigidbody e Colliders
        Rigidbody rb = block.GetComponent<Rigidbody>();
        if (rb == null) rb = block.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Collider parentCol = block.GetComponent<Collider>();
        if (parentCol == null) parentCol = block.AddComponent<BoxCollider>();
        parentCol.isTrigger = true;

        Transform pushChild = block.transform.Find("Empurrador");
        if (pushChild != null)
        {
            Collider childCol = pushChild.GetComponent<Collider>();
            if (childCol == null)
                childCol = pushChild.gameObject.AddComponent<BoxCollider>();
            childCol.isTrigger = false;
        }

        Renderer rend = block.GetComponentInChildren<Renderer>();
        float width = rend != null ? rend.bounds.size.x : 10f;

        // 🔹 Agora a altura (Y) é baseada na posição do objeto que tem o script
        float yPos = transform.position.y;

        block.transform.position = new Vector3(nextSpawnX + width / 2f, yPos, 0f);
        nextSpawnX += width;

        spawnedBlocks.Add(block);
    }

    void MoveBlocks()
    {
        foreach (GameObject block in spawnedBlocks)
        {
            if (block == null) continue;

            // Move bloco para esquerda
            block.transform.position += Vector3.left * moveSpeed * Time.deltaTime;

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
        if (first == null) return;

        Renderer rend = first.GetComponentInChildren<Renderer>();
        float width = rend != null ? rend.bounds.size.x : 10f;

        // Se saiu da tela → remove e cria outro
        if (first.transform.position.x + width / 2f < despawnOffset)
        {
            Destroy(first);
            spawnedBlocks.RemoveAt(0);
            SpawnBlock();
        }
    }

    void OnDrawGizmosSelected()
    {
        // Linha vermelha mostra o ponto de remoção
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(despawnOffset, -10f, 0), new Vector3(despawnOffset, 10f, 0));

        // Linha azul mostra o início do spawn
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(startOffset, -10f, 0), new Vector3(startOffset, 10f, 0));

        // Linha verde mostra a altura (Y) onde o cenário será instanciado
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(-30f, transform.position.y, 0), new Vector3(30f, transform.position.y, 0));
    }
}