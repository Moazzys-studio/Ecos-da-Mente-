using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class introducao_streamer : MonoBehaviour
{
    [Header("Imagens iniciais")]
    public GameObject imagem1;
    public GameObject imagem2;

    [Header("Objetos com animação")]
    public GameObject animacao1;
    public GameObject animacao2;

    [Header("Imagens finais")]
    public GameObject imagemFinal1;
    public GameObject imagemFinal2; // Zoom infinito

    [Header("Tempos")]
    public float tempoEntreImagens = 1f;
    public float tempoAnimacao = 3f;
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
                textoNarracao.text = texto; // escreve direto
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
        animacao1.SetActive(false);
        animacao2.SetActive(false);
        imagemFinal1.SetActive(false);
        imagemFinal2.SetActive(false);
        textoNarracao.text = "";

        // ---------------- IMAGEM 1 ----------------
        imagem1.SetActive(true);

        yield return StartCoroutine(EscreverTexto(
            "No início, Eco ainda conseguia criar. Ali, no silêncio do estúdio, ele sentia que finalmente podia respirar — como se o mundo lá fora não existisse."
        ));

        if (!pular)
            yield return new WaitForSeconds(tempoEntreImagens);

        // ---------------- IMAGEM 2 ----------------
        imagem2.SetActive(true);

        yield return StartCoroutine(EscreverTexto(
            "Mas quando os ataques começaram, algo nele começou a quebrar. Devagar… como uma rachadura que ninguém vê, mas que se espalha por dentro."
        ));

        if (!pular)
            yield return new WaitForSeconds(tempoEntreImagens);

        imagem1.SetActive(false);
        imagem2.SetActive(false);

        // ---------------- ANIMAÇÕES ----------------
        animacao1.SetActive(true);
        animacao2.SetActive(true);

        yield return StartCoroutine(EscreverTexto(
            "As mensagens não paravam na tela. Elas atravessavam o silêncio, ecoavam por dentro, se distorciam… até virarem pensamentos que ele não conseguia mais calar."
        ));

        if (!pular)
            yield return new WaitForSeconds(tempoAnimacao);

        animacao1.SetActive(false);
        animacao2.SetActive(false);

        // ---------------- IMAGEM FINAL 1 ----------------
        imagemFinal1.SetActive(true);

        yield return StartCoroutine(EscreverTexto(
            "Aos poucos, até o silêncio do estúdio começou a pesar. Cada detalhe — a luz, o espaço, o ar — parecia repetir o que ele leu, como se a própria mente estivesse contra ele."
        ));

        if (!pular)
            yield return new WaitForSeconds(tempoEntreImagens);

        // ---------------- IMAGEM FINAL 2 + ZOOM ----------------
        imagemFinal2.SetActive(true);

        yield return StartCoroutine(EscreverTexto(
            "Quando Eco baixa a cabeça, não é só tristeza. É a sensação de ser engolido pelos próprios pensamentos… como se nem descansando ele conseguisse se afastar do que fizeram ele acreditar."
        ));

        if (!pular)
            yield return new WaitForSeconds(delayZoom);

        zoomAtivo = !pular;

        if (!pular)
            yield return new WaitForSeconds(tempoParaMudarCena);

        // ---------------- FINAL OU PULO ----------------
        SceneManager.LoadScene("Infinite runner");
    }
}
