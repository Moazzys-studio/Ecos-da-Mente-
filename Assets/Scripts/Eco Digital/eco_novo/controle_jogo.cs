using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class controle_jogo : MonoBehaviour
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
    public int confiancaMaxima = 20;
    public float velocidadeBarra = 3f;
    private int confiancaAnterior = -1;

    [Header("Cores da Barra de Confiança")]
    public Color corInicial = Color.yellow;
    public Color corFinal = Color.blue;

    void Update()
    {
        // ---------------- VIDA ----------------
        if (Ecodigital_variaveisglobal.vidaPlayer != vidaAnterior)
        {
            vidaAnterior = Ecodigital_variaveisglobal.vidaPlayer;
            AtualizarTodosSlots();
        }

        // ---------------- CONFIANÇA ----------------
        if (Ecodigital_variaveisglobal.objetosDestruidosSemAcertar != confiancaAnterior)
        {
            confiancaAnterior = Ecodigital_variaveisglobal.objetosDestruidosSemAcertar;
            StartCoroutine(AtualizarBarraDeConfianca());
        }

        // ---------------- CHECA SE A VIDA ACABOU ----------------
        if (Ecodigital_variaveisglobal.vidaPlayer <= 0)
        {
            SceneManager.LoadScene("DigitalPerdeu");
        }

        // ---------------- CHECA VITÓRIA (CONFIANÇA MÁXIMA) ----------------
        if (Ecodigital_variaveisglobal.objetosDestruidosSemAcertar >= confiancaMaxima)
        {
            variaveis_banco.ganhoudigital = true;
            variaveis_banco.SalvarBanco();
            btns_menu.Fases_tela = true;
            troca_imagem.mudou_CD1 = true;
            SceneManager.LoadScene("DigitalGanhou");
        }
    }

    // ---------------------------------------------------------------
    //                    SISTEMA DE VIDAS
    // ---------------------------------------------------------------
    void AtualizarTodosSlots()
    {
        int vida = Ecodigital_variaveisglobal.vidaPlayer;

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
        for (float t = 0; t < 1; t += Time.deltaTime / duracaoFade)
        {
            img.color = new Color(1, 1, 1, 1 - t);
            yield return null;
        }

        img.sprite = novoSprite;
        img.color = new Color(1, 1, 1, 0);

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
        int valor = Ecodigital_variaveisglobal.objetosDestruidosSemAcertar;

        float alvo = Mathf.Clamp01((float)valor / confiancaMaxima);
        float atual = barraConfianca.fillAmount;

        while (Mathf.Abs(atual - alvo) > 0.001f)
        {
            atual = Mathf.Lerp(atual, alvo, Time.deltaTime * velocidadeBarra);
            barraConfianca.fillAmount = atual;

            // -------- MUDANÇA DE COR BASEADA NA METADE --------
            if (atual < 0.5f)
            {
                barraConfianca.color = corInicial;
            }
            else
            {
                barraConfianca.color = corFinal;
            }

            yield return null;
        }

        barraConfianca.fillAmount = alvo;

        if (alvo < 0.5f)
            barraConfianca.color = corInicial;
        else
            barraConfianca.color = corFinal;
    }
}
