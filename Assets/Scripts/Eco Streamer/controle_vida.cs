using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class controle_vida : MonoBehaviour
{
   [Header("Vidas")]
    public Image vida1;
    public Image vida2;
    public Image vida3;

    public Sprite vidaAtiva;      
    public Sprite vidaPerdida;

    public float duracaoFade = 0.4f;
    public float tempoParaSumir = 2f;
    public int qtdPiscadas = 4;
    public float velocidadePiscada = 0.15f;

    private int vidaAnterior = -1;

    [Header("Barra de Confiança (FillAmount)")]
    public Image barraConfianca;
    public int confiancaMaxima = 20;          // Valor máximo para encher a barra
    public float velocidadeBarra = 3f;         // Velocidade da animação ao encher
    private int confiancaAnterior = -1;


    void Update()
    {
        // ---------------- VIDA ----------------
        if (Eco_Streamer_Variaveis.vida_ecoStreamer != vidaAnterior)
        {
            vidaAnterior = Eco_Streamer_Variaveis.vida_ecoStreamer;
            AtualizarTodosSlots();
        }

        // ---------------- CONFIANÇA ----------------
        if (Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca != confiancaAnterior)
        {
            confiancaAnterior = Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca;
            StartCoroutine(AtualizarBarraDeConfianca());
        }
    }

    // ---------------------------------------------------------------
    //                    SISTEMA DE VIDAS
    // ---------------------------------------------------------------
    void AtualizarTodosSlots()
    {
        int vida = Eco_Streamer_Variaveis.vida_ecoStreamer;

        AtualizarSlot(vida1, vida >= 1);
        AtualizarSlot(vida2, vida >= 2);
        AtualizarSlot(vida3, vida >= 3);
    }

    void AtualizarSlot(Image img, bool ativa)
    {
        Sprite novoSprite = ativa ? vidaAtiva : vidaPerdida;

        if (img.sprite != novoSprite)
        {
            StartCoroutine(TrocarImagemComFade(img, novoSprite));

            if (!ativa)
                StartCoroutine(PiscarAntesDeSumir(img));
        }
    }

    IEnumerator TrocarImagemComFade(Image img, Sprite novoSprite)
    {
        // Fade-out
        for (float t = 0; t < 1; t += Time.deltaTime / duracaoFade)
        {
            img.color = new Color(1, 1, 1, 1 - t);
            yield return null;
        }

        img.sprite = novoSprite;
        img.color = new Color(1, 1, 1, 0);

        // Fade-in
        for (float t = 0; t < 1; t += Time.deltaTime / duracaoFade)
        {
            img.color = new Color(1, 1, 1, t);
            yield return null;
        }

        img.color = new Color(1, 1, 1, 1);
    }

    IEnumerator PiscarAntesDeSumir(Image img)
    {
        yield return new WaitForSeconds(tempoParaSumir);

        for (int i = 0; i < qtdPiscadas; i++)
        {
            img.color = new Color(1, 1, 1, 0);
            yield return new WaitForSeconds(velocidadePiscada);

            img.color = new Color(1, 1, 1, 1);
            yield return new WaitForSeconds(velocidadePiscada);
        }

        // Fade final
        for (float t = 0; t < 1; t += Time.deltaTime / duracaoFade)
        {
            img.color = new Color(1, 1, 1, 1 - t);
            yield return null;
        }

        img.color = new Color(1, 1, 1, 0);
    }


    // ---------------------------------------------------------------
    //                SISTEMA DE CONFIANÇA (FILL AMOUNT)
    // ---------------------------------------------------------------
    IEnumerator AtualizarBarraDeConfianca()
    {
        int valor = Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca;

        float alvo = Mathf.Clamp01((float)valor / confiancaMaxima);
        float atual = barraConfianca.fillAmount;

        while (Mathf.Abs(atual - alvo) > 0.001f)
        {
            atual = Mathf.Lerp(atual, alvo, Time.deltaTime * velocidadeBarra);
            barraConfianca.fillAmount = atual;
            yield return null;
        }

        barraConfianca.fillAmount = alvo;
    }
}
