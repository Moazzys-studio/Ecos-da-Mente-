using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class NarracaoSequencial : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private TextMeshProUGUI textoLegenda;

    [Header("Falas em ordem")]
    [SerializeField] private List<FalaNarracao> falas = new List<FalaNarracao>();

    [Header("Opções")]
    [Tooltip("Limpa a legenda quando terminar todas as falas.")]
    [SerializeField] private bool limparLegendaNoFinal = true;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (textoLegenda != null)
            textoLegenda.text = "";

        if (falas.Count > 0)
            StartCoroutine(RodarNarracao());
    }

    private IEnumerator RodarNarracao()
    {
        foreach (var fala in falas)
        {
            if (fala == null || fala.clip == null)
                continue;

            // Define texto da fala
            if (textoLegenda != null)
                textoLegenda.text = fala.texto;

            // Toca o áudio da fala
            audioSource.clip = fala.clip;
            audioSource.Play();

            // Espera o tempo do áudio + uma pausinha extra
            float duracao = fala.clip.length + fala.pausaDepois;
            yield return new WaitForSeconds(duracao);
        }

        if (limparLegendaNoFinal && textoLegenda != null)
            textoLegenda.text = "";
    }
}

[System.Serializable]
public class FalaNarracao
{
    [Tooltip("Áudio dessa fala (cortado no editor).")]
    public AudioClip clip;

    [Tooltip("Texto da legenda correspondente.")]
    [TextArea(1, 3)]
    public string texto;

    [Tooltip("Pausa extra depois dessa fala (em segundos).")]
    public float pausaDepois = 0.3f;
}
