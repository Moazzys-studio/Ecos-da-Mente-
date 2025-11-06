using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DaltonismoFeature : ScriptableRendererFeature
{
    public enum Modo { Nenhum, Protanopia, Deuteranopia, Tritanopia, Custom }

    [Header("Config")]
    public Shader shaderURP;
    [Range(0f,1f)] public float intensidade = 1f;
    public Modo modo = Modo.Protanopia;

    [Tooltip("Usado só no modo CUSTOM (linhas da matriz RGB).")]
    public Vector3 customRow0 = new Vector3(1,0,0);
    public Vector3 customRow1 = new Vector3(0,1,0);
    public Vector3 customRow2 = new Vector3(0,0,1);

    [Tooltip("Evento da Pass")]
    public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

    Material _mat;
    ColorBlitPass _pass;

    class ColorBlitPass : ScriptableRenderPass
    {
        readonly ProfilingSampler _prof = new ProfilingSampler("Daltonismo Blit (URP14)");
        Material _mat;
        RTHandle _cameraColor;
        float _intensity;

        public ColorBlitPass(Material mat, RenderPassEvent evt)
        {
            _mat = mat;
            renderPassEvent = evt;
        }

        public void SetTarget(RTHandle colorHandle, float intensity)
        {
            _cameraColor = colorHandle;
            _intensity = intensity;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_mat == null || _cameraColor == null) return;

            var cam = renderingData.cameraData.camera;
            if (cam.cameraType != CameraType.Game) return; // evita Scene/Preview

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _prof))
            {
                _mat.SetFloat("_Intensity", _intensity);
                // Blit em-place (padrão URP 14)
                Blitter.BlitCameraTexture(cmd, _cameraColor, _cameraColor, _mat, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public override void Create()
    {
        if (shaderURP != null)
            _mat = CoreUtils.CreateEngineMaterial(shaderURP);

        _pass = new ColorBlitPass(_mat, passEvent);
    }

    void SetKeywordsAndProps()
    {
        if (_mat == null) return;

        // limpa
        _mat.DisableKeyword("MODE_PROTAN");
        _mat.DisableKeyword("MODE_DEUTER");
        _mat.DisableKeyword("MODE_TRITAN");
        _mat.DisableKeyword("MODE_CUSTOM");

        // define
        switch (modo)
        {
            case Modo.Protanopia:   _mat.EnableKeyword("MODE_PROTAN");  break;
            case Modo.Deuteranopia: _mat.EnableKeyword("MODE_DEUTER");  break;
            case Modo.Tritanopia:   _mat.EnableKeyword("MODE_TRITAN");  break;
            case Modo.Custom:
                _mat.EnableKeyword("MODE_CUSTOM");
                _mat.SetVector("_CustomRow0", (Vector4)customRow0);
                _mat.SetVector("_CustomRow1", (Vector4)customRow1);
                _mat.SetVector("_CustomRow2", (Vector4)customRow2);
                break;
            case Modo.Nenhum:
            default:
                // nenhuma keyword => matriz identidade no shader
                break;
        }
    }

    // URP 14: configurar alvos/entradas AQUI, NÃO em AddRenderPasses
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (_mat == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        // Garante Color input para o shader
        _pass.ConfigureInput(ScriptableRenderPassInput.Color);

        // Passa o target da câmera (permitido aqui)
        _pass.SetTarget(renderer.cameraColorTargetHandle, intensidade);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_mat == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        SetKeywordsAndProps();
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) CoreUtils.Destroy(_mat);
    }
}
