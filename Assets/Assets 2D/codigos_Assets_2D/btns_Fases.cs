using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class btns_Fases : MonoBehaviour
{
    public Button btnFase1;
    public Button btnFase2;
    public Button btnFase3;

    void Start()
    {
        AtualizarBotoes();
    }

    void Update()
   {
    AtualizarBotoes();
   }

    void AtualizarBotoes()
{
    // -------- FASE 1 --------
    if (variaveis_banco.ganhoudigital == true)
    {
        btnFase1.interactable = false;
        DeixarTransparente(btnFase1);
    }
    else
    {
        btnFase1.interactable = true;
        DeixarOpaco(btnFase1);
    }

    // -------- FASE 2 --------
    if (variaveis_banco.ganhoutrabalho == true)
    {
        btnFase2.interactable = false;
        DeixarTransparente(btnFase2);
    }
    else
    {
        btnFase2.interactable = true;
        DeixarOpaco(btnFase2);
    }

    // -------- FASE 3 --------
    if (variaveis_banco.ganhoustreamer == true)
    {
        btnFase3.interactable = false;
        DeixarTransparente(btnFase3);
    }
    else
    {
        btnFase3.interactable = true;
        DeixarOpaco(btnFase3);
    }
}

// --- Funções extras ---
void DeixarTransparente(Button btn)
{
    Color c = btn.image.color;
    c.a = 0.3f;
    btn.image.color = c;
}

void DeixarOpaco(Button btn)
{
    Color c = btn.image.color;
    c.a = 1f;
    btn.image.color = c;
}

    // --------------------
    // BOTÕES DAS FASES
    // --------------------
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
        btns_menu.Fases_tela = true;
        SceneManager.LoadScene("Estudio");
    }
}
