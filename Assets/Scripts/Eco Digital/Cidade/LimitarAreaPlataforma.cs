using UnityEngine;

/// <summary>
/// Versão simples com margem de borda suave:
/// - Cola o player no topo da plataforma sob os pés (raycast para baixo).
/// - Impede queda voltando ao último ponto seguro.
/// - NÃO clampa X/Z nem mexe na velocidade planar.
/// - Adiciona um "colchão" (edgeMargin) perto da borda da plataforma atual:
///   puxa suavemente o player para dentro APENAS se não houver outra plataforma colada além da borda.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LimitarAreaPlataforma : MonoBehaviour
{
    [Header("Detecção de chão")]
    [Tooltip("Camadas consideradas 'plataforma/solo'.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("Altura do ponto de origem do raycast (acima dos pés).")]
    [SerializeField, Min(0.05f)] private float rayOriginHeight = 0.5f;

    [Tooltip("Alcance máximo do raycast para baixo.")]
    [SerializeField, Min(0.2f)] private float rayDistance = 2.0f;

    [Tooltip("Suavização para colar no Y do topo (0 = teleporta).")]
    [SerializeField, Min(0f)] private float snapSmooth = 0f;

    [Tooltip("Zera a velocidade vertical ao colar no topo (evita 'quicar').")]
    [SerializeField] private bool zeroVelYOnSnap = true;

    [Header("Anti-queda")]
    [Tooltip("Se não houver chão sob os pés, voltar para o último ponto seguro.")]
    [SerializeField] private bool preventFall = true;

    [Tooltip("Tolerância de variação de altura para atualizar o último ponto seguro.")]
    [SerializeField, Min(0f)] private float maxStepHeight = 0.6f;

    [Header("Margem de Borda Suave")]
    [Tooltip("Distância (em metros) a partir da borda onde começa o 'colchão'.")]
    [SerializeField, Min(0f)] private float edgeMargin = 0.25f;

    [Tooltip("Força (m/s) do empurrão suave para dentro quando dentro do edgeMargin.")]
    [SerializeField, Min(0f)] private float edgePushStrength = 2.5f;

    [Tooltip("Quanto além da borda checar por outra plataforma (m).")]
    [SerializeField, Min(0.02f)] private float beyondEdgeProbe = 0.12f;

    [Tooltip("Altura do raycast ao checar plataforma além da borda.")]
    [SerializeField, Min(0.2f)] private float edgeProbeRayHeight = 1.0f;

    private Rigidbody rb;
    private Vector3 lastSafePos;
    private bool hasSafePos = false;

    // plataforma atual sob os pés (do último hit)
    private Transform currentPlatform;     // root Transform que representa a plataforma atual
    private BoxCollider currentBox;        // se existir
    private MeshRenderer currentRenderer;  // se existir

#if UNITY_600_OR_NEWER
    private Vector3 Vel { get => rb.linearVelocity; set => rb.linearVelocity = value; }
#else
    private Vector3 Vel { get => rb.velocity; set => rb.velocity = value; }
#endif

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Vector3 pos = rb.position;

        // 1) Raycast para baixo a partir de um ponto um pouco acima dos pés
        Vector3 origin = pos + Vector3.up * rayOriginHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            // Atualiza plataforma atual e componentes
            UpdateCurrentPlatform(hit.transform);

            float targetY = hit.point.y;

            // Atualiza último ponto seguro se o degrau não for absurdo
            if (!hasSafePos || Mathf.Abs(targetY - lastSafePos.y) <= maxStepHeight)
            {
                lastSafePos = new Vector3(pos.x, targetY, pos.z);
                hasSafePos = true;
            }

            // 2) Cola no topo (apenas Y). X/Z ficam por conta da física
            float newY = (snapSmooth <= 0f)
                ? targetY
                : Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-snapSmooth * Time.fixedDeltaTime));

            Vector3 snapped = new Vector3(pos.x, newY, pos.z);

            // 3) Margem de borda suave (se temos info geométrica da plataforma)
            if (edgeMargin > 0f && currentPlatform != null && (currentBox != null || currentRenderer != null))
            {
                Vector3 pushed = ApplySoftEdgeMargin(snapped, Time.fixedDeltaTime);
                rb.MovePosition(pushed);
            }
            else
            {
                rb.MovePosition(snapped);
            }

            if (zeroVelYOnSnap)
            {
                var v = Vel; v.y = 0f; Vel = v;
            }
        }
        else if (preventFall && hasSafePos)
        {
            // Sem chão: volta para o último ponto seguro (inclui X/Z e Y)
            rb.MovePosition(lastSafePos);

            if (zeroVelYOnSnap)
            {
                var v = Vel; v.y = 0f; Vel = v;
            }

            // Plataforma atual é desconhecida aqui; será atualizada no próximo hit
        }
        // Caso contrário (sem chão e sem lastSafePos), não fazemos nada.
    }

    /// <summary>
    /// Atualiza referências da plataforma atual a partir de um Transform atingido.
    /// Sobe alguns níveis na hierarquia para achar um root estável.
    /// </summary>
    private void UpdateCurrentPlatform(Transform t)
    {
        if (!t) { currentPlatform = null; currentBox = null; currentRenderer = null; return; }

        // Sobe até 5 níveis tentando achar um "root" com collider/renderer
        Transform p = t;
        for (int i = 0; i < 5 && p != null; i++)
        {
            if (p.GetComponent<BoxCollider>() || p.GetComponent<MeshRenderer>()) break;
            p = p.parent;
        }

        currentPlatform = p ?? t;
        currentBox = currentPlatform.GetComponent<BoxCollider>();
        currentRenderer = currentPlatform.GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Aplica um empurrão suave para dentro, caso o player esteja dentro do "edgeMargin".
    /// Não empurra se houver outra plataforma colada logo além da borda naquele lado.
    /// Mantém Y já ajustado previamente.
    /// </summary>
    private Vector3 ApplySoftEdgeMargin(Vector3 worldPos, float dt)
    {
        // 1) Obter retângulo do topo em espaço local da plataforma atual
        if (!TryGetTopRectLocal(currentPlatform, out Vector3 centerL, out Vector2 sizeL, out float topLocalY))
            return worldPos;

        // 2) Converter posição do player para local da plataforma
        Vector3 local = currentPlatform.InverseTransformPoint(worldPos);

        float halfX = sizeL.x * 0.5f;
        float halfZ = sizeL.y * 0.5f;

        // Converter a edgeMargin (metros no mundo) para "margem local" em X e Z
        Vector3 scl = currentPlatform.lossyScale;
        float marginLocalX = edgeMargin / Mathf.Max(Mathf.Abs(scl.x), 0.0001f);
        float marginLocalZ = edgeMargin / Mathf.Max(Mathf.Abs(scl.z), 0.0001f);

        // Limites "externos" (borda real considerando o raio ~ mínimo) — aqui não usamos raio, só margem
        float minX = centerL.x - halfX;
        float maxX = centerL.x + halfX;
        float minZ = centerL.z - halfZ;
        float maxZ = centerL.z + halfZ;

        // Limites "internos" onde começa o colchão
        float inMinX = minX + marginLocalX;
        float inMaxX = maxX - marginLocalX;
        float inMinZ = minZ + marginLocalZ;
        float inMaxZ = maxZ - marginLocalZ;

        Vector2 pushDirLocal = Vector2.zero;

        // Se está na faixa de margem (entre minX..inMinX OU inMaxX..maxX), calculamos a direção de empurrar
        if (local.x < inMinX && local.x >= minX)
        {
            // Antes de empurrar, verificamos se há plataforma logo "além da borda" (lado -X)
            if (!HasAdjacentBeyondEdgeLocal(new Vector3(minX, local.y, local.z), Vector3.left))
                pushDirLocal.x += 1f; // empurra +X (para dentro)
        }
        else if (local.x > inMaxX && local.x <= maxX)
        {
            if (!HasAdjacentBeyondEdgeLocal(new Vector3(maxX, local.y, local.z), Vector3.right))
                pushDirLocal.x -= 1f; // empurra -X
        }

        if (local.z < inMinZ && local.z >= minZ)
        {
            if (!HasAdjacentBeyondEdgeLocal(new Vector3(local.x, local.y, minZ), Vector3.back))
                pushDirLocal.y += 1f; // empurra +Z
        }
        else if (local.z > inMaxZ && local.z <= maxZ)
        {
            if (!HasAdjacentBeyondEdgeLocal(new Vector3(local.x, local.y, maxZ), Vector3.forward))
                pushDirLocal.y -= 1f; // empurra -Z
        }

        if (pushDirLocal == Vector2.zero) return worldPos; // sem empurrão

        // Intensidade proporcional ao quão fundo na margem o player está
        float depthX = 0f;
        if (pushDirLocal.x > 0f) depthX = Mathf.InverseLerp(minX, inMinX, local.x);            // 0..1 (0 = na borda, 1 = saiu da margem)
        else if (pushDirLocal.x < 0f) depthX = 1f - Mathf.InverseLerp(inMaxX, maxX, local.x);  // 0..1

        float depthZ = 0f;
        if (pushDirLocal.y > 0f) depthZ = Mathf.InverseLerp(minZ, inMinZ, local.z);
        else if (pushDirLocal.y < 0f) depthZ = 1f - Mathf.InverseLerp(inMaxZ, maxZ, local.z);

        float depth = Mathf.Clamp01(Mathf.Max(depthX, depthZ));

        // Direção mundial do empurrão
        Vector3 dirWorld =
            (currentPlatform.TransformDirection(new Vector3(pushDirLocal.x, 0f, pushDirLocal.y))).normalized;

        // Deslocamento suave (metros) — não altera Y (já foi colado)
        Vector3 delta = dirWorld * (edgePushStrength * depth * dt);
        return new Vector3(worldPos.x + delta.x, worldPos.y, worldPos.z + delta.z);
    }

    /// <summary>
    /// Verifica se existe outra plataforma colada logo além da borda correspondente.
    /// Recebe um ponto em LOCAL (na borda) e uma direção LOCAL para fora (±X/±Z),
    /// e faz um raycast para baixo em um ponto "um pouco além".
    /// </summary>
    private bool HasAdjacentBeyondEdgeLocal(Vector3 edgeLocalPoint, Vector3 outwardLocalDir)
    {
        // Ponto um pouco além da borda em espaço LOCAL
        Vector3 probeLocal = edgeLocalPoint + outwardLocalDir.normalized * LocalProbeOffset(outwardLocalDir);

        // Converte para mundo
        Vector3 probeWorld = currentPlatform.TransformPoint(probeLocal);
        Vector3 rayStart = probeWorld + Vector3.up * edgeProbeRayHeight * 0.5f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, edgeProbeRayHeight, groundMask, QueryTriggerInteraction.Ignore))
        {
            // Se bateu em alguma superfície caminhável, consideramos que há "plataforma colada"
            // (não precisamos distinguir se é a mesma ou outra — a ideia é não puxar para dentro).
            return true;
        }
        return false;
    }

    /// <summary>
    /// Retorna um offset local consistente em metros ao longo do eixo correto,
    /// convertendo o beyondEdgeProbe do mundo para o espaço local (respeitando escala).
    /// </summary>
    private float LocalProbeOffset(Vector3 outwardLocalDir)
    {
        Vector3 s = currentPlatform.lossyScale;
        // decide eixo dominante (X ou Z)
        if (Mathf.Abs(outwardLocalDir.x) > Mathf.Abs(outwardLocalDir.z))
            return beyondEdgeProbe / Mathf.Max(Mathf.Abs(s.x), 0.0001f);
        else
            return beyondEdgeProbe / Mathf.Max(Mathf.Abs(s.z), 0.0001f);
    }

    /// <summary>
    /// Calcula o retângulo do topo (centro e tamanho em XZ, em espaço LOCAL da plataforma).
    /// Funciona com BoxCollider (preferível) ou MeshRenderer.
    /// </summary>
    private bool TryGetTopRectLocal(Transform platform, out Vector3 centerLocal, out Vector2 sizeLocal, out float topLocalY)
    {
        centerLocal = Vector3.zero; sizeLocal = Vector2.zero; topLocalY = 0f;
        if (!platform) return false;

        var bc = platform.GetComponent<BoxCollider>();
        if (bc)
        {
            centerLocal = bc.center;
            sizeLocal = new Vector2(bc.size.x, bc.size.z);
            topLocalY = bc.center.y + bc.size.y * 0.5f;
            return true;
        }

        var mr = platform.GetComponent<MeshRenderer>();
        if (mr)
        {
            Bounds wb = mr.bounds;
            Vector3 c = wb.center, e = wb.extents;

            // Converte as 8 pontas para local
            Vector3[] corners = new Vector3[8];
            int i = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        corners[i++] = platform.InverseTransformPoint(c + Vector3.Scale(e, new Vector3(sx, sy, sz)));

            Vector3 min = corners[0], max = corners[0];
            for (int k = 1; k < corners.Length; k++) { min = Vector3.Min(min, corners[k]); max = Vector3.Max(max, corners[k]); }

            centerLocal = (min + max) * 0.5f;
            sizeLocal = new Vector2(max.x - min.x, max.z - min.z);

            Vector3 topWorld = wb.center + Vector3.up * wb.extents.y;
            topLocalY = platform.InverseTransformPoint(topWorld).y;
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        Vector3 pos = Application.isPlaying ? rb.position : transform.position;

        // Ray principal
        Vector3 origin = pos + Vector3.up * rayOriginHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + Vector3.down * rayDistance);

        // Visualização da margem (se possível)
        if (Application.isPlaying && currentPlatform && (currentBox || currentRenderer))
        {
            if (TryGetTopRectLocal(currentPlatform, out Vector3 centerL, out Vector2 sizeL, out float topLocalY))
            {
                Vector3 s = currentPlatform.lossyScale;
                float marginLocalX = edgeMargin / Mathf.Max(Mathf.Abs(s.x), 0.0001f);
                float marginLocalZ = edgeMargin / Mathf.Max(Mathf.Abs(s.z), 0.0001f);

                float halfX = sizeL.x * 0.5f;
                float halfZ = sizeL.y * 0.5f;

                Vector3 c = new Vector3(centerL.x, topLocalY, centerL.z);

                // retângulo externo (borda real)
                Vector3[] ext =
                {
                    new Vector3(+halfX,0f,+halfZ), new Vector3(-halfX,0f,+halfZ),
                    new Vector3(-halfX,0f,-halfZ), new Vector3(+halfX,0f,-halfZ),
                };
                // retângulo interno (início da margem)
                Vector3[] inn =
                {
                    new Vector3(+halfX - marginLocalX,0f,+halfZ - marginLocalZ),
                    new Vector3(-halfX + marginLocalX,0f,+halfZ - marginLocalZ),
                    new Vector3(-halfX + marginLocalX,0f,-halfZ + marginLocalZ),
                    new Vector3(+halfX - marginLocalX,0f,-halfZ + marginLocalZ),
                };

                // desenha
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = currentPlatform.TransformPoint(c + ext[i]);
                    Vector3 b = currentPlatform.TransformPoint(c + ext[(i + 1) % 4]);
                    Gizmos.DrawLine(a, b);
                }

                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.55f);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = currentPlatform.TransformPoint(c + inn[i]);
                    Vector3 b = currentPlatform.TransformPoint(c + inn[(i + 1) % 4]);
                    Gizmos.DrawLine(a, b);
                }
            }
        }
    }
#endif
}
