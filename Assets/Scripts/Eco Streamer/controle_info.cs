using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class controle_info : MonoBehaviour
{
    [Header("Imagem de introdução")]
    public GameObject imagemIntro;

    [Header("Tempo em segundos que a imagem fica na tela")]
    public float tempoDaImagem = 3f;

    public static bool podeComecarIntro;
    void Start()
    {
        StartCoroutine(IniciarComPausa());
    }

    IEnumerator IniciarComPausa()
    {
        Time.timeScale = 0f;

        if (imagemIntro != null)
            imagemIntro.SetActive(true);

        //Aguarda até que a variável global esteja TRUE
        while (!podeComecarIntro)
        {
            yield return null; // espera 1 frame
        }

        // Agora sim começa a contar o tempo
        yield return new WaitForSecondsRealtime(tempoDaImagem);

        if (imagemIntro != null)
            imagemIntro.SetActive(false);

        Time.timeScale = 1f;

        // Libera o spawn
        emoji_spaw.podeSpawnar = true;
    }
}