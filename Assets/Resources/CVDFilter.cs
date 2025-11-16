using UnityEngine;
using UnityEngine.Rendering;
using System.Reflection;

namespace SOG.CVDFilter
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public class CVDFilter : MonoBehaviour
    {
        public static CVDFilter Instance { get; private set; }

        private Volume postProcessVolume;

        private CVDProfilesSO profiles;
        [SerializeField] private VisionTypeNames currentType;

        public VisionTypeNames visionTypeNames { get { return currentType; } }

        public VisionTypeInfo SelectedVisionType { get; private set; }

        private const string soFileName = "CVDProfiles";

        void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance == null)
                {
                    print("[Singleton] Initializing Instance " + typeof(CVDFilter) + "...");
                    Instance = this;
                    DontDestroyOnLoad(gameObject);

                }
                else if (Instance != this)
                {
                    print("[Singleton - CVDFilter] Instance already exists: " + Instance);
                    Destroy(Instance.gameObject);
                    Instance = this;
                    DontDestroyOnLoad(gameObject);
                }

                Setup();
            }
        }

        void Reset()
        {
            Setup();
            ChangeProfile();
        }

        void Start()
        {
            if (Instance == this)
            {
                ChangeProfile();
            }
        }

        private void Setup()
        {
            AssignProfileSO();
            ConfigureVolume();
        }

        private void AssignProfileSO()
        {
            profiles = Resources.Load<CVDProfilesSO>(soFileName);
            if (profiles == null)
            {
                Debug.Log("[CVDFilter] Error - Unable to locate CVDProfiles ScriptableObject!");
                Debug.LogErrorFormat("[{0}] ({1}): Error - Unable to locate file \"{2}\". "
                + "There should be a single ScriptableObject called \"{2}.asset\" in Resources folder.", GetType().Name, MethodBase.GetCurrentMethod().Name, soFileName);
                return;
            }
            SelectedVisionType = profiles.VisionTypes[0];
        }

        private void ConfigureVolume()
        {
            postProcessVolume = GetComponent<Volume>();
            if (postProcessVolume == null)
            {
                Debug.LogError("Volume component not found!");
                return;
            }
            postProcessVolume.isGlobal = true;
        }

        public void ChangeProfile()
        {

            if (profiles == null)
            {
                Debug.LogErrorFormat("[{0}] ({1}): Error - Unable to locate {2}.", GetType().Name, MethodBase.GetCurrentMethod().Name, soFileName);
                return;
            }

            SelectedVisionType = profiles.VisionTypes[(int)currentType];
            postProcessVolume.profile = SelectedVisionType.profile;
        }

        public void ChangeCurrentType(VisionTypeNames newType)
        {
            currentType = newType;
            ChangeProfile();
        }

        public void SetupProfiles(CVDProfilesSO newProfiles)
        {
            profiles = newProfiles;
            SelectedVisionType = profiles.VisionTypes[0];
            ConfigureVolume();
        }
    }

    public enum VisionTypeNames
    {
        // Inglês
        Normal,
        Protanopia,
        Protanomaly,
        Deuteranopia,
        Deuteranomaly,
        Tritanopia,
        Tritanomaly,
        Achromatopsia,
        Achromatomaly
    }

    [System.Serializable]
    public struct VisionTypeInfo
    {
        public VisionTypeNames typeName;
        public string description;
        public VolumeProfile profile;
        public Texture2D previewImage;
    }
}
