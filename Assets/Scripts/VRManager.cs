#if UNITY_2019_3_OR_NEWER
#define USING_XR_MANAGEMENT
#endif

// Definição automática do Oculus SDK removida - agora é manual
//#if UNITY_PACKAGE_MANAGER_SUPPORTS_OCULUS || USING_VR_MANAGEMENT
//#define USING_OCULUS_SDK
//#endif

using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Networking; // Adicionado para UnityWebRequest
using System.IO; // Adicionado para Path
using System.Collections.Generic; // Adicionado para Dictionary
using System.Linq; // Adicionado para Where
using System.Diagnostics; // Adicionado para Process
using Debug = UnityEngine.Debug; // Especificar explicitamente qual Debug usar

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
#endif

#if USING_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

// Adicione essa diretiva para os trechos que usam o SDK do Oculus
#if USING_OCULUS_SDK
using Oculus.VR;
#endif

public class VRManager : MonoBehaviour {
    [Header("Video Settings")]
    public VideoPlayer videoPlayer;
    public string[] videoFiles = { "lencois.mp4", "rio.mp4", "pantanal.mp4" };
    private string currentVideo = "";

    [Header("External Storage Settings")]
    [Tooltip("Pasta externa onde os vídeos estão armazenados")]
    public string externalFolder = "Download"; // Pasta padrão de downloads
    [Tooltip("Tentar carregar vídeos do armazenamento externo primeiro")]
    public bool useExternalStorage = true;
    [Tooltip("Continuar mesmo se o acesso ao armazenamento externo for negado")]
    public bool continueWithoutStorage = true; // Nova opção para ignorar erros de permissão

    [Header("Fade Settings")]
    public GameObject fadeSphere;

    [Header("View Restriction Settings")]
    [Tooltip("Objeto que contém a esfera do vídeo 360")]
    public Transform videoSphere;

    private bool isViewRestricted = false;
    private Transform cameraTransform;
    private bool isTransitioning = false;

    [Header("Networking")]
    public string serverUri = "ws://192.168.1.30:8181";
    private ClientWebSocket webSocket;

    [Header("UI Elements")]
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI debugText; // Texto para mostrar informações de debug

    // Alterado para público para que outros componentes possam verificar
    public bool isPlaying = false;
    private string externalStoragePath = "";
    private bool hasAutoStarted = false; // Controla se o vídeo já foi iniciado automaticamente
    private bool waitingForCommands = true;
    private float waitingTimer = 0f;

    [Header("Connection Settings")]
    [Tooltip("Intervalo de verificação de conexão em segundos")]
    public float connectionCheckInterval = 5f;
    [Tooltip("Número máximo de tentativas de reconexão")]
    public int maxReconnectAttempts = 10;
    private int reconnectAttempts = 0;
    private bool isReconnecting = false;
    private bool wasPaused = false;

    [Header("Debug Settings")]
    [Tooltip("Ativa modo de diagnóstico com mais informações")]
    public bool diagnosticMode = true;
    [Tooltip("Permanecer offline, não tentar conectar ao servidor")]
    public bool offlineMode = true; // Alterado para TRUE por padrão
    [Tooltip("Ignorar erros de rede e continuar")]
    public bool ignoreNetworkErrors = true;
    [Tooltip("Tempo em segundos para mostrar diagnóstico se a aplicação não avançar")]
    public float showDiagnosticTimeout = 10f; // Reduzido para 10 segundos
    [Tooltip("Tempo em segundos para iniciar automaticamente um vídeo se ficar preso")]
    public float autoStartVideoTimeout = 15f; // Novo campo
    [Tooltip("Habilitar autostart de vídeo quando offline")]
    public bool enableAutoStart = false; // Nova opção para controlar autostart

    [Header("Rotation Debug Settings")]
    [Tooltip("Habilitar debug visual da rotação")]
    public bool enableRotationDebug = true;
    [Tooltip("Bloquear rotação da câmera")]
    public bool isRotationLocked = false;
    [Tooltip("Velocidade de rotação no editor")]
    public float editorRotationSpeed = 60f;
    [Tooltip("Ângulo máximo de rotação vertical")]
    public float maxVerticalAngle = 180f;
    [Tooltip("Ângulo máximo de rotação horizontal")]
    public float maxHorizontalAngle = 180f;

    [Header("Editor Test Settings")]
    [Tooltip("Ponto focal padrão para testes no editor")]
    public Vector3 defaultFocalPoint = new Vector3(0, 0, 0);
    [Tooltip("Ângulo máximo de desvio permitido durante foco")]
    public float maxFocalDeviation = 30f;
    [Tooltip("Velocidade de retorno ao ponto focal")]
    public float returnToFocalSpeed = 2f;
    private bool isManualFocusActive = false;

    private Quaternion northOrientation = Quaternion.identity; // Referência da orientação "norte"
    private bool isNorthCorrectionActive = false; // Flag para controlar correção da orientação

    [Header("VR Settings")]
    [Tooltip("Referência ao XR Origin - necessário para controle de rotação em VR")]
    public Transform xrOrigin;

    [Header("Camera References")]
    [Tooltip("Referência manual para a câmera principal (opcional)")]
    public Camera mainCamera;
    private Transform activeCamera;

    // Estrutura para armazenar intervalos de bloqueio
    [System.Serializable]
    public class LockTimeRange {
        public float startTime;
        public float endTime;
        [Tooltip("Ângulo máximo de rotação permitido")]
        public float maxAngle = 75f;
        [Tooltip("Velocidade de retorno ao centro")]
        public float resetSpeed = 2f;
        
        public LockTimeRange(float start, float end) {
            startTime = start;
            endTime = end;
        }
    }

    // Dicionário de intervalos de bloqueio por vídeo - alterado para ser configurável pelo editor
    [System.Serializable]
    public class VideoLockSettings {
        public string videoFileName;
        public List<LockTimeRange> lockRanges = new List<LockTimeRange>();
    }

    [Header("Lock Time Ranges")]
    [Tooltip("Configure aqui os intervalos de bloqueio para cada vídeo")]
    public List<VideoLockSettings> videoLockSettings = new List<VideoLockSettings>();
    private Dictionary<string, List<LockTimeRange>> lockTimeRanges = new Dictionary<string, List<LockTimeRange>>();

    void Awake() {
        #if UNITY_ANDROID && !UNITY_EDITOR
        // Configurar para tela cheia no Meta Quest 3
        Screen.fullScreen = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        
        // Forçar modo VR e configurações de renderização específicas para 360°
        #if USING_OCULUS_SDK
        try {
            // Configurações do OVRManager para Quest 3 - só executa se o SDK estiver disponível
            OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            OVRManager.instance.useRecommendedMSAALevel = true;
            
            // Configurar display
            if (OVRPlugin.systemDisplayFrequenciesAvailable) {
                // Definir frequência de atualização para 90Hz
                OVRPlugin.systemDisplayFrequency = 90.0f;
                Debug.Log("Definida frequência de atualização para 90Hz no Meta Quest");
            }
            
            // Desativar filtro antialiasing que pode causar problemas
            OVRManager.eyeTextureAntiAliasing = OVRManager.EyeTextureAntiAliasing.NoAntiAliasing;
            
            // Configurar para experiência 360 imersiva
            Debug.Log("👉 Configurado OVRManager para modo de visualização 360°");
            
            // Forçar um redimensionamento da tela
            OVRManager.display.RecenterDisplay();
        }
        catch (Exception e) {
            Debug.LogError("Erro ao configurar OVRManager: " + e.Message);
        }
        #else
        // Configurações para Quest sem o SDK do Oculus
        Debug.Log("Oculus SDK não encontrado. Usando configurações básicas de VR.");
        
        // Configurações básicas de renderização para VR sem Oculus SDK
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 72; // Framerate padrão para Quest 3
        #endif
        
        Debug.Log("⚠️ SOLICITANDO PERMISSÕES NO AWAKE...");
        ForceRequestStoragePermission();
        
        // Garantir que o modo de renderização é apropriado para 360°
        QualitySettings.vSyncCount = 0; // Desabilitar VSync para melhor performance em VR
        Application.targetFrameRate = 72; // Definir framerate alvo para dispositivos Meta Quest
        
        // Configurar câmera para modo imersivo se estiver usando Video360
        var videoSphere = FindObjectOfType<VideoPlayer>()?.gameObject;
        if (videoSphere != null) {
            Debug.Log("Configurando esfera de vídeo para modo 360°");
            
            // Garantir que a esfera de vídeo esteja configurada corretamente
            var sphereRenderer = videoSphere.GetComponent<Renderer>();
            if (sphereRenderer != null) {
                sphereRenderer.material.SetInt("_ZWrite", 0);
                sphereRenderer.material.SetInt("_Cull", 1); // Cull Front - renderizar o interior da esfera
            }
        }
        #endif
    }

    async void Start() {
        try {
            Debug.Log("✅ Cliente VR iniciando...");
            
            // Mostrar mensagem inicial para informar que a aplicação está iniciando
            if (messageText != null) {
                messageText.text = "Iniciando aplicação VR...";
                messageText.gameObject.SetActive(true);
            }
            
            if (diagnosticMode) {
                // Adicionar texto de debug visível
                UpdateDebugText("Inicializando VR Player...");
            }
            
            // Inicializar referências da câmera
            InitializeCameraReferences();
            
            #if UNITY_ANDROID && !UNITY_EDITOR && USING_OCULUS_SDK
            // Verificar se existe o OVRCameraRig na cena e configurá-lo corretamente
            var ovrRig = FindObjectOfType<OVRCameraRig>();
            if (ovrRig != null) {
                Debug.Log("Verificando configuração do OVRCameraRig para 360°...");
                
                // Garantir que o tracking origin seja floor para experiência melhor
                var ovrManager = FindObjectOfType<OVRManager>();
                if (ovrManager != null) {
                    ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                    Debug.Log("OVRManager tracking origin definido para FloorLevel");
                }
                
                // Posicionar o OVRCameraRig no centro da cena
                if (videoSphere != null) {
                    ovrRig.transform.position = videoSphere.position;
                    Debug.Log("OVRCameraRig posicionado no centro da esfera de vídeo");
                    
                    // Desativar qualquer componente que restrinja movimento
                    var limiter = ovrRig.GetComponent<CameraRotationLimiter>();
                    if (limiter != null && limiter.enabled) {
                        limiter.enabled = false;
                        Debug.Log("⚠️ CameraRotationLimiter desativado para permitir visão 360°");
                    }
                }
                
                // Verificar se o render scale está adequado para Quest 3
                float recommendedScale = 1.3f; // Valor recomendado para Quest 3
                if (OVRManager.instance != null && OVRManager.instance.eyeTextureResolutionScale < recommendedScale) {
                    OVRManager.instance.eyeTextureResolutionScale = recommendedScale;
                    Debug.Log($"Render scale ajustado para {recommendedScale} para melhor qualidade no Quest 3");
                }
            } else {
                Debug.LogWarning("OVRCameraRig não encontrado na cena. A experiência 360° pode não funcionar corretamente.");
            }
            #else
            // Configuração alternativa para quando não temos o Oculus SDK
            Debug.Log("Usando configuração VR alternativa para 360° sem OVRCameraRig");
            
            // Posicionar a camera no centro da esfera de vídeo
            if (videoSphere != null && activeCamera != null) {
                activeCamera.transform.position = videoSphere.position;
                Debug.Log("Câmera posicionada no centro da esfera de vídeo");
                
                // Verificar se há um limitador de rotação
                var limiter = FindObjectOfType<CameraRotationLimiter>();
                if (limiter != null) {
                    // Configurar o limitador para a esfera de vídeo
                    limiter.sphereTransform = videoSphere;
                    Debug.Log("CameraRotationLimiter configurado para a esfera de vídeo");
                    
                    // Verificar se deve ativar a limitação
                    if (videoPlayer != null && videoPlayer.isPlaying) {
                        limiter.IsLimitActive = true;
                    }
                }
            }
            #endif
            
            SetFadeAlpha(0); // Inicialmente, tela transparente
            StartCoroutine(FadeOutToIdle());

            // Verificar se o videoPlayer está configurado
            bool videoPlayerReady = EnsureVideoPlayerIsReady();
            if (!videoPlayerReady) {
                UpdateDebugText("ALERTA: VideoPlayer não encontrado! Alguns recursos podem não funcionar.");
                if (messageText != null) {
                    messageText.text = "AVISO: VideoPlayer não configurado";
                }
            }

            // Agora altera a mensagem para "Aguardando..."
            if (messageText != null) {
                messageText.text = "Aguardando início da sessão...";
            }
            
            // Iniciar o timer de espera
            waitingForCommands = true;
            waitingTimer = 0f;
            hasAutoStarted = false;

            // Adicionar o ConnectionHelper se não existir
            ConnectionHelper connectionHelper = FindObjectOfType<ConnectionHelper>();
            if (connectionHelper == null)
            {
                GameObject helperObj = new GameObject("ConnectionHelper");
                connectionHelper = helperObj.AddComponent<ConnectionHelper>();
                connectionHelper.vrManager = this;

                // Configurar callback para quando a conexão ficar presa
                connectionHelper.OnConnectionStuck += (msg) => {
                    LogError(msg);
                    UpdateDebugText("ALERTA: " + msg + "\n\nToque em um botão do controle para mostrar diagnóstico");
                    
                    // Se estiver muito tempo sem resposta, tentar modo offline
                    if (waitingTimer > showDiagnosticTimeout * 2)
                    {
                        Log("Ativando modo offline automaticamente após muito tempo sem resposta");
                        offlineMode = true;
                    }
                };
            }
            
            // Configurar o evento para quando o vídeo terminar
            if (videoPlayer != null) {
                videoPlayer.loopPointReached += OnVideoEnd;
            } else {
                LogError("VideoPlayer não encontrado!");
            }

            // Verificar se o fadeSphere está configurado
            if (fadeSphere == null) {
                LogError("FadeSphere não está configurado. Algumas transições podem não funcionar.");
            }
            
            // Verificar se o videoSphere está configurado
            if (videoSphere == null) {
                LogError("VideoSphere não está configurado. O bloqueio de visão não vai funcionar.");
                // Tenta encontrar automaticamente
                var viewSetup = FindObjectOfType<ViewSetup>();
                if (viewSetup != null && viewSetup.videoPlayerObject != null) {
                    videoSphere = viewSetup.videoPlayerObject;
                    Log("VideoSphere configurado automaticamente: " + videoSphere.name);
                }
            }

            try {
                // Inicializar caminho do armazenamento externo
                InitializeExternalStorage();
            }
            catch (Exception e) {
                LogError("Erro ao inicializar armazenamento externo: " + e.Message);
                if (continueWithoutStorage) {
                    Debug.Log("🔄 Continuando sem acesso ao armazenamento externo devido a erro: " + e.Message);
                    useExternalStorage = false; // Desativar uso do armazenamento externo automaticamente
                }
            }
            
            try {
                // Inicializar intervalos de bloqueio para cada vídeo
                InitializeLockTimeRanges();
            }
            catch (Exception e) {
                LogError("Erro ao inicializar intervalos de bloqueio: " + e.Message);
            }

            // Configurações de bloqueio - configurar para modo 1 (fixar em ponto específico)
            // Este modo geralmente funciona melhor no Quest
            Log("Configurando o modo de bloqueio para fixar em ponto específico");

            if (!offlineMode) {
                try {
                    await ConnectWebSocket();
                    // Inicia a verificação periódica de conexão
                    InvokeRepeating(nameof(CheckConnection), connectionCheckInterval, connectionCheckInterval);
                }
                catch (Exception e) {
                    LogError("Erro ao conectar ao servidor: " + e.Message);
                    if (diagnosticMode) {
                        UpdateDebugText("ERRO DE CONEXÃO: " + e.Message + "\nModo offline ativado.");
                    }
                    
                    // Sempre ativar modo offline em caso de erro
                    offlineMode = true;
                    Debug.Log("⚠️ Modo offline ativado automaticamente após erro de conexão");
                }
            } else {
                Log("Modo offline ativado. Não conectando ao servidor.");
                if (diagnosticMode) {
                    UpdateDebugText("MODO OFFLINE\nCarregando primeiro vídeo disponível...");
                }
                
                // Iniciar automaticamente um vídeo em modo offline após um curto delay
                Invoke(nameof(AutoStartFirstVideo), 3f);
            }
            
            // Adiciona botões de diagnóstico se necessário
            if (diagnosticMode) {
                StartCoroutine(CreateDiagnosticButtons());
            }
            
            // Desativar scripts conflitantes para garantir controle centralizado
            DisableConflictingScripts();
            
            Log("Inicialização concluída!");

            #if UNITY_EDITOR
            // Abrir a janela de debug automaticamente no editor
            RotationDebugUI.ShowWindow();
            #endif
        }
        catch (Exception e) {
            LogError("ERRO CRÍTICO durante inicialização: " + e.Message);
            UpdateDebugText("ERRO CRÍTICO: " + e.Message + "\n\nReinicie o aplicativo.");
            
            // Em caso de erro crítico, tentar modo offline e autostart
            offlineMode = true;
            Invoke(nameof(AutoStartFirstVideo), 5f);
        }
    }
    
    // Helper para logs condicionais
    void Log(string message) {
        if (diagnosticMode) {
            Debug.Log("🔍 [DIAGNÓSTICO] " + message);
        } else {
            Debug.Log(message);
        }
    }
    
    // Helper para logs de erro
    void LogError(string message) {
        Debug.LogError("❌ " + message);
    }

    // Helper para logs de alerta
    void LogWarning(string message) {
        Debug.LogWarning("⚠️ " + message);
    }

    // Criar botões de diagnóstico na interface
    IEnumerator CreateDiagnosticButtons() {
        yield return new WaitForSeconds(2.0f);
        
        // Aqui criaremos botões para testes básicos
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) {
            yield break;
        }
        
        // Criar botões para teste de vídeos
        foreach (string videoFile in videoFiles) {
            GameObject button = new GameObject("Button_" + videoFile);
            button.transform.SetParent(canvas.transform, false);
            
            // Adicionar componentes do botão...
            // (Código simplificado para evitar complexidade)
            
            // Se estamos em modo UI apenas, adicionar evento diretamente
            Button btn = button.AddComponent<Button>();
            string videoName = videoFile; // Captura para o lambda
            btn.onClick.AddListener(() => PlayVideoWithFade(videoName));
            
            Log("Botão de diagnóstico criado para: " + videoFile);
        }
    }

    // Configuração dos intervalos de bloqueio para cada vídeo
    void InitializeLockTimeRanges() {
        // Limpar dicionário antigo
        lockTimeRanges.Clear();
        
        // Primeiro carregar do editor
        foreach (var setting in videoLockSettings) {
            if (!string.IsNullOrEmpty(setting.videoFileName)) {
                lockTimeRanges[setting.videoFileName] = setting.lockRanges;
                Log($"Carregados {setting.lockRanges.Count} intervalos para o vídeo {setting.videoFileName} das configurações do editor");
            }
        }
        
        // Se não temos dados do editor para alguns vídeos, adicionar os valores padrão
        if (!lockTimeRanges.ContainsKey("rio.mp4")) {
            List<LockTimeRange> rioRanges = new List<LockTimeRange> {
                new LockTimeRange(20, 44) { maxAngle = 75f, resetSpeed = 2f },
                new LockTimeRange(159, 228) { maxAngle = 75f, resetSpeed = 2f } // 02:39 até 03:48
            };
            lockTimeRanges.Add("rio.mp4", rioRanges);
            Log("Adicionados intervalos padrão para rio.mp4");
        }
        
        if (!lockTimeRanges.ContainsKey("amazonia.mp4")) {
            List<LockTimeRange> amazoniaRanges = new List<LockTimeRange> {
                new LockTimeRange(115, 130) { maxAngle = 75f, resetSpeed = 2f }, // 01:55 até 02:10
                new LockTimeRange(197, 205) { maxAngle = 75f, resetSpeed = 2f }  // 03:17 até 03:25
            };
            lockTimeRanges.Add("amazonia.mp4", amazoniaRanges);
            Log("Adicionados intervalos padrão para amazonia.mp4");
        }
        
        if (!lockTimeRanges.ContainsKey("noronha.mp4")) {
            List<LockTimeRange> noronhaRanges = new List<LockTimeRange> {
                new LockTimeRange(40, 59) { maxAngle = 75f, resetSpeed = 2f }
            };
            lockTimeRanges.Add("noronha.mp4", noronhaRanges);
            Log("Adicionados intervalos padrão para noronha.mp4");
        }
        
        Debug.Log("🔒 Intervalos de bloqueio configurados para todos os vídeos");
        
        // Exibir informações para depuração
        foreach (var entry in lockTimeRanges) {
            string videoName = entry.Key;
            List<LockTimeRange> ranges = entry.Value;
            
            string rangeInfo = $"Vídeo {videoName} tem {ranges.Count} intervalos de bloqueio:";
            foreach (var range in ranges) {
                rangeInfo += $"\n  - {range.startTime}s a {range.endTime}s";
            }
            
            Debug.Log(rangeInfo);
        }
    }

    // Atualizar texto de debug na UI
    void UpdateDebugText(string message) {
        if (debugText != null) {
            debugText.text = message;
            debugText.gameObject.SetActive(true); // Garante que está visível
        } else if (diagnosticMode) {
            // Se não temos debugText mas estamos em modo diagnóstico, 
            // tenta criar um texto de debug
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null) {
                GameObject debugObj = new GameObject("DebugText");
                debugObj.transform.SetParent(canvas.transform, false);
                debugText = debugObj.AddComponent<TextMeshProUGUI>();
                debugText.fontSize = 24;
                debugText.color = Color.white;
                debugText.rectTransform.anchoredPosition = new Vector2(0, 200);
                debugText.rectTransform.sizeDelta = new Vector2(600, 400);
                debugText.alignment = TextAlignmentOptions.Center;
                debugText.text = message;
            }
        }
    }

    void Update() {
        try {
            // Verificar referências importantes
            if (videoSphere == null) {
                videoSphere = GameObject.Find("VideoSphere")?.transform;
                if (videoSphere == null) {
                    var possibleSpheres = FindObjectsOfType<Transform>().Where(t => 
                        t.name.Contains("Video") && t.name.Contains("Sphere") ||
                        t.name.Contains("360") && t.name.Contains("Sphere"));
                    
                    if (possibleSpheres.Any()) {
                        videoSphere = possibleSpheres.First();
                        Debug.Log("VideoSphere encontrado automaticamente: " + videoSphere.name);
                    }
                }
            }
        
            // Verificar se o vídeo está tocando e se deve aplicar restrição de visualização
            if (videoPlayer != null && videoPlayer.isPlaying && !string.IsNullOrEmpty(currentVideo)) {
                CheckForViewRestriction();
            }

            // Controles para teste no editor
            #if UNITY_EDITOR
            HandleEditorControls();
            #endif

            // Verificar timeout para mostrar diagnóstico se estiver aguardando muito tempo
            if (waitingForCommands) {
                waitingTimer += Time.deltaTime;
                
                // Após o timeout, mostrar interface de diagnóstico
                if (waitingTimer > showDiagnosticTimeout && !hasAutoStarted) {
                    LogError("Timeout ao aguardar comandos do servidor após " + showDiagnosticTimeout + " segundos");
                    
                    // Mostrar diagnóstico
                    var diagnostic = FindObjectOfType<DiagnosticUI>();
                    if (diagnostic != null) {
                        diagnostic.ShowDiagnostic();
                        UpdateDebugText("Aguardou muito tempo por comandos.\nInterface de diagnóstico ativada.");
                    } else {
                        // Tentar criar se não existir
                        DiagnosticUI.ShowDiagnosticUI();
                    }
                }
                
                // Após mais tempo, iniciar automaticamente um vídeo apenas se enableAutoStart for true
                if (waitingTimer > autoStartVideoTimeout && !hasAutoStarted && enableAutoStart) {
                    LogError("Timeout prolongado. Iniciando vídeo automaticamente.");
                    waitingForCommands = false;
                    
                    // Ativar modo offline e iniciar primeiro vídeo
                    offlineMode = true;
                    AutoStartFirstVideo();
                }
            }

            // Debug visual da rotação
            if (enableRotationDebug && cameraTransform != null) {
                DrawRotationDebug();
            }
        }
        catch (Exception e) {
            LogError("Erro no Update: " + e.Message);
        }
    }

    // Verifica se o tempo atual do vídeo está em um intervalo de bloqueio
    void CheckForViewRestriction() {
        if (videoPlayer == null || !videoPlayer.isPlaying || string.IsNullOrEmpty(currentVideo)) 
            return;
            
        float currentTime = (float)videoPlayer.time;
        bool shouldBeRestricted = false;
        LockTimeRange currentRange = null;
        
        // Procura nos intervalos de bloqueio do vídeo atual
        if (lockTimeRanges.ContainsKey(currentVideo)) {
            foreach (var range in lockTimeRanges[currentVideo]) {
                if (currentTime >= range.startTime && currentTime <= range.endTime) {
                    shouldBeRestricted = true;
                    currentRange = range;
                    break;
                }
            }
        }
        
        // Atualizar estado de restrição
        isViewRestricted = shouldBeRestricted;
        
        // Se estamos em um período de bloqueio, aplicar restrições
        if (isViewRestricted && currentRange != null) {
            ApplyViewRestriction(currentRange);
        }
    }
    
    // Aplica a restrição de visualização ajustando a rotação da esfera de vídeo
    void ApplyViewRestriction(LockTimeRange range) {
        #if UNITY_ANDROID && !UNITY_EDITOR
        if (xrOrigin == null || cameraTransform == null) return;

        // Obter rotação atual do XROrigin
        float currentYaw = xrOrigin.eulerAngles.y;
        if (currentYaw > 180) currentYaw -= 360;

        // Se a rotação exceder o limite
        if (Mathf.Abs(currentYaw) > range.maxAngle) {
            // Calcular a rotação alvo mantendo X e Z
            Vector3 currentRotation = xrOrigin.eulerAngles;
            float clampedYaw = Mathf.Clamp(currentYaw, -range.maxAngle, range.maxAngle);
            
            Quaternion targetRotation = Quaternion.Euler(
                currentRotation.x,
                clampedYaw,
                currentRotation.z
            );

            // Aplicar rotação suave
            xrOrigin.rotation = Quaternion.Lerp(
                xrOrigin.rotation,
                targetRotation,
                range.resetSpeed * Time.deltaTime
            );
        }
        #else
        if (videoSphere == null || cameraTransform == null) return;

        // Obter rotação atual
        Vector3 currentRotation = videoSphere.localEulerAngles;
        float currentYaw = currentRotation.y;
        if (currentYaw > 180) currentYaw -= 360;

        // Se exceder o limite
        if (Mathf.Abs(currentYaw) > range.maxAngle) {
            // Calcular rotação alvo
            float clampedYaw = Mathf.Clamp(currentYaw, -range.maxAngle, range.maxAngle);
            Quaternion targetRotation = Quaternion.Euler(
                currentRotation.x,
                clampedYaw,
                currentRotation.z
            );

            // Aplicar rotação suave
            videoSphere.rotation = Quaternion.Lerp(
                videoSphere.rotation,
                targetRotation,
                range.resetSpeed * Time.deltaTime
            );
        }
        #endif
    }

    // Método para verificar se o VideoPlayer está configurado corretamente
    bool EnsureVideoPlayerIsReady() {
        if (videoPlayer == null) {
            videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer == null) {
                LogError("VideoPlayer não encontrado! Tentando encontrar na cena...");
                videoPlayer = FindObjectOfType<VideoPlayer>();
                if (videoPlayer == null) {
                    LogError("Nenhum VideoPlayer encontrado na cena!");
                    return false;
                }
            }
        }
        
        // Verificar se o VideoPlayer tem uma renderTexture ou um targetCamera
        if (videoPlayer.targetTexture == null && videoPlayer.targetCamera == null) {
            LogWarning("VideoPlayer não tem targetTexture ou targetCamera configurado!");
        }
        
        return true;
    }

    // Método para reproduzir um vídeo com fade
    public void PlayVideoWithFade(string videoName) {
        if (string.IsNullOrEmpty(videoName)) {
            LogError("Nome do vídeo não especificado!");
            return;
        }
        
        Debug.Log($"🎬 Iniciando reprodução do vídeo: {videoName}");
        
        // Resetar o timer quando inicia um vídeo
        waitingForCommands = false;
        waitingTimer = 0f;
        hasAutoStarted = true; // Marcar como iniciado para evitar autostart
        
        // Atualizar o vídeo atual
        currentVideo = videoName;
        
        // Resetar o estado de bloqueio ao iniciar novo vídeo
        isViewRestricted = false;
        
        // Para testes: exibir informações sobre os intervalos de bloqueio
        if (diagnosticMode && lockTimeRanges.ContainsKey(videoName)) {
            string blockInfo = $"Vídeo {videoName} tem {lockTimeRanges[videoName].Count} bloqueios:";
            foreach (var block in lockTimeRanges[videoName]) {
                blockInfo += $"\n{block.startTime}s-{block.endTime}s";
            }
            UpdateDebugText(blockInfo);
        }
        
        // Iniciar o fade in
        StartCoroutine(FadeIn(() => {
            // Após o fade in, configurar e iniciar o vídeo
            PrepareAndPlayVideo(videoName);
            
            // Inicializar com orientação neutra
            if (videoSphere != null) {
                videoSphere.rotation = Quaternion.identity;
                Log("Vídeo iniciado com orientação padrão");
            }
        }));
    }
    
    // Método para preparar e reproduzir um vídeo
    void PrepareAndPlayVideo(string videoName) {
        try {
            if (videoPlayer == null) {
                LogError("VideoPlayer não está configurado!");
                return;
            }
            
            // Ativar o objeto do VideoPlayer se estiver desativado
            if (!videoPlayer.gameObject.activeSelf) {
                videoPlayer.gameObject.SetActive(true);
            }
            
            // Parar qualquer vídeo em reprodução
            if (videoPlayer.isPlaying) {
                videoPlayer.Stop();
            }
            
            // Configurar o caminho do vídeo
            string videoPath = "";
            bool videoFound = false;
            
            // Tentar carregar do armazenamento externo primeiro, se habilitado
            if (useExternalStorage) {
                try {
                    string externalPath = Path.Combine(externalStoragePath, videoName);
                    if (File.Exists(externalPath)) {
                        videoPath = externalPath;
                        videoFound = true;
                        Debug.Log($"✅ Vídeo encontrado no armazenamento externo: {externalPath}");
                    } else {
                        Debug.LogWarning($"⚠️ Vídeo não encontrado no armazenamento externo: {externalPath}");
                    }
                } catch (Exception e) {
                    LogError($"Erro ao acessar armazenamento externo: {e.Message}");
                }
            }
            
            // Se não encontrou no armazenamento externo, usar o caminho interno (streaming assets)
            if (!videoFound) {
                #if UNITY_ANDROID && !UNITY_EDITOR
                videoPath = Path.Combine(Application.streamingAssetsPath, videoName);
                #else
                videoPath = "file://" + Path.Combine(Application.streamingAssetsPath, videoName);
                #endif
                
                Debug.Log($"🔄 Usando vídeo interno: {videoPath}");
            }
            
            // Configurar o VideoPlayer
            videoPlayer.url = videoPath;
            videoPlayer.isLooping = false;
            videoPlayer.playOnAwake = false;
            
            // Configurações adicionais para garantir que o vídeo seja reproduzido em 360°
            #if UNITY_ANDROID && !UNITY_EDITOR
            // Garantir que o renderMode é apropriado para 360°
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetMaterialRenderer = videoSphere?.GetComponent<Renderer>();
            videoPlayer.targetMaterialProperty = "_MainTex";
            
            // Configurar a esfera de vídeo para rotação correta
            if (videoSphere != null) {
                // Resetar a rotação da esfera para garantir visualização correta
                videoSphere.rotation = Quaternion.identity;
                Debug.Log("Esfera de vídeo resetada para visualização 360° ideal");
                
                // Garantir que renderização da esfera esteja correta (dentro para fora)
                Renderer sphereRenderer = videoSphere.GetComponent<Renderer>();
                if (sphereRenderer != null && sphereRenderer.material != null) {
                    sphereRenderer.material.SetInt("_Cull", 1); // Front culling - renderiza apenas o interior
                    sphereRenderer.material.SetInt("_ZWrite", 0); // Desativa escrita no z-buffer
                    Debug.Log("Material da esfera configurado para visualização 360°");
                }
                
                // Verificar se há um ViewSetup na cena e forçar reconfiguração
                var viewSetup = FindObjectOfType<ViewSetup>();
                if (viewSetup != null && viewSetup.videoPlayerObject != null) {
                    viewSetup.forceFullSphereRendering = true;
                    
                    // Verificar se o método ConfigureVideoSphereForFullRendering existe
                    var methodInfo = viewSetup.GetType().GetMethod("ConfigureVideoSphereForFullRendering", 
                        System.Reflection.BindingFlags.Instance | 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic);
                    
                    if (methodInfo != null) {
                        methodInfo.Invoke(viewSetup, null);
                        Debug.Log("ViewSetup solicitado para reconfigurar esfera de vídeo");
                    } else {
                        // Configuração manual da esfera
                        ConfigureVideoSphereForVR(viewSetup.videoPlayerObject);
                    }
                }
                
                // Atualizar o CameraRotationLimiter
                var limiter = FindObjectOfType<CameraRotationLimiter>();
                if (limiter != null) {
                    limiter.sphereTransform = videoSphere;
                    limiter.videoPlayer = videoPlayer;
                    Debug.Log("CameraRotationLimiter configurado para o novo vídeo");
                }
            }
            #endif
            
            // Configurar eventos
            videoPlayer.prepareCompleted += (vp) => {
                Debug.Log("✅ Vídeo preparado com sucesso!");
                vp.Play();
                isPlaying = true;
                
                // Iniciar o fade out após o vídeo começar
                StartCoroutine(FadeOut());
                
                // Atualizar a UI
                if (messageText != null) {
                    messageText.text = $"Reproduzindo: {videoName}";
                    StartCoroutine(HideMessageAfterDelay(3f));
                }
                
                // Enviar timecode a cada 5 segundos
                InvokeRepeating(nameof(SendTimecode), 5f, 5f);
            };
            
            videoPlayer.errorReceived += (vp, error) => {
                LogError($"❌ Erro no VideoPlayer: {error}");
                UpdateDebugText($"ERRO DE VÍDEO: {error}\nVerifique se o arquivo existe e tem permissões.");
                isPlaying = false;
            };
            
            // Preparar o vídeo (carrega o vídeo mas não inicia a reprodução)
            videoPlayer.Prepare();
            
            Debug.Log($"🔄 Preparando vídeo: {videoPath}");
            
            // Mostrar mensagem de carregamento
            if (messageText != null) {
                messageText.text = "Carregando vídeo...";
                messageText.gameObject.SetActive(true);
            }
            
            // Configurar a transparência do vídeo para 1 (totalmente visível)
            videoPlayer.targetCameraAlpha = 1;
        } catch (Exception e) {
            LogError($"❌ Erro ao preparar vídeo: {e.Message}");
            UpdateDebugText($"ERRO: {e.Message}");
        }
    }
    
    // Método auxiliar para configurar a esfera de vídeo para VR
    private void ConfigureVideoSphereForVR(Transform sphere) {
        if (sphere == null) return;
        
        // Configurar o material da esfera
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null) {
            // Configurar culling para renderizar o interior da esfera
            renderer.material.SetInt("_Cull", 1); // Front culling
            renderer.material.SetInt("_ZWrite", 0); // Desativar escrita no z-buffer
            
            Debug.Log("Esfera de vídeo configurada manualmente para VR");
        }
        
        // Verificar se temos um MeshFilter para inverter normais se necessário
        MeshFilter meshFilter = sphere.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null) {
            // Verificar se as normais já estão invertidas
            bool needsInversion = true;
            
            // Criar uma cópia da malha para não modificar o asset original
            if (needsInversion) {
                try {
                    Mesh mesh = Instantiate(meshFilter.sharedMesh);
                    
                    // Inverter normais
                    Vector3[] normals = mesh.normals;
                    for (int i = 0; i < normals.Length; i++) {
                        normals[i] = -normals[i];
                    }
                    mesh.normals = normals;
                    
                    // Inverter ordem dos triângulos para renderização correta
                    int[] triangles = mesh.triangles;
                    for (int i = 0; i < triangles.Length; i += 3) {
                        int temp = triangles[i];
                        triangles[i] = triangles[i + 2];
                        triangles[i + 2] = temp;
                    }
                    mesh.triangles = triangles;
                    
                    // Atribuir malha modificada
                    meshFilter.mesh = mesh;
                    Debug.Log("Normais da esfera invertidas para renderização interna");
                } catch (Exception e) {
                    Debug.LogError("Erro ao inverter normais da esfera: " + e.Message);
                }
            }
        }
    }
    
    // Método para pausar o vídeo
    public void PauseVideo() {
        if (videoPlayer != null && videoPlayer.isPlaying) {
            videoPlayer.Pause();
            Debug.Log("⏸️ Vídeo pausado");
            
            // Atualizar a UI
            if (messageText != null) {
                messageText.text = "Vídeo pausado";
                messageText.gameObject.SetActive(true);
            }
            
            // Parar de enviar timecodes
            CancelInvoke(nameof(SendTimecode));
        }
    }
    
    // Método para retomar a reprodução do vídeo
    public void ResumeVideo() {
        if (videoPlayer != null && !videoPlayer.isPlaying) {
            videoPlayer.Play();
            Debug.Log("▶️ Vídeo retomado");
            
            // Atualizar a UI
            if (messageText != null) {
                messageText.text = "Vídeo retomado";
                StartCoroutine(HideMessageAfterDelay(2f));
            }
            
            // Retomar envio de timecodes
            InvokeRepeating(nameof(SendTimecode), 5f, 5f);
        }
    }
    
    // Método para parar o vídeo
    public void StopVideo() {
        if (videoPlayer != null) {
            videoPlayer.Stop();
            isPlaying = false;
            Debug.Log("⏹️ Vídeo parado");
            
            // Atualizar a UI
            if (messageText != null) {
                messageText.text = "Vídeo parado";
                StartCoroutine(HideMessageAfterDelay(2f));
            }
            
            // Parar de enviar timecodes
            CancelInvoke(nameof(SendTimecode));
        }
    }
    
    // Método para parar o vídeo com fade
    public void StopVideoWithFade() {
        Debug.Log("🎬 Parando vídeo com fade");
        
        // Iniciar o fade in
        StartCoroutine(FadeIn(() => {
            // Após o fade in, parar o vídeo
            StopVideo();
            
            // Iniciar o fade out para o estado de espera
            StartCoroutine(FadeOutToIdle());
            
            // Atualizar a UI
            if (messageText != null) {
                messageText.text = "Aguardando próximo vídeo...";
                messageText.gameObject.SetActive(true);
            }
        }));
    }

    // Novo método para desenhar o debug visual da rotação
    void DrawRotationDebug() {
        if (!enableRotationDebug || cameraTransform == null) return;

        // Atualizar o debug UI no editor
        #if UNITY_EDITOR
        // O RotationDebugUI já cuida da visualização
        return;
        #endif
    }

    // Método atualizado para controles no editor
    #if UNITY_EDITOR
    void HandleEditorControls() {
        if (videoSphere == null) return;

        // Se a rotação estiver bloqueada, não permitir movimento
        if (isRotationLocked) return;

        // Controle de foco com tecla F
        if (Input.GetKeyDown(KeyCode.F)) {
            isManualFocusActive = !isManualFocusActive;
            Log(isManualFocusActive ? "Foco manual ativado" : "Foco manual desativado");
        }

        // Detectar teclas de setas
        float horizontalInput = 0f;
        float verticalInput = 0f;

        // FUNDAMENTAL: Para criar uma experiência natural de visualização 360°,
        // quando o usuário pressiona uma tecla de direção, a esfera deve girar na direção OPOSTA

        // Quando o usuário pressiona Esquerda, quer olhar para a esquerda,
        // então rotacionamos a esfera para a direita (valor positivo)
        if (Input.GetKey(KeyCode.LeftArrow)) horizontalInput = 1f;
        
        // Quando o usuário pressiona Direita, quer olhar para a direita,
        // então rotacionamos a esfera para a esquerda (valor negativo)
        if (Input.GetKey(KeyCode.RightArrow)) horizontalInput = -1f;
        
        // Quando o usuário pressiona Cima, quer olhar para cima,
        // então rotacionamos a esfera para baixo (valor positivo no eixo X)
        if (Input.GetKey(KeyCode.UpArrow)) verticalInput = 1f;
        
        // Quando o usuário pressiona Baixo, quer olhar para baixo,
        // então rotacionamos a esfera para cima (valor negativo no eixo X)
        if (Input.GetKey(KeyCode.DownArrow)) verticalInput = -1f;
        
        float rotationSpeed = editorRotationSpeed * Time.deltaTime;
        
        if (horizontalInput != 0 || verticalInput != 0) {
            // Simulando a experiência de estar dentro da esfera olhando para fora

            // Pegar a rotação atual
            Vector3 currentRotation = videoSphere.eulerAngles;

            // Aplicar as rotações
            currentRotation.y += horizontalInput * rotationSpeed;
            currentRotation.x += verticalInput * rotationSpeed;

            // Limitação do ângulo vertical para evitar problemas
            float xAngle = currentRotation.x;
            if (xAngle > 180) xAngle -= 360;  // Converter para o intervalo -180 a 180
            
            // Limitar o ângulo vertical
            if (Mathf.Abs(xAngle) > maxVerticalAngle) {
                xAngle = Mathf.Clamp(xAngle, -maxVerticalAngle, maxVerticalAngle);
                currentRotation.x = xAngle;
            }
            
            // Manter o Z em zero para que o horizonte permaneça nivelado
            currentRotation.z = 0;
            
            // Aplicar a rotação
            videoSphere.eulerAngles = currentRotation;
            
            // Log para debug
            if (enableRotationDebug) {
                Debug.Log($"Visualizando: Ângulo X (cima/baixo)={xAngle:F1}° Y (esq/dir)={currentRotation.y:F1}°");
            }
        }

        // Resetar rotação com barra de espaço
        if (Input.GetKeyDown(KeyCode.Space)) {
            videoSphere.rotation = Quaternion.identity;
            Log("Visualização resetada para o centro");
        }
        
        // Teclas para ajuste da velocidade de rotação
        if (Input.GetKey(KeyCode.LeftBracket)) { // [
            editorRotationSpeed = Mathf.Max(10f, editorRotationSpeed - 10f * Time.deltaTime);
            Debug.Log($"Velocidade de rotação reduzida: {editorRotationSpeed:F1}");
        }
        
        if (Input.GetKey(KeyCode.RightBracket)) { // ]
            editorRotationSpeed = Mathf.Min(120f, editorRotationSpeed + 10f * Time.deltaTime);
            Debug.Log($"Velocidade de rotação aumentada: {editorRotationSpeed:F1}");
        }
        
        // Teclas para ajuste do ângulo vertical máximo
        if (Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus)) {
            maxVerticalAngle = Mathf.Min(maxVerticalAngle + 5f * Time.deltaTime, 180f);
            Debug.Log($"Ângulo vertical máximo: {maxVerticalAngle:F1}°");
        }
        
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) {
            maxVerticalAngle = Mathf.Max(maxVerticalAngle - 5f * Time.deltaTime, 10f);
            Debug.Log($"Ângulo vertical máximo: {maxVerticalAngle:F1}°");
        }
    }
    #endif

    void OnApplicationPause(bool pauseStatus) {
        Debug.Log($"🎮 Aplicação {(pauseStatus ? "pausada" : "retomada")}");
        wasPaused = pauseStatus;
        
        if (!pauseStatus) {
            // Aplicação foi retomada após pausa (usuário colocou o headset novamente)
            Debug.Log("🔄 Headset retomado, verificando conexão...");
            CancelInvoke(nameof(CheckConnection)); // Cancela verificação anterior se houver
            Invoke(nameof(CheckConnection), 2f); // Verifica conexão após 2 segundos
            UpdateDebugText("Verificando conexão após retomada...");
        }
    }
    
    // Verificar periodicamente se a conexão está ativa
    void CheckConnection() {
        if (webSocket == null || webSocket.State != WebSocketState.Open) {
            if (!isReconnecting) {
                Debug.LogWarning("🔍 Conexão WebSocket fechada ou inválida. Tentando reconectar...");
                UpdateDebugText("Conexão perdida. Reconectando...");
                ReconnectWebSocket();
            }
        } else {
            // Conexão está OK, reseta contagem de tentativas
            reconnectAttempts = 0;
            
            // Envia um ping para garantir que a conexão está ativa
            SendPing();
        }
    }
    
    async void SendPing() {
        try {
            if (webSocket != null && webSocket.State == WebSocketState.Open) {
                string pingMessage = "PING:" + DateTime.Now.Ticks;
                byte[] data = Encoding.UTF8.GetBytes(pingMessage);
                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                Debug.Log("📡 Ping enviado");
            }
        } catch (Exception e) {
            Debug.LogError($"❌ Erro ao enviar ping: {e.Message}");
            if (!isReconnecting) {
                ReconnectWebSocket();
            }
        }
    }

    async void ReconnectWebSocket() {
        if (isReconnecting) return;
        
        isReconnecting = true;
        
        if (reconnectAttempts >= maxReconnectAttempts) {
            Debug.LogError($"❌ Excedido número máximo de tentativas de reconexão ({maxReconnectAttempts})");
            UpdateDebugText("Falha na reconexão. Tente reiniciar o aplicativo.");
            isReconnecting = false;
            return;
        }
        
        reconnectAttempts++;
        
        // Fecha a conexão anterior se ainda existir
        if (webSocket != null) {
            try {
                // Tenta fechar a conexão de forma limpa
                if (webSocket.State == WebSocketState.Open) {
                    CancellationTokenSource cts = new CancellationTokenSource(1000); // Timeout de 1 segundo
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconectando", cts.Token);
                }
                webSocket.Dispose();
            } catch (Exception e) {
                Debug.LogWarning($"⚠️ Erro ao fechar websocket: {e.Message}");
            }
        }
        
        Debug.Log($"🔄 Tentativa de reconexão {reconnectAttempts}/{maxReconnectAttempts}...");
        UpdateDebugText($"Reconectando... Tentativa {reconnectAttempts}/{maxReconnectAttempts}");
        
        // Aguarda um tempo com base no número de tentativas (backoff exponencial)
        float waitTime = Mathf.Min(1 * Mathf.Pow(1.5f, reconnectAttempts - 1), 10);
        await Task.Delay((int)(waitTime * 1000));
        
        // Tenta conectar novamente
        await ConnectWebSocket();
        
        isReconnecting = false;
    }

    async Task ConnectWebSocket() {
        webSocket = new ClientWebSocket();
        Debug.Log("🌐 Tentando conectar ao WebSocket em " + serverUri);

        try {
            await webSocket.ConnectAsync(new Uri(serverUri), CancellationToken.None);
            Debug.Log("✅ Conexão WebSocket bem-sucedida.");
            UpdateDebugText("Conexão estabelecida");
            ReceiveMessages();
            SendClientInfo(); // Enviar informações do cliente ao conectar
            
            // Conexão bem-sucedida, reseta contador de tentativas
            reconnectAttempts = 0;
        } catch (Exception e) {
            Debug.LogError($"❌ Erro ao conectar ao WebSocket: {e.Message}");
            UpdateDebugText("Erro de conexão: " + e.Message);
            
            // Agenda uma nova tentativa
            if (!isReconnecting) {
                Invoke(nameof(ReconnectWebSocket), 5f);
            }
        }
    }

    async void ReceiveMessages() {
        byte[] buffer = new byte[1024];

        while (webSocket != null && webSocket.State == WebSocketState.Open) {
            try {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Close) {
                    Debug.LogWarning("🔌 Servidor solicitou fechamento da conexão");
                    break;
                }
                
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // Ignora mensagens de ping
                if (message.StartsWith("PING:")) continue;
                
                Debug.Log($"🔵 Mensagem recebida do Admin: {message}");
                ProcessReceivedMessage(message);
            } catch (Exception e) {
                if (webSocket == null) break;
                
                Debug.LogError($"❌ Erro ao receber mensagem: {e.Message}");
                break;
            }
        }

        Debug.LogWarning("🚨 Loop de recebimento de mensagens encerrado!");
        
        // Só tenta reconectar se não estiver em processo de reconexão e o objeto ainda existir
        if (!isReconnecting && webSocket != null && this != null && !this.isActiveAndEnabled) {
            Debug.Log("🔄 Agendando reconexão após falha no recebimento de mensagens");
            // Usar um coroutine em vez de Invoke para evitar problemas de referência
            StartCoroutine(ReconnectAfterDelay(2f));
        }
    }
    
    // Novo método para substituir o Invoke que estava causando problemas
    private IEnumerator ReconnectAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        if (this != null && this.isActiveAndEnabled) {
            ReconnectWebSocket();
        }
    }

    void ProcessReceivedMessage(string message) {
        Debug.Log($"📩 Mensagem recebida do Admin: {message}");
        
        // Resetar o timer quando recebe qualquer mensagem
        waitingForCommands = false;
        waitingTimer = 0f;
        
        // Notificar o ConnectionHelper
        ConnectionHelper connectionHelper = FindObjectOfType<ConnectionHelper>();
        if (connectionHelper != null)
        {
            connectionHelper.NotifyMessageReceived(message);
        }

        if (message.StartsWith("play:")) {
            string movieName = message.Substring(5).Trim();
            PlayVideoWithFade(movieName);
        } 
        else if (message.Contains("pause")) {
            PauseVideo();
        } 
        else if (message.Contains("resume")) {
            ResumeVideo();
        } 
        else if (message.Contains("stop")) {
            StopVideo();
        } 
        else if (message.StartsWith("aviso:")) {
            string avisoMensagem = message.Substring(6).Trim();
            ShowTemporaryMessage(avisoMensagem);
        } 
        else if (message.StartsWith("seek:")) {
            string timeStr = message.Substring(5).Trim(); // Correção para pegar apenas o valor numérico
            if (double.TryParse(timeStr, out double time)) {
                if (videoPlayer != null) {
                    videoPlayer.time = time;
                    Debug.Log($"⏱️ Vídeo posicionado para: {time:F2} segundos");
                }
            } else {
                Debug.LogWarning($"⚠️ Valor inválido para 'seek': {message}");
            }
        } 
        else {
            Debug.LogWarning($"⚠️ Mensagem desconhecida recebida: {message}");
        }
    }

    void ShowTemporaryMessage(string message) {
        if (messageText != null) {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
        }
    }

    IEnumerator HideMessageAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        HideMessage();
    }

    // Oculta a mensagem
    void HideMessage() {
        if (messageText != null) {
            messageText.gameObject.SetActive(false);
        }
    }

    void OnMessageReceived(string message) {
        Debug.Log($"📩 Mensagem recebida do Admin: {message}");

        // Exibe a mensagem ao receber do Admin
        if (messageText != null) {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
        }

        ProcessReceivedMessage(message);
    }

    void OnVideoEnd(VideoPlayer vp) {
        Debug.Log("🎬 Vídeo concluído! Retornando ao lobby...");
        StopVideoWithFade();
    }

    IEnumerator FadeIn(Action onComplete = null) {
        float duration = 0.7f;
        float elapsedTime = 0;
        Material fadeMat = fadeSphere.GetComponent<Renderer>().material;

        while (elapsedTime < duration) {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0, 1, elapsedTime / duration);
            fadeMat.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        onComplete?.Invoke();
    }

    IEnumerator FadeOut() {
        float duration = 0.7f;
        float elapsedTime = 0;
        Material fadeMat = fadeSphere.GetComponent<Renderer>().material;

        while (elapsedTime < duration) {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1, 0, elapsedTime / duration);
            fadeMat.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
    }

    IEnumerator FadeOutToIdle() {
        float duration = 0.7f;
        float elapsedTime = 0;
        Material fadeMat = fadeSphere.GetComponent<Renderer>().material;

        while (elapsedTime < duration) {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1, 0, elapsedTime / duration);
            fadeMat.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        videoPlayer.targetCameraAlpha = 0;
        videoPlayer.gameObject.SetActive(false);
    }

    void SetFadeAlpha(float alpha) {
        Material fadeMat = fadeSphere.GetComponent<Renderer>().material;
        fadeMat.color = new Color(0, 0, 0, alpha);
    }

    void SendTimecode() {
        if (videoPlayer.isPlaying) {
            float currentTime = (float)videoPlayer.time;
            string timecodeMessage = $"TIMECODE:{currentTime:F1}";

            Debug.Log($"⏱️ Enviando timecode: {timecodeMessage}");

            if (webSocket != null && webSocket.State == WebSocketState.Open) {
                byte[] data = Encoding.UTF8.GetBytes(timecodeMessage);
                webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            } else {
                Debug.LogError("❌ Erro ao enviar timecode: WebSocket não está conectado.");
            }
        }
    }

    async void SendClientInfo() {
        string clientName = SystemInfo.deviceName;
        string clientIP = GetLocalIPAddress();
        string clientOS = SystemInfo.operatingSystem;
        int batteryLevel = GetBatteryLevel();

        string infoMessage = $"CLIENT_INFO:{clientName}|{clientIP}|{clientOS}|{batteryLevel}%";

        if (webSocket.State == WebSocketState.Open) {
            byte[] data = Encoding.UTF8.GetBytes(infoMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.Log($"✅ Enviando informações do cliente: {infoMessage}");
        }
    }

    // Obtém o nível da bateria no Android ou no Windows
    int GetBatteryLevel() {
        #if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
        using (AndroidJavaObject batteryStatus = activity.Call<AndroidJavaObject>("registerReceiver", null, intentFilter)) {
            int level = batteryStatus.Call<int>("getIntExtra", "level", -1);
            int scale = batteryStatus.Call<int>("getIntExtra", "scale", -1);
            if (level == -1 || scale == -1) return -1;
            return (int)((level / (float)scale) * 100);
        }
        #elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return (int)(SystemInfo.batteryLevel * 100); // Em alguns PCs pode não funcionar corretamente
        #else
        return -1; // Plataforma não suportada
        #endif
    }

    string GetLocalIPAddress() {
        string localIP = "127.0.0.1";
        foreach (var ip in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())) {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                localIP = ip.ToString();
                break;
            }
        }
        return localIP;
    }

    void OnDestroy() {
        // Limpa as invocações pendentes
        CancelInvoke();
        
        // Fecha a conexão WebSocket de forma limpa
        if (webSocket != null && webSocket.State == WebSocketState.Open) {
            try {
                // A operação é assíncrona, mas no OnDestroy não podemos aguardar
                // Estamos apenas iniciando o processo de fechamento
                webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Aplicativo fechado", CancellationToken.None);
            } catch (Exception e) {
                Debug.LogError($"❌ Erro ao fechar WebSocket: {e.Message}");
            }
        }
    }

    // Métodos de teste para diagnóstico
    public void TestPlayVideo(string videoName) {
        if (string.IsNullOrEmpty(videoName)) {
            videoName = videoFiles.Length > 0 ? videoFiles[0] : "rio.mp4";
        }
        
        Log("Teste de reprodução manual: " + videoName);
        PlayVideoWithFade(videoName);
    }
    
    public void TestConnection() {
        Log("Teste de conexão manual");
        offlineMode = false;
        ReconnectWebSocket();
    }
    
    public void TestListFiles() {
        Log("Teste de listagem de arquivos manual");
        DebugExternalStorageFiles();
    }

    // Método para testar se consegue iniciar um vídeo sem servidor
    public void ForcePlayVideo(string videoName = "")
    {
        if (string.IsNullOrEmpty(videoName))
        {
            // Tentar encontrar um vídeo disponível
            string[] availableVideos = null;
            
            try {
                string externalPath = Path.Combine("/sdcard", "Download");
                if (Directory.Exists(externalPath))
                {
                    availableVideos = Directory.GetFiles(externalPath, "*.mp4");
                }
            }
            catch (Exception e) {
                LogError("Erro ao listar vídeos disponíveis: " + e.Message);
            }
            
            if (availableVideos != null && availableVideos.Length > 0)
            {
                // Usar o primeiro vídeo encontrado
                string fileName = Path.GetFileName(availableVideos[0]);
                videoName = fileName;
                Log("Vídeo encontrado para reprodução forçada: " + fileName);
            }
            else if (videoFiles.Length > 0)
            {
                // Usar o primeiro da lista padrão
                videoName = videoFiles[0];
            }
            else
            {
                // Último recurso
                videoName = "rio.mp4";
            }
        }
        
        // Ativar modo offline
        Log("Ativando modo offline para reprodução forçada");
        offlineMode = true;
        
        // Reproduzir o vídeo
        Log("Forçando reprodução de: " + videoName);
        PlayVideoWithFade(videoName);
    }

    // Novo método para iniciar automaticamente o primeiro vídeo disponível
    void AutoStartFirstVideo()
    {
        if (hasAutoStarted) return;  // Evita múltiplos autostarts
        
        hasAutoStarted = true;
        Debug.Log("🔄 Iniciando reprodução automática do primeiro vídeo disponível...");
        
        // Se já estiver reproduzindo um vídeo, não faz nada
        if (isPlaying) return;
        
        // Tenta encontrar vídeos disponíveis no armazenamento externo
        string[] availableVideos = null;
        try {
            string externalPath = Path.Combine("/sdcard", "Download");
            if (Directory.Exists(externalPath))
            {
                availableVideos = Directory.GetFiles(externalPath, "*.mp4");
            }
        }
        catch (Exception e) {
            LogError("Erro ao listar vídeos disponíveis para autostart: " + e.Message);
        }
        
        string videoToPlay = "";
        
        // Se encontrou vídeos externos, usa o primeiro
        if (availableVideos != null && availableVideos.Length > 0)
        {
            videoToPlay = Path.GetFileName(availableVideos[0]);
            Log("Autostart: Vídeo externo encontrado: " + videoToPlay);
        }
        // Senão, tenta usar um da lista pré-definida
        else if (videoFiles.Length > 0)
        {
            videoToPlay = videoFiles[0];
            Log("Autostart: Usando vídeo da lista pré-definida: " + videoToPlay);
        }
        else
        {
            // Último recurso
            videoToPlay = "rio.mp4";
            Log("Autostart: Usando vídeo padrão: " + videoToPlay);
        }
        
        // Reproduzir o vídeo
        Log("Iniciando reprodução automática de: " + videoToPlay);
        PlayVideoWithFade(videoToPlay);
        
        // Atualizar UI
        if (messageText != null) {
            messageText.text = "Modo offline: reproduzindo " + videoToPlay;
        }
    }

    // Corrigir o método para encontrar corretamente os scripts conflitantes
    void DisableConflictingScripts() {
        // Verificar se há um CameraRotationLimiter na cena e desativá-lo
        var allScripts = FindObjectsOfType<MonoBehaviour>();
        bool foundLimiter = false;
        
        foreach (var script in allScripts) {
            // Procurar CameraRotationLimiter
            if (script.GetType().Name == "CameraRotationLimiter") {
                script.enabled = false; // Desativa o script sem remover o componente
                Log("CameraRotationLimiter encontrado e desativado. Usando controle centralizado no VRManager.");
                foundLimiter = true;
            }
            
            // Verificar ViewSetup - mantemos ele ativo, mas temos que coordenar
            if (script.GetType().Name == "ViewSetup") {
                if (script.enabled) {
                    Log("ViewSetup encontrado e ativo. VRManager irá coordenar o controle da rotação.");
                }
            }
        }
        
        if (!foundLimiter) {
            Log("Nenhum CameraRotationLimiter encontrado. VRManager já tem controle total.");
        }
    }

    // Inicializa o caminho do armazenamento externo
    void InitializeExternalStorage() {
        try {
            #if UNITY_ANDROID && !UNITY_EDITOR
            // No Android, configurar para o diretório de download padrão
            externalStoragePath = Path.Combine("/sdcard", externalFolder);
            
            // Verificar se o diretório existe
            if (!Directory.Exists(externalStoragePath)) {
                // Tentar diretório alternativo
                externalStoragePath = Path.Combine(Application.persistentDataPath, externalFolder);
                if (!Directory.Exists(externalStoragePath)) {
                    // Criar o diretório se não existir
                    Directory.CreateDirectory(externalStoragePath);
                }
            }
            Log($"Armazenamento externo configurado para: {externalStoragePath}");
            #else
            // No editor ou outras plataformas, usar StreamingAssets
            externalStoragePath = Application.streamingAssetsPath;
            Log($"Usando StreamingAssets para vídeos: {externalStoragePath}");
            #endif
            
            // Listar os arquivos disponíveis
            DebugExternalStorageFiles();
        } catch (Exception e) {
            LogError($"Erro ao inicializar armazenamento externo: {e.Message}");
            // Fallback para caminho interno
            externalStoragePath = Application.streamingAssetsPath;
        }
    }
    
    // Método para ajudar no diagnóstico do acesso a arquivos externos
    void DebugExternalStorageFiles() {
        if (!diagnosticMode) return;
        
        try {
            string[] searchPaths = new string[] { 
                externalStoragePath,
                Application.streamingAssetsPath,
                Application.persistentDataPath,
                "/sdcard/Download",
                "/sdcard/Movies"
            };
            
            foreach (string path in searchPaths) {
                if (!Directory.Exists(path)) {
                    Log($"Diretório não encontrado: {path}");
                    continue;
                }
                
                string[] files = Directory.GetFiles(path, "*.mp4");
                if (files.Length > 0) {
                    Log($"Encontrados {files.Length} vídeos MP4 em: {path}");
                    foreach (string file in files) {
                        Log($"  - {Path.GetFileName(file)}");
                    }
                } else {
                    Log($"Nenhum vídeo MP4 encontrado em: {path}");
                }
            }
        } catch (Exception e) {
            LogError($"Erro ao listar arquivos: {e.Message}");
        }
    }

    // Método para forçar a solicitação de permissão de armazenamento no Android
    void ForceRequestStoragePermission() {
        #if UNITY_ANDROID && !UNITY_EDITOR
        try {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext")) {
                // Verificar versões do Android e permissões necessárias
                string[] permissions = new string[] {
                    "android.permission.READ_EXTERNAL_STORAGE",
                    "android.permission.WRITE_EXTERNAL_STORAGE"
                };
                
                bool hasPermissions = true;
                foreach (string permission in permissions) {
                    int checkResult = activity.Call<int>("checkSelfPermission", permission);
                    if (checkResult != 0) { // PackageManager.PERMISSION_GRANTED == 0
                        hasPermissions = false;
                        break;
                    }
                }
                
                if (!hasPermissions) {
                    Debug.Log("⚠️ Solicitando permissões de armazenamento...");
                    activity.Call("requestPermissions", permissions, 101);
                } else {
                    Debug.Log("✅ Permissões de armazenamento já concedidas");
                }
            }
        } catch (Exception e) {
            LogError($"Erro ao solicitar permissões: {e.Message}");
        }
        #endif
    }

    void InitializeCameraReferences()
    {
        // Primeiro tentar usar a câmera configurada manualmente
        if (mainCamera != null)
        {
            activeCamera = mainCamera.transform;
            Log("Usando câmera configurada manualmente: " + mainCamera.name);
            return;
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        // No Quest, tentar encontrar a câmera do XR Rig
        if (xrOrigin != null)
        {
            var xrCamera = xrOrigin.GetComponentInChildren<Camera>();
            if (xrCamera != null)
            {
                activeCamera = xrCamera.transform;
                Log("Usando câmera do XR Rig: " + xrCamera.name);
                return;
            }
        }
        
        // Procurar por câmeras com nomes comuns do Quest
        string[] questCameraNames = new string[] { "CenterEyeAnchor", "OVRCameraRig" };
        foreach (string name in questCameraNames)
        {
            var questCamera = GameObject.Find(name)?.GetComponent<Camera>();
            if (questCamera != null)
            {
                activeCamera = questCamera.transform;
                Log("Usando câmera Quest encontrada: " + questCamera.name);
                return;
            }
        }
        #endif

        // Fallback para Camera.main
        if (Camera.main != null)
        {
            activeCamera = Camera.main.transform;
            Log("Usando Camera.main: " + Camera.main.name);
            return;
        }

        // Último recurso: procurar qualquer câmera na cena
        var cameras = FindObjectsOfType<Camera>();
        if (cameras.Length > 0)
        {
            activeCamera = cameras[0].transform;
            LogWarning("Usando primeira câmera encontrada: " + cameras[0].name);
            return;
        }

        LogError("Nenhuma câmera encontrada! O sistema de rotação não funcionará.");
    }

    #if UNITY_EDITOR
    [MenuItem("Ferramentas/Compilar Windows %#b")]
    public static void BuildWindowsPlayerDirectly()
    {
        BuildWindowsPlayer("Builds/Windows", "SocketClient", false, true);
    }

    public static void BuildWindowsPlayer(string buildPath, string buildName, bool developmentBuild, bool showLogs)
    {
        try
        {
            // Salva a plataforma atual
            BuildTarget currentTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup currentTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            
            // Define o caminho completo da build
            string fullPath = Path.Combine(Application.dataPath, "..", buildPath);
            
            // Cria o diretório se não existir
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
            
            // Define o caminho do executável
            string exePath = Path.Combine(fullPath, buildName + ".exe");
            
            // Obtém as cenas ativas no build settings
            string[] scenes = GetEnabledScenes();
            
            if (scenes.Length == 0)
            {
                Debug.LogError("Não há cenas adicionadas ao Build Settings. Adicione pelo menos uma cena.");
                EditorUtility.DisplayDialog("Erro de Compilação", 
                    "Não há cenas adicionadas ao Build Settings. Adicione pelo menos uma cena.", "OK");
                return;
            }
            
            // Define as opções de build
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = developmentBuild ? BuildOptions.Development : BuildOptions.None
            };
            
            // Inicia a build
            if (showLogs)
                Debug.Log("Iniciando compilação para Windows em: " + exePath);
                
            // Versão para Unity mais recente que retorna BuildReport
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;
            
            // Verifica se houve erro
            if (summary.result == BuildResult.Succeeded)
            {
                if (showLogs)
                {
                    Debug.Log("Build concluída com sucesso!");
                    Debug.Log($"Tamanho: {summary.totalSize / 1048576} MB");
                    Debug.Log($"Tempo: {summary.totalTime.TotalSeconds:F2} segundos");
                }
                
                EditorUtility.DisplayDialog("Build Concluída", 
                    $"Build concluída com sucesso em:\n{exePath}", "OK");
                
                // Abre o explorador no local da build
                EditorUtility.RevealInFinder(fullPath);
            }
            else
            {
                string errorMessage = $"Build falhou com resultado: {summary.result}";
                Debug.LogError(errorMessage);
                EditorUtility.DisplayDialog("Erro de Compilação", errorMessage, "OK");
            }
            
            // Restaura a plataforma original se for diferente
            if (currentTarget != BuildTarget.StandaloneWindows64)
            {
                if (showLogs)
                    Debug.Log($"Restaurando plataforma original: {currentTarget}");
                
                EditorUserBuildSettings.SwitchActiveBuildTarget(currentTargetGroup, currentTarget);
            }
        }
        catch (Exception e)
        {
            string errorMessage = $"Erro durante a compilação: {e.Message}";
            Debug.LogError(errorMessage);
            EditorUtility.DisplayDialog("Erro de Compilação", errorMessage, "OK");
        }
    }
    
    private static string[] GetEnabledScenes()
    {
        // Retorna todas as cenas habilitadas no Build Settings
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        List<string> enabledScenes = new List<string>();
        
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.enabled)
            {
                enabledScenes.Add(scene.path);
            }
        }
        
        return enabledScenes.ToArray();
    }
    
    [MenuItem("Ferramentas/Compilador Windows")]
    public static void ShowBuilderWindow()
    {
        BuilderWindow window = EditorWindow.GetWindow<BuilderWindow>("Compilador Windows");
        window.Show();
    }
    
    // Classe para janela do editor
    public class BuilderWindow : EditorWindow
    {
        private string buildPath = "Builds/Windows";
        private string buildName = "SocketClient";
        private bool developmentBuild = false;
        private bool showLogs = true;
        
        private void OnGUI()
        {
            GUILayout.Label("Configurações de Compilação", EditorStyles.boldLabel);
            
            buildPath = EditorGUILayout.TextField("Caminho da Build:", buildPath);
            buildName = EditorGUILayout.TextField("Nome da Build:", buildName);
            developmentBuild = EditorGUILayout.Toggle("Build de Desenvolvimento:", developmentBuild);
            showLogs = EditorGUILayout.Toggle("Mostrar Logs:", showLogs);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Compilar para Windows"))
            {
                BuildWindowsPlayer(buildPath, buildName, developmentBuild, showLogs);
            }
        }
    }

    [MenuItem("Ferramentas/GitHub/Commit Alterações %#g")]
    public static void CommitChangesToGitHub()
    {
        GitHubWindow window = EditorWindow.GetWindow<GitHubWindow>("GitHub Commit");
        window.Show();
    }
    
    [MenuItem("Ferramentas/GitHub/Ver Histórico")]
    public static void ShowGitHistory()
    {
        RunGitCommand("log --pretty=format:\"%h - %an, %ar : %s\" -10", "Histórico de Commits", true);
    }
    
    [MenuItem("Ferramentas/GitHub/Atualizar Repositório")]
    public static void PullFromGitHub()
    {
        RunGitCommand("pull", "Atualizar do GitHub", true);
    }
    
    [MenuItem("Ferramentas/GitHub/Ver Status")]
    public static void CheckGitStatus()
    {
        RunGitCommand("status", "Status do Repositório", true);
    }
    
    private static void RunGitCommand(string command, string title, bool showResult = false)
    {
        try
        {
            string workingDir = Path.GetDirectoryName(Application.dataPath);
            Process process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = command;
            process.StartInfo.WorkingDirectory = workingDir;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            
            string output = "";
            string error = "";
            
            process.OutputDataReceived += (sender, args) => {
                if (args.Data != null)
                    output += args.Data + "\n";
            };
            
            process.ErrorDataReceived += (sender, args) => {
                if (args.Data != null)
                    error += args.Data + "\n";
            };
            
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                Debug.LogError($"Erro ao executar comando git: {error}");
                EditorUtility.DisplayDialog("Erro Git", $"Erro ao executar comando git:\n{error}", "OK");
            }
            else if (showResult)
            {
                Debug.Log($"Resultado do comando git: {output}");
                EditorUtility.DisplayDialog(title, output, "OK");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao executar git: {e.Message}");
            EditorUtility.DisplayDialog("Erro", $"Falha ao executar git:\n{e.Message}", "OK");
        }
    }
    
    // Classe para janela de commits do GitHub
    public class GitHubWindow : EditorWindow
    {
        private string commitMessage = "";
        private Vector2 scrollPosition;
        private bool pushAfterCommit = true;
        private bool viewChangesFirst = true;
        private string gitStatus = "";
        private bool isRefreshing = false;
        
        private void OnEnable()
        {
            // Carregar o status do git ao abrir a janela
            RefreshGitStatus();
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Commit para GitHub", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            GUILayout.Label("Status do Repositório", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
            GUI.enabled = false;
            EditorGUILayout.TextArea(isRefreshing ? "Carregando..." : gitStatus);
            GUI.enabled = true;
            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("Atualizar Status"))
            {
                RefreshGitStatus();
            }
            
            EditorGUILayout.Space();
            
            GUILayout.Label("Mensagem de Commit (CHANGELOG):", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Descreva quais alterações foram feitas nesta versão", MessageType.Info);
            
            // Área de texto para mensagem de commit
            commitMessage = EditorGUILayout.TextArea(commitMessage, GUILayout.Height(100));
            
            EditorGUILayout.Space();
            
            // Opções
            viewChangesFirst = EditorGUILayout.Toggle("Visualizar alterações antes", viewChangesFirst);
            pushAfterCommit = EditorGUILayout.Toggle("Push após commit", pushAfterCommit);
            
            EditorGUILayout.Space();
            
            // Botões
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Visualizar Alterações"))
            {
                RunGitCommand("diff", "Alterações", true);
            }
            
            if (GUILayout.Button("Adicionar Tudo"))
            {
                RunGitCommand("add .", "Adicionar Arquivos", false);
                RefreshGitStatus();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Verifica se a mensagem de commit está vazia
            GUI.enabled = !string.IsNullOrEmpty(commitMessage);
            
            if (GUILayout.Button("Commit & Push"))
            {
                PerformCommitAndPush();
            }
            
            GUI.enabled = true;
        }
        
        private void RefreshGitStatus()
        {
            isRefreshing = true;
            gitStatus = "Carregando...";
            
            EditorApplication.delayCall += () => {
                try
                {
                    string workingDir = Path.GetDirectoryName(Application.dataPath);
                    Process process = new Process();
                    process.StartInfo.FileName = "git";
                    process.StartInfo.Arguments = "status";
                    process.StartInfo.WorkingDirectory = workingDir;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    gitStatus = output;
                }
                catch (Exception e)
                {
                    gitStatus = $"Erro ao obter status: {e.Message}";
                    Debug.LogError($"Erro ao obter status do git: {e.Message}");
                }
                
                isRefreshing = false;
                Repaint();
            };
        }
        
        private void PerformCommitAndPush()
        {
            try
            {
                // Se o usuário quiser visualizar as alterações primeiro
                if (viewChangesFirst)
                {
                    RunGitCommand("diff --staged", "Alterações para Commit", true);
                }
                
                // Adicionar arquivos ao staging, se ainda não foram adicionados
                RunGitCommand("add .", "Adicionar Arquivos", false);
                
                // Fazer o commit com a mensagem
                string safeMessage = commitMessage.Replace("\"", "\\\"");
                RunGitCommand($"commit -m \"{safeMessage}\"", "Commit", false);
                
                // Push se selecionado
                if (pushAfterCommit)
                {
                    RunGitCommand("push", "Push para GitHub", false);
                }
                
                // Mostrar mensagem de sucesso
                EditorUtility.DisplayDialog("Sucesso", "Alterações enviadas para o GitHub com sucesso!", "OK");
                
                // Limpar a mensagem de commit
                commitMessage = "";
                
                // Atualizar o status
                RefreshGitStatus();
            }
            catch (Exception e)
            {
                Debug.LogError($"Erro durante commit/push: {e.Message}");
                EditorUtility.DisplayDialog("Erro", $"Erro durante commit/push:\n{e.Message}", "OK");
            }
        }
    }

    [MenuItem("Ferramentas/VR/Verificar Configurações para Quest 3 %#v")]
    public static void CheckQuestVRSettings()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("Verificação de Configurações para Meta Quest 3");
        report.AppendLine("============================================");
        report.AppendLine();
        
        bool hasIssues = false;
        
        // 1. Verificar a plataforma atual
        BuildTarget currentTarget = EditorUserBuildSettings.activeBuildTarget;
        if (currentTarget != BuildTarget.Android)
        {
            report.AppendLine("❌ Plataforma atual não é Android. Mude para Android nas configurações de build.");
            hasIssues = true;
        }
        else
        {
            report.AppendLine("✅ Plataforma Android selecionada corretamente");
        }
        
        // 2. Verificar XR Plugin Management
        bool xrPluginInstalled = false;
        bool oculusPluginEnabled = false;
        
        var packageList = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
        foreach (var package in packageList)
        {
            if (package.name == "com.unity.xr.management")
            {
                xrPluginInstalled = true;
                report.AppendLine($"✅ XR Plugin Management instalado (versão {package.version})");
            }
            
            if (package.name == "com.unity.xr.oculus")
            {
                report.AppendLine($"✅ Oculus XR Plugin instalado (versão {package.version})");
                
                // Verificar se está habilitado para Android
                if (EditorBuildSettings.TryGetConfigObject("UnityEditor.XR.ARCore.ARCoreSettings", out UnityEngine.Object obj))
                {
                    // Não conseguimos verificar diretamente, então adicionamos uma verificação manual
                    report.AppendLine("⚠️ Verifique se o Oculus está habilitado em Project Settings > XR Plugin Management > Android");
                }
            }
        }
        
        if (!xrPluginInstalled)
        {
            report.AppendLine("❌ XR Plugin Management não encontrado. Instale pelo Package Manager.");
            hasIssues = true;
        }
        
        // 3. Verificar configurações do Player
        var colorSpace = PlayerSettings.colorSpace;
        if (colorSpace != ColorSpace.Linear)
        {
            report.AppendLine("❌ O Color Space deve ser Linear para melhor qualidade VR. (Project Settings > Player > Other Settings)");
            hasIssues = true;
        }
        else
        {
            report.AppendLine("✅ Color Space está configurado como Linear");
        }
        
        // 4. Verificar se está em fullscreen
        if (!PlayerSettings.defaultIsFullScreen)
        {
            report.AppendLine("❌ Fullscreen Window não está ativado. (Project Settings > Player > Resolution and Presentation)");
            hasIssues = true;
        }
        else
        {
            report.AppendLine("✅ Fullscreen Window está ativado");
        }
        
        // 5. Verificar orientação
        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft)
        {
            report.AppendLine("⚠️ A orientação padrão não é LandscapeLeft. Recomendado para Quest. (Project Settings > Player > Resolution and Presentation)");
        }
        else
        {
            report.AppendLine("✅ Orientação padrão configurada como LandscapeLeft");
        }
        
        // 6. Verificar OVRManager (apenas se USING_OCULUS_SDK estiver definido)
        #if USING_OCULUS_SDK
        var ovrManager = GameObject.FindObjectOfType<OVRManager>();
        if (ovrManager == null)
        {
            report.AppendLine("❌ OVRManager não encontrado na cena. Adicione um OVRCameraRig para experiência VR.");
            hasIssues = true;
        }
        else
        {
            report.AppendLine("✅ OVRManager encontrado na cena");
        }
        #else
        report.AppendLine("⚠️ Oculus Integration não instalado. Recomendado para a melhor experiência VR no Quest.");
        report.AppendLine("   Instale o Oculus Integration do Asset Store para uma experiência VR completa.");
        hasIssues = true;
        #endif
        
        // 7. Verificar configurações da cena para visualização 360°
        var videoSpheres = FindObjectsOfType<Transform>().Where(t => 
            (t.name.Contains("Video") && t.name.Contains("Sphere")) ||
            (t.name.Contains("360") && t.name.Contains("Sphere")));
            
        if (!videoSpheres.Any())
        {
            report.AppendLine("⚠️ Nenhuma esfera de vídeo 360° encontrada na cena. Necessária para experiência VR 360°.");
        }
        else
        {
            report.AppendLine($"✅ Encontrada(s) {videoSpheres.Count()} esfera(s) para vídeo 360°");
            
            // Verificar cada esfera
            foreach (var sphere in videoSpheres)
            {
                var renderer = sphere.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                {
                    // Tentar verificar o shader/material
                    string shaderName = renderer.material.shader?.name ?? "Desconhecido";
                    report.AppendLine($"   - Esfera: {sphere.name}, Shader: {shaderName}");
                    
                    // Adicionar dica para verificar cull mode
                    report.AppendLine("   ⚠️ Verifique se o material está configurado para renderizar o INTERIOR da esfera (Cull Front)");
                }
            }
        }
        
        // 8. Verificar sistema de renderização
        var renderPipeline = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;
        if (renderPipeline != null)
        {
            report.AppendLine($"⚠️ Usando pipeline de renderização personalizada: {renderPipeline.name}");
            report.AppendLine("   Certifique-se de que é compatível com VR. Padrão recomendado para Quest 3.");
        }
        else
        {
            report.AppendLine("✅ Usando pipeline de renderização padrão (recomendado para Quest)");
        }
        
        // 9. VR RECOMENDAÇÕES FINAIS
        report.AppendLine("\nRECOMENDAÇÕES ADICIONAIS:");
        report.AppendLine("1. Configure o Quality Settings para otimizar desempenho no Quest 3");
        report.AppendLine("2. Para vídeos 360°, use esferas com normais invertidas (apontando para dentro)");
        report.AppendLine("3. Posicione a câmera sempre no centro da esfera de vídeo");
        report.AppendLine("4. Em seus materiais, desative 'ZWrite' para evitar problemas de profundidade");
        
        // 10. ADICIONAR INSTRUÇÕES PARA INSTALAR OCULUS INTEGRATION
        report.AppendLine("\nINSTRUÇÕES PARA INSTALAR OCULUS INTEGRATION:");
        report.AppendLine("1. Abra o Unity Asset Store (Window > Asset Store)");
        report.AppendLine("2. Busque por 'Oculus Integration' e baixe o pacote gratuitamente");
        report.AppendLine("3. Após o download, selecione 'Import' (você pode desmarcar amostras para um pacote menor)");
        report.AppendLine("4. Durante a importação, responda 'Yes' para atualizar o Oculus Utilities para o formato do Unity");
        report.AppendLine("5. Quando solicitado para ativar o backend do XR, selecione 'Yes' ou configure depois em 'Project Settings'");
        report.AppendLine("6. Adicione #define USING_OCULUS_SDK no topo do VRManager.cs");
        
        // Exibir resultado
        if (hasIssues)
        {
            report.AppendLine("\n❌ Foram encontrados problemas nas configurações que podem afetar a experiência VR 360°.");
        }
        else
        {
            report.AppendLine("\n✅ Todas as configurações essenciais parecem corretas para experiência VR 360°!");
        }
        
        // Exibir o relatório em uma janela
        VRDiagnosticWindow.ShowDiagnostic(report.ToString());
        
        Debug.Log(report.ToString());
    }
    
    // Classe para mostrar a janela de diagnóstico do VR
    public class VRDiagnosticWindow : EditorWindow
    {
        private string diagnosticText;
        private Vector2 scrollPosition;
        
        public static void ShowDiagnostic(string text)
        {
            VRDiagnosticWindow window = GetWindow<VRDiagnosticWindow>("Diagnóstico VR Quest 3");
            window.diagnosticText = text;
            window.Show();
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUIStyle style = new GUIStyle(EditorStyles.textArea);
            style.wordWrap = true;
            style.richText = true;
            EditorGUILayout.TextArea(diagnosticText, style, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Abrir Configurações do Player"))
            {
                EditorApplication.ExecuteMenuItem("Edit/Project Settings/Player");
            }
            
            if (GUILayout.Button("Abrir XR Plugin Management"))
            {
                EditorApplication.ExecuteMenuItem("Edit/Project Settings/XR Plugin Management");
            }
            
            if (GUILayout.Button("Fechar"))
            {
                Close();
            }
        }
    }
    #endif
}
