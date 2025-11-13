using UnityEngine;
using UnityEngine.UI;

public class RelogioTurnoUI : MonoBehaviour
{
    [SerializeField] private Image relogioImage;

    public bool EstaAtivo { get; private set; }

    private float duracao;
    private float tempoAtual;

    private void Awake()
    {
        if (relogioImage == null)
            relogioImage = GetComponentInChildren<Image>();
        PararRelogio();
    }

    private void Update()
    {
        if (!EstaAtivo || relogioImage == null)
            return;

        tempoAtual += Time.deltaTime;
        float frac = Mathf.Clamp01(tempoAtual / duracao);
        relogioImage.fillAmount = 1f - frac;

        if (frac >= 1f)
            PararRelogio();
    }

    public void IniciarRelogio(float dur)
    {
        if (relogioImage == null) return;

        duracao    = Mathf.Max(0.01f, dur);
        tempoAtual = 0f;
        EstaAtivo  = true;

        if (relogioImage.transform.parent != null)
            relogioImage.transform.parent.gameObject.SetActive(true);

        relogioImage.enabled    = true;
        relogioImage.fillAmount = 1f;
    }

    public void PararRelogio()
    {
        EstaAtivo = false;

        if (relogioImage == null) return;

        relogioImage.fillAmount = 0f;
        relogioImage.enabled    = false;

        if (relogioImage.transform.parent != null)
            relogioImage.transform.parent.gameObject.SetActive(false);
    }
}
