using System;
using System.Collections.Generic;
using UnityEngine;

public enum TipoGesto
{
    Nenhum = 0,
    X = 1,
    V = 2,
    Circulo = 3,
    Quadrado = 4
}

public class GestorGestos : MonoBehaviour
{
    public static GestorGestos I { get; private set; }

    [Header("Depuração")]
    [SerializeField] private bool logReconhecimentos = false;

    // Lista de alvos (projéteis) registrados — destruímos quando um gesto é reconhecido.
    private readonly List<AlvoProjetilPorGesto> _alvos = new List<AlvoProjetilPorGesto>();

    public event Action<TipoGesto> OnGestoReconhecido;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    public void RegistrarAlvo(AlvoProjetilPorGesto alvo)
    {
        if (alvo != null && !_alvos.Contains(alvo)) _alvos.Add(alvo);
    }
    public void RemoverAlvo(AlvoProjetilPorGesto alvo)
    {
        if (alvo != null) _alvos.Remove(alvo);
    }

    /// <summary>Chame isso quando um gesto for reconhecido pelo desenho.</summary>
    public void AnunciarGesto(TipoGesto gesto)
    {
        if (logReconhecimentos) Debug.Log($"[GestorGestos] Reconhecido: {gesto}");
        OnGestoReconhecido?.Invoke(gesto);

        // Destrói todos os projéteis que pedem esse gesto.
        if (gesto != TipoGesto.Nenhum)
        {
            // Copia pra evitar issues com lista mudando durante o loop
            var snapshot = _alvos.ToArray();
            foreach (var alvo in snapshot)
            {
                if (alvo == null) continue;
                if (alvo.gestoNecessario == gesto) alvo.Destruir();
            }
        }
    }
}

public static class ReconhecedorGestosSimples
{
    /// <summary>
    /// Heurísticas suaves:
    /// - V: traço aberto com um ápice bem marcado (1 vértice agudo) e dois “braços”.
    /// - Círculo: traço fechado, raio ~constante (baixa variação).
    /// - Quadrado: traço fechado, ~4 cantos com ângulos ~90° e caixa envolvente quase quadrada.
    /// - X: **precisa de 2 traços** quase retilíneos que se cruzam (feito fora, via método auxiliar).
    /// </summary>
    public static TipoGesto ReconhecerDeUmTraço(List<Vector2> pts)
    {
        if (pts == null || pts.Count < 8) return TipoGesto.Nenhum;

        // Simplifica levemente o traço para reduzir ruído
        var simplificados = RamerDouglasPeucker(pts, 2.0f);

        bool fechado = EhFechado(pts, out float perimetro);
        var bbox = BoundingBox(pts, out float width, out float height);
        Vector2 centro = (bbox.min + bbox.max) * 0.5f;

        // CÍRCULO: fechado, variação de raio pequena
        if (fechado)
        {
            float media, desvio;
            RaioStats(pts, centro, out media, out desvio);
            float coefVar = (media > 1e-3f) ? (desvio / media) : 999f;

            // QUADRADO: ~4 cantos marcados e aspecto ~1:1
            var cantos = ContarCantos(simplificados, 35f); // ângulo mínimo ~35°
            float aspect = (height > 1e-3f) ? width / height : 999f;

            bool possivelQuadrado = (cantos >= 3 && cantos <= 6) && (aspect > 0.65f && aspect < 1.35f);
            bool possivelCirculo  = (coefVar < 0.28f); // tolerante

            if (possivelQuadrado && !possivelCirculo) return TipoGesto.Quadrado;
            if (possivelCirculo && !possivelQuadrado) return TipoGesto.Circulo;

            // Ambíguo: escolhe pelo que está mais “forte”
            if (possivelQuadrado && possivelCirculo)
                return (coefVar < 0.18f) ? TipoGesto.Circulo : TipoGesto.Quadrado;
        }
        else
        {
            // V: traço aberto com UM vértice agudo e dois ramos com boa extensão
            var (apiceIndex, angApice) = EncontrarApexMaisAgudo(simplificados);
            if (apiceIndex > 0)
            {
                bool angAgudo = angApice <= 80f; // tolerante
                // braços razoáveis (comprimento mínimo fracionário do total)
                float total = Comprimento(pts);
                float esq = ComprimentoSegmento(pts, 0, apiceIndex);
                float dir = ComprimentoSegmento(pts, apiceIndex, pts.Count - 1);
                bool bracosOk = (esq > total * 0.25f) && (dir > total * 0.25f);

                if (angAgudo && bracosOk) return TipoGesto.V;
            }
        }

        return TipoGesto.Nenhum;
    }

    /// <summary>Reconhece X a partir de DOIS traços: ambos quase retilíneos e com interseção.</summary>
    public static bool TentaReconhecerX(List<Vector2> ptsA, List<Vector2> ptsB)
    {
        if (ptsA == null || ptsB == null) return false;
        if (ptsA.Count < 4 || ptsB.Count < 4) return false;

        var linhaA = MelhorLinha(ptsA);
        var linhaB = MelhorLinha(ptsB);

        // Ângulo entre as linhas
        float dot = Mathf.Clamp(Vector2.Dot(linhaA.dir.normalized, linhaB.dir.normalized), -1f, 1f);
        float ang = Mathf.Acos(dot) * Mathf.Rad2Deg;
        float angRel = Mathf.Min(ang, 180f - ang); // 0..90

        bool linhasRetas = (linhaA.retaScore > 0.85f && linhaB.retaScore > 0.85f);
        bool anguloBom = (angRel > 25f && angRel < 85f);

        // Checa interseção aproximada dos segmentos
        bool cruzam = SegmentosSeCruzam(linhaA.p0, linhaA.p1, linhaB.p0, linhaB.p1);
        return linhasRetas && anguloBom && cruzam;
    }

    // ---------- Utilitários geométricos abaixo ----------

    public static float Comprimento(List<Vector2> pts)
    {
        float s = 0f;
        for (int i = 1; i < pts.Count; i++) s += Vector2.Distance(pts[i - 1], pts[i]);
        return s;
    }

    public static float ComprimentoSegmento(List<Vector2> pts, int i0, int i1)
    {
        if (i0 < 0) i0 = 0;
        if (i1 >= pts.Count) i1 = pts.Count - 1;
        float s = 0f;
        for (int i = i0 + 1; i <= i1; i++) s += Vector2.Distance(pts[i - 1], pts[i]);
        return s;
    }

    public static (Vector2 min, Vector2 max) BoundingBox(List<Vector2> pts, out float w, out float h)
    {
        Vector2 min = pts[0], max = pts[0];
        foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        w = Mathf.Max(1e-4f, max.x - min.x);
        h = Mathf.Max(1e-4f, max.y - min.y);
        return (min, max);
    }

    public static bool EhFechado(List<Vector2> pts, out float perimetro)
    {
        perimetro = Comprimento(pts);
        float gap = Vector2.Distance(pts[0], pts[pts.Count - 1]);
        // fechado se o gap é pequeno relativo ao tamanho total
        return (gap < Mathf.Max(12f, perimetro * 0.2f));
    }

    public static void RaioStats(List<Vector2> pts, Vector2 centro, out float media, out float desvio)
    {
        media = 0f; desvio = 0f;
        if (pts == null || pts.Count == 0) return;
        foreach (var p in pts) media += Vector2.Distance(p, centro);
        media /= pts.Count;
        float var = 0f;
        foreach (var p in pts)
        {
            float d = Vector2.Distance(p, centro);
            var += (d - media) * (d - media);
        }
        var /= Mathf.Max(1, pts.Count - 1);
        desvio = Mathf.Sqrt(var);
    }

    public static int ContarCantos(List<Vector2> pts, float angMin)
    {
        if (pts.Count < 3) return 0;
        int cantos = 0;
        for (int i = 1; i < pts.Count - 1; i++)
        {
            Vector2 a = (pts[i] - pts[i - 1]).normalized;
            Vector2 b = (pts[i + 1] - pts[i]).normalized;
            float ang = Vector2.Angle(a, b);
            if (ang <= (180f - angMin)) cantos++;
        }
        return cantos;
    }

    public static (int index, float angGraus) EncontrarApexMaisAgudo(List<Vector2> pts)
    {
        int best = -1; float bestAng = 999f;
        for (int i = 1; i < pts.Count - 1; i++)
        {
            Vector2 a = (pts[i] - pts[i - 1]).normalized;
            Vector2 b = (pts[i + 1] - pts[i]).normalized;
            float ang = Vector2.Angle(a, b);
            if (ang < bestAng) { bestAng = ang; best = i; }
        }
        return (best, bestAng);
    }

    public static (Vector2 p0, Vector2 p1, Vector2 dir, float retaScore) MelhorLinha(List<Vector2> pts)
    {
        // retaScore via correlação direcional simples
        Vector2 p0 = pts[0];
        Vector2 p1 = pts[pts.Count - 1];
        Vector2 dir = (p1 - p0);
        float len = dir.magnitude;
        if (len < 1e-3f) return (p0, p1, Vector2.right, 0f);
        dir /= len;

        float somaProj = 0f, somaAbs = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            Vector2 d = (pts[i] - pts[i - 1]);
            float proj = Mathf.Abs(Vector2.Dot(d.normalized, dir));
            somaProj += proj;
            somaAbs += 1f;
        }
        float score = (somaAbs > 1e-3f) ? (somaProj / somaAbs) : 0f;
        return (p0, p1, dir * len, Mathf.Clamp01(score));
    }

    public static bool SegmentosSeCruzam(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        // interseção de segmentos 2D
        float o1 = Orient(a, b, c);
        float o2 = Orient(a, b, d);
        float o3 = Orient(c, d, a);
        float o4 = Orient(c, d, b);
        if (o1 * o2 < 0 && o3 * o4 < 0) return true;
        return false;
    }
    private static float Orient(Vector2 p, Vector2 q, Vector2 r) => (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);

    // Ramer–Douglas–Peucker para simplificar traço
    public static List<Vector2> RamerDouglasPeucker(List<Vector2> pts, float epsilon)
    {
        if (pts == null || pts.Count < 3) return new List<Vector2>(pts ?? new List<Vector2>());
        return RDPRec(pts, 0, pts.Count - 1, epsilon);
    }
    private static List<Vector2> RDPRec(List<Vector2> pts, int start, int end, float eps)
    {
        float dmax = 0f; int index = -1;
        for (int i = start + 1; i < end; i++)
        {
            float d = DistPointToSegment(pts[i], pts[start], pts[end]);
            if (d > dmax) { dmax = d; index = i; }
        }
        if (dmax > eps)
        {
            var res1 = RDPRec(pts, start, index, eps);
            var res2 = RDPRec(pts, index, end, eps);
            res1.RemoveAt(res1.Count - 1);
            res1.AddRange(res2);
            return res1;
        }
        else
        {
            return new List<Vector2> { pts[start], pts[end] };
        }
    }
    private static float DistPointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f);
        t = Mathf.Clamp01(t);
        Vector2 proj = a + t * ab;
        return Vector2.Distance(p, proj);
    }
}
