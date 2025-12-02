using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
    public Slider sliderVolume;

    void Start()
    {
        // Carregar volume salvo ou 1 caso não exista
        float volumeSalvo = PlayerPrefs.GetFloat("volumeMusica", 1f);

        VolumeGloba.volumeMusica = volumeSalvo;
        sliderVolume.value = volumeSalvo;

        sliderVolume.onValueChanged.AddListener(AjustarVolume);
    }

    void AjustarVolume(float novoValor)
    {
        VolumeGloba.volumeMusica = novoValor;

        PlayerPrefs.SetFloat("volumeMusica", novoValor);
        PlayerPrefs.Save();
    }
}
