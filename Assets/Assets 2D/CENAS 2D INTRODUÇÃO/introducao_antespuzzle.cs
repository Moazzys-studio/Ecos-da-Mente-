using System.Collections;
using UnityEngine;
using TMPro;

public class introducao_antespuzzle : MonoBehaviour
{
    [Header("Imagens")]
    public GameObject imagem1;
    public GameObject imagem2;
    public GameObject imagem3;
    public GameObject imagem4;

    [Header("Canvas da introdução")]
    public GameObject canvasIntroducao;

    [Header("Texto da Narrativa")]
    public TMP_Text textoNarracao;

    [Header("Configurações")]
    public float tempoEntreImagens = 1f;
    public float velocidadeFadeTexto = 2f;

    void Start()
    {
        // TRAVA O JOGO ENQUANTO A INTRODUÇÃO ESTIVER ATIVA
        Time.timeScale = 0f;

        StartCoroutine(Sequencia());
    }

    IEnumerator EscreverTexto(string texto)
    {
        textoNarracao.text = "";

        foreach (char c in texto)
        {
            textoNarracao.text += c;

            //  VELOCIDADE DO TEXTO — pegando diretamente da variável global
            yield return new WaitForSecondsRealtime(variaveis_menu.velocidadeLetra);
        }
    }

    IEnumerator FadeOutTexto()
    {
        Color cor = textoNarracao.color;

        while (cor.a > 0)
        {
            cor.a -= Time.unscaledDeltaTime * velocidadeFadeTexto;
            textoNarracao.color = cor;
            yield return null;
        }
    }

    IEnumerator Sequencia()
    {
        imagem1.SetActive(false);
        imagem2.SetActive(false);
        imagem3.SetActive(false);
        imagem4.SetActive(false);

        textoNarracao.text = "";

        string textoUnico =
            "Eco tentava acreditar que estava tudo bem, mas as palavras começavam a pesar. Quanto mais tentava se acalmar, mais a mente acelerava. E mesmo cercado de gente, ele se sentia sozinho.";

        imagem1.SetActive(true);
        StartCoroutine(EscreverTexto(textoUnico));

        yield return new WaitForSecondsRealtime(tempoEntreImagens);
        imagem2.SetActive(true);

        yield return new WaitForSecondsRealtime(tempoEntreImagens);
        imagem3.SetActive(true);

        yield return new WaitForSecondsRealtime(tempoEntreImagens);
        imagem4.SetActive(true);

        yield return new WaitUntil(() => textoNarracao.text.Length >= textoUnico.Length);

        imagem1.SetActive(false);
        imagem2.SetActive(false);
        imagem3.SetActive(false);
        imagem4.SetActive(false);

        yield return StartCoroutine(FadeOutTexto());

        
        canvasIntroducao.SetActive(false);

       
    }
}
