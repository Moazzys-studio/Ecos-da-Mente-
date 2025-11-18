using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class NpcTurnoController : MonoBehaviour
{
    [Header("Componentes")]
    [SerializeField] private NavMeshAgent agente;
    [SerializeField] private Animator animator;

    [Header("Parâmetros do Animator")]
    [SerializeField] private string boolWalking  = "Walking";
    [SerializeField] private string boolCharging = "Charging";

    private void Reset()
    {
        agente   = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (agente == null)
            agente = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void Update()
    {
        AtualizarWalkingPorVelocidade();
    }

    public IEnumerator AndarAte(Transform destino)
    {
        if (agente == null || destino == null)
            yield break;

        agente.isStopped = false;
        agente.ResetPath();
        agente.SetDestination(destino.position);

        SetWalking(true);
        SetCharging(false);

        while (agente.pathPending)
            yield return null;

        while (agente.remainingDistance > agente.stoppingDistance)
            yield return null;

        SetWalking(false);
    }

    public void SetIdle()
    {
        if (animator == null) return;
        animator.SetBool(boolWalking,  false);
        animator.SetBool(boolCharging, false);
    }

    public void SetCharging(bool charging)
    {
        if (animator == null) return;
        animator.SetBool(boolCharging, charging);
        if (charging)
            animator.SetBool(boolWalking, false);
    }

    public void AjustarRotacao(Quaternion rot)
    {
        transform.rotation = rot;
    }

    private void SetWalking(bool walking)
    {
        if (animator == null) return;
        animator.SetBool(boolWalking, walking);
    }

    private void AtualizarWalkingPorVelocidade()
    {
        if (agente == null || animator == null) return;

        bool andando = !agente.isStopped && agente.velocity.sqrMagnitude > 0.01f;
        animator.SetBool(boolWalking, andando);
    }
}
