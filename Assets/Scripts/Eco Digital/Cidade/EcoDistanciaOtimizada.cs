using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Liga/desliga Animators e Lights com base na distância até o Eco.
/// Útil para cidades grandes: tudo que estiver longe fica desativado.
/// </summary>
public class EcoDistanciaOtimizada : MonoBehaviour
{
    [Header("Referência principal")]
    [Tooltip("Transform do Eco (jogador).")]
    public Transform eco;

    [Header("Configuração de distância")]
    [Tooltip("Raio em que os objetos ficam ATIVOS.")]
    [Min(0f)]
    public float raioAtivo = 25f;

    [Tooltip("Tempo (em segundos) entre cada atualização de checagem.")]
    [Min(0.01f)]
    public float intervaloAtualizacao = 0.5f;

    [Header("Busca automática")]
    [Tooltip("Procurar automaticamente todos os Animators da cena no Start.")]
    public bool procurarAnimatorsNoStart = true;

    [Tooltip("Procurar automaticamente todas as Lights da cena no Start.")]
    public bool procurarLightsNoStart = true;

    [Header("Listas (podem ser preenchidas à mão ou via Start)")]
    [Tooltip("Animators que serão ligados/desligados pela distância.")]
    public List<Animator> animatorsAlvo = new List<Animator>();

    [Tooltip("Lights que serão ligadas/desligadas pela distância.\n" +
             "Directional Lights são sempre mantidas ativas.")]
    public List<Light> lightsAlvo = new List<Light>();

    float _proxAtualizacao = 0f;

    void Start()
    {
        if (eco == null)
        {
            Debug.LogWarning("[EcoDistanciaOtimizada] Eco não definido no inspector.");
        }

        if (procurarAnimatorsNoStart)
        {
            Animator[] encontrados = FindObjectsOfType<Animator>();
            foreach (var a in encontrados)
            {
                if (a == null) continue;

                // ignora o próprio Eco, se tiver Animator
                if (eco != null && a.transform.IsChildOf(eco))
                    continue;

                if (!animatorsAlvo.Contains(a))
                    animatorsAlvo.Add(a);
            }
        }

        if (procurarLightsNoStart)
        {
            Light[] encontradas = FindObjectsOfType<Light>();
            foreach (var l in encontradas)
            {
                if (l == null) continue;

                // ignora luzes grudadas no Eco se quiser que fiquem sempre ativas
                if (eco != null && l.transform.IsChildOf(eco))
                    continue;

                // NÃO controla Directional Lights (sempre ligadas)
                if (l.type == LightType.Directional)
                    continue;

                if (!lightsAlvo.Contains(l))
                    lightsAlvo.Add(l);
            }
        }

        // força uma primeira atualização
        AtualizarAtivos();
        _proxAtualizacao = Time.time + intervaloAtualizacao;
    }

    void Update()
    {
        if (eco == null) return;

        if (Time.time >= _proxAtualizacao)
        {
            AtualizarAtivos();
            _proxAtualizacao = Time.time + intervaloAtualizacao;
        }
    }

    void AtualizarAtivos()
    {
        if (eco == null) return;

        float raioSqr = raioAtivo * raioAtivo;
        Vector3 posEco = eco.position;

        // Animators
        for (int i = animatorsAlvo.Count - 1; i >= 0; i--)
        {
            Animator a = animatorsAlvo[i];
            if (a == null)
            {
                animatorsAlvo.RemoveAt(i);
                continue;
            }

            float distSqr = (a.transform.position - posEco).sqrMagnitude;
            bool deveAtivar = distSqr <= raioSqr;

            if (a.enabled != deveAtivar)
                a.enabled = deveAtivar;
        }

        // Lights
        for (int i = lightsAlvo.Count - 1; i >= 0; i--)
        {
            Light l = lightsAlvo[i];
            if (l == null)
            {
                lightsAlvo.RemoveAt(i);
                continue;
            }

            // Garante que Directional Light nunca seja desligada,
            // mesmo que alguém coloque manualmente na lista.
            if (l.type == LightType.Directional)
            {
                if (!l.enabled) l.enabled = true;
                continue;
            }

            float distSqr = (l.transform.position - posEco).sqrMagnitude;
            bool deveAtivar = distSqr <= raioSqr;

            if (l.enabled != deveAtivar)
                l.enabled = deveAtivar;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (eco == null) return;

        Gizmos.color = new Color(0f, 1f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(eco.position, raioAtivo);
    }
#endif
}
