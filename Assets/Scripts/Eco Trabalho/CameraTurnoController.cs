using UnityEngine;
using Cinemachine;

public class CameraTurnoController : MonoBehaviour
{
    [Header("Animators das Câmeras")]
    [SerializeField] private Animator cameraSupervisorAnimator;
    [SerializeField] private Animator cameraHellyAnimator;

    [Header("Virtual Cameras")]
    [Tooltip("Camera padrão do Eco (geral).")]
    [SerializeField] private CinemachineVirtualCamera camEco;

    [Tooltip("Camera usada no turno do Supervisor.")]
    [SerializeField] private CinemachineVirtualCamera camSupervisor;

    [Tooltip("Camera usada no turno da Helly.")]
    [SerializeField] private CinemachineVirtualCamera camHelly;

    [Header("Prioridades")]
    [Tooltip("Prioridade base das câmeras que NÃO estão no turno.")]
    [SerializeField] private int prioridadeBase = 10;

    [Tooltip("Prioridade da câmera do dono do turno.")]
    [SerializeField] private int prioridadeTurno = 20;

    [Header("Config")]
    [SerializeField] private string triggerCameraInterativa = "CameraInterativa";

    // --------------------------------------------------------------------
    // 1) Chamar ISSO quando começa o turno (pra trocar de Virtual Camera)
    // --------------------------------------------------------------------
    public void AtivarCameraDoDono(DonoTurno dono)
{
    // zera todas pra base
    SetPrioridade(camEco,        prioridadeBase);
    SetPrioridade(camSupervisor, prioridadeBase);
    SetPrioridade(camHelly,      prioridadeBase);

    switch (dono)
    {
        case DonoTurno.Supervisor:
            SetPrioridade(camSupervisor, prioridadeTurno);
            break;

        case DonoTurno.Helly:
            SetPrioridade(camHelly, prioridadeTurno);
            break;

        case DonoTurno.Nenhum:
        default:
            // Eco é a câmera padrão
            SetPrioridade(camEco, prioridadeTurno);
            break;
    }
}


    private void SetPrioridade(CinemachineVirtualCamera vcam, int prioridade)
    {
        if (vcam == null) return;
        vcam.Priority = prioridade;
    }

    // --------------------------------------------------------------------
    // 2) Disparar o trigger da animação interativa (já tinha)
    // --------------------------------------------------------------------
    public void DispararCameraInterativa(DonoTurno dono)
{
    Animator alvo = null;

    switch (dono)
    {
        case DonoTurno.Supervisor:
            alvo = cameraSupervisorAnimator;
            break;

        case DonoTurno.Helly:
            alvo = cameraHellyAnimator;
            break;

        default:
            return;
    }

    if (alvo == null || string.IsNullOrEmpty(triggerCameraInterativa))
    {
        Debug.LogWarning("[CameraTurnoController] Animator ou nome do trigger não configurado.");
        return;
    }

    Debug.Log($"[CameraTurnoController] Disparando trigger '{triggerCameraInterativa}' para {dono} no Animator '{alvo.name}'");

    alvo.ResetTrigger(triggerCameraInterativa);
    alvo.SetTrigger(triggerCameraInterativa);
}

}
