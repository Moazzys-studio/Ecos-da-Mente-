using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;                
using UnityEngine.InputSystem.EnhancedTouch; 

public class troca_imagem2 : MonoBehaviour
{
    public Image imagem;
    public Sprite novaImagem;

    public static bool mudou_CD2 = false;
    public static bool objetoJaDestruido = false; // <-- memória do objeto destruído

    public float duracaoAnimacao = 1f;
    public float delayInicio = 1f;

    private bool animacaoRodando = false;

    void Start()
    {
        // Se já foi destruído uma vez, se destrói de novo imediatamente
         if (objetoJaDestruido || variaveis_banco.ganhoustreamer == true)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            mudou_CD2 = true;
        }

        if (mudou_CD2 && !animacaoRodando)
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

        // Marca que o objeto foi destruído PERMANENTEMENTE
        objetoJaDestruido = true;

        Destroy(gameObject);
    }
}
