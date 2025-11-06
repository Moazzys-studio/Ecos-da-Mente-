using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class FPS : MonoBehaviour
{
    public TextMeshProUGUI textoFPS; 
    public float tempoAtualizacao = 0.2f;

    private float contadorTempo;

    void Awake()
    {
        DontDestroyOnLoad(gameObject); 
    }

    void Update()
    {
        contadorTempo += Time.deltaTime;

        if (contadorTempo >= tempoAtualizacao)
        {
            float fps = 1f / Time.deltaTime;
            textoFPS.text = fps.ToString("F0") + " FPS";
            contadorTempo = 0f;
        }
    }
}
