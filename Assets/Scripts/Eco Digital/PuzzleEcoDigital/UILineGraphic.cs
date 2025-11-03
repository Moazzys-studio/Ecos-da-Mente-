using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Desenha uma linha 2D (polilinha) na UI (Canvas) com espessura.
/// Use: defina os pontos via SetPoints(List<Vector2>), onde os pontos estão em
/// coordenadas LOCAIS do RectTransform deste Graphic.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UILineGraphic : Graphic
{
    [Header("Linha")]
    [Tooltip("Espessura da linha em unidades de UI (pixels).")]
    [SerializeField] private float thickness = 6f;

    [Tooltip("Arredondar as pontas da linha.")]
    [SerializeField] private bool roundCaps = true;

    [Tooltip("Arredondar junções entre segmentos.")]
    [SerializeField] private bool roundJoins = true;

    private readonly List<Vector2> points = new();

    public float Thickness
    {
        get => thickness;
        set { thickness = Mathf.Max(0.1f, value); SetVerticesDirty(); }
    }

    /// <summary>Substitui todos os pontos e redesenha.</summary>
    public void SetPoints(IReadOnlyList<Vector2> pts)
    {
        points.Clear();
        if (pts != null) points.AddRange(pts);
        SetVerticesDirty();
    }

    /// <summary>Limpa os pontos (não desenha nada).</summary>
    public void ClearPoints()
    {
        points.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points == null || points.Count < 2 || color.a <= 0.0001f || thickness <= 0.01f)
            return;

        float half = thickness * 0.5f;

        // Desenha segmento a segmento
        Vector2 prevDir = Vector2.zero;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 p0 = points[i];
            Vector2 p1 = points[i + 1];

            Vector2 dir = (p1 - p0);
            if (dir.sqrMagnitude < 0.001f) continue;
            dir.Normalize();
            Vector2 normal = new Vector2(-dir.y, dir.x); // 90°

            // Largura lateral
            Vector2 v0 = p0 + normal * half;
            Vector2 v1 = p0 - normal * half;
            Vector2 v2 = p1 - normal * half;
            Vector2 v3 = p1 + normal * half;

            // Junções arredondadas (simples)
            if (roundJoins && i > 0)
            {
                // poderia expandir ou suavizar; mantemos simples para performance
            }

            // Quad do segmento
            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;

            int idx = vh.currentVertCount;

            vert.position = v0; vh.AddVert(vert);
            vert.position = v1; vh.AddVert(vert);
            vert.position = v2; vh.AddVert(vert);
            vert.position = v3; vh.AddVert(vert);

            vh.AddTriangle(idx + 0, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx + 0);

            prevDir = dir;
        }

        // Pontas arredondadas (opcional simples – discos aproximados)
        if (roundCaps && points.Count >= 2)
        {
            AddRoundCap(vh, points[0], (points[1] - points[0]).normalized, half, true);
            AddRoundCap(vh, points[^1], (points[^1] - points[^2]).normalized, half, false);
        }
    }

    private void AddRoundCap(VertexHelper vh, Vector2 center, Vector2 dir, float radius, bool start)
    {
        // Semelhante a semi-círculo; steps baixos para custo baixo
        int steps = 8;
        float ang0 = Mathf.Atan2(dir.y, dir.x) + (start ? Mathf.PI : 0f);
        float delta = Mathf.PI / steps;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        int centerIdx = vh.currentVertCount;
        vert.position = center; vh.AddVert(vert);

        for (int i = 0; i <= steps; i++)
        {
            float a = ang0 + (start ? -i : i) * delta;
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            vert.position = p;
            vh.AddVert(vert);

            if (i >= 1)
            {
                vh.AddTriangle(centerIdx, centerIdx + i, centerIdx + i + 1);
            }
        }
    }
}
