using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class elementos : MonoBehaviour
{
    public float velocidadeRotacao = 50f;   // Velocidade da rotação
    public float amplitude = 20f;           // Quanto a imagem vai subir/descer (em pixels)
    public float velocidadeFlutuacao = 2f;  // Velocidade do sobe e desce

    private RectTransform rectTransform;
    private Vector3 posicaoInicial;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        posicaoInicial = rectTransform.anchoredPosition;
    }

    void Update()
    {
        // Rotação contínua
        rectTransform.Rotate(Vector3.forward * velocidadeRotacao * Time.deltaTime);

        // Movimento suave de sobe e desce
        float movimentoY = Mathf.Sin(Time.time * velocidadeFlutuacao) * amplitude;
        rectTransform.anchoredPosition = posicaoInicial + new Vector3(0, movimentoY, 0);
    }
}
