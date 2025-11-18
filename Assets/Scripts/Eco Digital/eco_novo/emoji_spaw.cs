using System.Collections;
using UnityEngine;

public class emoji_spaw : MonoBehaviour
{
    public Transform[] spawnPoints;        
    public GameObject[] inimigoPrefabs;    
    public float delay = 2f;               // tempo ENTRE spawns
    public float delayInicial = 2f;        // tempo ANTES de começar a spawnar

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        //AGUARDA ANTES DE COMEÇAR A GERAR INIMIGOS
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
