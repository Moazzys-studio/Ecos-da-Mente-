using System.Collections;
using UnityEngine;

public class emoji_spaw : MonoBehaviour
{
    public Transform[] spawnPoints;        // 4 pontos de spawn
    public GameObject[] inimigoPrefabs;    // 4 prefabs diferentes
    public float delay = 2f;               // tempo entre cada spawn

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnInimigo();
            yield return new WaitForSeconds(delay);
        }
    }

    void SpawnInimigo()
    {
        // escolhe spawn aleatório
        int indexSpawn = Random.Range(0, spawnPoints.Length);

        // escolhe prefab aleatório
        int indexPrefab = Random.Range(0, inimigoPrefabs.Length);

        // instancia
        Instantiate(
            inimigoPrefabs[indexPrefab],
            spawnPoints[indexSpawn].position,
            spawnPoints[indexSpawn].rotation
        );
    }
}
