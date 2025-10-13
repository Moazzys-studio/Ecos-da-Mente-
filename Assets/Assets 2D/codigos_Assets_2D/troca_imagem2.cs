using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class troca_imagem2 : MonoBehaviour
{
    public Image imagem;             
    public Sprite novaImagem;        
    public static bool mudou_CD2 = false;       
    public float duracaoAnimacao = 1f; 

    void Update()
    {
        if (mudou_CD2)
        {
            StartCoroutine(TrocarEAnimar());
            mudou_CD2 = false;
        }
    }

    private IEnumerator TrocarEAnimar()
    {
        imagem.sprite = novaImagem;
        Color cor = imagem.color;
        cor.a = 1f;
        imagem.color = cor;
        yield return new WaitForSeconds(0.2f);
        float t = 0;
        while (t < duracaoAnimacao)
        {
            t += Time.deltaTime;
            cor.a = Mathf.Lerp(1f, 0f, t / duracaoAnimacao);
            imagem.color = cor;
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
