using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class btns_menu : MonoBehaviour
{
   public GameObject creditos;
    public GameObject tutorial;
    public GameObject fases;

    void Start()
    {
        creditos.SetActive(false);
        tutorial.SetActive(false);
        fases.SetActive(false);
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
}
