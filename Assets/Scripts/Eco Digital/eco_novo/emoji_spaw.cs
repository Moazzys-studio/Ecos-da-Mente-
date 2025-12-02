using System.Collections;
using UnityEngine;

public class emoji_spaw : MonoBehaviour
{
     [Header("Configuração do Spawn")]
    public Transform[] spawnPoints;
    public GameObject[] inimigoPrefabs;

    [Header("Tempos")]
    public float delay = 2f;         // tempo ENTRE spawns
    public float delayInicial = 2f;  // tempo ANTES de começar a spawnar

    // 🔥 Variável global que libera o spawn
    public static bool podeSpawnar = false;

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        // 🔥 Aguarda até que o controle_info ative a variável
        while (podeSpawnar == false)
        {
            yield return null; // espera 1 frame
        }

        // Quando liberar, espera o delay inicial
        yield return new WaitForSeconds(delayInicial);

        while (true)
        {
            SpawnInimigo();
            yield return new WaitForSeconds(delay);
        }
    }

    void SpawnInimigo()
    {
        int indexSpawn = Random.Range(0, spawnPoints.Length);
        int indexPrefab = Random.Range(0, inimigoPrefabs.Length);

        Instantiate(
            inimigoPrefabs[indexPrefab],
            spawnPoints[indexSpawn].position,
            spawnPoints[indexSpawn].rotation
        );
    }
}