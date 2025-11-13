using UnityEngine;

public class CarroFluxo : MonoBehaviour
{
    [Header("Movimento")]
    public Transform destino;
    public float velocidadeMin = 3f;
    public float velocidadeMax = 7f;
    public float suavidadeMudancaVelocidade = 2f;
    public float tempoTrocaVelMin = 2f;
    public float tempoTrocaVelMax = 5f;

    [Header("Fluxo orgânico")]
    public Transform carroDaFrente;
    public float distanciaSeguimento = 8f;
    public float distanciaFreioForte = 4f;

    [Header("Direção das rodas")]
    [Tooltip("1 ou -1. Definido pelo FluxoCarros quando o carro é spawnado.")]
    public int sentidoRodas = 1;

    float velocidadeAtual;
    float velocidadeAlvo;
    float tempoTrocaVelocidadeAtual;

    // Exposto para o script das rodas
    public float VelocidadeAtual => velocidadeAtual;
    public int SentidoRodas => sentidoRodas;

    void Start()
    {
        velocidadeAlvo  = Random.Range(velocidadeMin, velocidadeMax);
        velocidadeAtual = velocidadeAlvo;
        DefinirProximaTrocaVelocidade();
    }

    void Update()
    {
        AtualizarVelocidadeOrganica();
        AjustarPorCarroDaFrente();
        MoverCarro();
    }

    void AtualizarVelocidadeOrganica()
    {
        tempoTrocaVelocidadeAtual -= Time.deltaTime;

        if (tempoTrocaVelocidadeAtual <= 0f)
        {
            velocidadeAlvo = Random.Range(velocidadeMin, velocidadeMax);
            DefinirProximaTrocaVelocidade();
        }

        velocidadeAtual = Mathf.Lerp(
            velocidadeAtual,
            velocidadeAlvo,
            suavidadeMudancaVelocidade * Time.deltaTime
        );
    }

    void DefinirProximaTrocaVelocidade()
    {
        tempoTrocaVelocidadeAtual = Random.Range(tempoTrocaVelMin, tempoTrocaVelMax);
    }

    void AjustarPorCarroDaFrente()
    {
        if (carroDaFrente == null) return;

        float dist = Vector3.Distance(transform.position, carroDaFrente.position);

        if (dist <= distanciaFreioForte)
        {
            velocidadeAlvo = 0f;
            return;
        }

        if (dist <= distanciaSeguimento)
        {
            float t = (dist - distanciaFreioForte) / (distanciaSeguimento - distanciaFreioForte);
            t = Mathf.Clamp01(t);

            float velSegura = Mathf.Lerp(0f, velocidadeMin, t);
            velocidadeAlvo = Mathf.Min(velocidadeAlvo, velSegura);
        }
    }

    void MoverCarro()
    {
        if (destino == null) return;

        Vector3 direcao = destino.position - transform.position;
        float distanciaFrame = velocidadeAtual * Time.deltaTime;

        if (direcao.magnitude <= distanciaFrame)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += direcao.normalized * distanciaFrame;
    }
}
