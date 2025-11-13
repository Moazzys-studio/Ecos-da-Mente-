using UnityEngine;

public class FluxoCarros : MonoBehaviour
{
    [Header("Prefabs de carros")]
    public CarroFluxo[] prefabsCarros;

    [Header("Pontos da rua")]
    public Transform inicioMaoDeCima;
    public Transform fimMaoDeCima;
    public Transform inicioMaoDeBaixo;
    public Transform fimMaoDeBaixo;

    [Header("Spawn / Tráfego")]
    public float intervaloMinSpawn = 1.5f;
    public float intervaloMaxSpawn = 4f;

    [Range(0f, 1f)]
    public float probabilidadeMaoDeBaixo = 0.5f;

    public float distanciaMinEntreCarros = 8f;

    [Header("Velocidades por mão")]
    public float velocidadeMinBaixo = 3f;
    public float velocidadeMaxBaixo = 6f;
    public float velocidadeMinCima  = 3f;
    public float velocidadeMaxCima  = 6f;

    [Header("Variação interna dos carros")]
    public float tempoTrocaVelMin = 2f;
    public float tempoTrocaVelMax = 5f;
    public float suavidadeMudancaVel = 2f;

    float proximoSpawn;

    CarroFluxo ultimoCarroBaixo;
    CarroFluxo ultimoCarroCima;

    void Start()
    {
        AgendarProximoSpawn();
    }

    void Update()
    {
        if (Time.time >= proximoSpawn)
        {
            TentarSpawnarCarro();
            AgendarProximoSpawn();
        }
    }

    void AgendarProximoSpawn()
    {
        float intervalo = Random.Range(intervaloMinSpawn, intervaloMaxSpawn);
        proximoSpawn = Time.time + Mathf.Max(0.2f, intervalo);
    }

    void TentarSpawnarCarro()
    {
        if (prefabsCarros == null || prefabsCarros.Length == 0) return;

        bool spawnNaMaoDeBaixo = Random.value < probabilidadeMaoDeBaixo;

        Transform pontoInicio;
        Transform pontoFim;
        float rotacaoY;
        float velMin, velMax;
        CarroFluxo carroDaFrente;
        int sentidoRodas; // +1 ou -1

        if (spawnNaMaoDeBaixo)
        {
            pontoInicio   = inicioMaoDeBaixo;
            pontoFim      = fimMaoDeBaixo;
            rotacaoY      = -90f;
            velMin        = velocidadeMinBaixo;
            velMax        = velocidadeMaxBaixo;
            carroDaFrente = ultimoCarroBaixo;
            sentidoRodas  = -1;   // sentido “normal” para essa mão

            if (ultimoCarroBaixo != null &&
                Vector3.Distance(ultimoCarroBaixo.transform.position, pontoInicio.position) < distanciaMinEntreCarros)
            {
                return;
            }
        }
        else
        {
            pontoInicio   = inicioMaoDeCima;
            pontoFim      = fimMaoDeCima;
            rotacaoY      = 90f;
            velMin        = velocidadeMinCima;
            velMax        = velocidadeMaxCima;
            carroDaFrente = ultimoCarroCima;
            sentidoRodas  = -1;  // sentido contrário pra outra mão

            if (ultimoCarroCima != null &&
                Vector3.Distance(ultimoCarroCima.transform.position, pontoInicio.position) < distanciaMinEntreCarros)
            {
                return;
            }
        }

        if (pontoInicio == null || pontoFim == null) return;

        int index = Random.Range(0, prefabsCarros.Length);
        CarroFluxo prefab = prefabsCarros[index];
        if (prefab == null) return;

        CarroFluxo carro =
            Instantiate(prefab, pontoInicio.position, Quaternion.Euler(0f, rotacaoY, 0f));

        carro.destino                    = pontoFim;
        carro.velocidadeMin              = velMin;
        carro.velocidadeMax              = velMax;
        carro.tempoTrocaVelMin           = tempoTrocaVelMin;
        carro.tempoTrocaVelMax           = tempoTrocaVelMax;
        carro.suavidadeMudancaVelocidade = suavidadeMudancaVel;
        carro.sentidoRodas               = sentidoRodas;
        carro.carroDaFrente              = carroDaFrente != null ? carroDaFrente.transform : null;

        if (spawnNaMaoDeBaixo)
            ultimoCarroBaixo = carro;
        else
            ultimoCarroCima  = carro;
    }
}
