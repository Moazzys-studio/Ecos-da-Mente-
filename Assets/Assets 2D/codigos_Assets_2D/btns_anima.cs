using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;
using TMPro;


public class btns_anima : MonoBehaviour
{
   public float velocidadeRotacao = 50f;   // Velocidade da rotação
    public float amplitude = 20f;           // Quanto o botão vai subir/descer
    public float velocidadeFlutuacao = 2f;  // Velocidade do sobe e desce
    public RectTransform imagemFundo;       // Arraste o "Background" do botão

    private RectTransform rectTransform;
    private Vector3 posicaoInicial;
    private float anguloAtual;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        posicaoInicial = rectTransform.anchoredPosition;
    }

    void Update()
    {
        if (imagemFundo != null)
        {
            // Gira apenas no eixo Z (sem distorcer)
            anguloAtual += velocidadeRotacao * Time.deltaTime;
            imagemFundo.localEulerAngles = new Vector3(0, 0, anguloAtual);
        }

        // Faz o botão inteiro subir e descer
        float movimentoY = Mathf.Sin(Time.time * velocidadeFlutuacao) * amplitude;
        rectTransform.anchoredPosition = posicaoInicial + new Vector3(0, movimentoY, 0);
    }
}
