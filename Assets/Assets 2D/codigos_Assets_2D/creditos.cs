using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class creditos : MonoBehaviour
{
   [Header("creditos")]
    public RectTransform imagemUI;

    [Header("Configurações de Movimento")]
    public float velocidade = 200f;
    public Vector2 posicaoFinal;

    [Header("Delay")]
    public float delayInicial = 1f;

    [Header("botão Voltar")]
    public GameObject objetoParaAtivar;

    private bool chegou = false;
    private bool podeSubir = false;
    private Vector2 posicaoInicial;

    void Start()
    {
        posicaoInicial = imagemUI.anchoredPosition;
    }

    void OnEnable()
    {
        ResetarCreditos();
        Invoke(nameof(AtivarMovimento), delayInicial); 
    }

    void Update()
    {
        if (!podeSubir || chegou || imagemUI == null) return;

        imagemUI.anchoredPosition = Vector2.MoveTowards(
            imagemUI.anchoredPosition,
            posicaoFinal,
            velocidade * Time.deltaTime
        );

        if (imagemUI.anchoredPosition == posicaoFinal)
        {
            chegou = true;

            if (objetoParaAtivar != null)
                objetoParaAtivar.SetActive(true);
        }
    }

    private void AtivarMovimento()
    {
        podeSubir = true;
    }

    private void ResetarCreditos()
    {
        imagemUI.anchoredPosition = posicaoInicial;
        chegou = false;
        podeSubir = false;

        if (objetoParaAtivar != null)
            objetoParaAtivar.SetActive(false);
    }

    public void FecharCreditos()
    {
        gameObject.SetActive(false);
    }
}






