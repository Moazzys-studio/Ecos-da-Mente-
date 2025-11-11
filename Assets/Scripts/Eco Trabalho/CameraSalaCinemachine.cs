using UnityEngine;
using Cinemachine;

public class CameraSalaVCam : MonoBehaviour
{
    [Header("VCam (Body = Transposer, LockToTargetOnAssign)")]
    [SerializeField] private CinemachineVirtualCamera vcam;

    [Header("Visão FORA")]
    [SerializeField] private float rotX_Fora = 0f;
    [SerializeField] private Vector3 followOffset_Fora = new Vector3(0f, 2f, 11f);

    [Header("Visão DENTRO")]
    [SerializeField] private float rotX_Dentro = 20f;
    [SerializeField] private Vector3 followOffset_Dentro = new Vector3(0f, 6f, 11f);

    [Header("Interpolação")]
    [SerializeField, Min(0f)] private float duracao = 0.6f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0,0,1,1);

    private CinemachineTransposer transposer;
    private Coroutine tween;

    private void Awake()
    {
        if (!vcam) vcam = GetComponent<CinemachineVirtualCamera>();
        transposer = vcam ? vcam.GetCinemachineComponent<CinemachineTransposer>() : null;
        if (!transposer)
        {
            Debug.LogError("[CameraSalaVCam] A VCam precisa ter Body = Transposer.");
            enabled = false;
            return;
        }
        // estado inicial: FORA
        AplicarInstantaneo(fora:true);
    }

    /// <summary>
    /// Chame com true = DENTRO, false = FORA.
    /// </summary>
    public void OnEstadoSala(bool dentro)
    {
        if (dentro) IniciarTween(followOffset_Dentro, rotX_Dentro);
        else        IniciarTween(followOffset_Fora,  rotX_Fora);
    }

    private void IniciarTween(Vector3 alvoOffset, float alvoRotX)
    {
        if (tween != null) StopCoroutine(tween);
        tween = StartCoroutine(TweenCamera(
            transposer.m_FollowOffset, alvoOffset,
            GetRotX(), alvoRotX,
            duracao
        ));
    }

    private System.Collections.IEnumerator TweenCamera(
        Vector3 fromOff, Vector3 toOff, float fromX, float toX, float tTotal)
    {
        if (tTotal <= 0f)
        {
            AplicarInstantaneo(toOff, toX);
            yield break;
        }

        float t = 0f;
        float dX = ShortestAngleDelta(fromX, toX);

        while (t < 1f)
        {
            t += Time.deltaTime / tTotal;
            float k = easing.Evaluate(Mathf.Clamp01(t));

            transposer.m_FollowOffset = Vector3.LerpUnclamped(fromOff, toOff, k);

            float rotX = fromX + dX * k;
            var e = vcam.transform.eulerAngles;
            vcam.transform.rotation = Quaternion.Euler(rotX, e.y, e.z);

            yield return null;
        }

        AplicarInstantaneo(toOff, toX);
        tween = null;
    }

    private void AplicarInstantaneo(bool fora)
    {
        if (fora) AplicarInstantaneo(followOffset_Fora, rotX_Fora);
        else      AplicarInstantaneo(followOffset_Dentro, rotX_Dentro);
    }

    private void AplicarInstantaneo(Vector3 off, float rotX)
    {
        transposer.m_FollowOffset = off;
        var e = vcam.transform.eulerAngles;
        vcam.transform.rotation = Quaternion.Euler(rotX, e.y, e.z);
    }

    private float GetRotX()
    {
        float x = vcam.transform.eulerAngles.x % 360f;
        return x < 0 ? x + 360f : x;
    }

    private static float ShortestAngleDelta(float from, float to)
    {
        float delta = (to - from) % 360f;
        if (delta > 180f) delta -= 360f;
        if (delta < -180f) delta += 360f;
        return delta;
    }
}
