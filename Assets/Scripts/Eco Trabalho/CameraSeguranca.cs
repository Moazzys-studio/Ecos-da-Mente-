using UnityEngine;

[DisallowMultipleComponent]
public class CameraSeguranca : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Filho que gira em yaw (o pivot que deve rodar).")]
    public Transform pivotYaw;
    [Tooltip("Transform do Player (ou usa tag 'Player').")]
    public Transform player;

    [Header("Ativação")]
    public float raioAtivacao = 10f;
    [Tooltip("Ignorar diferença de altura ao mirar (só XZ).")]
    public bool ignorarAltura = true;

    [Header("Limites & Velocidades")]
    [Range(0f,180f)] public float anguloMax = 90f;  // limite ±
    public float velFollow = 360f;                  // deg/s
    public float velRetorno = 240f;                 // deg/s

    [Header("Ajustes")]
    [Tooltip("Offset no alvo (ex.: altura da cabeça).")]
    public Vector3 alvoOffset = new Vector3(0f, 1.6f, 0f);
    [Tooltip("Se o modelo veio invertido (frente para trás), marque.")]
    public bool flipForward = false;
    [Tooltip("Quando muito perto, congela a atualização para evitar jitter.")]
    public float distanciaMinimaTravar = 0.5f;

    [Header("Debug")]
    public bool desenharGizmos = true;
    [Tooltip("Inverte o sentido da rotação (se estiver virando para o lado oposto).")]
    public bool inverterDirecao = false;


    // runtime
    float _zeroYaw;                 // yaw local “de repouso” (do pivot)
    bool  _ativo;
    float _yawAtual;                // cache do yaw local ([-180,180])

    void Reset()
    {
        if (transform.childCount > 0) pivotYaw = transform.GetChild(0);
    }

    void Awake()
    {
        if (!pivotYaw)
        {
            Debug.LogWarning($"[{name}] Defina o pivotYaw (filho que gira no Y).");
            enabled = false; return;
        }
        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform;
        }

        _zeroYaw = Normaliza(pivotYaw.localEulerAngles.y);
        _yawAtual = _zeroYaw;
    }

    void Update()
    {
        if (!player || !pivotYaw) return;

        // 1) ativa por distância
        float dist = Vector3.Distance(player.position, pivotYaw.position);
        _ativo = dist <= raioAtivacao;

        if (_ativo)
        {
            // 2) direção pro player, no WORLD
            Vector3 dirW = (player.position + alvoOffset) - pivotYaw.position;
            if (ignorarAltura) dirW.y = 0f;
            // muito perto -> não atualiza (evita jitter de atan2)
            if (dirW.sqrMagnitude > distanciaMinimaTravar * distanciaMinimaTravar)
            {
                // 3) converte para ESPAÇO LOCAL DO PRÓPRIO PIVOT
                Vector3 dirL = pivotYaw.InverseTransformDirection(dirW.normalized);

                // 4) calcula yaw local contínuo (em graus) relativo ao forward do pivot
                //    atan2(x,z) dá rotação em torno de Y local.
                float ff    = flipForward ? -1f : 1f;           // corrige frente invertida
                float sinal = inverterDirecao ? -1f : 1f;       // INVERTE O SENTIDO
                float yawAlvo = Mathf.Atan2(dirL.x * ff * sinal,
                                            dirL.z * ff) * Mathf.Rad2Deg;

                // 5) aplica limite ±anguloMax ao DESVIO em relação ao zero local
                float desvio = Mathf.Clamp(yawAlvo, -anguloMax, anguloMax);

                // 6) alvo final de yaw local = zeroYaw + desvio
                float yawFinal = _zeroYaw + desvio;

                // 7) aproxima o ângulo atual até o alvo (seguindo o passo do jogo)
                _yawAtual = Mathf.MoveTowardsAngle(_yawAtual, yawFinal, velFollow * Time.deltaTime);

                // 8) aplica no pivot (somente Y local)
                Vector3 eul = pivotYaw.localEulerAngles;
                eul.y = Unwrap(_yawAtual); // volta para [0..360) pra setar
                pivotYaw.localEulerAngles = eul;
            }
        }
        else
        {
            // volta pro zero local suavemente
            _yawAtual = Mathf.MoveTowardsAngle(_yawAtual, _zeroYaw, velRetorno * Time.deltaTime);
            Vector3 eul = pivotYaw.localEulerAngles;
            eul.y = Unwrap(_yawAtual);
            pivotYaw.localEulerAngles = eul;
        }
    }

    // [-180,180]
    float Normaliza(float ang)
    {
        ang %= 360f;
        if (ang > 180f) ang -= 360f;
        if (ang < -180f) ang += 360f;
        return ang;
    }
    // [0,360)
    float Unwrap(float ang)
    {
        ang %= 360f;
        if (ang < 0f) ang += 360f;
        return ang;
    }

    void OnDrawGizmosSelected()
    {
        if (!desenharGizmos) return;
        Transform p = pivotYaw ? pivotYaw : transform;

        Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireSphere(p.position, raioAtivacao);

        if (player)
        {
            Vector3 alvo = player.position + alvoOffset;
            if (ignorarAltura) alvo.y = p.position.y;
            Gizmos.color = Color.white;
            Gizmos.DrawLine(p.position, alvo);
            Gizmos.DrawWireSphere(alvo, 0.06f);
        }

        // cone ±anguloMax no plano XZ local
        Vector3 fwd = (pivotYaw ? pivotYaw.forward : transform.forward) * (flipForward ? -1f : 1f);
        fwd.y = 0f; if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
        float r = Mathf.Min(2f, raioAtivacao * 0.5f);
        Quaternion qL = Quaternion.AngleAxis(-anguloMax, Vector3.up);
        Quaternion qR = Quaternion.AngleAxis(anguloMax,  Vector3.up);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawLine(p.position, p.position + (qL * fwd).normalized * r);
        Gizmos.DrawLine(p.position, p.position + (qR * fwd).normalized * r);
    }
}
