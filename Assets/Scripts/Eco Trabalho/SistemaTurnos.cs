using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SistemaTurnos : MonoBehaviour
{
    [Header("Prefabs já na Cena (NPCs de cada turno)")]
    public GameObject turno1NPC;
    public GameObject turno2NPC;
    public GameObject turno3NPC;
    public GameObject turno4NPC;

    [Header("Timer do Canvas")]
    public Image timerImagem;

    [Header("Configurações de Turno")]
    public float delayInicial = 2f;
    public float tempoTurno = 5f;
    public float delayEntreTurnos = 1f;

    [Range(1, 4)]
    public int turnoAtual = 1;

    [Header("UI - Texto do Turno Atual")]
    public TMPro.TextMeshProUGUI textoTurno;

    [Header("UI - Resultado do Turno")]
    public TMPro.TextMeshProUGUI resultadoTurnoText;

    [Header("PESO DE CADA TURNO (DEFINA AQUI)")]
    public float pesoTurno1 = 1f;
    public float pesoTurno2 = 1f;
    public float pesoTurno3 = 1f;
    public float pesoTurno4 = 1f;

    // ---------------------------
    // CONFIGURAÇÃO DO ACERTO
    // ---------------------------
    [Header("Faixa de acerto da balança (°)")]
    public float anguloMinAcerto = -95f;
    public float anguloMaxAcerto = -85f;

    void Start()
    {
        if (resultadoTurnoText != null)
            resultadoTurnoText.text = ""; // limpa no início

        StartCoroutine(IniciarSistema());
    }

    IEnumerator IniciarSistema()
    {
        yield return new WaitForSeconds(delayInicial);

        while (true)
        {
            AtualizarTurno();

            Debug.Log("🔄 INICIO DO TURNO: " + turnoAtual);

            float t = 0f;
            while (t < tempoTurno)
            {
                t += Time.deltaTime;
                timerImagem.fillAmount = 1f - (t / tempoTurno);
                yield return null;
            }

            timerImagem.fillAmount = 0f;

            // -------------------------------
            // VERIFICA ACERTO DO TURNO
            // -------------------------------
            float anguloAtual = ControleBalanca.rotX_Global;

            if (anguloAtual >= anguloMinAcerto && anguloAtual <= anguloMaxAcerto)
            {
                Debug.Log("ACERTOU O TURNO " + turnoAtual + " | Ângulo: " + anguloAtual);
                controle_vidaTrabalho.acertos++;

                MostrarResultado("ACERTOU!", new Color32(0, 255, 0, 255)); // verde
            }
            else
            {
                Debug.Log("ERROU O TURNO " + turnoAtual + " | Ângulo: " + anguloAtual);
                controle_vidaTrabalho.vidas--;

                MostrarResultado("ERROU!", new Color32(255, 0, 0, 255)); // vermelho
            }

            yield return new WaitForSeconds(delayEntreTurnos);

            // Limpa o texto antes do próximo turno
            if (resultadoTurnoText != null)
                resultadoTurnoText.text = "";

            turnoAtual++;
            if (turnoAtual > 4)
                turnoAtual = 1;
        }
    }

    void AtualizarTurno()
    {
        if (textoTurno != null)
            textoTurno.text = "Turno " + turnoAtual;

        turno1NPC.SetActive(false);
        turno2NPC.SetActive(false);
        turno3NPC.SetActive(false);
        turno4NPC.SetActive(false);

        switch (turnoAtual)
        {
            case 1:
                turno1NPC.SetActive(true);
                AplicarPesoGlobal(pesoTurno1, true);
                break;

            case 2:
                turno2NPC.SetActive(true);
                AplicarPesoGlobal(pesoTurno2, false);
                break;

            case 3:
                turno3NPC.SetActive(true);
                AplicarPesoGlobal(pesoTurno3, true);
                break;

            case 4:
                turno4NPC.SetActive(true);
                AplicarPesoGlobal(pesoTurno4, false);
                break;
        }

        timerImagem.fillAmount = 1f;
    }

    void AplicarPesoGlobal(float peso, bool esquerda)
    {
        if (esquerda)
        {
            ControleBalanca.pesoEsquerda = peso;
            ControleBalanca.pesoDireita = 0f;
        }
        else
        {
            ControleBalanca.pesoDireita = peso;
            ControleBalanca.pesoEsquerda = 0f;
        }
    }

    // -------------------------------------------------
    //     FUNÇÃO: MOSTRAR RESULTADO DO TURNO NA TELA
    // -------------------------------------------------
    void MostrarResultado(string mensagem, Color cor)
    {
        if (resultadoTurnoText == null) return;

        resultadoTurnoText.text = mensagem;
        resultadoTurnoText.color = cor;
    }
}
