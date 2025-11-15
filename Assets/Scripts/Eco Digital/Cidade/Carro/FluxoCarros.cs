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

    [Header("Curvas")]
    [Tooltip("Ponto de curva da mão de baixo (deve ficar na esquina).")]
    public Transform curvaBaixo;
    [Tooltip("Ponto de curva da mão de cima (deve ficar na outra esquina).")]
    public Transform curvaCima;

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
        Transform pontoCurva;
        Transform pontoFim;

        float velMin, velMax;
        CarroFluxo carroDaFrente;
        int sentidoRodas;
        CarroFluxo.MaoDirecao maoDirecao;

        if (spawnNaMaoDeBaixo)
        {
            pontoInicio   = inicioMaoDeBaixo;
            pontoCurva    = curvaBaixo;
            pontoFim      = fimMaoDeBaixo;
            velMin        = velocidadeMinBaixo;
            velMax        = velocidadeMaxBaixo;
            carroDaFrente = ultimoCarroBaixo;
            sentidoRodas  = -1; // mantém tuas rodas como estavam
            maoDirecao    = CarroFluxo.MaoDirecao.Baixo;

            if (ultimoCarroBaixo != null &&
                Vector3.Distance(ultimoCarroBaixo.transform.position, pontoInicio.position) < distanciaMinEntreCarros)
            {
                return;
            }
        }
        else
        {
            pontoInicio   = inicioMaoDeCima;
            pontoCurva    = curvaCima;
            pontoFim      = fimMaoDeCima;
            velMin        = velocidadeMinCima;
            velMax        = velocidadeMaxCima;
            carroDaFrente = ultimoCarroCima;
            sentidoRodas  = -1;
            maoDirecao    = CarroFluxo.MaoDirecao.Cima;

            if (ultimoCarroCima != null &&
                Vector3.Distance(ultimoCarroCima.transform.position, pontoInicio.position) < distanciaMinEntreCarros)
            {
                return;
            }
        }

        if (pontoInicio == null || pontoFim == null || pontoCurva == null)
            return;

        int index = Random.Range(0, prefabsCarros.Length);
        CarroFluxo prefab = prefabsCarros[index];
        if (prefab == null) return;

        // Rotação de spawn EXATA como você pediu
        Quaternion rot;
        if (spawnNaMaoDeBaixo)
        {
            // Mão de baixo: spawn em Y = -90
            rot = Quaternion.Euler(0f, -90f, 0f);
        }
        else
        {
            // Mão de cima: spawn em Y = 0
            rot = Quaternion.Euler(0f, 0f, 0f);
        }

        // Instancia o carro
        CarroFluxo carro = Instantiate(prefab, pontoInicio.position, rot);

        // Define a rota: primeiro vai pra curva, depois pro fim
        carro.pontosRota = new Transform[]
        {
            pontoCurva,
            pontoFim
        };

        // Configura movimentação
        carro.velocidadeMin              = velMin;
        carro.velocidadeMax              = velMax;
        carro.tempoTrocaVelMin           = tempoTrocaVelMin;
        carro.tempoTrocaVelMax           = tempoTrocaVelMax;
        carro.suavidadeMudancaVelocidade = suavidadeMudancaVel;

        // Direção, rodas, mão
        carro.sentidoRodas  = sentidoRodas;
        carro.mao           = maoDirecao;
        carro.carroDaFrente = carroDaFrente != null ? carroDaFrente.transform : null;

        if (spawnNaMaoDeBaixo)
            ultimoCarroBaixo = carro;
        else
            ultimoCarroCima  = carro;
    }
}
