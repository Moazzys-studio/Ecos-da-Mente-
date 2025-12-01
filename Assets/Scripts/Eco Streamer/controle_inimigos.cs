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
    public TMP_Text txtQuantidadeInimigos;
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

        // Reset das cores iniciais
        foreach (Material mat in materiais)
        {
            if (mat != null)
                mat.color = corInicial;
        }

        // Verifica imediatamente inimigos inexistentes
        VerificarInimigoValido();
    }

    // =============================================================
    // Zerar coletável e resetar material do inimigo atual
    // =============================================================
    private void ResetarColetavelEAparencia()
    {
        Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca = 0;
        progresso = 0f;

        if (indiceAtual < materiais.Count && materiais[indiceAtual] != null)
            materiais[indiceAtual].color = corInicial;
    }

    // =============================================================
    // Pula inimigos que já não existem na cena
    // =============================================================
    private void VerificarInimigoValido()
    {
        while (indiceAtual < inimigos.Count &&
               inimigos[indiceAtual] == null)
        {
            // Marca este inimigo como concluído
            if (materiais[indiceAtual] != null)
                materiais[indiceAtual].color = corFinal;

            indiceAtual++;
            AtualizarTexto();

            // Prepara o próximo inimigo
            if (indiceAtual < materiais.Count)
                ResetarColetavelEAparencia();
        }
    }

    // =============================================================
    // Quando o jogador coleta algo
    // =============================================================
    public void OnCollectiblePicked()
    {
        Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca++;

        // Sempre verificar se o inimigo atual existe
        VerificarInimigoValido();

        if (materiais.Count == 0 || indiceAtual >= materiais.Count)
            return;

        progresso = (float)Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca / pontosParaMudarCor;

        Material matAtual = materiais[indiceAtual];
        if (matAtual != null)
        {
            matAtual.color = Color.Lerp(corInicial, corFinal, progresso);
        }

        // Quando completar a barra de cor
        if (Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca >= pontosParaMudarCor)
        {
            Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca = 0;
            progresso = 0f;

            // Destruir inimigo se existir
            if (indiceAtual < inimigos.Count && inimigos[indiceAtual] != null)
            {
                Destroy(inimigos[indiceAtual], tempoParaDestruir);
            }

            indiceAtual++;
            AtualizarTexto();

            // Resetar para o próximo inimigo
            ResetarColetavelEAparencia();

            // Caso o próximo inimigo também não exista
            VerificarInimigoValido();

            // Quando acabar todos
            if (indiceAtual >= materiais.Count && !iniciouContagem)
            {
                iniciouContagem = true;
                StartCoroutine(ContagemParaCena());
            }
        }
    }

    // =============================================================
    // UI — texto de inimigos restantes
    // =============================================================
    private void AtualizarTexto()
    {
        if (txtQuantidadeInimigos != null)
        {
            int restantes = Mathf.Max(0, materiais.Count - indiceAtual);

            if (restantes > 1)
                txtQuantidadeInimigos.text = restantes + " Haters te perseguindo";
            else if (restantes == 1)
                txtQuantidadeInimigos.text = "1 Hater te perseguindo";
            else
                txtQuantidadeInimigos.text = "Nenhum Hater restante!";
        }
    }

    // =============================================================
    // Contagem final para trocar a cena
    // =============================================================
    private IEnumerator ContagemParaCena()
    {
        int tempo = 3;

        while (tempo > 0)
        {
            txtTempo.text = "" + tempo;
            yield return new WaitForSeconds(1f);
            tempo--;
        }
        variaveis_banco.ganhoustreamer = true;
        variaveis_banco.SalvarBanco();
        btns_menu.Fases_tela = true;
        SceneManager.LoadScene("StreamerGanhou");
    }
}