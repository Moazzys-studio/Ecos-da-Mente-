using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class controle_vidaTrabalho : MonoBehaviour
{
    [Header("Vidas")]
    public Image vida1;
    public Image vida2;
    public Image vida3;

    public Sprite vidaAtiva;
    public Sprite vidaPerdida;

    [Header("Animações das vidas")]
    public float duracaoFade = 0.4f;
    public float tempoParaSumir = 0.5f;
    public int qtdPiscadas = 4;
    public float velocidadePiscada = 0.15f;

    // AQUI ESTÁ A VIDA ESTÁTICA
    public static int vidas = 3;

    private int vidaAnterior = -1;

    [Header("Sistema de Acertos (para vitória)")]
    public int acertosParaVencer = 3;
    public static int acertos;
    

    void Start()
    {
        // garante que sempre começa com 3 vidas
        vidas = 3;

        // reseta acertos
        acertos = 0;

        AtualizarSlotsDeVida();
        vidaAnterior = vidas;
    }

    void Update()
    {
        // atualiza UI quando perder vida
        if (vidas != vidaAnterior)
        {
            vidaAnterior = vidas;
            AtualizarSlotsDeVida();
        }

        // perdeu
        if (vidas <= 0)
        {
            SceneManager.LoadScene("derrotaTrabalho");
        }

        // venceu
        if (acertos >= acertosParaVencer)
        {
            SceneManager.LoadScene("vitoriaTrabalho");
        }
    }

    // -----------------------------------------------------
    //                SISTEMA DE VIDAS
    // -----------------------------------------------------
    void AtualizarSlotsDeVida()
    {
        AtualizarSlot(vida1, vidas >= 1);
        AtualizarSlot(vida2, vidas >= 2);
        AtualizarSlot(vida3, vidas >= 3);
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
        for (float t = 0; t < 1f; t += Time.deltaTime / duracaoFade)
        {
            img.color = new Color(1, 1, 1, 1 - t);
            yield return null;
        }

        img.sprite = novoSprite;
        img.color = new Color(1, 1, 1, 0);

        for (float t = 0; t < 1f; t += Time.deltaTime / duracaoFade)
        {
            img.color = new Color(1, 1, 1, t);
            yield return null;
        }
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

        for (float t = 0; t < 1f; t += Time.deltaTime / duracaoFade)
        {
            img.color = new Color(1, 1, 1, 1 - t);
            yield return null;
        }

        img.color = new Color(1, 1, 1, 0);
    }
}