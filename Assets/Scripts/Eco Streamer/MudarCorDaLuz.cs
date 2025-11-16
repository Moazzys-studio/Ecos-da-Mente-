using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MudarCorDaLuz : MonoBehaviour
{
    [Header("Luz a ser alterada")]
    public Light luz;  

    [Header("Cores da transição")]
    public Color corInicial = Color.white;
    public Color corFinal = Color.red;

    [Header("Configuração")]
    public float tempoTransicao = 3f;
    private float tempoAtual = 0f;
    private bool mudando = true;

    void Start()
    {
        if (luz != null)
        {
            luz.color = corInicial;    
        }
    }

    void Update()
    {
        if (!mudando || luz == null) return;

        tempoAtual += Time.deltaTime;
        float t = tempoAtual / tempoTransicao;

        // Faz a transição suave
        luz.color = Color.Lerp(corInicial, corFinal, t);

        // Para quando terminar
        if (t >= 1f)
        {
            mudando = false;
        }
    }

    public void ReiniciarTransicao()
    {
        tempoAtual = 0f;
        mudando = true;
    }
}
