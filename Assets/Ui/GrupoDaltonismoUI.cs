using UnityEngine;
using UnityEngine.UI;

public class GrupoDaltonismoUI : MonoBehaviour
{
    [Header("Renderer Feature (ARRASTE O ASSET)")]
    [SerializeField] private DaltonismoFeature daltonismoFeature; // ScriptableObject do Renderer

    [Header("Switches (ChaveUI)")]
    [SerializeField] private ChaveUI swProtanopia;
    [SerializeField] private ChaveUI swDeuteranopia;
    [SerializeField] private ChaveUI swTritanopia;

    [Header("Intensidade (opcional)")]
    [SerializeField] private Slider sliderIntensidade = null; // 0..1

    [Header("Comportamento")]
    [Tooltip("Permitir que nenhum modo fique ligado (desliga efeito)?")]
    [SerializeField] private bool permitirNenhum = true;

    // guarda último ligado para evitar ficar sem seleção quando permitirNenhum=false
    private ChaveUI _ultimoLigado;

    private void Awake()
    {
        // Conecta eventos
        if (swProtanopia) swProtanopia.aoMudar.AddListener(OnProtanChanged);
        if (swDeuteranopia) swDeuteranopia.aoMudar.AddListener(OnDeuterChanged);
        if (swTritanopia) swTritanopia.aoMudar.AddListener(OnTritanChanged);

        if (sliderIntensidade)
        {
            sliderIntensidade.minValue = 0f;
            sliderIntensidade.maxValue = 1f;
            sliderIntensidade.onValueChanged.AddListener(OnIntensityChanged);
        }

        // Estado inicial: se nenhum marcado e não pode ficar sem, liga Protanopia
        GarantirEstadoInicial();
        AplicarModoAtual();
        AplicarIntensidadeAtual();
    }

    private void OnDestroy()
    {
        if (swProtanopia) swProtanopia.aoMudar.RemoveListener(OnProtanChanged);
        if (swDeuteranopia) swDeuteranopia.aoMudar.RemoveListener(OnDeuterChanged);
        if (swTritanopia) swTritanopia.aoMudar.RemoveListener(OnTritanChanged);
        if (sliderIntensidade) sliderIntensidade.onValueChanged.RemoveListener(OnIntensityChanged);
    }

    // -------------------- Callbacks --------------------

    private void OnProtanChanged(bool on)
    {
        if (on)
        {
            DesligarExceto(swProtanopia);
            _ultimoLigado = swProtanopia;
            SetModo(DaltonismoFeature.Modo.Protanopia);
        }
        else
        {
            SeNenhumSelecionadoTrataVazio();
        }
    }

    private void OnDeuterChanged(bool on)
    {
        if (on)
        {
            DesligarExceto(swDeuteranopia);
            _ultimoLigado = swDeuteranopia;
            SetModo(DaltonismoFeature.Modo.Deuteranopia);
        }
        else
        {
            SeNenhumSelecionadoTrataVazio();
        }
    }

    private void OnTritanChanged(bool on)
    {
        if (on)
        {
            DesligarExceto(swTritanopia);
            _ultimoLigado = swTritanopia;
            SetModo(DaltonismoFeature.Modo.Tritanopia);
        }
        else
        {
            SeNenhumSelecionadoTrataVazio();
        }
    }

    private void OnIntensityChanged(float v)
    {
        if (daltonismoFeature == null) return;
        daltonismoFeature.intensidade = Mathf.Clamp01(v);
        // nada além disso — a feature lê esse valor a cada frame
    }

    // -------------------- Helpers --------------------

    private void DesligarExceto(ChaveUI keep)
    {
        if (swProtanopia && swProtanopia != keep && swProtanopia.EstaLigado) swProtanopia.Definir(false, false);
        if (swDeuteranopia && swDeuteranopia != keep && swDeuteranopia.EstaLigado) swDeuteranopia.Definir(false, false);
        if (swTritanopia && swTritanopia != keep && swTritanopia.EstaLigado) swTritanopia.Definir(false, false);
    }

    private void SeNenhumSelecionadoTrataVazio()
    {
        bool nenhum = !(swProtanopia && swProtanopia.EstaLigado)
                   && !(swDeuteranopia && swDeuteranopia.EstaLigado)
                   && !(swTritanopia && swTritanopia.EstaLigado);

        if (!nenhum) return;

        if (permitirNenhum)
        {
            SetModo(DaltonismoFeature.Modo.Nenhum);
        }
        else
        {
            // restaura último ligado ou força um padrão
            var alvo = _ultimoLigado ?? swProtanopia;
            if (alvo) alvo.Definir(true, true);
        }
    }

    private void SetModo(DaltonismoFeature.Modo modo)
    {
        if (daltonismoFeature == null) return;
        daltonismoFeature.modo = modo;
        // AddRenderPasses/SetupRenderPasses vão ler isso no frame
    }

    private void GarantirEstadoInicial()
    {
        bool algum = (swProtanopia && swProtanopia.EstaLigado)
                  || (swDeuteranopia && swDeuteranopia.EstaLigado)
                  || (swTritanopia && swTritanopia.EstaLigado);

        if (!algum)
        {
            if (permitirNenhum)
            {
                // todos off e modo Nenhum
                SetModo(DaltonismoFeature.Modo.Nenhum);
            }
            else
            {
                // liga padrão (Protanopia)
                if (swProtanopia) swProtanopia.Definir(true, false, true);
                SetModo(DaltonismoFeature.Modo.Protanopia);
                _ultimoLigado = swProtanopia;
            }
        }
        else
        {
            // define _ultimoLigado baseado no que estiver on
            if (swProtanopia && swProtanopia.EstaLigado) _ultimoLigado = swProtanopia;
            else if (swDeuteranopia && swDeuteranopia.EstaLigado) _ultimoLigado = swDeuteranopia;
            else if (swTritanopia && swTritanopia.EstaLigado) _ultimoLigado = swTritanopia;
        }
    }

    private void AplicarModoAtual()
    {
        if (daltonismoFeature == null) return;

        // espelha switches -> feature (caso já venham presetados no prefab/cena)
        if (swProtanopia && swProtanopia.EstaLigado) daltonismoFeature.modo = DaltonismoFeature.Modo.Protanopia;
        else if (swDeuteranopia && swDeuteranopia.EstaLigado) daltonismoFeature.modo = DaltonismoFeature.Modo.Deuteranopia;
        else if (swTritanopia && swTritanopia.EstaLigado) daltonismoFeature.modo = DaltonismoFeature.Modo.Tritanopia;
        else daltonismoFeature.modo = DaltonismoFeature.Modo.Nenhum;
    }

    private void AplicarIntensidadeAtual()
    {
        if (daltonismoFeature == null) return;
        if (sliderIntensidade) daltonismoFeature.intensidade = sliderIntensidade.value;
    }
}
