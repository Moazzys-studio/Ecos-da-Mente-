using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cenario_fundo : MonoBehaviour
{
    [Header("Configurações de Movimento")]
    public float velocidade = 5f;           // Velocidade de movimento para a esquerda
    public float larguraCenario = 20f;      // Largura do cenário (igual à largura do prefab)
    public GameObject prefabCenario;        // Prefab do cenário (a ser duplicado)

    private GameObject proximoCenario;
    private bool criouNovo = false;

    void Update()
    {
        // Mover o cenário para a esquerda
        transform.Translate(Vector3.left * velocidade * Time.deltaTime);

        // Quando o cenário estiver quase saindo da tela (80% do caminho), cria o próximo
        if (!criouNovo && transform.position.x < -larguraCenario * 0.2f)
        {
            CriarProximoCenario();
            criouNovo = true;
        }

        // Quando o cenário sair totalmente da tela, destrói ele
        if (transform.position.x < -larguraCenario * 1.5f)
        {
            Destroy(gameObject);
        }
    }

    void CriarProximoCenario()
    {
        // Posição do novo cenário — imediatamente à direita do atual
        Vector3 novaPosicao = new Vector3(
            transform.position.x + larguraCenario,
            transform.position.y,
            transform.position.z
        );

        proximoCenario = Instantiate(prefabCenario, novaPosicao, transform.rotation);

        // Corrigido: usa o mesmo nome da classe atual
        cenario_fundo novoScript = proximoCenario.GetComponent<cenario_fundo>();
        novoScript.prefabCenario = prefabCenario;
        novoScript.larguraCenario = larguraCenario;
        novoScript.velocidade = velocidade;
    }
}