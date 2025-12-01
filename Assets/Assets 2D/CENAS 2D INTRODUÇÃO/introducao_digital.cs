using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class introducao_digital : MonoBehaviour
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

    // ---- NOVO: botão de pular ----
    private bool pular = false;

    public void PularIntroducao()
    {
        pular = true;
    }
    // --------------------------------

    void Start()
    {
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
                textoNarracao.text = texto; // mostra tudo instantaneamente
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
            "Às vezes, Eco tentava se convencer de que estava tudo bem. Mas ali, no silêncio do quarto, cada palavra que ele lia parecia pesar mais do que deveria… como se a tela estivesse falando direto com ele."
        ));

        if (!pular)
            yield return new WaitForSeconds(tempoEntreImagens);

        // ---------------- IMAGEM 2 ----------------
        imagem2.SetActive(true);

        yield return StartCoroutine(EscreverTexto(
            "Mesmo tentando descansar, a mente dele não ficava quieta. As mensagens voltavam como eco—uma atrás da outra—misturando medo, dúvida e aquela sensação de que ele estava começando a acreditar no pior sobre si mesmo."
        ));

        if (!pular)
            yield return new WaitForSeconds(tempoEntreImagens);

        imagem1.SetActive(false);
        imagem2.SetActive(false);

        // ---------------- IMAGEM FINAL ----------------
        imagemFinal2.SetActive(true);

        yield return StartCoroutine(EscreverTexto(
            "Na cidade, cercado de gente, Eco percebeu que não era só cansaço. Era como se o mundo inteiro estivesse andando rápido demais, enquanto ele ficava preso dentro da própria cabeça. Rodeado de pessoas, era ali que ele se sentia sozinho."
        ));

        if (!pular)
            yield return new WaitForSeconds(delayZoom);

        zoomAtivo = !pular;

        if (!pular)
            yield return new WaitForSeconds(tempoParaMudarCena);

        // ---------------- FINAL OU PULO ----------------
        SceneManager.LoadScene("Cidade");
    }
}
