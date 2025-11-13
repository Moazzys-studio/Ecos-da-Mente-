using System.Collections;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CameraPistaController : MonoBehaviour
{
    [Header("Referência da Camera")]
    [Tooltip("Virtual Camera usada como visão normal/interativa.")]
    public CinemachineVirtualCamera virtualCamera;

    [Header("Layer da pista")]
    [Tooltip("Nome da layer usada pela pista.")]
    public string nomeLayerPista = "Pista";

    [Header("Visão Normal")]
    public float normalFOV = 27f;
    public float normalRotX = 4f;

    [Header("Visão Interativa (Pista)")]
    public float pistaFOV = 50f;
    public float pistaRotX = 30f;

    [Header("Tempos")]
    [Tooltip("Tempo da interpolação entre visões.")]
    public float tempoTransicao = 1f;

    [Tooltip("Tempo de espera após sair da Pista para voltar à visão normal.")]
    public float atrasoVoltarNormal = 4f;

    int layerPista;
    bool estaNaPista = false;
    Coroutine rotinaAtual;

    void Awake()
    {
        layerPista = LayerMask.NameToLayer(nomeLayerPista);

        
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer != layerPista)
            return;

        estaNaPista = true;

        if (rotinaAtual != null)
            StopCoroutine(rotinaAtual);

        rotinaAtual = StartCoroutine(
            TransicionarCamera(pistaFOV, pistaRotX)
        );
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.layer != layerPista)
            return;

        estaNaPista = false;

        if (rotinaAtual != null)
            StopCoroutine(rotinaAtual);

        rotinaAtual = StartCoroutine(VoltarDepoisDoAtraso());
    }

    IEnumerator VoltarDepoisDoAtraso()
    {
        float t = 0f;

        while (t < atrasoVoltarNormal)
        {
            // se voltar a encostar na pista, cancela o retorno
            if (estaNaPista)
                yield break;

            t += Time.deltaTime;
            yield return null;
        }

        if (!estaNaPista)
            yield return TransicionarCamera(normalFOV, normalRotX);
    }

    IEnumerator TransicionarCamera(float alvoFOV, float alvoRotX)
    {
        if (virtualCamera == null)
            yield break;

        float inicioFOV = virtualCamera.m_Lens.FieldOfView;
        Transform camTransform = virtualCamera.transform;
        Vector3 eulerInicial = camTransform.localEulerAngles;
        float inicioRotX = eulerInicial.x;

        float tempo = 0f;

        while (tempo < tempoTransicao)
        {
            tempo += Time.deltaTime;
            float t = Mathf.Clamp01(tempo / tempoTransicao);

            virtualCamera.m_Lens.FieldOfView =
                Mathf.Lerp(inicioFOV, alvoFOV, t);

            float novoX = Mathf.LerpAngle(inicioRotX, alvoRotX, t);
            camTransform.localEulerAngles =
                new Vector3(novoX, eulerInicial.y, eulerInicial.z);

            yield return null;
        }

        virtualCamera.m_Lens.FieldOfView = alvoFOV;
        camTransform.localEulerAngles =
            new Vector3(alvoRotX, eulerInicial.y, eulerInicial.z);
    }
}
