using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class VelocidadeText : MonoBehaviour
{
     [Header("UI")]
    public Slider slider;            
    public TMP_Text valueText;          
    [Header("Configuração")]
    public float minVel = 0.02f;        
    public float maxVel = 0.2f;         

    private const string PREF_KEY = "TextSpeed";

    private void Awake()
    {
        // Carrega velocidade salva ou usa a padrão
        float savedSpeed = PlayerPrefs.GetFloat(PREF_KEY, variaveis_menu.velocidadeLetra);

        if (slider != null)
        {
            float sliderValue = Mathf.InverseLerp(minVel, maxVel, savedSpeed);
            slider.value = sliderValue;

            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        variaveis_menu.velocidadeLetra = savedSpeed;

        AtualizarTexto(savedSpeed);
    }

    private void OnSliderChanged(float value)
    {
        // Converte slider (0 → 1) para velocidade real
        float velocidade = Mathf.Lerp(minVel, maxVel, value);

        variaveis_menu.velocidadeLetra = velocidade;

        PlayerPrefs.SetFloat(PREF_KEY, velocidade);

        AtualizarTexto(velocidade);
    }

    private void AtualizarTexto(float velocidade)
    {
        if (valueText != null)
            valueText.text = $"{velocidade:F2}";
    }
}
