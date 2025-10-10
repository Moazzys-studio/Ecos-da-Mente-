using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class creditos : MonoBehaviour
{
    public Image[] imagens;            // Arraste suas 8 imagens do Canvas
    public float tempoPorImagem = 3f;  // Tempo que cada imagem fica visível
    public float duracaoAnimacao = 0.5f; // Velocidade do zoom-in

    private Coroutine slideshowCoroutine;

    void OnEnable()
    {
        // Sempre que os créditos forem ativados, reinicia o slideshow
        ResetarImagens();
        if (imagens.Length > 0)
            slideshowCoroutine = StartCoroutine(RodarSlideshow());
    }

    void OnDisable()
    {
        // Para a coroutine quando o painel de créditos desativar
        if (slideshowCoroutine != null)
            StopCoroutine(slideshowCoroutine);
    }

    void ResetarImagens()
    {
        // Desliga todas as imagens
        foreach (var img in imagens)
        {
            img.gameObject.SetActive(false);
            img.rectTransform.localScale = Vector3.one;
        }
    }

    IEnumerator RodarSlideshow()
    {
        for (int i = 0; i < imagens.Length; i++)
        {
            // Liga a imagem atual
            imagens[i].gameObject.SetActive(true);

            // Começa pequena
            RectTransform rt = imagens[i].rectTransform;
            rt.localScale = Vector3.one * 0.5f;

            // Anima crescendo até escala normal
            float tempo = 0f;
            while (tempo < duracaoAnimacao)
            {
                tempo += Time.deltaTime;
                float t = tempo / duracaoAnimacao;
                rt.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one, t);
                yield return null;
            }

            rt.localScale = Vector3.one;

            // Se NÃO for a última imagem, espera e depois desativa
            if (i < imagens.Length - 1)
            {
                yield return new WaitForSeconds(tempoPorImagem);
                imagens[i].gameObject.SetActive(false);
            }
        }

        // Quando terminar, a última imagem permanece na tela
    }
            public void Voltar()
        {
            gameObject.SetActive(false);
        }
}






