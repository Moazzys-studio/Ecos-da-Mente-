using UnityEngine;

public class ChaveVibracaoBinder : MonoBehaviour
{
    [SerializeField] private ChaveUI chave; // arraste o switch da UI

    void Awake()
    {
        if (!chave) chave = GetComponent<ChaveUI>();

        // Estado inicial refletindo o PlayerPrefs
        bool habilitada = VibracaoManager.Instancia
            ? VibracaoManager.Instancia.Habilitada
            : PlayerPrefs.GetInt(VibracaoManager.KEY_VIBRA, 1) == 1;

        if (chave) chave.Definir(habilitada, false, true);

        if (chave) chave.aoMudar.AddListener(OnChaveMudou);
    }

    void OnDestroy()
    {
        if (chave) chave.aoMudar.RemoveListener(OnChaveMudou);
    }

    private void OnChaveMudou(bool ligado)
    {
        if (VibracaoManager.Instancia)
            VibracaoManager.Instancia.DefinirHabilitada(ligado);
        else
        {
            PlayerPrefs.SetInt(VibracaoManager.KEY_VIBRA, ligado ? 1 : 0);
            PlayerPrefs.Save();
        }

        // feedback rápido (opcional)
        if (ligado) VibracaoManager.Instancia?.VibracaoLeve();
    }
}
