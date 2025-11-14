using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class AtivarImagensAosPocos : MonoBehaviour
{
    [Header("Imagens que serão ativadas")]
    public List<GameObject> imagens; 

    [Header("Configuração")]
    public float tempoEntreAtivacoes = 0.5f;

    void Start()
    {
        StartCoroutine(AtivarImagens());
    }

    IEnumerator AtivarImagens()
    {
        foreach (GameObject img in imagens)
        {
            img.SetActive(true);  
            yield return new WaitForSeconds(tempoEntreAtivacoes);
        }
    }
}