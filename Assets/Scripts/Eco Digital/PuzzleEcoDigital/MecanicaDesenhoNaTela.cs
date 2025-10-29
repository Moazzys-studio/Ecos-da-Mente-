using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class MecanicaDesenhoNaTela : MonoBehaviour
{
    public enum PlanoProjecao { XY, XZ }
    public enum EspacoDesenho { Mundo, CameraOverlay }

    // ➤ Configuração Geral do Espaço de Desenho
    [Header("Espaço de Desenho")]
    [Tooltip("Define se o traço é desenhado no mundo ou preso à câmera Overlay (2D na tela).")]
    [SerializeField] private EspacoDesenho modoDesenho = EspacoDesenho.CameraOverlay;

    [Tooltip("Plano de projeção quando o desenho está no mundo.")]
    [SerializeField] private PlanoProjecao plano = PlanoProjecao.XY;

    // ➤ Referências de câmera
    [Header("Câmeras")]
    [Tooltip("Câmera utilizada para projetar o toque no mundo (apenas se usar espaço do tipo Mundo).")]
    [SerializeField] private Camera cameraMundo;

    [Tooltip("Câmera Overlay usada para projetar o desenho na tela.")]
    [SerializeField] private Camera cameraOverlay;

    // ➤ Comportamento Overlay
    [Header("Configuração de Overlay")]
    [Tooltip("Se ativado, o traço seguirá os movimentos da câmera Overlay.")]
    [SerializeField] private bool prenderNaCameraOverlay = true;

    [Tooltip("Se ativado, o LineRenderer usará coordenadas LOCAIS da câmera (2D real na tela).")]
    [SerializeField] private bool usarEspacoLocalOverlay = true;

    [Header("Configuração da Linha")]
    [Tooltip("Largura da linha em pixels, ajustada automaticamente conforme a câmera.")]
    [SerializeField, Min(0.5f)] private float larguraPixels = 8f;

    [Tooltip("Cor do traço.")]
    [SerializeField] private Gradient corLinha;

    [Tooltip("Material da linha (deixe vazio para fallback automático).")]
    [SerializeField] private Material materialLinha;

    // ➤ Projeção e distância
    [Header("Screen → World")]
    [Tooltip("Distância de projeção para câmeras em perspectiva.")]
    [SerializeField, Min(0.01f)] private float distanciaPerspectiva = 1.0f;

    // ➤ Plano fixo
    [Header("Plano Fixo (modo Mundo)")]
    [SerializeField] private bool usarZfixo = true;
    [SerializeField] private float zFixo = 0f;
    [SerializeField] private bool usarYfixo = false;
    [SerializeField] private float yFixo = 0f;

    // ➤ Entrada
    [Header("Entrada (Novo Input System controla)")]
    [Tooltip("⚠ Manter desativado! Input agora vem pelo script EcoGesturesInput.cs")]
    [SerializeField] private bool permitirInputAntigo = false;

    [Tooltip("Distância mínima (metros) entre pontos consecutivos.")]
    [SerializeField, Min(0.0001f)] private float distanciaMinimaPonto = 0.015f;

    [Tooltip("Quantidade máxima de pontos por traço.")]
    [SerializeField, Min(8)] private int maxPontosPorTraco = 4096;

    // ➤ Fade da linha
    [Header("Fade da Linha")]
    [Tooltip("Se ativado, o traço some após um curto tempo.")]
    [SerializeField] private bool fadeAutomatico = true;

    [Tooltip("Tempo antes do fade começar.")]
    [SerializeField, Min(0f)] private float atrasoFade = 0.6f;

    [Tooltip("Duração da animação de fade.")]
    [SerializeField, Min(0.05f)] private float duracaoFade = 0.5f;

    [Tooltip("Se ativado, a linha afinada durante o fade.")]
    [SerializeField] private bool fadeAfina = true;

    // ➤ Evento para reconhecimento
    public delegate void EventoTracoFinalizado(IReadOnlyList<Vector3> pontos3D, IReadOnlyList<Vector2> pontos2D, float unidadesPorPixel);
    [Tooltip("Evento disparado quando o traço termina.")]
    public event EventoTracoFinalizado AoFinalizarTraco;

    // Estado interno
    private LineRenderer linhaAtual;
    private List<Vector3> pontos3D = new();
    private List<Vector2> pontos2D = new();
    private bool desenhando = false;
    private int contadorTracos = 0;

    public float UnidadesPorPixel { get; private set; }

    private void Awake()
    {
        if (modoDesenho == EspacoDesenho.Mundo && cameraMundo == null)
            cameraMundo = Camera.main;

        if (modoDesenho == EspacoDesenho.CameraOverlay && cameraOverlay == null)
            cameraOverlay = Camera.main;
    }

    // ✅ Novo Input chama daqui
    public void IniciarTraco(Vector2 posicaoTela)
    {
        if (permitirInputAntigo) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        desenhando = true;
        pontos3D.Clear();
        pontos2D.Clear();
        CriarLinha();
        AdicionarPontoTela(posicaoTela);
    }

    public void AdicionarPontoTela(Vector2 posicaoTela)
    {
        if (!desenhando) return;
        Vector3 wp = ConverterParaMundo(posicaoTela);

        if (pontos3D.Count == 0 || (wp - pontos3D[^1]).sqrMagnitude >= (distanciaMinimaPonto * distanciaMinimaPonto))
            AdicionarPonto(wp);
    }

    public void FinalizarTraco()
    {
        if (!desenhando) return;
        desenhando = false;
        ConcluirTraco();
    }

    // ➤ Conversão da posição da tela em ponto no mundo
    private Vector3 ConverterParaMundo(Vector2 posicaoTela)
    {
        Camera cam = (modoDesenho == EspacoDesenho.Mundo) ? cameraMundo : cameraOverlay;
        if (cam == null) return Vector3.zero;

        Vector3 ponto = cam.ScreenToWorldPoint(new Vector3(posicaoTela.x, posicaoTela.y, distanciaPerspectiva));

        if (modoDesenho == EspacoDesenho.Mundo)
        {
            if (plano == PlanoProjecao.XY && usarZfixo) ponto.z = zFixo;
            if (plano == PlanoProjecao.XZ && usarYfixo) ponto.y = yFixo;
        }

        return ponto;
    }

    private void CriarLinha()
    {
        var go = new GameObject($"Traco_{contadorTracos++}");
        go.transform.SetParent((modoDesenho == EspacoDesenho.CameraOverlay ? cameraOverlay.transform : transform), false);

        linhaAtual = go.AddComponent<LineRenderer>();
        linhaAtual.positionCount = 0;
        linhaAtual.numCapVertices = 8;
        linhaAtual.numCornerVertices = 4;
        linhaAtual.material = materialLinha != null ? new Material(materialLinha) : new Material(Shader.Find("Sprites/Default"));

        AtualizarLarguraLinha();
    }

    private void AdicionarPonto(Vector3 ponto)
    {
        if (linhaAtual == null || pontos3D.Count >= maxPontosPorTraco) return;

        pontos3D.Add(ponto);
        pontos2D.Add(new Vector2(ponto.x, ponto.y));

        linhaAtual.positionCount = pontos3D.Count;
        linhaAtual.SetPosition(pontos3D.Count - 1, ponto);
        AtualizarLarguraLinha();
    }

    private void ConcluirTraco()
    {
        var copia3D = pontos3D.ToArray();
        var copia2D = pontos2D.ToArray();

        AoFinalizarTraco?.Invoke(copia3D, copia2D, UnidadesPorPixel);

        if (fadeAutomatico && linhaAtual != null)
        {
            var fader = linhaAtual.GetComponent<StrokeFader>();
            if (!fader) fader = linhaAtual.gameObject.AddComponent<StrokeFader>();
            fader.Begin(linhaAtual, atrasoFade, duracaoFade, fadeAfina, true);
        }

        linhaAtual = null;
        pontos3D.Clear();
        pontos2D.Clear();
    }

    private void AtualizarLarguraLinha()
    {
        Camera cam = (modoDesenho == EspacoDesenho.Mundo) ? cameraMundo : cameraOverlay;
        if (!cam) return;

        float mundoPorPixel = cam.orthographic ?
            (cam.orthographicSize * 2f) / Screen.height :
            (2f * distanciaPerspectiva * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) / Screen.height;

        UnidadesPorPixel = mundoPorPixel;
        float largura = larguraPixels * mundoPorPixel;
        linhaAtual.startWidth = linhaAtual.endWidth = largura;
    }
}
