using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class emojis_movimentacao : MonoBehaviour
{
    public float minSpeed = 2f;
    public float maxSpeed = 6f;
    float speed;

    Transform player;
    public float alturaOffset = 1.5f;

    private Vector3 alvoFixo;
    private bool tocouNoPlayer = false;

    // distância mínima para vibrar (bem curtinha)
    public float distanciaParaVibrar = 0.3f;
    private bool jaVibrou = false;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
        {
            Destroy(gameObject);
            return;
        }

        speed = Random.Range(minSpeed, maxSpeed);

        Vector3 offset = new Vector3(0, alturaOffset, 0);
        alvoFixo = player.position + offset;
    }

    private void Update()
    {
        if (player == null)
        {
            RegistrarDestruicaoSemAcertar();
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            alvoFixo,
            speed * Time.deltaTime
        );

        float distanciaPlayer = Vector3.Distance(transform.position, player.position);

        // -----------------------------
        // VIBRAR QUANDO ESTIVER MINIMAMENTE PERTO
        // -----------------------------
        if (!jaVibrou && distanciaPlayer <= distanciaParaVibrar)
        {
            VibracaoManager.Instancia?.VibracaoMedia(); // vibração ao ficar “bem perto”
            jaVibrou = true;
        }

        if (Vector3.Distance(transform.position, alvoFixo) < 0.05f)
        {
            RegistrarDestruicaoSemAcertar();
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            tocouNoPlayer = true;

            Ecodigital_variaveisglobal.vidaPlayer--;
            Debug.Log(Ecodigital_variaveisglobal.vidaPlayer);

            // vibração quando encosta de fato no player
            VibracaoManager.Instancia?.VibracaoForte();

            if (Ecodigital_variaveisglobal.vidaPlayer <= 0)
            {
                Destroy(other.gameObject);
            }

            Destroy(gameObject);
        }
    }

    void RegistrarDestruicaoSemAcertar()
    {
        if (!tocouNoPlayer)
        {
            Ecodigital_variaveisglobal.objetosDestruidosSemAcertar++;
            Debug.Log("Pontuação atual " + Ecodigital_variaveisglobal.objetosDestruidosSemAcertar);
        }
    }
}
