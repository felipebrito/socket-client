using UnityEngine;
using UnityEngine.Video;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine.UI;
using System.Text;
using System;

/// <summary>
/// Gerenciador principal do sistema VR, responsável pela conexão com servidor,
/// controle de vídeos e integração com o sistema de rotação.
/// </summary>
public class VRManager : MonoBehaviour
{
    [Header("Referências")]
    public Camera mainCamera;
    public Transform videoSphere;
    public Transform fadeSphere;
    public VideoPlayer videoPlayer;
    public Text messageText;
    private VideoRotationControl rotationControl;

    [Header("Configuração de Rede")]
    public string serverAddress = "ws://localhost:8080";
    public float reconnectInterval = 5f;
    public int maxReconnectAttempts = 3;
    public float pingInterval = 30f;

    [Header("Debug")]
    public bool enableRotationDebug = false;
    public bool isRotationLocked = false;
    public float maxVerticalAngle = 75f;
    public float maxHorizontalAngle = 75f;

    [Header("Estado")]
    public bool isPlaying = false;
    public bool offlineMode = false;
    public bool diagnosticMode = false;
    public bool isConnected = false;
    public string currentVideo = "";
    public string connectionStatus = "Desconectado";

    [Header("Fade")]
    public float fadeSpeed = 2f;
    private Material fadeMaterial;
    private Material videoMaterial;
    private bool isFading = false;
    private float fadeAlpha = 1f;

    // Eventos para notificar resultados dos testes e estados
    public delegate void TestResultHandler(bool success, string message);
    public event TestResultHandler OnTestResult;
    public delegate void ConnectionStateChanged(bool connected, string status);
    public event ConnectionStateChanged OnConnectionStateChanged;
    public delegate void VideoStateChanged(bool playing, string videoName);
    public event VideoStateChanged OnVideoStateChanged;

    private ClientWebSocket webSocket;
    private bool isReconnecting = false;
    private int reconnectAttempts = 0;
    private Queue<string> messageQueue = new Queue<string>();
    private bool isQuitting = false;
    private float lastPingTime = 0f;
    private float waitingTimer = 0f;
    private bool waitingForCommands = false;

    private void Start()
    {
        // Encontrar referências necessárias
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (videoPlayer == null)
            videoPlayer = GetComponentInChildren<VideoPlayer>();

        if (videoSphere == null && videoPlayer != null)
            videoSphere = videoPlayer.transform;

        if (fadeSphere == null)
            fadeSphere = transform.Find("FadeSphere");

        if (messageText == null)
            messageText = FindObjectOfType<Text>();

        rotationControl = FindObjectOfType<VideoRotationControl>();
        if (rotationControl == null)
        {
            Debug.LogWarning("VideoRotationControl não encontrado! O controle de rotação não funcionará.");
        }

        // Configurar VideoPlayer e materiais
        if (videoPlayer != null && videoSphere != null && fadeSphere != null)
        {
            videoPlayer.started += OnVideoStarted;
            videoPlayer.loopPointReached += OnVideoEnded;
            
            // Configurar renderização
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.aspectRatio = VideoAspectRatio.Stretch;
            
            // Configurar material do vídeo
            var videoRenderer = videoSphere.GetComponent<MeshRenderer>();
            if (videoRenderer != null)
            {
                // Criar material para o vídeo usando o shader específico para vídeo
                videoMaterial = new Material(Shader.Find("Custom/Video360"));
                if (videoMaterial != null)
                {
                    videoRenderer.material = videoMaterial;
                    videoPlayer.targetMaterialRenderer = videoRenderer;
                    videoPlayer.targetMaterialProperty = "_MainTex";
                    Debug.Log("Material do vídeo configurado com shader Custom/Video360");
                }
                else
                {
                    Debug.LogError("Shader Custom/Video360 não encontrado!");
                }
            }
            else
            {
                Debug.LogError("MeshRenderer não encontrado no videoSphere!");
            }

            // Configurar material do fade
            var fadeRenderer = fadeSphere.GetComponent<MeshRenderer>();
            if (fadeRenderer != null)
            {
                // Criar material para o fade usando o shader de fade
                fadeMaterial = new Material(Shader.Find("Custom/Unlit_Inverted"));
                if (fadeMaterial != null)
                {
                    fadeRenderer.material = fadeMaterial;
                    fadeAlpha = 1f; // Começa totalmente preto
                    fadeMaterial.SetFloat("_Alpha", fadeAlpha);
                    Debug.Log($"Material do fade configurado. Alpha inicial: {fadeAlpha}");
                }
                else
                {
                    Debug.LogError("Shader Custom/Unlit_Inverted não encontrado!");
                }
            }
            else
            {
                Debug.LogError("MeshRenderer não encontrado no fadeSphere!");
            }
            
            // Configurar qualidade
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            
            Debug.Log("VideoPlayer configurado com sucesso");
        }
        else
        {
            Debug.LogError("VideoPlayer, VideoSphere ou FadeSphere não encontrados!");
        }

        // Iniciar conexão se não estiver em modo offline
        if (!offlineMode)
        {
            StartCoroutine(ConnectToServer());
        }
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.started -= OnVideoStarted;
            videoPlayer.loopPointReached -= OnVideoEnded;
        }

        DisconnectFromServer();
    }

    private void Update()
    {
        // Atualizar estado de reprodução
        if (videoPlayer != null)
        {
            isPlaying = videoPlayer.isPlaying;
        }

        // Atualizar controle de rotação
        if (rotationControl != null)
        {
            rotationControl.SetRotationControlEnabled(isRotationLocked);
        }

        // Processar mensagens da fila
        while (messageQueue.Count > 0)
        {
            ProcessMessage(messageQueue.Dequeue());
        }

        // Verificar conexão periodicamente
        if (isConnected && Time.time - lastPingTime > pingInterval)
        {
            CheckConnection();
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    #region Conexão com Servidor

    private IEnumerator ConnectToServer()
    {
        while (!isConnected && !offlineMode)
        {
            if (webSocket != null)
            {
                webSocket.Dispose();
                webSocket = null;
            }

            webSocket = new ClientWebSocket();
            var uri = new System.Uri(serverAddress);
            
            Debug.Log("Tentando conectar ao servidor...");
            connectionStatus = "Conectando...";
            OnConnectionStateChanged?.Invoke(false, connectionStatus);

            var connectTask = webSocket.ConnectAsync(uri, System.Threading.CancellationToken.None);
            bool connectError = false;
            string errorMessage = "";

            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    isConnected = true;
                    connectionStatus = "Conectado";
                    OnConnectionStateChanged?.Invoke(true, connectionStatus);
                    Debug.Log("Conectado ao servidor!");

                    // Envia informações do cliente em formato texto
                    string clientInfo = $"CLIENT_INFO:{SystemInfo.deviceName}|{GetLocalIPAddress()}|{SystemInfo.operatingSystem}|{GetBatteryLevel()}%";
                    SendMessage(clientInfo);
                    Debug.Log($"Enviando informações do cliente: {clientInfo}");

                    // Envia lista de vídeos disponíveis
                    string[] videos = GetAvailableVideos();
                    if (videos.Length > 0)
                    {
                        string videoList = "VIDEOS:" + string.Join("|", videos);
                        SendMessage(videoList);
                        Debug.Log($"Enviando lista de vídeos: {videoList}");
                    }

                    StartCoroutine(ReceiveMessages());
                    StartCoroutine(SendHeartbeat());
                }
            }
            catch (System.Exception e)
            {
                connectError = true;
                errorMessage = e.Message;
            }

            if (connectError)
            {
                Debug.LogError($"Erro ao conectar: {errorMessage}");
                connectionStatus = "Erro de conexão";
                OnConnectionStateChanged?.Invoke(false, connectionStatus);

                reconnectAttempts++;
                if (reconnectAttempts >= maxReconnectAttempts)
                {
                    Debug.LogWarning("Máximo de tentativas de reconexão atingido. Ativando modo offline.");
                    offlineMode = true;
                    break;
                }

                yield return new WaitForSeconds(reconnectInterval);
            }
        }
    }

    private string[] GetAvailableVideos()
    {
        try
        {
            return Directory.GetFiles(Application.streamingAssetsPath, "*.mp4")
                          .Select(Path.GetFileName)
                          .ToArray();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro ao listar vídeos: {e.Message}");
            return new string[0];
        }
    }

    private void DisconnectFromServer()
    {
        if (webSocket != null)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    var closeTask = webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Desconectando", System.Threading.CancellationToken.None);
                    closeTask.Wait(1000); // Aguarda até 1 segundo para o fechamento
                }
            }
            catch (System.Exception e)
            {
                // Ignora erros de desconexão ao fechar o jogo
                if (!isQuitting)
                {
                    Debug.LogError($"Erro ao desconectar: {e.Message}");
                }
            }
            finally
            {
                webSocket.Dispose();
                webSocket = null;
            }
        }

        isConnected = false;
        connectionStatus = "Desconectado";
        OnConnectionStateChanged?.Invoke(false, connectionStatus);
    }

    private IEnumerator ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        bool shouldReconnect = false;

        while (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            var receiveTask = webSocket.ReceiveAsync(new System.ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);
            
            while (!receiveTask.IsCompleted)
            {
                yield return null;
            }

            try
            {
                if (receiveTask.Result.Count > 0)
                {
                    string message = System.Text.Encoding.UTF8.GetString(buffer, 0, receiveTask.Result.Count);
                    messageQueue.Enqueue(message);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Erro ao receber mensagem: {e.Message}");
                shouldReconnect = true;
                break;
            }
        }

        if (!offlineMode && shouldReconnect)
        {
            StartCoroutine(ConnectToServer());
        }
    }

    private IEnumerator SendHeartbeat()
    {
        while (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            yield return new WaitForSeconds(30);
            
            string heartbeat = $"HEARTBEAT:{isPlaying}|{currentVideo}|{isRotationLocked}|{connectionStatus}";
            SendMessage(heartbeat);
            Debug.Log($"Enviando heartbeat: {heartbeat}");
        }
    }

    private async void SendMessage(string message)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("Tentativa de enviar mensagem sem conexão ativa");
            return;
        }

        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new System.ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);
            Debug.Log($"Mensagem enviada: {message}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro ao enviar mensagem: {e.Message}");
        }
    }

    [System.Serializable]
    private class SerializableDictionary : ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<string> keys = new List<string>();
        [SerializeField]
        private List<string> values = new List<string>();

        private Dictionary<string, object> dictionary = new Dictionary<string, object>();

        public SerializableDictionary(Dictionary<string, object> dict)
        {
            dictionary = dict;
        }

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var kvp in dictionary)
            {
                keys.Add(kvp.Key);
                values.Add(JsonUtility.ToJson(kvp.Value));
            }
        }

        public void OnAfterDeserialize()
        {
            dictionary.Clear();
            for (int i = 0; i < keys.Count; i++)
            {
                dictionary.Add(keys[i], JsonUtility.FromJson<object>(values[i]));
            }
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            // Resetar o timer quando recebe qualquer mensagem
            waitingForCommands = false;
            waitingTimer = 0f;

            // Sempre exibe a mensagem recebida
            Debug.Log($"Mensagem do servidor: {message}");

            // Ignora mensagens de ping
            if (message.StartsWith("PING:")) return;

            // Processa comandos específicos
            if (message.StartsWith("play:"))
            {
                string videoName = message.Substring(5).Trim();
                ForcePlayVideo(videoName);
            }
            else if (message.StartsWith("aviso:"))
            {
                string avisoMensagem = message.Substring(6).Trim();
                ShowTemporaryMessage(avisoMensagem);
            }
            else if (message.StartsWith("seek:"))
            {
                string timeStr = message.Substring(5).Trim();
                if (double.TryParse(timeStr, out double time))
                {
                    if (videoPlayer != null)
                    {
                        videoPlayer.time = time;
                        Debug.Log($"⏱️ Vídeo posicionado para: {time:F2} segundos");
                    }
                }
            }
            else
            {
                // Processa outros comandos existentes
                ProcessExistingCommands(message);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro ao processar mensagem: {e.Message}");
        }
    }

    private void ProcessExistingCommands(string message)
    {
        if (message == "stop")
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                // Fade out antes de parar o vídeo
                StartCoroutine(FadeOut(() => {
                    videoPlayer.Stop();
                    isPlaying = false;
                    OnVideoStateChanged?.Invoke(false, currentVideo);
                    // Fade in para o lobby
                    StartCoroutine(FadeIn());
                }));
            }
        }
        else if (message == "pause")
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                isPlaying = false;
                OnVideoStateChanged?.Invoke(false, currentVideo);
            }
        }
        else if (message == "resume")
        {
            if (videoPlayer != null && !videoPlayer.isPlaying)
            {
                videoPlayer.Play();
                isPlaying = true;
                OnVideoStateChanged?.Invoke(true, currentVideo);
            }
        }
        else if (message == "lock")
        {
            isRotationLocked = true;
            if (rotationControl != null)
            {
                rotationControl.SetRotationControlEnabled(true);
            }
        }
        else if (message == "unlock")
        {
            isRotationLocked = false;
            if (rotationControl != null)
            {
                rotationControl.SetRotationControlEnabled(false);
            }
        }
    }

    private void ShowTemporaryMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
            StartCoroutine(HideMessageAfterDelay(5f));
        }
    }

    private IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideMessage();
    }

    private void HideMessage()
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Controle de Vídeo

    private void OnVideoStarted(VideoPlayer vp)
    {
        isPlaying = true;
        currentVideo = Path.GetFileName(vp.url);
        
        Debug.Log("Video iniciado, começando fade in...");
        // Garante que o fade in comece do preto
        if (fadeMaterial != null)
        {
            fadeAlpha = 1f;
            fadeMaterial.SetFloat("_Alpha", fadeAlpha);
            StartCoroutine(FadeIn());
        }
        else
        {
            Debug.LogError("Fade material é null no OnVideoStarted!");
        }
        
        // Mantém o simulador ativo mas com movimento reduzido durante o vídeo
        var simulator = FindObjectOfType<CameraMovementSimulator>();
        if (simulator != null)
        {
            simulator.SetSimulationActive(true);
            simulator.SetMovementIntensity(0.3f); // Reduz a intensidade do movimento durante o vídeo
        }

        // Exibe o estado do controle de rotação
        if (rotationControl != null)
        {
            Debug.Log("=== Estado do Controle de Rotação ===");
            Debug.Log($"Controle de rotação: {(rotationControl.IsRotationControlEnabled ? "Ativado" : "Desativado")}");
            Debug.Log($"Rotação travada: {isRotationLocked}");
            Debug.Log("=====================================");
        }
        
        OnVideoStateChanged?.Invoke(true, currentVideo);
    }

    private void OnVideoEnded(VideoPlayer vp)
    {
        // Fade out quando o vídeo termina
        StartCoroutine(FadeOut(() => {
            isPlaying = false;
            
            // Restaura a intensidade normal do movimento após o vídeo
            var simulator = FindObjectOfType<CameraMovementSimulator>();
            if (simulator != null)
            {
                simulator.SetMovementIntensity(1.0f);
            }
            
            OnVideoStateChanged?.Invoke(false, currentVideo);
        }));
    }

    /// <summary>
    /// Força a reprodução de um vídeo específico ou o primeiro disponível
    /// </summary>
    public void ForcePlayVideo(string videoName = null)
    {
        if (videoPlayer == null) return;

        // Inicia o fade out antes de trocar o vídeo
        StartCoroutine(FadeOut(() => {
            if (string.IsNullOrEmpty(videoName))
            {
                // Se nenhum nome foi especificado, tenta usar o vídeo atual
                if (string.IsNullOrEmpty(videoPlayer.url))
                {
                    Debug.LogWarning("Nenhum vídeo especificado e nenhum vídeo atual disponível.");
                    return;
                }
            }
            else
            {
                // Configura o novo vídeo
                string videoPath = Path.Combine(Application.streamingAssetsPath, videoName);
                if (!File.Exists(videoPath))
                {
                    Debug.LogError($"Vídeo não encontrado: {videoPath}");
                    return;
                }

                videoPlayer.url = videoPath;
                currentVideo = videoName;
                Debug.Log($"Configurando vídeo: {videoPath}");
            }

            // Garante que o material do vídeo está configurado corretamente
            if (videoSphere != null)
            {
                var renderer = videoSphere.GetComponent<MeshRenderer>();
                if (renderer != null && videoMaterial != null)
                {
                    renderer.material = videoMaterial;
                    videoPlayer.targetMaterialRenderer = renderer;
                    videoPlayer.targetMaterialProperty = "_MainTex";
                }
            }

            // Inicia a reprodução
            videoPlayer.Prepare();
            videoPlayer.Play();
            isPlaying = true;

            // Ativa o controle de rotação
            if (rotationControl != null)
            {
                rotationControl.SetRotationControlEnabled(true);
            }

            Debug.Log($"Iniciando vídeo: {Path.GetFileName(videoPlayer.url)}");
            OnVideoStateChanged?.Invoke(true, currentVideo);
        }));
    }

    #endregion

    #region Métodos de Teste

    /// <summary>
    /// Testa a reprodução de um vídeo específico
    /// </summary>
    public void TestPlayVideo(string videoName)
    {
        try
        {
            string videoPath = Path.Combine(Application.streamingAssetsPath, videoName);
            
            if (!File.Exists(videoPath))
            {
                OnTestResult?.Invoke(false, $"❌ Vídeo não encontrado: {videoName}");
                return;
            }

            ForcePlayVideo(videoName);
            OnTestResult?.Invoke(true, $"✅ Vídeo iniciado: {videoName}");
        }
        catch (System.Exception e)
        {
            OnTestResult?.Invoke(false, $"❌ Erro ao testar vídeo: {e.Message}");
        }
    }

    /// <summary>
    /// Testa a conexão com o servidor
    /// </summary>
    public void TestConnection()
    {
        try
        {
            if (offlineMode)
            {
                OnTestResult?.Invoke(false, "❌ Modo Offline ativo");
                return;
            }

            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                SendMessage("{\"type\":\"test\"}");
                OnTestResult?.Invoke(true, "✅ Conexão ativa");
            }
            else
            {
                StartCoroutine(ConnectToServer());
                OnTestResult?.Invoke(false, "⚠️ Tentando reconectar...");
            }
        }
        catch (System.Exception e)
        {
            OnTestResult?.Invoke(false, $"❌ Erro ao testar conexão: {e.Message}");
        }
    }

    /// <summary>
    /// Lista os arquivos de vídeo disponíveis
    /// </summary>
    public void TestListFiles()
    {
        try
        {
            string[] files = Directory.GetFiles(Application.streamingAssetsPath, "*.mp4");
            if (files.Length > 0)
            {
                string fileList = "✅ Vídeos encontrados:\n";
                foreach (string file in files)
                {
                    fileList += $"• {Path.GetFileName(file)}\n";
                }
                OnTestResult?.Invoke(true, fileList);
            }
            else
            {
                OnTestResult?.Invoke(false, "❌ Nenhum vídeo encontrado");
            }
        }
        catch (System.Exception e)
        {
            OnTestResult?.Invoke(false, $"❌ Erro ao listar arquivos: {e.Message}");
        }
    }

    #endregion

    private void CheckConnection()
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            if (!isReconnecting)
            {
                Debug.LogWarning("🔍 Conexão WebSocket fechada ou inválida. Tentando reconectar...");
                connectionStatus = "Conexão perdida. Reconectando...";
                OnConnectionStateChanged?.Invoke(false, connectionStatus);
                ReconnectWebSocket();
            }
        }
        else
        {
            // Conexão está OK, reseta contagem de tentativas
            reconnectAttempts = 0;
            
            // Envia um ping para garantir que a conexão está ativa
            SendPing();
        }
    }

    private async void SendPing()
    {
        try
        {
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                string pingMessage = "PING:" + System.DateTime.Now.Ticks;
                byte[] data = Encoding.UTF8.GetBytes(pingMessage);
                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);
                Debug.Log("📡 Ping enviado");
                lastPingTime = Time.time;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Erro ao enviar ping: {e.Message}");
            if (!isReconnecting)
            {
                ReconnectWebSocket();
            }
        }
    }

    private async void ReconnectWebSocket()
    {
        if (isReconnecting) return;
        
        isReconnecting = true;
        
        if (reconnectAttempts >= maxReconnectAttempts)
        {
            Debug.LogError($"❌ Excedido número máximo de tentativas de reconexão ({maxReconnectAttempts})");
            connectionStatus = "Falha na reconexão. Tente reiniciar o aplicativo.";
            OnConnectionStateChanged?.Invoke(false, connectionStatus);
            isReconnecting = false;
            return;
        }
        
        reconnectAttempts++;
        
        // Fecha a conexão anterior se ainda existir
        if (webSocket != null)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    var cts = new System.Threading.CancellationTokenSource(1000);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconectando", cts.Token);
                }
                webSocket.Dispose();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"⚠️ Erro ao fechar websocket: {e.Message}");
            }
        }
        
        Debug.Log($"🔄 Tentativa de reconexão {reconnectAttempts}/{maxReconnectAttempts}...");
        connectionStatus = $"Reconectando... Tentativa {reconnectAttempts}/{maxReconnectAttempts}";
        OnConnectionStateChanged?.Invoke(false, connectionStatus);
        
        // Aguarda um tempo com base no número de tentativas (backoff exponencial)
        float waitTime = Mathf.Min(1 * Mathf.Pow(1.5f, reconnectAttempts - 1), 10);
        await System.Threading.Tasks.Task.Delay((int)(waitTime * 1000));
        
        // Tenta conectar novamente
        await ConnectWebSocket();
        
        isReconnecting = false;
    }

    private async System.Threading.Tasks.Task ConnectWebSocket()
    {
        webSocket = new ClientWebSocket();
        Debug.Log("🌐 Tentando conectar ao WebSocket em " + serverAddress);

        try
        {
            await webSocket.ConnectAsync(new System.Uri(serverAddress), System.Threading.CancellationToken.None);
            Debug.Log("✅ Conexão WebSocket bem-sucedida.");
            connectionStatus = "Conexão estabelecida";
            OnConnectionStateChanged?.Invoke(true, connectionStatus);
            isConnected = true;
            
            // Envia informações do cliente
            SendClientInfo();
            
            // Inicia recebimento de mensagens
            StartCoroutine(ReceiveMessages());
            
            // Conexão bem-sucedida, reseta contador de tentativas
            reconnectAttempts = 0;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Erro ao conectar ao WebSocket: {e.Message}");
            connectionStatus = "Erro de conexão: " + e.Message;
            OnConnectionStateChanged?.Invoke(false, connectionStatus);
            
            if (!isReconnecting)
            {
                StartCoroutine(ReconnectAfterDelay(5f));
            }
        }
    }

    private IEnumerator ReconnectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (this != null && this.isActiveAndEnabled)
        {
            ReconnectWebSocket();
        }
    }

    private void SendTimecode()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            double currentTime = videoPlayer.time;
            Debug.Log($"⏱️ Enviando timecode: TIMECODE:{currentTime:F1}");
            
            // Envia o timecode para o VideoRotationControl
            if (rotationControl != null)
            {
                rotationControl.UpdateVideoTime(currentTime);
            }
            else
            {
                // Tenta encontrar o VideoRotationControl se ainda não foi encontrado
                rotationControl = FindObjectOfType<VideoRotationControl>();
                if (rotationControl == null)
                {
                    Debug.LogWarning("VideoRotationControl não encontrado para envio de timecode");
                }
            }
        }
    }

    private async void SendClientInfo()
    {
        string clientName = SystemInfo.deviceName;
        string clientIP = GetLocalIPAddress();
        string clientOS = SystemInfo.operatingSystem;
        int batteryLevel = GetBatteryLevel();

        string infoMessage = $"CLIENT_INFO:{clientName}|{clientIP}|{clientOS}|{batteryLevel}%";

        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            byte[] data = Encoding.UTF8.GetBytes(infoMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);
            Debug.Log($"✅ Enviando informações do cliente: {infoMessage}");
        }
    }

    private int GetBatteryLevel()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
        using (AndroidJavaObject batteryStatus = activity.Call<AndroidJavaObject>("registerReceiver", null, intentFilter))
        {
            int level = batteryStatus.Call<int>("getIntExtra", "level", -1);
            int scale = batteryStatus.Call<int>("getIntExtra", "scale", -1);
            if (level == -1 || scale == -1) return -1;
            return (int)((level / (float)scale) * 100);
        }
        #elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return (int)(SystemInfo.batteryLevel * 100);
        #else
        return -1;
        #endif
    }

    private string GetLocalIPAddress()
    {
        string localIP = "127.0.0.1";
        foreach (var ip in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                localIP = ip.ToString();
                break;
            }
        }
        return localIP;
    }

    private IEnumerator FadeOut(System.Action onComplete = null)
    {
        if (fadeMaterial == null)
        {
            Debug.LogError("Tentativa de fade out com material null!");
            onComplete?.Invoke();
            yield break;
        }
        
        Debug.Log("Iniciando fade out...");
        isFading = true;
        fadeAlpha = 0f;
        
        while (fadeAlpha < 1f)
        {
            fadeAlpha += Time.deltaTime * fadeSpeed;
            fadeMaterial.SetFloat("_Alpha", fadeAlpha);
            yield return null;
        }
        
        fadeAlpha = 1f;
        fadeMaterial.SetFloat("_Alpha", fadeAlpha);
        isFading = false;
        
        Debug.Log("Fade out completo");
        onComplete?.Invoke();
    }

    private IEnumerator FadeIn(System.Action onComplete = null)
    {
        if (fadeMaterial == null)
        {
            Debug.LogError("Tentativa de fade in com material null!");
            onComplete?.Invoke();
            yield break;
        }
        
        Debug.Log("Iniciando fade in...");
        isFading = true;
        fadeAlpha = 1f;
        
        while (fadeAlpha > 0f)
        {
            fadeAlpha -= Time.deltaTime * fadeSpeed;
            fadeMaterial.SetFloat("_Alpha", fadeAlpha);
            yield return null;
        }
        
        fadeAlpha = 0f;
        fadeMaterial.SetFloat("_Alpha", fadeAlpha);
        isFading = false;
        
        Debug.Log("Fade in completo");
        onComplete?.Invoke();
    }
} 