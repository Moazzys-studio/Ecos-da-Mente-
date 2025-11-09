using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DaltonismoFeature : ScriptableRendererFeature
{
    public enum Modo
    {
        Nenhum = 0,
        Protanopia = 1,
        Deuteranopia = 2,
        Tritanopia = 3,
        Achromatopsia = 4,
        Achromatomalia = 5
    }

    [Header("Config")]
    [Tooltip("Shader Hidden/Daltonismo/URPBlit (usa _Mode e _Intensity).")]
    public Shader shaderURP;

    [Range(0f, 1f)]
    public float intensidade = 1f;

    public Modo modo = Modo.Nenhum;

    [Tooltip("Evento da Pass")]
    public RenderPassEvent passEvent = RenderPassEvent.AfterRendering; // afeta mundo + UI (Screen Space - Camera)

    [Tooltip("Ignorar câmeras que não são do jogo (Scene/Preview/UI Overlay).")]
    public bool ignorarNaoGame = true;

    // IDs de propriedades (faltavam, causavam CS0103)
    static readonly int _IntensityID = Shader.PropertyToID("_Intensity");
    static readonly int _ModeID      = Shader.PropertyToID("_Mode");

    Material _mat;
    ColorBlitPass _pass;

    class ColorBlitPass : ScriptableRenderPass
    {
        readonly ProfilingSampler _prof = new ProfilingSampler("Daltonismo Blit (URP14)");
        readonly System.Func<Camera, bool> _podeProcessar;
        Material _mat;

        RTHandle _cameraColor; // destino final
        RTHandle _tempRT;      // RT temporário (evita blit in-place)
        float _intensity;

        // Flags de compatibilidade: Blitter usa _BlitTexture; cmd.Blit liga _MainTex
        bool _usaBlitter;

        // IDs locais (para evitar string a cada frame)
        static readonly int _IntensityID = Shader.PropertyToID("_Intensity");

        public ColorBlitPass(Material mat, RenderPassEvent evt, System.Func<Camera, bool> filtroCamera)
        {
            _mat = mat;
            renderPassEvent = evt;
            _podeProcessar = filtroCamera;
            ConfigureInput(ScriptableRenderPassInput.Color);
            _usaBlitter = (mat != null && mat.HasProperty("_BlitTexture"));
        }

        public void RefreshMaterial(Material mat)
        {
            _mat = mat;
            _usaBlitter = (mat != null && mat.HasProperty("_BlitTexture"));
        }

        public void SetTarget(RTHandle colorHandle, float intensity)
        {
            _cameraColor = colorHandle;
            _intensity = intensity;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (_mat == null || _cameraColor == null) return;
            if (_podeProcessar != null && !_podeProcessar(renderingData.cameraData.camera)) return;

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(
                ref _tempRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DaltonismoTmp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_mat == null || _cameraColor == null) return;
            var cam = renderingData.cameraData.camera;
            if (_podeProcessar != null && !_podeProcessar(cam)) return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _prof))
            {
                _mat.SetFloat(_IntensityID, _intensity);

                // Blit seguro: NUNCA src==dst
                if (_usaBlitter)
                {
#if UNITY_2022_2_OR_NEWER
                    Blitter.BlitCameraTexture(cmd, _cameraColor, _tempRT, _mat, 0);
                    Blitter.BlitCameraTexture(cmd, _tempRT, _cameraColor);
#else
                    cmd.Blit(_cameraColor, _tempRT, _mat, 0);
                    cmd.Blit(_tempRT, _cameraColor);
#endif
                }
                else
                {
                    // Compatível com shaders que usam _MainTex
                    cmd.Blit(_cameraColor, _tempRT, _mat, 0);
                    cmd.Blit(_tempRT, _cameraColor);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public override void Create()
    {
        CriarOuAtualizarMaterial();
        _pass = new ColorBlitPass(_mat, passEvent, PodeProcessarCamera);
    }

    void CriarOuAtualizarMaterial()
    {
        if (shaderURP != null)
        {
            if (_mat == null || _mat.shader != shaderURP)
            {
                CoreUtils.Destroy(_mat);
                _mat = CoreUtils.CreateEngineMaterial(shaderURP);
            }
        }
        else
        {
            CoreUtils.Destroy(_mat);
            _mat = null;
        }
    }

    bool PodeProcessarCamera(Camera cam)
    {
        if (!ignorarNaoGame) return true;
        if (cam == null) return false;
        return cam.cameraType == CameraType.Game;
    }

    void SetShaderParams()
    {
        if (_mat == null) return;
        _mat.SetFloat(_ModeID, (float)modo);
        _mat.SetFloat(_IntensityID, intensidade);
    }

    // ========= MÉTODOS PÚBLICOS =========
    public void ApplyParamsNow()
    {
        if (_mat == null) CriarOuAtualizarMaterial();
        SetShaderParams();
        if (_pass != null) _pass.RefreshMaterial(_mat);
    }

    public void SetModoUI(int modoIndex)
    {
        modo = (Modo)Mathf.Clamp(modoIndex, 0, 5);
        ApplyParamsNow();
    }
    // ====================================

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (_mat == null) return;
        if (ignorarNaoGame && renderingData.cameraData.cameraType != CameraType.Game) return;

        _pass.SetTarget(renderer.cameraColorTargetHandle, intensidade);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_mat == null) return;
        if (ignorarNaoGame && renderingData.cameraData.cameraType != CameraType.Game) return;

        SetShaderParams();                 // garante _Mode/_Intensity corretos
        _pass.renderPassEvent = passEvent; // respeita o Inspector
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) CoreUtils.Destroy(_mat);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        CriarOuAtualizarMaterial();
        if (_pass != null) _pass.RefreshMaterial(_mat);
        if (_pass != null) _pass.renderPassEvent = passEvent;
        SetShaderParams();
    }
#endif
}
