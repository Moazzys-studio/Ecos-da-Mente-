using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class variaveis_banco : MonoBehaviour
{
    public static bool ganhoudigital = false;
    public static bool ganhoutrabalho = false;
    public static bool ganhoustreamer = false;

    void Awake()
    {
        CarregarBanco();
    }

    // -----------------------------
    // SALVAR OS DADOS
    // -----------------------------
    public static void SalvarBanco()
    {
        PlayerPrefs.SetInt("ganhoudigital", ganhoudigital ? 1 : 0);
        PlayerPrefs.SetInt("ganhoutrabalho", ganhoutrabalho ? 1 : 0);
        PlayerPrefs.SetInt("ganhoustreamer", ganhoustreamer ? 1 : 0);

        PlayerPrefs.Save();
    }

    // -----------------------------
    // CARREGAR OS DADOS
    // -----------------------------
    public static void CarregarBanco()
    {
        ganhoudigital   = PlayerPrefs.GetInt("ganhoudigital", 0) == 1;
        ganhoutrabalho  = PlayerPrefs.GetInt("ganhoutrabalho", 0) == 1;
        ganhoustreamer  = PlayerPrefs.GetInt("ganhoustreamer", 0) == 1;
    }

    // -----------------------------
    // RESETAR (FUNÇÃO PARA O BOTÃO)
    // -----------------------------
    public void LimparBancoBtn()
    {
        // Remove as chaves salvas
        PlayerPrefs.DeleteKey("ganhoudigital");
        PlayerPrefs.DeleteKey("ganhoutrabalho");
        PlayerPrefs.DeleteKey("ganhoustreamer");

        PlayerPrefs.Save();

        // Zera as variáveis na memória
        ganhoudigital = false;
        ganhoutrabalho = false;
        ganhoustreamer = false;

        Debug.Log("Banco apagado pelo botão!");
    }
}
