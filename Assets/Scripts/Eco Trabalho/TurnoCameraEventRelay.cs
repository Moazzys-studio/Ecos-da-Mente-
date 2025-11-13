using UnityEngine;

public class TurnoCameraEventRelay : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TurnosManager turnosManager;

    [Header("Configuração do Turno")]
    [Tooltip("Número do turno que esta câmera deve disparar (1, 2 ou 3).")]
    [SerializeField] private int numeroTurno = 1;

    private void Reset()
    {
        turnosManager = FindFirstObjectByType<TurnosManager>();
    }

    /// <summary>
    /// Chame ESTE método no Animation Event da sua animação de câmera
    /// (no último frame da cutscene antes do turno começar).
    /// </summary>
    public void AnimationEvent_IniciarTurno()
    {
        if (turnosManager == null)
        {
            Debug.LogWarning("[TurnoCameraEventRelay] TurnosManager não atribuído.");
            return;
        }

        turnosManager.IniciarTurnoPorCamera(numeroTurno);
    }
}
