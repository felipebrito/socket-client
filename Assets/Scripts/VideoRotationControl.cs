using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using System.Linq;
using AparatoCustomAttributes;
using TMPro;
using System;

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
    private double currentVideoTime = 0;
    
    // Propriedade pública para acessar o estado do controle de rotação
    public bool IsRotationControlEnabled => isRotationControlEnabled;
    
    // Cache de valores para processamento mais rápido
    private readonly List<int> blockTimeIndices = new List<int>();
    private Dictionary<string, VideoBlock> videoBlockLookup = new Dictionary<string, VideoBlock>();

    private float transitionSpeed = 1.5f; // Velocidade reduzida para transição mais suave
    private bool isTransitioning = false;
    private Vector3 targetRotation = Vector3.zero;
    private CameraMovementSimulator cameraSimulator;
    private Vector3 lastRotation; // Guarda a última rotação antes de desativar para transição suave ao liberar

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
        public float startTime;
        public float endTime;
        
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
        SetRotationControlEnabled(false);
        
        Debug.Log("=== Configuração de Blocos ===");
        foreach (var kvp in videoBlockLookup)
        {
            Debug.Log($"Vídeo: {kvp.Key}");
            Debug.Log($"  Ângulo: {kvp.Value.angle}°");
            foreach (var timeBlock in kvp.Value.blockTimes)
            {
                Debug.Log($"  Bloco: {timeBlock.startTime:F1}s - {timeBlock.endTime:F1}s");
            }
        }
        Debug.Log("==============================");
    }

    public void UpdateVideoTime(double time)
    {
        currentVideoTime = time;
        CheckTimeBlocks();
    }

    private void CheckTimeBlocks()
    {
        if (videoPlayer == null || !videoPlayer.isPlaying || currentVideoBlock == null) return;

        float currentTime = (float)videoPlayer.time;
        bool isInAnyBlock = false;

        foreach (var timeBlock in currentVideoBlock.blockTimes)
        {
            if (currentTime >= timeBlock.startTime && currentTime <= timeBlock.endTime)
            {
                isInAnyBlock = true;
                
                // Força um ângulo mínimo de 45 graus se o configurado for 0
                float effectiveAngle = currentVideoBlock.angle <= 0 ? 45f : currentVideoBlock.angle;
                
                // Ativa o limitador com o ângulo efetivo
                if (cameraLimiter != null)
                {
                    // Primeiro desativa o simulador para evitar interferência
                    if (cameraSimulator != null)
                    {
                        cameraSimulator.SetSimulationActive(false);
                    }

                    // Força posição zero imediatamente em toda a hierarquia
                    Transform mainCamera = Camera.main?.transform;
                    if (mainCamera != null)
                    {
                        // Reseta todas as rotações para zero
                        Transform root = mainCamera;
                        while (root.parent != null)
                        {
                            root = root.parent;
                        }
                        
                        // Aplica zero do root até a câmera
                        Stack<Transform> hierarchy = new Stack<Transform>();
                        Transform current = mainCamera;
                        while (current != null)
                        {
                            hierarchy.Push(current);
                            current = current.parent;
                        }
                        
                        while (hierarchy.Count > 0)
                        {
                            Transform t = hierarchy.Pop();
                            t.localRotation = Quaternion.identity;
                            t.localEulerAngles = Vector3.zero;
                        }
                    }

                    // Configura o limitador após zerar as posições
                    cameraLimiter.IsLimitActive = true;
                    cameraLimiter.angle = effectiveAngle;
                    
                    Debug.Log($"🎯 Limite de rotação definido: ±{effectiveAngle:F1}°");
                }
                
                currentTimeBlockInfo = timeBlock.ToString();
                Debug.Log($"🔒 Dentro do bloco: {currentTimeBlockInfo}");
                break;
            }
        }

        if (!isInAnyBlock && cameraLimiter != null)
        {
            cameraLimiter.IsLimitActive = false;
            currentTimeBlockInfo = string.Empty;
            
            // Reativa o simulador apenas quando sair do bloco
            if (cameraSimulator != null)
            {
                cameraSimulator.SetSimulationActive(true);
                cameraSimulator.SetMovementIntensity(1.0f);
            }
            
            Debug.Log("🔓 Fora de blocos de tempo - rotação livre");
        }
    }

    private void StartTransitionToZero()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            targetRotation = Vector3.zero;
            
            // Guarda a rotação atual para transição suave ao liberar
            if (cameraLimiter != null && cameraLimiter.transform.parent != null)
            {
                lastRotation = cameraLimiter.transform.parent.localEulerAngles;
            }
            
            // Desativa temporariamente o simulador durante a transição
            if (cameraSimulator != null)
            {
                cameraSimulator.SetSimulationActive(false);
            }
            
            Debug.Log("🔄 Iniciando transição suave para posição inicial");
        }
    }

    private void UpdateTransition()
    {
        if (!isTransitioning || cameraLimiter == null) return;

        Transform mainCamera = Camera.main?.transform;
        if (mainCamera == null) return;

        // Pega a rotação atual da câmera principal
        Vector3 currentRotation = mainCamera.localEulerAngles;
        
        // Normaliza os ângulos para evitar rotações bruscas
        if (currentRotation.x > 180f) currentRotation.x -= 360f;
        if (currentRotation.y > 180f) currentRotation.y -= 360f;
        if (currentRotation.z > 180f) currentRotation.z -= 360f;

        // Calcula a diferença total para o alvo
        float totalDifference = Vector3.Distance(currentRotation, targetRotation);
        
        // Se estiver próximo o suficiente do alvo, finaliza a transição
        if (totalDifference < 0.1f)
        {
            // Garante que todos os componentes estejam na posição correta
            mainCamera.localEulerAngles = targetRotation;
            
            // Se estiver indo para zero, força zero em toda a hierarquia
            if (targetRotation == Vector3.zero)
            {
                Transform current = mainCamera;
                while (current != null)
                {
                    current.localEulerAngles = Vector3.zero;
                    current = current.parent;
                }
                
                if (cameraLimiter.sphereTransform != null)
                {
                    cameraLimiter.sphereTransform.localEulerAngles = Vector3.zero;
                }
            }
            
            // Reativa o simulador apenas se não estiver em modo bloqueado
            if (cameraSimulator != null && !cameraLimiter.IsLimitActive)
            {
                cameraSimulator.SetSimulationActive(true);
                cameraSimulator.SetMovementIntensity(1.0f);
            }
            
            isTransitioning = false;
            Debug.Log("✅ Transição completada suavemente");
            return;
        }

        // Calcula o passo da transição
        float step = transitionSpeed * Time.deltaTime * 30f;
        Vector3 newRotation = Vector3.Lerp(currentRotation, targetRotation, step);

        // Aplica a nova rotação na câmera principal
        mainCamera.localEulerAngles = newRotation;
        
        // Durante a transição para zero, mantém toda a hierarquia alinhada
        if (targetRotation == Vector3.zero)
        {
            Transform current = mainCamera;
            while (current != null)
            {
                current.localEulerAngles = Vector3.zero;
                current = current.parent;
            }
            
            if (cameraLimiter.sphereTransform != null)
            {
                cameraLimiter.sphereTransform.localEulerAngles = Vector3.zero;
            }
        }
        
        // Log da transição a cada mudança significativa
        if (totalDifference >= 15f)
        {
            Debug.Log($"🔄 Transição suave: {currentRotation:F1} → {newRotation:F1}");
        }
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
                Debug.Log($"Configurado bloco para vídeo: {block.videoTitle} com {block.blockTimes.Count} blocos de tempo");
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
        }

        videoPlayer.started += OnVideoStarted;
        videoPlayer.loopPointReached += OnVideoEnded;
        isInitialized = true;
        Debug.Log("VideoRotationControl inicializado");

        // Cache do simulador
        cameraSimulator = FindObjectOfType<CameraMovementSimulator>();
        if (cameraSimulator == null)
        {
            Debug.LogWarning("VideoRotationControl: CameraMovementSimulator não encontrado.");
        }
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
        
        if (cameraLimiter != null)
        {
            cameraLimiter.enabled = enabled;
            cameraLimiter.IsLimitActive = enabled;
            
            if (enabled)
            {
                // Primeiro desativa o simulador
                if (cameraSimulator != null)
                {
                    cameraSimulator.SetSimulationActive(false);
                }
                
                // Força posição zero imediatamente
                Transform mainCamera = Camera.main?.transform;
                if (mainCamera != null)
                {
                    Transform root = mainCamera;
                    while (root.parent != null)
                    {
                        root = root.parent;
                    }
                    
                    Stack<Transform> hierarchy = new Stack<Transform>();
                    Transform current = mainCamera;
                    while (current != null)
                    {
                        hierarchy.Push(current);
                        current = current.parent;
                    }
                    
                    while (hierarchy.Count > 0)
                    {
                        Transform t = hierarchy.Pop();
                        t.localRotation = Quaternion.identity;
                        t.localEulerAngles = Vector3.zero;
                    }
                }
            }
            else
            {
                // Reativa o simulador apenas quando desativar o controle
                if (cameraSimulator != null)
                {
                    cameraSimulator.SetSimulationActive(true);
                    cameraSimulator.SetMovementIntensity(1.0f);
                }
            }
            
            Debug.Log($"CameraLimiter: {(enabled ? "ativado" : "desativado")} e posição {(enabled ? "zerada" : "liberada")}");
        }
        
        Debug.Log($"VideoRotationControl: Controle de rotação {(enabled ? "ativado" : "desativado")}");
    }

    private void OnVideoStarted(VideoPlayer player)
    {
        if (player == null) return;
        
        string videoName = System.IO.Path.GetFileName(player.url);
        Debug.Log($"Vídeo iniciado: {videoName}");
        
        // Procura configuração para este vídeo
        if (videoBlockLookup.TryGetValue(videoName, out VideoBlock block))
        {
            currentVideoBlock = block;
            currentVideoTitleID = videoName;
            Debug.Log($"Configuração encontrada para {videoName}:");
            Debug.Log($"  Ângulo: {block.angle}°");
            foreach (var timeBlock in block.blockTimes)
            {
                Debug.Log($"  Bloco: {timeBlock.startTime:F1}s - {timeBlock.endTime:F1}s");
            }
            
            // Ativa o controle de rotação
            SetRotationControlEnabled(true);
        }
        else
        {
            Debug.LogWarning($"Nenhuma configuração encontrada para o vídeo: {videoName}");
            SetRotationControlEnabled(false);
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

    private void Update()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            currentVideoTime = videoPlayer.time;
            
            // Debug do tempo a cada segundo
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"⏱️ Tempo do vídeo: {currentVideoTime:F1}s");
                
                // Verifica e corrige a posição se necessário
                if (cameraLimiter != null && cameraLimiter.IsLimitActive)
                {
                    Transform mainCamera = Camera.main?.transform;
                    if (mainCamera != null && mainCamera.localEulerAngles != Vector3.zero)
                    {
                        Debug.Log("⚠️ Corrigindo desvio de posição da câmera");
                        mainCamera.localEulerAngles = Vector3.zero;
                        
                        Transform current = mainCamera;
                        while (current != null)
                        {
                            current.localEulerAngles = Vector3.zero;
                            current = current.parent;
                        }
                    }
                }
            }
            
            CheckTimeBlocks();
        }
    }
}
