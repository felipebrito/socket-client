using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using System.Linq;
using AparatoCustomAttributes;
using TMPro;

public class VideoRotationControl : MonoBehaviour
{
    [Header("Referências")]
    public CameraRotationLimiter cameraLimiter;
    public VideoPlayer videoPlayer;
    public TextMeshProUGUI messageText;

    [Header("Configuração de Vídeos")]
    [Tooltip("Lista de vídeos com blocos de tempo para limitar a rotação")]
    public List<VideoBlock> videoBlocks = new List<VideoBlock>();

    // Informações do vídeo atual
    [Header("Informações em Tempo Real")]
    [ReadOnly] public string currentVideoTitleID;
    [ReadOnly] public string currentTimeBlockInfo;
    
    // Cache para processamento mais eficiente
    private VideoBlock currentVideoBlock;
    private BlockTime currentTimeBlock;
    private int currentBlockIndex = -1;
    private bool isInitialized = false;
    private double lastCheckTime = -1;
    private double videoLength = 0;
    private bool isRotationControlEnabled = false;
    
    // Propriedade pública para acessar o estado do controle de rotação
    public bool IsRotationControlEnabled => isRotationControlEnabled;
    
    // Cache de valores para processamento mais rápido
    private readonly List<int> blockTimeIndices = new List<int>();
    private Dictionary<string, VideoBlock> videoBlockLookup = new Dictionary<string, VideoBlock>();

    [System.Serializable]
    public class VideoBlock
    {
        public string videoTitle;
        public List<BlockTime> blockTimes = new List<BlockTime>();
        public float angle = 75f;

        public void SortBlockTimes()
        {
            if (blockTimes != null && blockTimes.Count > 1)
            {
                blockTimes = blockTimes.OrderBy(b => b.startTime).ToList();
            }
        }
    }

    [System.Serializable]
    public class BlockTime
    {
        public double startTime;
        public double endTime;
        
        public override string ToString()
        {
            return $"{startTime:F1}s - {endTime:F1}s";
        }
    }

    private void Awake()
    {
        PrepareBlockLookups();
    }
    
    private void Start()
    {
        InitializeComponents();
    }
    
    private void PrepareBlockLookups()
    {
        videoBlockLookup.Clear();
        
        foreach (var block in videoBlocks)
        {
            block.SortBlockTimes();
            
            if (!string.IsNullOrEmpty(block.videoTitle) && !videoBlockLookup.ContainsKey(block.videoTitle))
            {
                videoBlockLookup[block.videoTitle] = block;
            }
        }
        
        Debug.Log($"VideoRotationControl: {videoBlockLookup.Count} vídeos configurados para controle de rotação");
    }

    private void InitializeComponents()
    {
        if (cameraLimiter == null)
        {
            cameraLimiter = GetComponentInChildren<CameraRotationLimiter>();
            if (cameraLimiter == null)
            {
                cameraLimiter = FindObjectOfType<CameraRotationLimiter>();
            }
            
            if (cameraLimiter == null)
            {
                Debug.LogError("VideoRotationControl: CameraRotationLimiter não encontrado. O controle de rotação não funcionará.");
                return;
            }
        }

        if (videoPlayer == null)
        {
            videoPlayer = GetComponentInChildren<VideoPlayer>();
            if (videoPlayer == null)
            {
                videoPlayer = cameraLimiter.gameObject.GetComponentInParent<VideoPlayer>();
            }
            
            if (videoPlayer == null)
            {
                Debug.LogError("VideoRotationControl: VideoPlayer não encontrado. O controle de tempos não funcionará.");
                return;
            }
        }

        if (messageText == null)
        {
            messageText = GetComponentInChildren<TextMeshProUGUI>();
            if (messageText == null)
            {
                messageText = FindObjectOfType<TextMeshProUGUI>();
            }
            
            if (messageText == null)
            {
                Debug.LogWarning("VideoRotationControl: TextMeshProUGUI não encontrado. As mensagens de status não serão exibidas.");
            }
        }

        videoPlayer.started += OnVideoStarted;
        videoPlayer.loopPointReached += OnVideoEnded;
        isInitialized = true;
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.started -= OnVideoStarted;
            videoPlayer.loopPointReached -= OnVideoEnded;
        }
    }

    public void SetRotationControlEnabled(bool enabled)
    {
        isRotationControlEnabled = enabled;
        if (!enabled && cameraLimiter != null)
        {
            cameraLimiter.IsLimitActive = false;
            currentTimeBlockInfo = string.Empty;
        }
        Debug.Log($"VideoRotationControl: Controle de rotação {(enabled ? "ativado" : "desativado")}");
    }

    void Update()
    {
        if (!isInitialized)
        {
            if (Time.frameCount % 60 == 0)
                InitializeComponents();
            return;
        }
        
        if (!isRotationControlEnabled || videoPlayer == null || cameraLimiter == null || !videoPlayer.isPlaying || videoPlayer.clip == null) 
        {
            if (cameraLimiter != null && cameraLimiter.IsLimitActive)
            {
                cameraLimiter.IsLimitActive = false;
                currentTimeBlockInfo = string.Empty;
                Debug.Log($"❌ Controle de rotação desativado: {(!isRotationControlEnabled ? "Controle desativado" : !videoPlayer.isPlaying ? "Vídeo não está tocando" : "Componentes faltando")}");
            }
            return;
        }

        if (currentVideoBlock == null)
        {
            if (videoPlayer.clip != null)
            {
                OnVideoStarted(videoPlayer);
                if (currentVideoBlock == null)
                {
                    cameraLimiter.IsLimitActive = false;
                    currentTimeBlockInfo = string.Empty;
                    Debug.Log($"❌ Controle de rotação desativado: Nenhuma configuração encontrada para o vídeo {videoPlayer.clip.name}");
                    return;
                }
            }
            else return;
        }

        double currentTime = videoPlayer.time;
        
        if (Time.frameCount % 60 == 0)
        {
            string blockStatus = cameraLimiter.IsLimitActive ? "🔒 BLOQUEADO" : "🔓 DESBLOQUEADO";
            Debug.Log($"⏱️ Tempo: {currentTime:F1}s | Estado: {blockStatus} | Ângulo: {cameraLimiter.angle:F1}°");
        }

        bool foundActiveBlock = false;
        BlockTime activeBlock = null;
        
        foreach (var block in currentVideoBlock.blockTimes)
        {
            if (currentTime >= block.startTime && currentTime <= block.endTime)
            {
                foundActiveBlock = true;
                activeBlock = block;
                break;
            }
        }

        if (foundActiveBlock)
        {
            if (!cameraLimiter.IsLimitActive)
            {
                cameraLimiter.angle = currentVideoBlock.angle;
                cameraLimiter.IsLimitActive = true;
                currentTimeBlock = activeBlock;
                currentTimeBlockInfo = $"🔒 {activeBlock}";
                Debug.Log($"🔒 Bloqueio ativado em {currentTime:F1}s | Bloco: {activeBlock} | Ângulo: {currentVideoBlock.angle:F1}°");
            }
        }
        else if (cameraLimiter.IsLimitActive)
        {
            cameraLimiter.IsLimitActive = false;
            currentTimeBlock = null;
            currentTimeBlockInfo = string.Empty;
            Debug.Log($"🔓 Bloqueio desativado em {currentTime:F1}s");
        }

        if (messageText != null)
        {
            messageText.text = currentTimeBlockInfo;
        }
    }

    private void OnVideoStarted(VideoPlayer player)
    {
        if (player == null || player.clip == null) return;
        
        string videoName = System.IO.Path.GetFileName(player.url);
        currentVideoTitleID = videoName;
        
        if (videoBlockLookup.TryGetValue(videoName, out VideoBlock block))
        {
            currentVideoBlock = block;
            videoLength = player.clip.length;
            Debug.Log($"✅ Configuração encontrada para o vídeo {videoName} com {block.blockTimes.Count} blocos de tempo");
        }
        else
        {
            currentVideoBlock = null;
            Debug.Log($"❌ Nenhuma configuração encontrada para o vídeo {videoName}");
        }
    }

    private void OnVideoEnded(VideoPlayer player)
    {
        currentVideoBlock = null;
        currentTimeBlock = null;
        currentTimeBlockInfo = string.Empty;
        cameraLimiter.IsLimitActive = false;
        Debug.Log("🔄 Vídeo finalizado, controle de rotação resetado");
    }
}
