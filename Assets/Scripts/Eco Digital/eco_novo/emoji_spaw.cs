using System.Collections;
using UnityEngine;

public class emoji_spaw : MonoBehaviour
{
    [Header("Configuração do Spawn")]
    public Transform[] spawnPoints;
    public GameObject[] inimigoPrefabs;

    [Header("Tempos")]
    public float delay = 2f;

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        // Spawn imediatamente ao iniciar a cena
        SpawnInimigo();

        // Continua spawnando a cada X segundos
        while (true)
        {
            yield return new WaitForSeconds(delay);
            SpawnInimigo();
        }
    }

    void SpawnInimigo()
    {
        if (spawnPoints.Length == 0 || inimigoPrefabs.Length == 0)
        {
            Debug.LogWarning("Não existem spawn points ou prefabs configurados.");
            return;
        }

        int indexSpawn = Random.Range(0, spawnPoints.Length);
        int indexPrefab = Random.Range(0, inimigoPrefabs.Length);

        Instantiate(
            inimigoPrefabs[indexPrefab],
            spawnPoints[indexSpawn].position,
            spawnPoints[indexSpawn].rotation
        );
    }
}