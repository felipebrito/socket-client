using UnityEngine;
using UnityEngine.Video;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;

public class VRManager : MonoBehaviour
{
    [Header("Configurações de Vídeo")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoSphere;
    [SerializeField] private GameObject fadeSphere;
    [SerializeField] private Material fadeMaterial;
    [SerializeField] private float fadeSpeed = 1.5f;
    [SerializeField] private bool autoPlayOnStart = false;
    [SerializeField] private string defaultVideoName = "rio.mp4";
    [SerializeField] private bool useExternalStorage = true;
    
    [Header("Configurações de Servidor")]
    [SerializeField] private string serverUri = "ws://192.168.1.30:8181";
    [SerializeField] private bool offlineMode = false;
    [SerializeField] private float connectionTimeout = 10f;
    
    [Header("Configurações de Rotação")]
    [SerializeField] private bool lockRotation = false;
    [SerializeField] private float maxHorizontalAngle = 45f;
    [SerializeField] private float maxVerticalAngle = 30f;
    [SerializeField] private float resetRotationSpeed = 2f;
    
    [System.Serializable]
    public class LockTimeRange
    {
        public float startTime;  // Segundos
        public float endTime;    // Segundos
        public float maxAngle;   // Ângulo máximo
        public float resetSpeed; // Velocidade de retorno
    }
    
    [Header("Intervalos de Bloqueio")]
    [SerializeField] private List<LockTimeRange> lockTimeRanges = new List<LockTimeRange>();
    
    [Header("Referências de UI")]
    [SerializeField] private TMPro.TextMeshProUGUI messageText;
    [SerializeField] private TMPro.TextMeshProUGUI debugText;
    
    // Componentes privados
    private ConnectionHelper connectionHelper;
    private Transform cameraTransform;
    private SmoothOrbitFollower orbitFollower;
    private float alphaValue = 1f;
    private bool isFading = false;
    private bool isPlaying = false;
    private string externalStoragePath = "/sdcard/Download/";
    private string streamingAssetsPath;
    private float lastPositionSent = 0f;
    private float positionUpdateInterval = 1f;
    
    // Constantes
    private const string TIMECODE_FORMAT = "TIMECODE:{0}";
    private const string STATUS_FORMAT = "STATUS:{0}";

    private void Awake()
    {
        // Inicialização de componentes
        connectionHelper = GetComponent<ConnectionHelper>();
        if (connectionHelper == null)
        {
            connectionHelper = gameObject.AddComponent<ConnectionHelper>();
        }
        
        cameraTransform = Camera.main.transform;
        orbitFollower = videoSphere.GetComponent<SmoothOrbitFollower>();
        if (orbitFollower == null)
        {
            orbitFollower = videoSphere.AddComponent<SmoothOrbitFollower>();
            orbitFollower.target = cameraTransform;
        }
        
        // Configurando caminhos
        streamingAssetsPath = Application.streamingAssetsPath + "/";
        
        // Configurando o videoPlayer
        videoPlayer.loopPointReached += OnVideoFinished;
        
        // Configurando material de fade
        Color fadeColor = fadeMaterial.color;
        fadeColor.a = alphaValue;
        fadeMaterial.color = fadeColor;
        
        // Ocultando textos de UI
        if (messageText != null) messageText.text = "";
        if (debugText != null) debugText.gameObject.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(FadeIn());
        
        // Configurando conexão com o servidor
        if (!offlineMode)
        {
            connectionHelper.Initialize(serverUri);
            connectionHelper.OnConnected += HandleConnected;
            connectionHelper.OnDisconnected += HandleDisconnected;
            connectionHelper.OnMessageReceived += HandleMessage;
            connectionHelper.OnError += HandleError;
            
            StartCoroutine(CheckConnection());
        }
        else
        {
            Debug.Log("Iniciando em modo offline");
            if (autoPlayOnStart)
            {
                PlayDefaultVideo();
            }
        }
    }

    private IEnumerator CheckConnection()
    {
        yield return new WaitForSeconds(connectionTimeout);
        
        if (!connectionHelper.IsConnected)
        {
            Debug.Log("Timeout de conexão atingido, entrando em modo offline");
            offlineMode = true;
            
            if (autoPlayOnStart)
            {
                PlayDefaultVideo();
            }
        }
    }

    private void Update()
    {
        // Enviar timecode periodicamente
        if (isPlaying && videoPlayer.isPlaying && connectionHelper.IsConnected)
        {
            if (Time.time - lastPositionSent > positionUpdateInterval)
            {
                SendTimecode();
                lastPositionSent = Time.time;
            }
        }
        
        // Verificar intervalos de bloqueio
        if (isPlaying && lockRotation && videoPlayer.isPlaying)
        {
            CheckLockTimeRanges();
        }
        
        // Controles de depuração (apenas no editor)
        #if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ResetVideoSphereRotation();
        }
        
        if (Input.GetKeyDown(KeyCode.F))
        {
            lockRotation = !lockRotation;
        }
        #endif
    }

    private void HandleConnected()
    {
        ShowMessage("Conectado ao servidor");
        SendClientInfo();
    }

    private void HandleDisconnected()
    {
        ShowMessage("Desconectado do servidor");
    }

    private void HandleError(string errorMessage)
    {
        ShowMessage("Erro: " + errorMessage);
    }

    private void HandleMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        
        if (message.StartsWith("play:"))
        {
            string videoName = message.Substring(5);
            PlayVideo(videoName);
        }
        else if (message == "pause")
        {
            PauseVideo();
        }
        else if (message == "resume")
        {
            ResumeVideo();
        }
        else if (message == "stop")
        {
            StopVideo();
        }
        else if (message.StartsWith("seek:"))
        {
            string timeStr = message.Substring(5);
            if (float.TryParse(timeStr, out float seekTime))
            {
                SeekVideo(seekTime);
            }
        }
        else if (message.StartsWith("aviso:"))
        {
            string texto = message.Substring(6);
            ShowMessage(texto);
        }
    }

    public void PlayDefaultVideo()
    {
        PlayVideo(defaultVideoName);
    }

    public void PlayVideo(string videoName)
    {
        if (isPlaying)
        {
            StartCoroutine(StopAndPlayVideo(videoName));
        }
        else
        {
            StartCoroutine(StartPlayingVideo(videoName));
        }
    }

    private IEnumerator StopAndPlayVideo(string videoName)
    {
        yield return StartCoroutine(FadeOut());
        StopVideo(false);
        yield return StartCoroutine(StartPlayingVideo(videoName));
    }

    private IEnumerator StartPlayingVideo(string videoName)
    {
        ShowMessage("Carregando vídeo: " + videoName);
        
        string videoPath = "";
        if (useExternalStorage)
        {
            // Tenta o caminho externo primeiro (Download do Android)
            videoPath = externalStoragePath + videoName;
            
            // Se não existir, tenta o caminho de StreamingAssets
            if (!File.Exists(videoPath))
            {
                videoPath = streamingAssetsPath + videoName;
                Debug.Log("Arquivo não encontrado no armazenamento externo, usando StreamingAssets: " + videoPath);
            }
        }
        else
        {
            videoPath = streamingAssetsPath + videoName;
        }
        
        if (videoName.Contains("://") || videoName.StartsWith("http"))
        {
            videoPath = videoName; // URL direta
        }
        
        Debug.Log("Carregando vídeo: " + videoPath);
        videoPlayer.url = videoPath;
        videoPlayer.Prepare();
        
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }
        
        ResetVideoSphereRotation();
        videoPlayer.Play();
        isPlaying = true;
        
        yield return StartCoroutine(FadeIn());
        ShowMessage("");
        
        SendStatusUpdate("playing");
    }

    public void PauseVideo()
    {
        if (isPlaying && videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
            SendStatusUpdate("paused");
        }
    }

    public void ResumeVideo()
    {
        if (isPlaying && !videoPlayer.isPlaying)
        {
            videoPlayer.Play();
            SendStatusUpdate("playing");
        }
    }

    public void StopVideo(bool fade = true)
    {
        if (fade)
        {
            StartCoroutine(FadeOutAndStop());
        }
        else
        {
            videoPlayer.Stop();
            isPlaying = false;
            SendStatusUpdate("stopped");
        }
    }

    private IEnumerator FadeOutAndStop()
    {
        yield return StartCoroutine(FadeOut());
        videoPlayer.Stop();
        isPlaying = false;
        SendStatusUpdate("stopped");
    }

    public void SeekVideo(float timeInSeconds)
    {
        if (isPlaying)
        {
            videoPlayer.time = timeInSeconds;
            SendTimecode();
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        StartCoroutine(FadeOut());
        isPlaying = false;
        SendStatusUpdate("finished");
    }

    private IEnumerator FadeOut()
    {
        if (isFading) yield break;
        
        isFading = true;
        alphaValue = 0f;
        
        while (alphaValue < 1f)
        {
            alphaValue += Time.deltaTime * fadeSpeed;
            Color fadeColor = fadeMaterial.color;
            fadeColor.a = alphaValue;
            fadeMaterial.color = fadeColor;
            yield return null;
        }
        
        isFading = false;
    }

    private IEnumerator FadeIn()
    {
        if (isFading) yield break;
        
        isFading = true;
        alphaValue = 1f;
        
        while (alphaValue > 0f)
        {
            alphaValue -= Time.deltaTime * fadeSpeed;
            Color fadeColor = fadeMaterial.color;
            fadeColor.a = alphaValue;
            fadeMaterial.color = fadeColor;
            yield return null;
        }
        
        isFading = false;
    }

    private void ResetVideoSphereRotation()
    {
        videoSphere.transform.rotation = Quaternion.identity;
    }

    private void CheckLockTimeRanges()
    {
        float currentTime = (float)videoPlayer.time;
        
        foreach (LockTimeRange range in lockTimeRanges)
        {
            if (currentTime >= range.startTime && currentTime <= range.endTime)
            {
                // Aplicar bloqueio
                orbitFollower.enabled = true;
                orbitFollower.maxDistance = range.maxAngle;
                orbitFollower.followSpeed = range.resetSpeed;
                return;
            }
        }
        
        // Fora de qualquer intervalo de bloqueio
        orbitFollower.enabled = false;
    }

    public void ShowMessage(string message, float duration = 3f)
    {
        if (messageText != null)
        {
            messageText.text = message;
            
            if (duration > 0)
            {
                StartCoroutine(ClearMessageAfterDelay(duration));
            }
        }
    }

    private IEnumerator ClearMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (messageText != null)
        {
            messageText.text = "";
        }
    }

    private void SendTimecode()
    {
        if (!connectionHelper.IsConnected) return;
        
        string timeCode = string.Format(TIMECODE_FORMAT, videoPlayer.time);
        connectionHelper.SendMessage("timecode", timeCode);
    }

    private void SendStatusUpdate(string status)
    {
        if (!connectionHelper.IsConnected) return;
        
        string statusMessage = string.Format(STATUS_FORMAT, status);
        connectionHelper.SendMessage("status", statusMessage);
    }

    private void SendClientInfo()
    {
        if (!connectionHelper.IsConnected) return;
        
        string deviceInfo = $"CLIENT_INFO:Unity {Application.unityVersion}, {SystemInfo.deviceModel}, {SystemInfo.deviceName}";
        connectionHelper.SendMessage("info", deviceInfo);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }
} 