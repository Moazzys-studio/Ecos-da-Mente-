using UnityEngine;
using SOG.CVDFilter;

public class ColorBlindOption : MonoBehaviour
{
    public CVDFilter cvdFilter;
    public CVDProfilesSO cVDProfilesSO;
    void Awake()
    {
        if (cvdFilter == null)
        {
            cvdFilter = CVDFilter.Instance;
        }
    }

    private void OnEnable() 
    {
        cvdFilter.SetupProfiles(cVDProfilesSO);
        LoadVisionTypePreference();
    }

    public void ChangeColorBlindMode(int colorBlindMode)
    {        
        VisionTypeNames newVisionType = (VisionTypeNames)colorBlindMode;
        cvdFilter.ChangeCurrentType(newVisionType);
        PlayerPrefs.SetInt("ColorBlindMode", colorBlindMode);    
    }
    void LoadVisionTypePreference()
    {
        if (cvdFilter != null)
        {
            if (PlayerPrefs.HasKey("ColorBlindMode"))
            {
                int savedVisionType = PlayerPrefs.GetInt("ColorBlindMode");
                VisionTypeNames visionType = (VisionTypeNames)savedVisionType;
                cvdFilter.ChangeCurrentType(visionType);
            }
            else
            {
                cvdFilter.ChangeCurrentType(VisionTypeNames.Normal);
            }
        }
        else
        {
            Debug.LogError("CVDFilter is not set.");
        }
    }
}
