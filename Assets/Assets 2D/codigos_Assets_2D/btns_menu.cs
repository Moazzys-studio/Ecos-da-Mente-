using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class btns_menu : MonoBehaviour
{
    public GameObject creditos;
    public GameObject tutorial;
    public GameObject fases;
    public GameObject config;
    public static bool Fases_tela = false;
    void Start()
    {
        creditos.SetActive(false);
        tutorial.SetActive(false);
        fases.SetActive(false);
        config.SetActive(false);
        
    }

    void Update()
    {
        if(Fases_tela == true)
        {
          fases.SetActive(true);
        }

    }

    public void Creditos()
    {
        creditos.SetActive(true);
    }

    public void Tutorial()
    {
        tutorial.SetActive(true);
    }

    public void Fases()
    {
        fases.SetActive(true);
    }
    public void voltar_menu()
    {
        Fases_tela = false;
        creditos.SetActive(false);
        tutorial.SetActive(false);
        fases.SetActive(false);
        config.SetActive(false);
    }
    public void Configura()
    {
       config.SetActive(true);
    }
}
