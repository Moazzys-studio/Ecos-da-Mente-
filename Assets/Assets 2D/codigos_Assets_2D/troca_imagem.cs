using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;                
using UnityEngine.InputSystem.EnhancedTouch; 

public class troca_imagem : MonoBehaviour
{
    public Image imagem;
    public Sprite novaImagem;
    public bool mudou_CD1 = false;
    public float duracaoAnimacao = 1f;

    void Update()
    {
        
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            mudou_CD1 = true;
        }

        if (mudou_CD1)
        {
            StartCoroutine(TrocarEAnimar());
            mudou_CD1 = false;
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
