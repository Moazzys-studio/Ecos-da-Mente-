using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPS : MonoBehaviour
{
     GUIStyle estilo = new GUIStyle();

    void Start()
    {
        estilo.fontSize = 50;          // Aumente aqui o tamanho da fonte
        estilo.normal.textColor = Color.white; // Cor do texto (opcional)
    }

    void OnGUI()
    {
        float fps = 1.0f / Time.deltaTime;
        GUI.Label(new Rect(50, 50, 300, 80), fps.ToString("F0") + " FPS", estilo);
    }
}
