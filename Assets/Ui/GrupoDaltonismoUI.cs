using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GrupoDaltonismoUI_Slider : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private DaltonismoFeature daltonismoFeature;
    [SerializeField] private Slider sliderModo;         // 0..5 (snap)
    [SerializeField] private Slider sliderIntensidade;  // 0..1
    [SerializeField] private ChaveUI chaveAtivar;       // chave custom
    [SerializeField] private TextMeshProUGUI textoModo; // rótulo TMP

    private const string KEY_ATIVO = "daltonismo_ativo";
    private const string KEY_MODO  = "daltonismo_modo";
    private const string KEY_INT   = "daltonismo_intensidade";

    private readonly string[] nomesModos =
    {
        "Desativado",     // 0 = Nenhum
        "Protanopia",     // 1
        "Deuteranopia",   // 2
        "Tritanopia",     // 3
        "Achromatopsia",  // 4
        "Achromatomalia"  // 5
    };

    private bool carregando = false;

    private void Awake()
    {
        //-------------------------------
        // Proteções extras (Unity 2022)
        //-------------------------------
        if (!sliderModo || !sliderIntensidade)
        {
            Debug.LogError("[GrupoDaltonismoUI] Sliders não atribuídos!");
            enabled = false;
            return;
        }
        if (!chaveAtivar)
        {
            Debug.LogError("[GrupoDaltonismoUI] ChaveAtivar não atribuída!");
            enabled = false;
            return;
        }

        carregando = true;

        int ativo = PlayerPrefs.GetInt(KEY_ATIVO, 0);
        int modo  = Mathf.Clamp(PlayerPrefs.GetInt(KEY_MODO, 0), 0, 5);
        float intensidade = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_INT, 1f));

        sliderModo.minValue = 0;
        sliderModo.maxValue = 5;
        sliderModo.wholeNumbers = true;
        sliderModo.SetValueWithoutNotify(modo);

        sliderIntensidade.minValue = 0;
        sliderIntensidade.maxValue = 1;
        sliderIntensidade.SetValueWithoutNotify(intensidade);

        chaveAtivar.Definir(ativo == 1, false, true);
        AtualizarTexto(modo);

        AplicarModo(modo, ativo == 1);
        AplicarIntensidade(intensidade);

        // Unity 2022 exige remover e adicionar com segurança
        sliderModo.onValueChanged.RemoveAllListeners();
        sliderModo.onValueChanged.AddListener(OnSliderModo);

        sliderIntensidade.onValueChanged.RemoveAllListeners();
        sliderIntensidade.onValueChanged.AddListener(OnIntensidade);

        chaveAtivar.aoMudar.RemoveAllListeners();
        chaveAtivar.aoMudar.AddListener(OnChave);

        carregando = false;
    }

    private void OnDestroy()
    {
        // Segurança para evitar callbacks quebrados
        if (sliderModo) sliderModo.onValueChanged.RemoveListener(OnSliderModo);
        if (sliderIntensidade) sliderIntensidade.onValueChanged.RemoveListener(OnIntensidade);
        if (chaveAtivar) chaveAtivar.aoMudar.RemoveListener(OnChave);
    }

    //---------------------------------------------
    // EVENTOS
    //---------------------------------------------
    private void OnSliderModo(float valor)
    {
        int modo = Mathf.RoundToInt(valor);
        AtualizarTexto(modo);

        if (!chaveAtivar.EstaLigado && modo > 0)
            chaveAtivar.Definir(true, true);

        if (chaveAtivar.EstaLigado)
            AplicarModo(modo, true);

        Salvar();
    }

    private void OnIntensidade(float valor)
    {
        AplicarIntensidade(valor);
        Salvar();
    }

    private void OnChave(bool ligada)
    {
        if (!ligada)
        {
            sliderModo.SetValueWithoutNotify(0);
            AtualizarTexto(0);
            AplicarModo(0, false);
        }
        else if (Mathf.Approximately(sliderModo.value, 0f))
        {
            sliderModo.SetValueWithoutNotify(1);
            AtualizarTexto(1);
            AplicarModo(1, true);
        }

        Salvar();
    }

    //---------------------------------------------
    // APLICAÇÕES DE VALORES
    //---------------------------------------------
    private void AtualizarTexto(int modo)
    {
        modo = Mathf.Clamp(modo, 0, nomesModos.Length - 1);

        if (textoModo != null)
            textoModo.text = nomesModos[modo];
    }

    private void AplicarModo(int modoIndex, bool ativo)
    {
        if (!daltonismoFeature) return;

        if (!ativo)
        {
            daltonismoFeature.SetModoUI(0);
            daltonismoFeature.ApplyParamsNow();
            return;
        }

        daltonismoFeature.SetModoUI(modoIndex);
        daltonismoFeature.ApplyParamsNow();
    }

    private void AplicarIntensidade(float v)
    {
        if (!daltonismoFeature) return;

        daltonismoFeature.intensidade = Mathf.Clamp01(v);
        daltonismoFeature.ApplyParamsNow();
    }

    //---------------------------------------------
    // SALVAMENTO
    //---------------------------------------------
    private void Salvar()
    {
        if (carregando) return;

        PlayerPrefs.SetInt(KEY_ATIVO, chaveAtivar.EstaLigado ? 1 : 0);
        PlayerPrefs.SetInt(KEY_MODO, Mathf.RoundToInt(sliderModo.value));
        PlayerPrefs.SetFloat(KEY_INT, sliderIntensidade.value);

        PlayerPrefs.Save();
    }
}