using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Audio/Sound Manager (PlayerPrefs)")]
public class SoundManager : MonoBehaviour
{
    [Header("UI (arraste Sliders 0..1)")]
    [SerializeField] private Slider sliderMusica;
    [SerializeField] private Slider sliderEfeito;

    [Header("Tags dos AudioSources")]
    [SerializeField] private string tagMusica = "Musica";
    [SerializeField] private string tagEfeito = "EfeitoSonoro";

    [Header("Varredura dinâmica (opcional)")]
    [SerializeField] private bool varrerPeriodicamente = true;
    [SerializeField, Min(0.1f)] private float intervaloVarredura = 2f;

    // Chaves PlayerPrefs
    public const string KEY_MUSIC = "musicVolume";
    public const string KEY_SFX   = "sfxVolume";

    // Defaults
    [Header("Defaults")]
    [Range(0,1)] public float volumeMusicaDefault = 1f;
    [Range(0,1)] public float volumeSfxDefault    = 1f;

    // Interno
    private readonly List<AudioSource> _musicas = new();
    private readonly List<AudioSource> _efeitos = new();

    void Awake()
    {
        // Carrega volumes salvos (ou defaults)
        float musicVol = PlayerPrefs.GetFloat(KEY_MUSIC, volumeMusicaDefault);
        float sfxVol   = PlayerPrefs.GetFloat(KEY_SFX,   volumeSfxDefault);

        if (sliderMusica)
        {
            sliderMusica.minValue = 0f; sliderMusica.maxValue = 1f;
            sliderMusica.SetValueWithoutNotify(musicVol);
            sliderMusica.onValueChanged.AddListener(SetVolumeMusica);
        }
        if (sliderEfeito)
        {
            sliderEfeito.minValue = 0f; sliderEfeito.maxValue = 1f;
            sliderEfeito.SetValueWithoutNotify(sfxVol);
            sliderEfeito.onValueChanged.AddListener(SetVolumeEfeito);
        }

        VarrerCena();
        AplicarVolume(_musicas, musicVol);
        AplicarVolume(_efeitos, sfxVol);
    }

    void OnEnable()
    {
        if (varrerPeriodicamente) StartCoroutine(CoVarredura());
    }

    void OnDisable()
    {
        if (varrerPeriodicamente) StopAllCoroutines();
    }

    void OnApplicationQuit()
    {
        // garante flush
        PlayerPrefs.Save();
    }

    // ---------- API (chame também direto sem sliders, se quiser) ----------
    public void SetVolumeMusica(float v)
    {
        v = Mathf.Clamp01(v);
        AplicarVolume(_musicas, v);
        PlayerPrefs.SetFloat(KEY_MUSIC, v);
        PlayerPrefs.Save();
    }

    public void SetVolumeEfeito(float v)
    {
        v = Mathf.Clamp01(v);
        AplicarVolume(_efeitos, v);
        PlayerPrefs.SetFloat(KEY_SFX, v);
        PlayerPrefs.Save();
    }

    // Atalhos úteis
    public float GetVolumeMusica() => PlayerPrefs.GetFloat(KEY_MUSIC, volumeMusicaDefault);
    public float GetVolumeEfeito() => PlayerPrefs.GetFloat(KEY_SFX,   volumeSfxDefault);

    public void ResetarVolumesParaDefault()
    {
        SetVolumeMusica(volumeMusicaDefault);
        SetVolumeEfeito(volumeSfxDefault);
        if (sliderMusica) sliderMusica.SetValueWithoutNotify(volumeMusicaDefault);
        if (sliderEfeito) sliderEfeito.SetValueWithoutNotify(volumeSfxDefault);
    }

    // ---------- Internos ----------
    private void AplicarVolume(List<AudioSource> lista, float v)
    {
        for (int i = lista.Count - 1; i >= 0; i--)
        {
            var src = lista[i];
            if (src == null) { lista.RemoveAt(i); continue; }
            src.volume = v;
            // opcional: src.mute = (v <= 0.0001f);
        }
    }

    private void VarrerCena()
    {
        _musicas.Clear();
        _efeitos.Clear();

        var todas = Resources.FindObjectsOfTypeAll<AudioSource>();
        foreach (var a in todas)
        {
            if (a == null) continue;
            if (!a.gameObject.scene.IsValid()) continue; // ignora prefabs não instanciados

            if (a.gameObject.CompareTag(tagMusica)) _musicas.Add(a);
            else if (a.gameObject.CompareTag(tagEfeito)) _efeitos.Add(a);
        }
    }

    private IEnumerator CoVarredura()
    {
        var wait = new WaitForSeconds(intervaloVarredura);
        while (true)
        {
            float music = GetVolumeMusica();
            float sfx   = GetVolumeEfeito();

            VarrerCena();
            AplicarVolume(_musicas, music);
            AplicarVolume(_efeitos, sfx);

            yield return wait;
        }
    }

    // Registro manual (se preferir evitar varredura)
    public void RegistrarFonte(AudioSource src)
    {
        if (src == null) return;
        if (src.gameObject.CompareTag(tagMusica))
        {
            if (!_musicas.Contains(src)) _musicas.Add(src);
            src.volume = GetVolumeMusica();
        }
        else if (src.gameObject.CompareTag(tagEfeito))
        {
            if (!_efeitos.Contains(src)) _efeitos.Add(src);
            src.volume = GetVolumeEfeito();
        }
    }
}
