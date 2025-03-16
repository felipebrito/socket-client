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
    public float maxVerticalAngle = 75f;
    [Tooltip("Ângulo máximo de rotação horizontal")]
    public float maxHorizontalAngle = 75f;

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
        Debug.Log("⚠️ SOLICITANDO PERMISSÕES NO AWAKE...");
        
        // Forçar a solicitação de permissão imediatamente no Awake
        ForceRequestStoragePermission();
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

            // Obter referência à câmera principal
            cameraTransform = Camera.main ? Camera.main.transform : null;
            if (cameraTransform == null) {
                LogError("Câmera principal não encontrada!");
                // Tenta obter qualquer câmera disponível
                Camera[] cameras = FindObjectsOfType<Camera>();
                if (cameras.Length > 0) {
                    cameraTransform = cameras[0].transform;
                    Log("Usando câmera alternativa: " + cameras[0].name);
                }
            }
            
            // Tentar encontrar câmera VR (Oculus/XR)
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (xrOrigin == null) {
                // Primeiro tentar encontrar pelo nome comum
                xrOrigin = GameObject.Find("XR Origin")?.transform;
                if (xrOrigin == null) {
                    // Tentar encontrar qualquer objeto que pareça ser um XR Origin
                    var possibleRigs = FindObjectsOfType<Transform>().Where(t => 
                        t.name.Contains("XR") && t.name.Contains("Origin") ||
                        t.name.Contains("VR") && t.name.Contains("Rig"));
                    
                    if (possibleRigs.Any()) {
                        xrOrigin = possibleRigs.First();
                        Log("XR Origin encontrado por busca: " + xrOrigin.name);
                    } else {
                        LogError("XR Origin não encontrado! O controle de rotação pode não funcionar corretamente.");
                    }
                } else {
                    Log("XR Origin encontrado automaticamente");
                }
            }
            #endif

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
        if (activeCamera == null || videoSphere == null) return;

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

        if (Input.GetKey(KeyCode.LeftArrow)) horizontalInput -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) horizontalInput += 1f;
        if (Input.GetKey(KeyCode.UpArrow)) verticalInput -= 1f;
        if (Input.GetKey(KeyCode.DownArrow)) verticalInput += 1f;

        // Aplicar rotação com base no input
        if (horizontalInput != 0 || verticalInput != 0) {
            // No editor, rotacionamos o VideoSphere ao invés da câmera
            Vector3 currentRotation = videoSphere.localEulerAngles;
            
            // Converter ângulos para o intervalo -180 a 180
            float currentX = currentRotation.x;
            if (currentX > 180) currentX -= 360;
            float currentY = currentRotation.y;
            if (currentY > 180) currentY -= 360;

            // Calcular nova rotação
            float newX = Mathf.Clamp(currentX + (verticalInput * editorRotationSpeed * Time.deltaTime), -maxVerticalAngle, maxVerticalAngle);
            float newY = Mathf.Clamp(currentY + (horizontalInput * editorRotationSpeed * Time.deltaTime), -maxHorizontalAngle, maxHorizontalAngle);

            // Aplicar rotação ao VideoSphere
            videoSphere.localRotation = Quaternion.Euler(newX, newY, 0);
        }

        // Resetar com barra de espaço
        if (Input.GetKeyDown(KeyCode.Space)) {
            videoSphere.localRotation = Quaternion.identity;
            Log("VideoSphere resetado para o centro");
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
}
