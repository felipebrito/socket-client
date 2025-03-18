using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using TMPro;

/// <summary>
/// Interface de diagnóstico para ajudar a resolver problemas no Meta Quest.
/// Este script cria uma interface simplificada quando há falhas na inicialização.
/// </summary>
public class DiagnosticUI : MonoBehaviour
{
    [Header("References")]
    public VRManager vrManager;
    public Canvas diagCanvas;
    public TextMeshProUGUI statusText;
    
    [Header("Settings")]
    public bool showDiagnosticOnStart = true;
    public float buttonWidth = 250f;
    public float buttonHeight = 80f;
    public float buttonSpacing = 20f;
    
    [Header("Estado da Aplicação")]
    [Tooltip("Últimos frames por segundo registrados")]
    public float currentFPS = 0;
    [Tooltip("Tempo total desde a inicialização")]
    public float appUptime = 0;
    [Tooltip("Se a interface está aguardando um vídeo iniciar")]
    public bool isWaiting = true;
    
    private bool isInitialized = false;
    private float fpsMeasureInterval = 0.5f;
    private float fpsTimer = 0;
    private int frameCount = 0;
    
    void Start()
    {
        if (vrManager == null)
        {
            vrManager = FindObjectOfType<VRManager>();
        }
        
        if (diagCanvas == null)
        {
            // Criar Canvas de diagnóstico se não existir
            GameObject canvasObj = new GameObject("DiagnosticCanvas");
            diagCanvas = canvasObj.AddComponent<Canvas>();
            diagCanvas.renderMode = RenderMode.WorldSpace;
            
            // Configurar para VR
            var canvasRT = diagCanvas.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(800, 600);
            canvasRT.position = new Vector3(0, 1.7f, 2);
            canvasRT.rotation = Quaternion.Euler(0, 180, 0);
            
            // Adicionar componentes necessários
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Criar painel de fundo
            GameObject panelObj = new GameObject("Panel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);
            RectTransform panelRT = panelImage.rectTransform;
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            
            // Adicionar texto de status
            GameObject textObj = new GameObject("StatusText");
            textObj.transform.SetParent(panelObj.transform, false);
            statusText = textObj.AddComponent<TextMeshProUGUI>();
            statusText.fontSize = 24;
            statusText.color = Color.white;
            statusText.alignment = TextAlignmentOptions.Center;
            RectTransform textRT = statusText.rectTransform;
            textRT.anchorMin = new Vector2(0, 0.7f);
            textRT.anchorMax = new Vector2(1, 0.95f);
            textRT.offsetMin = new Vector2(20, 0);
            textRT.offsetMax = new Vector2(-20, 0);
            
            statusText.text = "Diagnóstico VR Player\nVerificando sistema...";
        }
        
        // Inicializar interface
        if (showDiagnosticOnStart)
        {
            StartCoroutine(InitializeUI());
        }
        else
        {
            // Esconder até ser necessário
            if (diagCanvas != null)
            {
                diagCanvas.gameObject.SetActive(false);
            }
        }
        
        // Iniciar monitoramento de desempenho
        StartCoroutine(MonitorApplicationHealth());
    }
    
    void Update()
    {
        // Monitorar FPS
        frameCount++;
        fpsTimer += Time.deltaTime;
        appUptime += Time.deltaTime;
        
        if (fpsTimer >= fpsMeasureInterval)
        {
            currentFPS = frameCount / fpsTimer;
            frameCount = 0;
            fpsTimer = 0;
        }
    }
    
    // Monitoramento contínuo do estado da aplicação
    IEnumerator MonitorApplicationHealth()
    {
        yield return new WaitForSeconds(20.0f); // Espera inicial para a aplicação inicializar
        
        while (true)
        {
            // Verificar se ainda está na tela de "aguardando" após muito tempo
            if (vrManager != null && !vrManager.isPlaying)
            {
                isWaiting = true;
                
                // Verificar se passou muito tempo e ainda estamos na tela de aguardando
                if (appUptime > 45.0f) // 45 segundos é tempo suficiente para receber comandos
                {
                    Debug.LogWarning("Aplicação presa na tela de aguardando por mais de 45 segundos");
                    
                    // Forçar a exibição da interface de diagnóstico
                    if (!diagCanvas.gameObject.activeSelf)
                    {
                        ShowDiagnostic();
                        
                        // Atualizar status com informações relevantes
                        string statusInfo = "<color=yellow>ALERTA: APLICAÇÃO PRESA</color>\n\n" +
                                           $"Tempo de espera: {appUptime:F0} segundos\n" +
                                           $"FPS: {currentFPS:F1}\n" +
                                           $"Status da conexão: {(vrManager.offlineMode ? "Offline" : GetConnectionStatus())}\n\n" +
                                           "Use os botões abaixo para testar os vídeos diretamente.";
                                           
                        statusText.text = statusInfo;
                    }
                }
            }
            else
            {
                isWaiting = false;
            }
            
            yield return new WaitForSeconds(5.0f); // Verificar a cada 5 segundos
        }
    }
    
    string GetConnectionStatus()
    {
        if (vrManager == null) return "Desconhecido";
        
        // Simplicação - na implementação real, você teria acesso ao estado do websocket
        if (vrManager.diagnosticMode)
        {
            return "Conectado (sem comando)";
        }
        return "Conectado";
    }
    
    IEnumerator InitializeUI()
    {
        yield return new WaitForSeconds(1.0f);
        
        if (isInitialized || diagCanvas == null) yield break;
        isInitialized = true;
        
        // Configurar painel de botões
        GameObject buttonPanel = new GameObject("ButtonPanel");
        buttonPanel.transform.SetParent(diagCanvas.transform, false);
        VerticalLayoutGroup layout = buttonPanel.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = buttonSpacing;
        layout.padding = new RectOffset(20, 20, 20, 20);
        
        RectTransform buttonPanelRT = buttonPanel.GetComponent<RectTransform>();
        buttonPanelRT.anchorMin = new Vector2(0, 0.1f);
        buttonPanelRT.anchorMax = new Vector2(1, 0.6f);
        buttonPanelRT.offsetMin = Vector2.zero;
        buttonPanelRT.offsetMax = Vector2.zero;
        
        // Verificar sistema e criar botões de diagnóstico
        CheckSystem();
        CreateDiagnosticButtons(buttonPanel);
        
        // Adicionar botão para forçar início de vídeo
        CreateButton(buttonPanel, "FORÇAR INICIAR VÍDEO", () => {
            if (vrManager != null)
            {
                Debug.Log("Forçando início de vídeo");
                vrManager.ForcePlayVideo();
                HideDiagnostic();
            }
        });
        
        // Adicionar botão para tentar carregar os vídeos disponíveis
        CreateButton(buttonPanel, "CARREGAR LISTA DE VÍDEOS", () => {
            RefreshVideoList();
        });
        
        statusText.text += "\n\nDiagnóstico inicializado. Use os botões abaixo para testar.";
    }
    
    // Atualizar a lista de vídeos disponíveis
    void RefreshVideoList()
    {
        try
        {
            string externalPath = Path.Combine("/sdcard", "Download");
            if (Directory.Exists(externalPath))
            {
                string[] mp4Files = Directory.GetFiles(externalPath, "*.mp4");
                string fileList = $"<color=green>✓ Encontrados {mp4Files.Length} vídeos</color>\n\n";
                
                if (mp4Files.Length > 0)
                {
                    foreach (string file in mp4Files)
                    {
                        string fileName = Path.GetFileName(file);
                        fileList += $"• {fileName}\n";
                    }
                    statusText.text = fileList;
                }
                else
                {
                    statusText.text = "<color=red>❌ Nenhum vídeo MP4 encontrado em /sdcard/Download</color>\n\n" +
                                     "1. Verifique se os vídeos foram copiados\n" +
                                     "2. Verifique as permissões de armazenamento";
                }
            }
            else
            {
                statusText.text = "<color=red>❌ Pasta /sdcard/Download não encontrada!</color>\n\n" +
                                 "Erro de acesso ao armazenamento externo.";
            }
        }
        catch (System.Exception e)
        {
            statusText.text = $"<color=red>❌ ERRO AO ACESSAR ARQUIVOS:</color>\n\n{e.Message}";
        }
    }
    
    void CheckSystem()
    {
        if (statusText == null) return;
        
        statusText.text = "<b>DIAGNÓSTICO VR PLAYER</b>\n\n";
        
        // 1. Verificar VideoPlayer
        if (vrManager != null && vrManager.videoPlayer != null)
        {
            statusText.text += "✓ VideoPlayer: OK\n";
        }
        else
        {
            statusText.text += "❌ VideoPlayer: Não encontrado\n";
        }
        
        // 2. Verificar pasta de vídeos externos
        try
        {
            string externalPath = Path.Combine("/sdcard", "Download");
            if (Directory.Exists(externalPath))
            {
                string[] mp4Files = Directory.GetFiles(externalPath, "*.mp4");
                statusText.text += $"✓ Pasta /sdcard/Download: {mp4Files.Length} arquivos MP4\n";
                
                // Listar arquivos disponíveis
                if (mp4Files.Length > 0)
                {
                    statusText.text += "Vídeos disponíveis:\n";
                    foreach (string file in mp4Files)
                    {
                        statusText.text += $"- {Path.GetFileName(file)}\n";
                    }
                }
            }
            else
            {
                statusText.text += "❌ Pasta /sdcard/Download: Não encontrada\n";
            }
        }
        catch (System.Exception e)
        {
            statusText.text += $"❌ Erro ao acessar arquivos: {e.Message}\n";
        }
        
        // 3. Verificar conexão
        if (vrManager != null && !vrManager.offlineMode) 
        {
            statusText.text += "📡 Tentando conectar ao servidor...\n";
        }
        else
        {
            statusText.text += "⚠️ Modo offline ativado\n";
        }
        
        // 4. Status atual
        statusText.text += $"\nTempo de execução: {appUptime:F0} segundos\n";
        statusText.text += $"FPS atual: {currentFPS:F1}\n";
    }
    
    void CreateDiagnosticButtons(GameObject parent)
    {
        if (vrManager == null) return;
        
        // 1. Botão para testar vídeo do Rio
        CreateButton(parent, "Reproduzir Rio", () => {
            vrManager.TestPlayVideo("rio.mp4");
            HideDiagnostic();
        });
        
        // 2. Botão para testar vídeo de Pantanal
        CreateButton(parent, "Reproduzir Pantanal", () => {
            vrManager.TestPlayVideo("pantanal.mp4");
            HideDiagnostic();
        });
        
        // 3. Botão para listar arquivos
        CreateButton(parent, "Verificar Arquivos", () => {
            vrManager.TestListFiles();
            RefreshStatus();
        });
        
        // 4. Botão para testar conexão
        CreateButton(parent, "Testar Conexão", () => {
            vrManager.TestConnection();
            RefreshStatus();
        });
        
        // 5. Botão para fechar diagnóstico
        CreateButton(parent, "Fechar Diagnóstico", () => {
            HideDiagnostic();
        });
    }
    
    GameObject CreateButton(GameObject parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObj = new GameObject(label + "Button");
        buttonObj.transform.SetParent(parent.transform, false);
        
        // Configurar RectTransform
        RectTransform rt = buttonObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        
        // Adicionar imagem de fundo
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.6f);
        
        // Adicionar texto
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = label;
        buttonText.fontSize = 24;
        buttonText.alignment = TextAlignmentOptions.Center;
        
        RectTransform textRT = buttonText.rectTransform;
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        
        // Adicionar componente de botão com evento
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(action);
        
        // Configurar estados visuais do botão
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.6f);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.8f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.4f);
        button.colors = colors;
        
        return buttonObj;
    }
    
    public void ShowDiagnostic()
    {
        if (diagCanvas != null)
        {
            diagCanvas.gameObject.SetActive(true);
            RefreshStatus();
        }
    }
    
    public void HideDiagnostic()
    {
        if (diagCanvas != null)
        {
            diagCanvas.gameObject.SetActive(false);
        }
    }
    
    void RefreshStatus()
    {
        CheckSystem();
    }
    
    // Para mostrar diagnóstico a partir de qualquer lugar
    public static void ShowDiagnosticUI()
    {
        DiagnosticUI diag = FindObjectOfType<DiagnosticUI>();
        if (diag != null)
        {
            diag.ShowDiagnostic();
        }
        else
        {
            GameObject diagObj = new GameObject("DiagnosticUI");
            diag = diagObj.AddComponent<DiagnosticUI>();
            diag.showDiagnosticOnStart = true;
        }
    }
} 