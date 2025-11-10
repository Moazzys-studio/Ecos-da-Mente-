using UnityEngine;

public class Relogio : MonoBehaviour
{
    public Transform ponteiroMinutos;
    public Transform ponteiroSegundos;

    [Header("Tempo")]
    public float multiplicadorTempo = 1f;   // 1 = tempo real
    public float minutosIniciais = 0f;      // ex.: 90 => 01:30

    public enum Eixo { X, Y, Z }
    public Eixo eixoMinutos = Eixo.X;       // <- escolha X aqui
    public Eixo eixoSegundos = Eixo.X;      // <- e aqui também
    [Tooltip("Correção em graus para deixar o ponteiro apontando para 12h.")]
    public float offsetMinutos = 0f;
    public float offsetSegundos = 0f;

    [Header("Sentido")]
    [Tooltip("Se o giro está invertido no modelo, marque para inverter o sentido.")]
    public bool inverterRotacao = false;    // inverte ambos

    float tempoAtual; // segundos simulados

    void Start()
    {
        tempoAtual = minutosIniciais * 60f;
    }

    void Update()
    {
        tempoAtual += Time.deltaTime * multiplicadorTempo;

        float minutos  = (tempoAtual / 60f) % 60f;   // 0..59.999
        float segundos = tempoAtual % 60f;           // 0..59.999

        // Cada minuto/segundo avança 6°
        float sinal = inverterRotacao ? +1f : -1f;   // padrão: horário -> negativo no eixo “de frente”
        float angMin = sinal * (minutos  * 6f) + offsetMinutos;
        float angSeg = sinal * (segundos * 6f) + offsetSegundos;

        if (ponteiroMinutos)  AplicarRotacao(ponteiroMinutos,  angMin, eixoMinutos);
        if (ponteiroSegundos) AplicarRotacao(ponteiroSegundos, angSeg, eixoSegundos);
    }

    void AplicarRotacao(Transform t, float angGraus, Eixo eixo)
    {
        var e = t.localEulerAngles;
        angGraus = Wrap360(angGraus);
        switch (eixo)
        {
            case Eixo.X: e.x = angGraus; break;
            case Eixo.Y: e.y = angGraus; break;
            case Eixo.Z: e.z = angGraus; break;
        }
        t.localEulerAngles = e;
    }

    float Wrap360(float a)
    {
        a %= 360f;
        if (a < 0f) a += 360f;
        return a;
    }
}
