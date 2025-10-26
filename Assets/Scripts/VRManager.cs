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
using System.Linq;
using System.Security.Cryptography.X509Certificates; // Adicionado para Where

#if USING_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

// Adicione essa diretiva para os trechos que usam o SDK do Oculus
#if USING_OCULUS_SDK
using Oculus.VR;
#endif

/// <summary>
/// Gerenciador principal do sistema VR para reprodutor de vídeos 360° sincronizado.
/// 
/// Este componente controla a reprodução de vídeos 360° em dispositivos VR (Quest 3),
/// estabelecendo comunicação WebSocket com o ESP32 para sincronização de LEDs e comandos.
/// 
/// CARACTERÍSTICAS PRINCIPAIS:
/// - Sistema dual-player totalmente individualizado (User 1 e User 2)
/// - Comunicação WebSocket bidirecional com ESP32
/// - Controle de vídeo (Play, Pause, Resume, Stop)
/// - Envio automático de timecodes para LEDs de progresso
/// - Sistema de armazenamento externo para Quest 3
/// - Reconexão automática em caso de perda de conexão
/// - Modo offline com autostart
/// 
/// ARQUITETURA:
/// - Cada instância gerencia APENAS um player (User 1 ou User 2)
/// - Controle individualizado por userNumber (1 ou 2)
/// - Sistema de timecode independente por usuário
/// - Zero interferência entre players
/// 
/// MENSAGENS ENVIADAS:
/// - vr_connected{userNumber} : Indica conexão estabelecida
/// - percent{userNumber}:X : Progresso do vídeo (1-100)
/// - video_ended{userNumber} : Vídeo terminou naturalmente
/// 
/// MENSAGENS RECEBIDAS:
/// - button{userNumber} : Botão pressionado (toggle play/pause)
/// - long{userNumber} : Botão longo (stop completo)
/// - ready{userNumber} : LED ready deve acender
/// - vr_signal_lost{userNumber} : Perda de sinal detectada
/// - vr_hibernate{userNumber} : VR hibernado
/// 
/// CONFIGURAÇÃO NECESSÁRIA:
/// 1. Defina userNumber no Inspector (1 ou 2)
/// 2. Configure videoPlayer com o VideoPlayer da cena
/// 3. Configure videoSphere com o objeto 360°
/// 4. Configure serverUri com IP do ESP32
/// 5. Adicione vídeos à pasta configurada
/// 
/// AUTOR: Sistema BIJARI VR
/// DATA: 2024
/// </summary>
public class VRManager : MonoBehaviour {
    [Header("Video Settings")]
    public VideoPlayer videoPlayer;
    public VideoRotationControl videoRotationControl;
    public string[] videoFiles = { "Pierre_Final.mp4"};
    private string currentVideo = "";

    [Header("External Storage Settings")]
    [Tooltip("Pasta externa onde os vídeos estão armazenados")]
    public string externalFolder = "Download"; // Pasta padrão de downloads do Quest 3
    [Tooltip("Tentar carregar vídeos do armazenamento externo primeiro")]
    public bool useExternalStorage = true; // SEMPRE true para Quest 3
    [Tooltip("Continuar mesmo se o acesso ao armazenamento externo for negado")]
    public bool continueWithoutStorage = false; // FALSE para produção - deve ter acesso aos vídeos

    [Header("View Settings")]
    [Tooltip("Objeto que contém a esfera do vídeo 360")]
    public Transform videoSphere;

    private Transform cameraTransform;
    private bool isTransitioning = false;

    [Header("Networking")]
    public string serverUri = "ws://192.168.4.1:80";
    private ClientWebSocket webSocket;

    [Header("UI Elements")]
    public TextMeshPro messageText;
    public TextMeshProUGUI debugText; // Texto para mostrar informações de debug

    // Alterado para público para que outros componentes possam verificar
    public bool isPlaying = false;
    private double pausedTime = 0.0; // Tempo onde o vídeo foi pausado
    private float lastButtonTime = 0f; // Debounce para botões
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

    [Header("User Settings")]
    [Tooltip("Identifica se esta build é do usuário 1 ou 2 (afeta as mensagens enviadas)")]
    public int userNumber = 1; // 1 ou 2

    [Header("Debug Settings")]
    [Tooltip("Ativa modo de diagnóstico com mais informações")]
    public bool diagnosticMode = true; // TRUE temporariamente para debug
    [Tooltip("Permanecer offline, não tentar conectar ao servidor")]
    public bool offlineMode = false; // FALSE para conectar ao ESP32
    [Tooltip("Ignorar erros de rede e continuar")]
    public bool ignoreNetworkErrors = true;
    [Tooltip("Tempo em segundos para mostrar diagnóstico se a aplicação não avançar")]
    public float showDiagnosticTimeout = 10f; // Reduzido para 10 segundos
    [Tooltip("Tempo em segundos para iniciar automaticamente um vídeo se ficar preso")]
    public float autoStartVideoTimeout = 15f; // Novo campo

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

    void Awake() {
        #if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("⚠️ SOLICITANDO PERMISSÕES NO AWAKE...");
        
        // Forçar a solicitação de permissão imediatamente no Awake
        ForceRequestStoragePermission();
        #endif
    }

    async void Start() {
        videoPlayer.Prepare();
        try {
            Debug.Log("✅ Cliente VR iniciando...");
            
            // Validar userNumber
            if (userNumber < 1 || userNumber > 2) {
                LogWarning($"userNumber inválido ({userNumber}). Definindo como 1.");
                userNumber = 1;
            }
            Log($"🎮 Configurado como Usuário {userNumber}");
            
            // Inicializar sistema individualizado de timecode para este usuário
            InitializeUserTimecodeSystem();
            
            /*// Mostrar mensagem inicial para informar que a aplicação está iniciando
            if (messageText != null) {
                messageText.text = "Iniciando aplicação VR...";
                messageText.gameObject.SetActive(true);
            }*/
            
            if (diagnosticMode) {
                // Adicionar texto de debug visível
                UpdateDebugText($"Inicializando VR Player - Usuário {userNumber}...");
            }
            
            // Verificar se o videoPlayer está configurado
            bool videoPlayerReady = EnsureVideoPlayerIsReady();
            if (!videoPlayerReady) {
                UpdateDebugText("ALERTA: VideoPlayer não encontrado! Alguns recursos podem não funcionar.");
                if (messageText != null) {
                    messageText.text = "AVISO: VideoPlayer não configurado";
                    messageText.gameObject.SetActive(true);
                }
            }

            // Agora altera a mensagem para "Aguardando..."
          /*  if (messageText != null) {
                messageText.text = "Aguardando início da sessão...";
                messageText.gameObject.SetActive(true);
            }*/
            
            // Iniciar o timer de espera
            waitingForCommands = true;
            waitingTimer = 0f;
            hasAutoStarted = false;

            // Monitoramento de conexão simplificado
            // Removido ConnectionHelper para evitar dependências desnecessárias
            
            // Configurar o evento para quando o vídeo terminar
            if (videoPlayer != null) {
                videoPlayer.loopPointReached += OnVideoEnd;
            } else {
                LogError("VideoPlayer não encontrado!");
            }

            // Obter referência à câmera principal
           // cameraTransform = Camera.main ? Camera.main.transform : null;
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
            
            // Configurações simplificadas - sem bloqueio de visualização
            Log("Configuração simplificada - sem bloqueio de visualização");

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
            btn.onClick.AddListener(() => PlayVideo());
            
            Log("Botão de diagnóstico criado para: " + videoFile);
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
            // Controles para teste no editor
            #if UNITY_EDITOR
            HandleEditorControls();
            #endif

            // Verificar timeout para mostrar diagnóstico se estiver aguardando muito tempo
            if (waitingForCommands) {
                waitingTimer += Time.deltaTime;
            }
        }
        catch (Exception e) {
            LogError("Erro no Update: " + e.Message);
        }
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

    // Método para reproduzir um vídeo
    public void PlayVideo() {
       
        

        
        // Resetar o timer quando inicia um vídeo
        waitingForCommands = false;
        waitingTimer = 0f;
        hasAutoStarted = true; // Marcar como iniciado para evitar autostart
        
        // Atualizar o vídeo atual
        //currentVideo = videoName;
        
        // // Para testes: exibir informações sobre o vídeo
        // if (diagnosticMode) {
        //     UpdateDebugText($"Iniciando vídeo: {videoName}");
        // }
        
        // Configurar e iniciar o vídeo diretamente
        PrepareAndPlayVideo();
        
        // Inicializar com orientação neutra
        if (videoSphere != null) {
            videoSphere.rotation = Quaternion.identity;
            Log("Vídeo iniciado com orientação padrão");
        }
    }
    
    // Método para preparar e reproduzir um vídeo
    void PrepareAndPlayVideo()
    {
        videoPlayer.gameObject.SetActive(true);
        videoPlayer.Play();
        isPlaying = true; // Atualizar estado
        
        // Iniciar envio de timecodes quando o vídeo começar
        CancelInvoke(nameof(SendTimecode)); // Garantir que não há instâncias anteriores
        InvokeRepeating(nameof(SendTimecode), 0.5f, 1f);
        Log("🎯 Envio de timecodes iniciado");
    }
    
    // Método para pausar o vídeo
    public void PauseVideo() {
        if (videoPlayer != null && videoPlayer.isPlaying) {
            pausedTime = videoPlayer.time; // Salvar o tempo de pausa
            videoPlayer.Pause();
            isPlaying = false; // CORRIGIDO: Atualizar estado
            Debug.Log($"⏸️ Vídeo pausado no tempo: {pausedTime:F2}s");
            
            // Atualizar a UI
            if (messageText != null) {
                messageText.text = $"Vídeo pausado em {pausedTime:F1}s";
                messageText.gameObject.SetActive(true);
            }
            
            // Parar de enviar timecodes
            CancelInvoke(nameof(SendTimecode));
        }
    }
    
    // Método para retomar a reprodução do vídeo
    public void ResumeVideo() {
        if (videoPlayer == null) {
            Debug.LogError("❌ VideoPlayer não encontrado para retomar");
            return;
        }
        
        // Se o vídeo não está tocando, retomar
        if (!videoPlayer.isPlaying) {
            Debug.Log($"▶️ Definindo tempo para: {pausedTime:F2}s");
            
            // CORRIGIDO: Verificar se temos um tempo válido salvo
            if (pausedTime <= 0) {
                Debug.LogWarning("⚠️ Tempo de pausa inválido, iniciando do início");
                PlayVideo();
                return;
            }
            
            // Usar o tempo salvo quando foi pausado
            videoPlayer.time = pausedTime;
            
            // Aguardar um frame para garantir que o tempo foi definido
            StartCoroutine(ResumeAfterSeek());
        } else {
            Debug.Log("⚠️ Vídeo já está tocando, não precisa retomar");
        }
    }
    
    private System.Collections.IEnumerator ResumeAfterSeek() {
        // Aguardar um frame para garantir que o tempo foi aplicado
        yield return null;
        
        videoPlayer.Play();
        isPlaying = true;
        Debug.Log($"▶️ Vídeo retomado do tempo: {pausedTime:F2}s");
        
        // Atualizar a UI
        if (messageText != null) {
            messageText.text = $"Vídeo retomado de {pausedTime:F1}s";
            StartCoroutine(HideMessageAfterDelay(2f));
        }
        
        // Retomar envio de timecodes com o mesmo intervalo original (1f)
        CancelInvoke(nameof(SendTimecode)); // Garantir que não há instâncias anteriores
        InvokeRepeating(nameof(SendTimecode), 0.5f, 1f);
    }
    
    // Método para parar o vídeo
    public void StopVideo() {
        if (videoPlayer != null) {
            videoPlayer.Stop();
            isPlaying = false;
            
            // Reset individualizado para o usuário específico
            if (lastSentPercentByUser.ContainsKey(userNumber)) {
                lastSentPercentByUser[userNumber] = -1;
            }
            Debug.Log($"⏹️ Vídeo parado - User {userNumber} resetado");
            
            // Atualizar a UI
            if (messageText != null) {
                messageText.text = "Vídeo parado";
                StartCoroutine(HideMessageAfterDelay(2f));
            }
            
            // Parar de enviar timecodes
            CancelInvoke(nameof(SendTimecode));
        }
    }
    
    // Método para parar o vídeo completamente
    public void StopVideoCompletely() {
        Debug.Log("🎬 Parando vídeo completamente");
        
        // Parar o vídeo
        StopVideo();
        
        // CORRIGIDO: Resetar o tempo de pausa quando parar completamente
        pausedTime = 0.0;
        
        
        // Resetar estado para aguardar novo comando
        waitingForCommands = true;
        waitingTimer = 0f;
        hasAutoStarted = false;
    }

    // Novo método para controles de simulação no editor
    #if UNITY_EDITOR
    private Vector3 editorRotation = Vector3.zero;
    private float editorRotationSpeed = 60f; // Graus por segundo
    
    void HandleEditorControls() {
        if (cameraTransform == null || videoSphere == null) return;

        // Controle de foco com tecla F
        if (Input.GetKeyDown(KeyCode.F)) {
            isManualFocusActive = !isManualFocusActive;
            Log(isManualFocusActive ? "Foco manual ativado" : "Foco manual desativado");
            UpdateDebugText(isManualFocusActive ? "Foco Ativo" : "Foco Desativado");
        }

        // Detectar teclas de setas
        if (Input.GetKey(KeyCode.LeftArrow)) {
            editorRotation.y += editorRotationSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.RightArrow)) {
            editorRotation.y -= editorRotationSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.UpArrow)) {
            editorRotation.x += editorRotationSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.DownArrow)) {
            editorRotation.x -= editorRotationSpeed * Time.deltaTime;
        }

        // Resetar com barra de espaço
        if (Input.GetKeyDown(KeyCode.Space)) {
            editorRotation = Vector3.zero;
            Log("Posição resetada para o centro");
            UpdateDebugText("Visualização centralizada");
        }

        // Se o foco estiver ativo, limitar a rotação
        if (isManualFocusActive) {
            float maxAngle = 75f; // Usando o mesmo ângulo padrão do CameraRotationLimiter
            editorRotation.y = Mathf.Clamp(editorRotation.y, -maxAngle, maxAngle);
            editorRotation.x = Mathf.Clamp(editorRotation.x, -maxAngle, maxAngle);
        }

        // Atualizar a rotação da câmera
        cameraTransform.localRotation = Quaternion.Euler(editorRotation.x, editorRotation.y, 0);
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
            
            // Ping desabilitado para evitar interferência
            // SendPing();
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
            
            // Enviar vr_connected para indicar que está pronto
            await SendVRConnected();
            
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
        
        // FLUXO SIMPLIFICADO - comandos que ESP32 envia para ambos os usuários
        if (message == $"button{userNumber}") {
            // Debounce: evitar processamento múltiplo em menos de 500ms
            float currentTime = Time.time;
            if (currentTime - lastButtonTime < 0.5f) {
                Debug.Log($"🎬 BUTTON{userNumber} IGNORADO (debounce)");
                return;
            }
            lastButtonTime = currentTime;
            
            // ESP32 enviou sinal de botão pressionado (clique rápido)
            Debug.Log($"🎬 BUTTON{userNumber} - Estado: {(isPlaying ? "PLAYING" : "STOPPED")}");
            Debug.Log($"🎬 BUTTON{userNumber} - VideoPlayer.isPlaying: {(videoPlayer != null ? videoPlayer.isPlaying.ToString() : "null")}");
            Debug.Log($"🎬 BUTTON{userNumber} - pausedTime: {pausedTime:F2}s");
            
            // CORRIGIDO: Lógica melhorada para pause/resume
            if (videoPlayer == null) {
                Debug.Log("🎬 VideoPlayer não encontrado - iniciando novo vídeo...");
                PlayVideo();
            } else if (videoPlayer.isPlaying) {
                // Vídeo está tocando - pausar
                Debug.Log("⏸️ PAUSANDO VÍDEO...");
                PauseVideo();
            } else if (!videoPlayer.isPlaying && pausedTime > 0) {
                // Vídeo está pausado e temos tempo salvo - retomar
                Debug.Log("▶️ RETOMANDO VÍDEO...");
                ResumeVideo();
            } else if (!videoPlayer.isPlaying && pausedTime == 0) {
                // Vídeo parado completamente - iniciar novo
                Debug.Log("🎬 INICIANDO NOVO VÍDEO...");
                PlayVideo();
            } else {
                // Estado inconsistente - tentar retomar
                Debug.Log("⚠️ Estado inconsistente - tentando retomar");
                ResumeVideo();
            }
        } 
        else if (message == $"long{userNumber}") {
            // ESP32 enviou sinal de botão pressionado por longo tempo (2+ segundos)
            Debug.Log($"⏹️ Botão pressionado por longo tempo - Parando vídeo...");
            StopVideoCompletely(); // CORRIGIDO: Usar StopVideoCompletely para parar completamente
            // Enviar vr_connected para sinalizar que está pronto novamente
            _ = SendVRConnected(); // Fire and forget
            // Resetar estado para aguardar novo comando
            waitingForCommands = true;
            waitingTimer = 0f;
            hasAutoStarted = false;
        } 
        else if (message == $"vr_signal_lost{userNumber}") {
            // VR perdeu sinal - parar tudo
            Debug.Log("📡 VR Signal Lost - Parando vídeo...");
            StopVideo();
            UpdateDebugText("VR Signal Lost - Conexão perdida");
        }
        else if (message == $"vr_hibernate{userNumber}") {
            // VR hibernado - parar tudo
            Debug.Log("😴 VR Hibernate - Parando vídeo...");
            StopVideo();
            UpdateDebugText("VR Hibernate - Headset hibernado");
        }
        else if (message == $"ready{userNumber}") {
            // Player está pronto - LED verde deve estar aceso
            Debug.Log($"✅ Player {userNumber} pronto - LED verde deve estar aceso");
            UpdateDebugText($"Player {userNumber} pronto - LED verde aceso");
        }
        else if (message.StartsWith($"status{userNumber}:")) {
            // ESP32 está simulando animação - ignorar completamente
            // O Unity deve enviar percent{userNumber}:X baseado no progresso real do vídeo
            // Debug.Log($"📊 Status simulado ignorado: {message}");
        } 
        else {
            Debug.LogWarning($"⚠️ Mensagem desconhecida: {message}");
        }
    }

    void ShowTemporaryMessage(string message) {
        if (messageText != null) {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
            StartCoroutine(HideMessageAfterDelay(5f));
        }
    }

    IEnumerator HideMessageAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        HideMessage();
    }

    // Oculta a mensagem
    void HideMessage() {
        if (messageText != null) {
            messageText.text = "";
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
        
        // Enviar mensagem específica para o ESP32 indicando que o vídeo terminou
        SendVideoEnded();
        
        StopVideoCompletely();
    }

    // Sistema individualizado de timecode para cada usuário
    private Dictionary<int, int> lastSentPercentByUser = new Dictionary<int, int>();
    
    // Inicializar sistema individualizado de timecode para este usuário
    void InitializeUserTimecodeSystem() {
        if (!lastSentPercentByUser.ContainsKey(userNumber)) {
            lastSentPercentByUser[userNumber] = -1;
            Log($"🎯 Sistema de timecode individualizado inicializado para User {userNumber}");
        }
    }
    
    void SendTimecode() {
        if (videoPlayer == null || !videoPlayer.isPlaying) return;
        
        float currentTime = (float)videoPlayer.time;
        float videoDuration = (float)videoPlayer.length;
        
        if (videoDuration <= 0) return;
        
        int percent = Mathf.RoundToInt((currentTime / videoDuration) * 100f);
        if (percent > 100) percent = 100;
        
        // Inicializar o usuário no dicionário se não existir
        if (!lastSentPercentByUser.ContainsKey(userNumber)) {
            lastSentPercentByUser[userNumber] = -1;
        }
        
        int lastSentPercent = lastSentPercentByUser[userNumber];
        
        // Garantir que sempre envie pelo menos 1% quando iniciar (para acender primeiro LED)
        if (percent == 0 && lastSentPercent == -1) {
            percent = 1;
        }
        
        // Só enviar se mudou pelo menos 1% ou é o primeiro envio
        if (lastSentPercent == -1 || Mathf.Abs(percent - lastSentPercent) >= 1) {
            lastSentPercentByUser[userNumber] = percent;
            
            string message = $"percent{userNumber}:" + percent.ToString();
            
            if (webSocket != null && webSocket.State == WebSocketState.Open) {
                byte[] data = Encoding.UTF8.GetBytes(message);
                webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                Debug.Log($"🎯 PERCENTUAL USER {userNumber}: {message} (tempo: {currentTime:F1}s / {videoDuration:F1}s)");
            }
        }
    }

    // Enviar mensagem de VR conectado
    async Task SendVRConnected() {
        try {
            if (webSocket != null && webSocket.State == WebSocketState.Open) {
                string message = $"vr_connected{userNumber}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                Debug.Log($"✅ Enviando: {message}");
            }
        } catch (Exception e) {
            Debug.LogError($"❌ Erro ao enviar vr_connected{userNumber}: {e.Message}");
        }
    }
    
    // Enviar mensagem de vídeo terminado
    async Task SendVideoEnded() {
        try {
            if (webSocket != null && webSocket.State == WebSocketState.Open) {
                string message = $"video_ended{userNumber}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                Debug.Log($"🎬 Enviando: {message}");
            }
        } catch (Exception e) {
            Debug.LogError($"❌ Erro ao enviar video_ended{userNumber}: {e.Message}");
        }
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
            videoName = videoFiles.Length > 0 ? videoFiles[0] : "Pierre_Final.mp4";
        }
        
        Log("Teste de reprodução manual: " + videoName);
        PlayVideo();
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
                videoName = "Pierre_Final.mp4";
            }
        }
        
        // Ativar modo offline
        Log("Ativando modo offline para reprodução forçada");
        offlineMode = true;
        
        // Reproduzir o vídeo
        Log("Forçando reprodução de: " + videoName);
        PlayVideo();
    }

    // Novo método para iniciar automaticamente o primeiro vídeo disponível
    void AutoStartFirstVideo()
    {
        if (hasAutoStarted) return;  // Evita múltiplos autostarts
        
        hasAutoStarted = true;
        Debug.Log("🔄 Iniciando reprodução automática do primeiro vídeo disponível...");
        
        // Se já estiver reproduzindo um vídeo, não faz nada
        if (isPlaying) return;
        
        // Tenta encontrar vídeos disponíveis no armazenamento externo do Quest 3
        string[] availableVideos = null;
        string[] searchPaths = {
            "/sdcard/Download",                    // Caminho principal Quest 3
            "/storage/emulated/0/Download",         // Caminho alternativo Quest 3
            "/storage/self/primary/Download",      // Caminho alternativo 2 Quest 3
            "/sdcard/Movies",                      // Pasta Movies do Quest 3
            externalStoragePath                    // Caminho configurado
        };
        
        foreach (string path in searchPaths) {
            try {
                if (Directory.Exists(path)) {
                    availableVideos = Directory.GetFiles(path, "*.mp4");
                    if (availableVideos != null && availableVideos.Length > 0) {
                        Log($"✅ Vídeos encontrados em: {path}");
                        break;
                    }
                }
            } catch (Exception e) {
                LogError($"Erro ao verificar diretório {path}: {e.Message}");
            }
        }
        
        string videoToPlay = "";
        
        // Se encontrou vídeos externos, usa o primeiro
        if (availableVideos != null && availableVideos.Length > 0)
        {
            videoToPlay = Path.GetFileName(availableVideos[0]);
            Log($"🎬 Autostart: Vídeo externo encontrado: {videoToPlay}");
        }
        // Senão, tenta usar um da lista pré-definida
        else if (videoFiles.Length > 0)
        {
            videoToPlay = videoFiles[0];
            Log($"🎬 Autostart: Usando vídeo da lista pré-definida: {videoToPlay}");
        }
        else
        {
            // Último recurso
            videoToPlay = "Pierre_Final.mp4";
            Log($"🎬 Autostart: Usando vídeo padrão: {videoToPlay}");
        }
        
        // Reproduzir o vídeo
        Log($"🚀 Iniciando reprodução automática de: {videoToPlay}");
        PlayVideo();
        
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
            // No Quest 3, configurar para o diretório de download padrão
            // Tentar múltiplos caminhos possíveis para Downloads
            string[] possiblePaths = {
                Path.Combine("/sdcard", externalFolder),           // Caminho principal
                Path.Combine("/storage/emulated/0", externalFolder), // Caminho alternativo
                Path.Combine("/storage/self/primary", externalFolder), // Caminho alternativo 2
                Path.Combine(Application.persistentDataPath, externalFolder) // Fallback interno
            };
            
            externalStoragePath = "";
            foreach (string path in possiblePaths) {
                if (Directory.Exists(path)) {
                    externalStoragePath = path;
                    Log($"✅ Diretório Downloads encontrado: {externalStoragePath}");
                    break;
                }
            }
            
            // Se não encontrou nenhum diretório existente, criar no primeiro caminho
            if (string.IsNullOrEmpty(externalStoragePath)) {
                externalStoragePath = possiblePaths[0]; // Usar /sdcard/Download
                try {
                    Directory.CreateDirectory(externalStoragePath);
                    Log($"📁 Diretório Downloads criado: {externalStoragePath}");
                } catch (Exception e) {
                    LogError($"❌ Erro ao criar diretório Downloads: {e.Message}");
                    // Fallback para persistentDataPath
                    externalStoragePath = possiblePaths[3];
                    Directory.CreateDirectory(externalStoragePath);
                    Log($"📁 Usando diretório interno: {externalStoragePath}");
                }
            }
            
            Log($"🎬 Armazenamento externo configurado para Quest 3: {externalStoragePath}");
            #else
            // No editor ou outras plataformas, usar StreamingAssets
            externalStoragePath = Application.streamingAssetsPath;
            Log($"🖥️ Usando StreamingAssets para vídeos (Editor): {externalStoragePath}");
            #endif
            
            // Listar os arquivos disponíveis
            DebugExternalStorageFiles();
        } catch (Exception e) {
            LogError($"❌ Erro ao inicializar armazenamento externo: {e.Message}");
            // Fallback para caminho interno
            externalStoragePath = Application.streamingAssetsPath;
            Log($"🔄 Fallback para StreamingAssets: {externalStoragePath}");
        }
    }
    
    // Método para ajudar no diagnóstico do acesso a arquivos externos
    void DebugExternalStorageFiles() {
        if (!diagnosticMode) return;
        
        try {
            string[] searchPaths = new string[] { 
                externalStoragePath,
                "/sdcard/Download",                    // Caminho principal Quest 3
                "/storage/emulated/0/Download",       // Caminho alternativo Quest 3
                "/storage/self/primary/Download",      // Caminho alternativo 2 Quest 3
                Application.streamingAssetsPath,
                Application.persistentDataPath,
                "/sdcard/Movies",                     // Pasta Movies do Quest 3
                "/sdcard/DCIM"                        // Pasta DCIM do Quest 3
            };
            
            Log("🔍 Verificando diretórios de vídeos no Quest 3:");
            foreach (string path in searchPaths) {
                if (!Directory.Exists(path)) {
                    Log($"❌ Diretório não encontrado: {path}");
                    continue;
                }
                
                string[] files = Directory.GetFiles(path, "*.mp4");
                if (files.Length > 0) {
                    Log($"✅ Encontrados {files.Length} vídeos MP4 em: {path}");
                    foreach (string file in files) {
                        Log($"  📹 {Path.GetFileName(file)}");
                    }
                } else {
                    Log($"📂 Diretório vazio: {path}");
                }
            }
        } catch (Exception e) {
            LogError($"❌ Erro ao listar arquivos: {e.Message}");
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
}