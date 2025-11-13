using UnityEngine;

public class RodasCarro : MonoBehaviour
{
    [Tooltip("CarroFluxo que controla o movimento deste carro.")]
    public CarroFluxo carro;

    [Tooltip("Graus de rotação por unidade de velocidade.")]
    public float fatorGiroRodas = 80f;

    [Tooltip("Inverte o giro local desta roda (caso o modelo esteja espelhado).")]
    public bool inverterLocal = false;

    Vector3 eulerInicial;
    float anguloX; // acumulado só no X

    void Start()
    {
        eulerInicial = transform.localEulerAngles;
    }

    void Update()
    {
        if (carro == null) return;

        float vel = carro.VelocidadeAtual;
        int sentidoGlobal = carro.SentidoRodas;    // +1 ou -1 definido pelo FluxoCarros
        float sentidoLocal = inverterLocal ? -1f : 1f;

        float sentidoFinal = sentidoGlobal * sentidoLocal;

        anguloX += vel * fatorGiroRodas * sentidoFinal * Time.deltaTime;

        float x = eulerInicial.x + anguloX;
        transform.localEulerAngles = new Vector3(x, eulerInicial.y, eulerInicial.z);
    }
}
