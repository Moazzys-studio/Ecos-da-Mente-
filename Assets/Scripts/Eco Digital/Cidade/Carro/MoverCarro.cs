using UnityEngine;

public class CarroFluxo : MonoBehaviour
{
    public enum MaoDirecao
    {
        Baixo,
        Cima
    }

    [Header("Percurso (em ordem)")]
    [Tooltip("Pontos que o carro vai seguir na sequência. Ex: Curva -> Saída.")]
    public Transform[] pontosRota;

    [Header("Direção da mão (define rotações fixas)")]
    public MaoDirecao mao = MaoDirecao.Baixo;

    [Header("Movimento")]
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

    [Header("Rotação na curva")]
    [Tooltip("Velocidade de interpolação da rotação na curva.")]
    public float velocidadeRotacaoCurva = 4f;

    float velocidadeAtual;
    float velocidadeAlvo;
    float tempoTrocaVelocidadeAtual;

    int indiceDestinoAtual = 0;

    // Interpolação de rotação na curva
    bool virandoNaCurva = false;
    Quaternion rotacaoAlvoCurva;

    // Exposto para o script das rodas
    public float VelocidadeAtual => velocidadeAtual;
    public int SentidoRodas => sentidoRodas;

    void Start()
    {
        velocidadeAlvo  = Random.Range(velocidadeMin, velocidadeMax);
        velocidadeAtual = velocidadeAlvo;
        DefinirProximaTrocaVelocidade();

        // Rotação inicial EXATA por mão (como você pediu)
        switch (mao)
        {
            case MaoDirecao.Baixo:
                transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                break;
            case MaoDirecao.Cima:
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                break;
        }

        rotacaoArvoCurvaInicial();
    }

    void Update()
    {
        if (pontosRota == null || pontosRota.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        AtualizarVelocidadeOrganica();
        AjustarPorCarroDaFrente();
        MoverCarro();
        AtualizarRotacaoCurvaSuave();
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
        if (indiceDestinoAtual >= pontosRota.Length)
        {
            Destroy(gameObject);
            return;
        }

        Transform alvo = pontosRota[indiceDestinoAtual];
        if (alvo == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 direcao = alvo.position - transform.position;
        float distanciaFrame = velocidadeAtual * Time.deltaTime;

        // Chegou no ponto atual?
        if (direcao.magnitude <= distanciaFrame)
        {
            indiceDestinoAtual++;

            // se acabou a rota, destrói
            if (indiceDestinoAtual >= pontosRota.Length)
            {
                Destroy(gameObject);
                return;
            }

            // ao sair do primeiro ponto (curva) e ir pro segundo (saída), inicia rotação suave
            AplicarRotacaoDeCurva();
            return;
        }

        // Move sem mexer em rotação aqui
        transform.position += direcao.normalized * distanciaFrame;
    }

    void AplicarRotacaoDeCurva()
    {
        // Você pediu:
        // - Mão de baixo: na curva, rota até -180
        // - Mão de cima:  na curva, rota até  90
        // Rota [Curva, Saída] => quando indiceDestinoAtual == 1, ele acabou de sair da Curva indo pra Saída.
        if (indiceDestinoAtual != 1) return;

        switch (mao)
        {
            case MaoDirecao.Baixo:
                rotacaoAlvoCurva = Quaternion.Euler(0f, -180f, 0f);
                break;

            case MaoDirecao.Cima:
                rotacaoAlvoCurva = Quaternion.Euler(0f, 90f, 0f);
                break;
        }

        virandoNaCurva = true;
    }

    void AtualizarRotacaoCurvaSuave()
    {
        if (!virandoNaCurva) return;

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            rotacaoAlvoCurva,
            velocidadeRotacaoCurva * Time.deltaTime
        );

        // Quando já estiver praticamente alinhado, fixa e encerra interpolação
        if (Quaternion.Angle(transform.rotation, rotacaoAlvoCurva) < 0.5f)
        {
            transform.rotation = rotacaoAlvoCurva;
            virandoNaCurva = false;
        }
    }

    void rotacaoArvoCurvaInicial()
    {
        // valor inicial só pra ter algo consistente, mas quem manda mesmo é AplicarRotacaoDeCurva
        rotacaoAlvoCurva = transform.rotation;
        virandoNaCurva = false;
    }
}
