using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Transparencia : MonoBehaviour
{
   [Range(0f, 1f)]
    public float alpha = 0.5f;

    private Material[] materiais;

    void Start()
    {
        // Pega todos os materiais de todos os renderers (Mesh + SkinnedMesh)
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        materiais = new Material[renderers.Length];

        int index = 0;
        foreach (Renderer rend in renderers)
        {
            foreach (Material mat in rend.materials)
            {
                AtivarTransparencia(mat);
                materiais[index] = mat;
                index++;
            }
        }
    }

    void Update()
    {
        // Atualiza o alpha dos materiais
        foreach (Material mat in materiais)
        {
            if (mat == null) continue;
            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
        }
    }

    void AtivarTransparencia(Material mat)
    {
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }
}
