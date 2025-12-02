using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class troca_imagem : MonoBehaviour
{
    public Image imagem;
    public Sprite novaImagem;

    public static bool mudou_CD1 = false;

    public float duracaoAnimacao = 1f;
    public float delayInicio = 1f;

    private bool animacaoRodando = false;

    void Start()
    {
        if (variaveis_banco.ganhoudigital == true)
        {
           Destroy(gameObject);
           return;
       }
       else if (variaveis_banco.ganhoutrabalho == true)
        {
            Destroy(gameObject);
            return;
        }

    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            mudou_CD1 = true;
        }

        if (mudou_CD1 && !animacaoRodando)
        {
            StartCoroutine(TrocarEAnimar());
            animacaoRodando = true;
        }
    }

    private IEnumerator TrocarEAnimar()
    {
        yield return new WaitForSeconds(delayInicio);

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

        // SALVA PERMANENTEMENTE
        variaveis_banco.ganhoutrabalho = true;
        variaveis_banco.SalvarBanco();

        Destroy(gameObject);
    }
}
