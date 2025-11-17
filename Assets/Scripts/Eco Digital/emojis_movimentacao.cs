using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class emojis_movimentacao : MonoBehaviour
{
     public float minSpeed = 2f;      // velocidade mínima
    public float maxSpeed = 6f;      // velocidade máxima
    float speed;                     // velocidade sorteada

    Transform player;

    public float alturaOffset = 1.5f;

    private Vector3 alvoFixo;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // se não achar o player, destrói
        if (player == null)
        {
            Destroy(gameObject);
            return;
        }

        // sorteia velocidade aleatória
        speed = Random.Range(minSpeed, maxSpeed);

        Vector3 offset = new Vector3(0, alturaOffset, 0);
        alvoFixo = player.position + offset;
    }

    private void Update()
    {
        if (player == null)
        {
            Destroy(gameObject);
            return;
        }

        // mover até o alvo
        transform.position = Vector3.MoveTowards(
            transform.position,
            alvoFixo,
            speed * Time.deltaTime
        );

        // verifica se chegou no destino final
        float distancia = Vector3.Distance(transform.position, alvoFixo);

        if (distancia < 0.05f) // margem para evitar erro de precisão
        {
            Destroy(gameObject);
        }
    }
}
