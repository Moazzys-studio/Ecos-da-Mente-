using UnityEngine;
using System.Collections.Generic;

public class controle_inimigos : MonoBehaviour
{
     [Header("Materiais a serem modificados (arraste no Inspector)")]
    public List<Material> materiais = new List<Material>(); // Ex: 3 materiais (na ordem certa)

    [Header("Configurações da transição de cor")]
    public int pontosParaMudarCor = 20;   // Quantos coletáveis precisa para completar a transição
    public Color corInicial = Color.white;
    public Color corFinal = Color.red;

    private int indiceAtual = 0;     // Qual material está sendo modificado
    private float progresso = 0f;    // Progresso da interpolação (0 → 1)

    private void Start()
    {
        // Garante que todos os materiais comecem com a cor inicial
        foreach (Material mat in materiais)
        {
            if (mat != null)
                mat.color = corInicial;
        }
    }

    public void OnCollectiblePicked()
    {
        // Soma pontos de confiança (variável global)
        Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca++;

        // Garante que temos materiais válidos
        if (materiais.Count == 0 || indiceAtual >= materiais.Count)
            return;

        // Calcula o progresso (0 a 1)
        progresso = (float)Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca / pontosParaMudarCor;

        // Muda a cor apenas do material atual
        Material matAtual = materiais[indiceAtual];
        if (matAtual != null)
        {
            Color novaCor = Color.Lerp(corInicial, corFinal, progresso);
            matAtual.color = novaCor;
        }

        // Quando atingir o limite, avança para o próximo material
        if (Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca >= pontosParaMudarCor)
        {
            Eco_Streamer_Variaveis.ecoStreamer_pontosDEconfianca = 0;
            progresso = 0f;
            indiceAtual++;

            if (indiceAtual >= materiais.Count)
            {
                Debug.Log("Todos os materiais já mudaram de cor!");
            }
            else
            {
                Debug.Log("Avançou para o material " + (indiceAtual + 1));
            }
        }
    }
}