using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class btns_ecoStreamer : MonoBehaviour
{
    public void tentar()
    {
      Eco_Streamer_Variaveis.vida_ecoStreamer = 3;
      Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca = 0;
      Eco_Streamer_Variaveis.ecoStreamer_inimigos = 3;
      SceneManager.LoadScene("Infinite runner");
    }
    
    public void sair()
    {
      Eco_Streamer_Variaveis.vida_ecoStreamer = 3;
      Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca = 0;
      Eco_Streamer_Variaveis.ecoStreamer_inimigos = 3;
      SceneManager.LoadScene("Menu");
    }
    public void sair_digital()
    {
      Ecodigital_variaveisglobal.vidaPlayer = 3;
      Ecodigital_variaveisglobal.objetosDestruidosSemAcertar =0;
      SceneManager.LoadScene("Menu");
    }
    public void sair_trabalho()
    {
      controle_vidaTrabalho.vidas = 3;
      controle_vidaTrabalho.acertos = 0;
      SceneManager.LoadScene("Menu");
    }
    
    public void tentar_digital()
    {
      Ecodigital_variaveisglobal.vidaPlayer = 3;
      Ecodigital_variaveisglobal.objetosDestruidosSemAcertar =0;
      SceneManager.LoadScene("PuzzleEcoDigital");
    }
    public void tentar_trabalho()
    {
      controle_vidaTrabalho.vidas = 3;
      controle_vidaTrabalho.acertos = 0;
      SceneManager.LoadScene("PuzzleTrabalho");
    }
}
