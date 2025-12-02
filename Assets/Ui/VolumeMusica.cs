using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeMusica : MonoBehaviour
{
    private AudioSource audioMusica;

    void Start()
    {
        audioMusica = GetComponent<AudioSource>();

        // Ajusta o volume inicial conforme o valor global
        audioMusica.volume = VolumeGloba.volumeMusica;
    }

    void Update()
    {
        // Mantém o volume sempre igual ao valor global
        audioMusica.volume = VolumeGloba.volumeMusica;
    }
}