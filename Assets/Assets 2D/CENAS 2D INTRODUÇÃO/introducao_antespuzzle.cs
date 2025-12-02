using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class introducao_antespuzzle : MonoBehaviour
{
    [Header("Imagens")]
    public GameObject imagem1;
    public GameObject imagem2;
    public GameObject imagem3;
    public GameObject imagem4;

    [Header("Canvas da introdução")]
    public GameObject canvasIntroducao;
    public GameObject Canvainfo;

    [Header("Texto da Narrativa")]
    public TMP_Text textoNarracao;

    [Header("Configurações")]
    public float tempoEntreImagens = 1f;
    public float velocidadeFadeTexto = 2f;

    // ---- NOVO: botão para acelerar a introdução ----
    private bool acelerar = false;

    // Chamado pelo BTN no Unity
    public void AcelerarIntro()
    {
        Debug.Log(acelerar);
        acelerar = true;
    }
    // ------------------------------------------------

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
            // Se acelerar, pula tudo e escreve direto
            if (acelerar)
            {
                textoNarracao.text = texto;
                yield break;
            }

            textoNarracao.text += c;

            yield return new WaitForSecondsRealtime(variaveis_menu.velocidadeLetra);
        }
    }

    IEnumerator FadeOutTexto()
    {
        Color cor = textoNarracao.color;

        while (cor.a > 0)
        {
            // Se acelerar, some instantaneamente
            if (acelerar)
            {
                cor.a = 0;
                textoNarracao.color = cor;
                yield break;
            }

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

        // ---- PRIMEIRA IMAGEM E TEXTO ----
        imagem1.SetActive(true);
        StartCoroutine(EscreverTexto(textoUnico));

        if (!acelerar)
            yield return new WaitForSecondsRealtime(tempoEntreImagens);

        imagem2.SetActive(true);

        if (!acelerar)
            yield return new WaitForSecondsRealtime(tempoEntreImagens);

        imagem3.SetActive(true);

        if (!acelerar)
            yield return new WaitForSecondsRealtime(tempoEntreImagens);

        imagem4.SetActive(true);

        // Espera o texto terminar (ou aceleração)
        yield return new WaitUntil(() =>
            acelerar || textoNarracao.text.Length >= textoUnico.Length
        );

        // Se acelerou: pula tudo e desativa imagens
        imagem1.SetActive(false);
        imagem2.SetActive(false);
        imagem3.SetActive(false);
        imagem4.SetActive(false);

        yield return StartCoroutine(FadeOutTexto());
        Time.timeScale = 1f;
        controle_info.podeComecarIntro = true;
        canvasIntroducao.SetActive(false);
        
    }
}