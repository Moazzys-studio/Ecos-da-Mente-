using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class introducao_trabalho : MonoBehaviour
{
    [Header("Imagens iniciais")]
    public GameObject imagem1;
    public GameObject imagem2;

    [Header("Imagem final única (zoom)")]
    public GameObject imagemFinal2;

    [Header("Tempos")]
    public float tempoEntreImagens = 1f;
    public float tempoParaMudarCena = 2f;

    [Header("Zoom")]
    public float velocidadeZoom = 0.2f;
    public float delayZoom = 1f;
    private bool zoomAtivo = false;

    [Header("Texto na Tela")]
    public TMP_Text textoNarracao;

    // ---- NOVO: controle do botão de pular ----
    private bool pular = false;

    public void PularIntroducao()
    {
        pular = true;
    }
    // ------------------------------------------

    void Start()
    {
        textoNarracao.overflowMode = TextOverflowModes.Truncate;
        textoNarracao.enableWordWrapping = true;

        StartCoroutine(Sequencia());
    }

    void Update()
    {
        if (zoomAtivo && imagemFinal2 != null && !pular)
        {
            imagemFinal2.transform.localScale += Vector3.one * velocidadeZoom * Time.deltaTime;
        }
    }

    IEnumerator EscreverTexto(string texto)
    {
        textoNarracao.text = "";

        foreach (char c in texto)
        {
            if (pular)
            {
                textoNarracao.text = texto; // escreve tudo direto
                yield break;
            }

            textoNarracao.text += c;
            yield return new WaitForSeconds(variaveis_menu.velocidadeLetra);
        }

        if (!pular)
            yield return new WaitForSeconds(0.6f);
    }

    IEnumerator Sequencia()
    {
        imagem1.SetActive(false);
        imagem2.SetActive(false);
        imagemFinal2.SetActive(false);
        textoNarracao.text = "";

        // ---------------- IMAGEM 1 ----------------
        imagem1.SetActive(true);
        yield return StartCoroutine(EscreverTexto(
           "Todos os dias começam assim: com Eco deixando o elevador como quem abandona um pedaço de si ali dentro. O cansaço em seu rosto não é só sono — é o peso de tentar viver sem equilíbrio. Antes mesmo de chegar ao escritório, ele já sente que está ficando para trás… do trabalho, da vida pessoal e de si mesmo."
        ));

        if (!pular)
            yield return new WaitForSeconds(tempoEntreImagens);

        // ---------------- IMAGEM 2 ----------------
        imagem2.SetActive(true);
        yield return StartCoroutine(EscreverTexto(
           "Enquanto caminha, Eco tenta entender onde tudo desandou. Ele fala consigo mesmo, tentando organizar pensamentos, mas não consegue. O trabalho ocupa tanto espaço que sua vida pessoal quase desaparece, e ele não encontra forma de equilibrar as duas."
        ));

        if (!pular)
            yield return new WaitForSeconds(tempoEntreImagens);

        // ---------------- IMAGEM FINAL ----------------
        imagem1.SetActive(false);
        imagem2.SetActive(false);

        imagemFinal2.SetActive(true);
        yield return StartCoroutine(EscreverTexto(
            "Nos dias repetidos, Eco parece duplicado: um chega cansado, o outro passa apressado. Ambos tentam viver, mas nenhum consegue. Preso entre demandas do trabalho e a vida pessoal que escorre pelos dedos, Eco perde o equilíbrio que tanto procura."
        ));

        if (!pular)
            yield return new WaitForSeconds(delayZoom);

        zoomAtivo = !pular;

        if (!pular)
            yield return new WaitForSeconds(tempoParaMudarCena);

        // ---------------- PULAR OU FINALIZAR ----------------
        SceneManager.LoadScene("Escritorio");
    }
}