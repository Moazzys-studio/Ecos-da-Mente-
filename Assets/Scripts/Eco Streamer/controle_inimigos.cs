using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;

public class controle_inimigos : MonoBehaviour
{
    [Header("Materiais a serem modificados (arraste no Inspector na mesma ordem dos inimigos)")]
    public List<Material> materiais = new List<Material>();

    [Header("Inimigos na cena (ordem exata 1, 2, 3...)")]
    public List<GameObject> inimigos = new List<GameObject>();

    [Header("Texto UI")]
    public TMP_Text txtQuantidadeInimigos;  // Mostra inimigos + contagem
    public TMP_Text txtTempo;

    [Header("Configurações")]
    public int pontosParaMudarCor = 20;
    public Color corInicial = Color.white;
    public Color corFinal = Color.red;
    public float tempoParaDestruir = 1.5f;

    private int indiceAtual = 0;
    private float progresso = 0f;
    private bool iniciouContagem = false;

    private void Start()
    {
        AtualizarTexto();

        foreach (Material mat in materiais)
        {
            if (mat != null)
                mat.color = corInicial;
        }
    }

    public void OnCollectiblePicked()
    {
        Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca++;

        if (materiais.Count == 0 || indiceAtual >= materiais.Count)
            return;

        progresso = (float)Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca / pontosParaMudarCor;

        Material matAtual = materiais[indiceAtual];
        if (matAtual != null)
        {
            matAtual.color = Color.Lerp(corInicial, corFinal, progresso);
        }

        if (Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca >= pontosParaMudarCor)
        {
            Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca = 0;
            progresso = 0f;

            if (indiceAtual < inimigos.Count && inimigos[indiceAtual] != null)
            {
                Destroy(inimigos[indiceAtual], tempoParaDestruir);
            }

            indiceAtual++;

            AtualizarTexto();

            // Quando concluir TODOS os inimigos
            if (indiceAtual >= materiais.Count && !iniciouContagem)
            {
                iniciouContagem = true;
                StartCoroutine(ContagemParaCena());
            }
        }
    }

    private void AtualizarTexto()
    {
        if (txtQuantidadeInimigos != null)
        {
            int restantes = materiais.Count - indiceAtual;

            if (restantes > 1)
            {
                txtQuantidadeInimigos.text = restantes + " Haters te perseguindo";
            }
            else if (restantes == 1)
            {
                txtQuantidadeInimigos.text = "1 Hater te perseguindo";
            }
            else
            {
                txtQuantidadeInimigos.text = "Nenhum Hater restante!";
            }
        }
    }

    // CONTAGEM REGRESSIVA NA TELA ⭐
    private IEnumerator ContagemParaCena()
    {
        int tempo = 3;

        while (tempo > 0)
        {
            txtTempo.text = "" + tempo;
            yield return new WaitForSeconds(1f);
            tempo--;
        }

        // Troca a cena após 3 segundos
        SceneManager.LoadScene("StreamerGanhou");
    }
}