using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class btns_Fases : MonoBehaviour
{
    public void Fase_1()
    {
      SceneManager.LoadScene("Introdução ecodigital");
    }
    public void Fase_2()
    {
      SceneManager.LoadScene("Introdução do trabalho");
    }
    public void Fase_3()
    {
      SceneManager.LoadScene("Estudio");
    }
}
