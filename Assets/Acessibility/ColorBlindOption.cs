// using UnityEngine;
// using SOG.CVDFilter;

// public class ColorBlindOption : MonoBehaviour
// {
//     public CVDFilter cvdFilter;
//     public CVDProfilesSO cVDProfilesSO;
//     void Awake()
//     {
//         if (cvdFilter == null)
//         {
//             cvdFilter = CVDFilter.Instance;
//         }
//     }

//     private void OnEnable() 
//     {
//         cvdFilter.SetupProfiles(cVDProfilesSO);
//         LoadVisionTypePreference();
//     }

//     public void ChangeColorBlindMode(int colorBlindMode)
//     {        
//         VisionTypeNames newVisionType = (VisionTypeNames)colorBlindMode;
//         cvdFilter.ChangeCurrentType(newVisionType);
//         PlayerPrefs.SetInt("ColorBlindMode", colorBlindMode);    
//     }
//     void LoadVisionTypePreference()
//     {
//         if (cvdFilter != null)
//         {
//             if (PlayerPrefs.HasKey("ColorBlindMode"))
//             {
//                 int savedVisionType = PlayerPrefs.GetInt("ColorBlindMode");
//                 VisionTypeNames visionType = (VisionTypeNames)savedVisionType;
//                 cvdFilter.ChangeCurrentType(visionType);
//             }
//             else
//             {
//                 cvdFilter.ChangeCurrentType(VisionTypeNames.Normal);
//             }
//         }
//         else
//         {
//             Debug.LogError("CVDFilter is not set.");
//         }
//     }
// }

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SOG.CVDFilter;

public class ColorBlindOption : MonoBehaviour
{
    [Header("UI")]
    public Slider slider;
    public TMP_Text modeText;

    [Header("Chave Daltonismo")]
    public ChaveUI chaveDaltonismo;
    private int ultimoModo = 1; // último modo diferente de Normal

    [Header("CVD Filter")]
    public CVDFilter cvdFilter;
    public CVDProfilesSO cVDProfilesSO;

    // Nomes que aparecerão no texto (9 modos)
    private string[] modeNames = {
        "Normal",
        "Protanopia",
        "Deuteranopia",
        "Tritanopia",
        "Achromatopsia",
        "Achromatomaly",
        "Protanomaly",
        "Deuteranomaly",
        "Tritanomaly"
    };

    void Awake()
    {
        if (cvdFilter == null)
            cvdFilter = CVDFilter.Instance;
    }

    private void OnEnable() 
    {
        cvdFilter.SetupProfiles(cVDProfilesSO);
        LoadVisionTypePreference();
    }

    void Start()
    {
        if (slider != null)
            slider.onValueChanged.AddListener(ChangeColorBlindModeFromSlider);

        if (chaveDaltonismo != null)
            chaveDaltonismo.aoMudar.AddListener(OnToggleChanged);
    }

    // ----------------------------------------
    // SLIDER → ALTERA MODO DE VISÃO
    // ----------------------------------------
    public void ChangeColorBlindMode(int colorBlindMode)
    {        
        Debug.Log("colorBlindMode "+ colorBlindMode);
        VisionTypeNames newVisionType = (VisionTypeNames)colorBlindMode;

        cvdFilter.ChangeCurrentType(newVisionType);
        PlayerPrefs.SetInt("ColorBlindMode", colorBlindMode);
        
        if (modeText != null)
            modeText.text = modeNames[colorBlindMode];
    }

    public void ChangeColorBlindModeFromSlider(float value)
    {
        int intValue = (int)value;

        Debug.Log($"Mudou para indice -> {value}");

        // guarda o último modo válido
        if (intValue > 0)
            ultimoModo = intValue;

        Debug.Log($"CHAVE DALTONISMO -> {chaveDaltonismo}");
        // se slider = 0 → chave desliga
        if (chaveDaltonismo != null)
        {
            if (intValue == 0)
                chaveDaltonismo.Definir(false, false);
            else
                chaveDaltonismo.Definir(true, false);
        }

        ChangeColorBlindMode(intValue);
    }

    // ----------------------------------------
    // CHAVE → ALTERA SLIDER
    // ----------------------------------------
    private void OnToggleChanged(bool ligado)
    {
        if (!ligado)
        {
            // desligou → volta para Normal
            slider.value = 0;
            ChangeColorBlindMode(0);
        }
        else
        {
            // ligou → volta para o último modo
            slider.value = ultimoModo;
            ChangeColorBlindMode(ultimoModo);
        }
    }

    // ----------------------------------------
    // CARREGA MODO SALVO
    // ----------------------------------------
    void LoadVisionTypePreference()
    {
        if (cvdFilter == null)
        {
            Debug.LogError("CVDFilter is not set.");
            return;
        }

        int savedVisionType = PlayerPrefs.GetInt("ColorBlindMode", 0);

        cvdFilter.ChangeCurrentType((VisionTypeNames)savedVisionType);

        if (slider != null)
            slider.value = savedVisionType;

        if (modeText != null)
            modeText.text = modeNames[savedVisionType];

        // Ajusta a chave no início
        if (chaveDaltonismo != null)
            chaveDaltonismo.Definir(savedVisionType > 0, false, true);
    }
}