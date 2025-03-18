using UnityEngine;
using UnityEngine.Video;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class CameraMovementSimulator : MonoBehaviour
{
    [Header("Configurações de Simulação")]
    [Tooltip("Controla a intensidade do movimento aleatório")]
    public float movementIntensity = 0.5f;
    
    [Tooltip("Velocidade da simulação do movimento")]
    public float movementSpeed = 1.0f;
    
    [Tooltip("Quando verdadeiro, foca nas áreas de interesse durante bloqueios")]
    public bool focusOnInterestAreas = true;
    
    [Header("Configurações de Visualização")]
    [Tooltip("Mostra elementos visuais indicando o centro e limites")]
    public bool showVisualIndicators = true;
    
    [Tooltip("Cor do indicador de centro")]
    public Color centerIndicatorColor = Color.red;
    
    [Tooltip("Cor dos limites de bloqueio")]
    public Color limitIndicatorColor = Color.yellow;
    
    [Header("Referências (Preenchidas Automaticamente)")]
    public VideoRotationControl rotationController;
    public CameraRotationLimiter rotationLimiter;
    public VideoPlayer videoPlayer;
    
    // Variáveis privadas
    private Transform cameraTransform;
    private Vector3 targetRotation;
    private Vector3 noiseOffset;
    private bool isLimitActive = false;
    private Vector3 directionToInterest = Vector3.forward;
    private float currentAngleLimit = 0f;
    private bool isMovingToInterest = false;
    private float interestAreaProgress = 0f;
    private bool isSimulationActive = false;
    
    // Constantes
    private const float NOISE_SCALE = 0.5f;
    private const float FOCUS_SPEED_MULTIPLIER = 3.0f;
    private const float MIN_DISTANCE_TO_TARGET = 5.0f;
    
    // Para debug
    private string currentBlockInfo = "";
    
    void Start()
    {
        cameraTransform = transform;
        InitializeComponents();
        StartCoroutine(SimulateRandomFocus());
        
        // Força a simulação a começar ativa
        isSimulationActive = true;
        Debug.Log("Simulador iniciado. Estado inicial da simulação: " + (isSimulationActive ? "Ativo" : "Inativo"));
    }

    private void InitializeComponents()
    {
        // Auto-detecção dos componentes
        if (rotationController == null)
        {
            rotationController = FindObjectOfType<VideoRotationControl>();
            if (rotationController == null)
            {
                Debug.LogError("VideoRotationControl não encontrado! A simulação não funcionará corretamente.");
            }
            else
            {
                Debug.Log("VideoRotationControl encontrado: " + rotationController.name);
            }
        }
        
        if (rotationLimiter == null && rotationController != null)
        {
            rotationLimiter = rotationController.cameraLimiter;
            if (rotationLimiter != null)
            {
                Debug.Log("CameraRotationLimiter encontrado através do VideoRotationControl");
            }
        }
        
        if (rotationLimiter == null)
        {
            rotationLimiter = GetComponentInChildren<CameraRotationLimiter>();
            if (rotationLimiter == null)
            {
                rotationLimiter = FindObjectOfType<CameraRotationLimiter>();
            }
            
            if (rotationLimiter == null)
            {
                Debug.LogError("CameraRotationLimiter não encontrado! O bloqueio não funcionará.");
            }
            else
            {
                Debug.Log("CameraRotationLimiter encontrado: " + rotationLimiter.name);
            }
        }

        if (videoPlayer == null)
        {
            videoPlayer = GetComponentInParent<VideoPlayer>();
            if (videoPlayer == null)
            {
                videoPlayer = FindObjectOfType<VideoPlayer>();
            }
            
            if (videoPlayer != null)
            {
                videoPlayer.started += OnVideoStarted;
                videoPlayer.loopPointReached += OnVideoEnded;
                Debug.Log("VideoPlayer encontrado e eventos configurados: " + videoPlayer.name);
            }
            else
            {
                Debug.LogError("VideoPlayer não encontrado! A simulação não sincronizará com o vídeo.");
            }
        }
        
        // Inicializa valores aleatórios para cada direção
        ResetNoiseOffset();
        
        // Log do estado inicial
        Debug.Log($"Estado dos componentes:\n" +
                 $"- VideoRotationControl: {(rotationController != null ? "OK" : "Faltando")}\n" +
                 $"- CameraRotationLimiter: {(rotationLimiter != null ? "OK" : "Faltando")}\n" +
                 $"- VideoPlayer: {(videoPlayer != null ? "OK" : "Faltando")}");
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.started -= OnVideoStarted;
            videoPlayer.loopPointReached -= OnVideoEnded;
        }
    }

    private void OnVideoStarted(VideoPlayer vp)
    {
        isSimulationActive = true;
        ResetNoiseOffset();
        Debug.Log("Simulação iniciada com o vídeo");
    }

    private void OnVideoEnded(VideoPlayer vp)
    {
        isSimulationActive = false;
        Debug.Log("Simulação parada com o fim do vídeo");
    }

    private void ResetNoiseOffset()
    {
        noiseOffset = new Vector3(
            Random.Range(0f, 100f),
            Random.Range(0f, 100f),
            Random.Range(0f, 100f)
        );
    }
    
    void Update()
    {
        // Verifica se pode executar
        if (!isSimulationActive)
        {
            return;
        }

        // Verifica se o limitador está ativo
        bool isLimited = rotationLimiter != null && rotationLimiter.IsLimitActive;
        
        if (isLimited)
        {
            // Se estiver limitado, para o movimento
            Debug.Log($"Movimento pausado - Limitador ativo com ângulo: {rotationLimiter.angle}°");
            return;
        }

        // Movimento livre randomizado apenas quando não estiver limitado
        SimulateBrownianMovement();
        
        // Aplica a rotação à câmera
        if (cameraTransform != null)
        {
            cameraTransform.localEulerAngles = targetRotation;
        }
    }
    
    // Movimento browniano para simular um usuário explorando livremente
    void SimulateBrownianMovement()
    {
        float time = Time.time * movementSpeed;
        
        // Usa ruído Perlin para cada eixo de rotação
        float xNoise = Mathf.PerlinNoise(time, noiseOffset.x) * 2.0f - 1.0f;
        float yNoise = Mathf.PerlinNoise(time, noiseOffset.y) * 2.0f - 1.0f;
        
        // Rotação mais suave nas direções horizontal e vertical
        targetRotation = new Vector3(
            Mathf.Lerp(targetRotation.x, xNoise * 30f * movementIntensity, Time.deltaTime),
            Mathf.Lerp(targetRotation.y, yNoise * 180f * movementIntensity, Time.deltaTime),
            0f
        );
    }
    
    // Movimento focado em uma direção de interesse
    void SimulateFocusedMovement()
    {
        if (isMovingToInterest)
        {
            // Aumenta o progresso em direção ao alvo
            interestAreaProgress += Time.deltaTime * FOCUS_SPEED_MULTIPLIER;
            
            if (interestAreaProgress >= 1.0f)
            {
                interestAreaProgress = 1.0f;
                isMovingToInterest = false;
            }
            
            // Calcula o ângulo desejado, mas dentro dos limites
            Vector3 currentEuler = cameraTransform.localEulerAngles;
            float yawAngle = Mathf.Atan2(directionToInterest.x, directionToInterest.z) * Mathf.Rad2Deg;
            float pitchAngle = -Mathf.Asin(directionToInterest.y) * Mathf.Rad2Deg;
            
            // Limita o movimento pelo ângulo permitido
            float limitedYawAngle = Mathf.Clamp(yawAngle, -currentAngleLimit, currentAngleLimit);
            
            // Interpola suavemente para a direção de interesse
            targetRotation = new Vector3(
                Mathf.Lerp(currentEuler.x, pitchAngle, interestAreaProgress),
                Mathf.Lerp(currentEuler.y, limitedYawAngle, interestAreaProgress),
                0f
            );
            
            // Pequenos movimentos tipo "respiração" para simular um usuário real
            float breathingX = Mathf.Sin(Time.time * 0.5f) * 2f * (1f - interestAreaProgress);
            float breathingY = Mathf.Sin(Time.time * 0.7f) * 3f * (1f - interestAreaProgress);
            targetRotation += new Vector3(breathingX, breathingY, 0);
        }
        else
        {
            // Pequenos movimentos após chegar no alvo
            float breathingX = Mathf.Sin(Time.time * 0.5f) * 2f;
            float breathingY = Mathf.Sin(Time.time * 0.7f) * 3f;
            targetRotation += new Vector3(breathingX, breathingY, 0) * 0.3f;
        }
    }
    
    // Quando entrar em uma área limitada
    void OnEnterLimitedArea()
    {
        if (rotationLimiter != null)
        {
            // Salva o ângulo limite atual
            currentAngleLimit = rotationLimiter.angle;
            
            // Escolhe uma direção aleatória dentro do limite de rotação
            float randomAngle = Random.Range(-currentAngleLimit, currentAngleLimit) * Mathf.Deg2Rad;
            directionToInterest = new Vector3(Mathf.Sin(randomAngle), 0f, Mathf.Cos(randomAngle)).normalized;
            
            // Inicia a transição para essa área
            interestAreaProgress = 0f;
            isMovingToInterest = true;
            
            // Atualiza informações para debug
            if (rotationController != null)
            {
                currentBlockInfo = rotationController.currentTimeBlockInfo;
                Debug.Log($"<color=yellow>Bloqueio Ativado:</color> {currentBlockInfo} | Área de interesse: {directionToInterest}");
            }
        }
    }
    
    // Quando sair de uma área limitada
    void OnExitLimitedArea()
    {
        currentBlockInfo = "";
        isMovingToInterest = false;
        ResetNoiseOffset();
        Debug.Log("<color=cyan>Bloqueio Desativado:</color> Movimento livre restaurado");
    }
    
    // Coroutine para mudar aleatoriamente o foco durante longos períodos
    IEnumerator SimulateRandomFocus()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(5f, 15f));
            
            if (isSimulationActive && !isLimitActive && !isMovingToInterest)
            {
                ResetNoiseOffset();
                Debug.Log("Mudando foco aleatório");
            }
        }
    }
    
    // Desenha indicadores visuais na tela
    void OnGUI()
    {
        // Sempre mostra o estado da simulação
        GUI.Label(new Rect(10, 10, 300, 20), $"Simulação: {(isSimulationActive ? "Ativa" : "Inativa")}");
        
        if (!showVisualIndicators || !isSimulationActive) return;
        
        // Informações detalhadas
        float y = 30;
        GUI.Label(new Rect(10, y, 300, 20), $"Estado: {(isLimitActive ? "🔒 BLOQUEADO" : "🔓 LIVRE")}"); y += 20;
        
        if (rotationLimiter != null)
        {
            GUI.Label(new Rect(10, y, 300, 20), $"Ângulo: {rotationLimiter.angle:F1}°"); y += 20;
        }
        
        if (rotationController != null)
        {
            GUI.Label(new Rect(10, y, 300, 20), $"Bloco: {rotationController.currentTimeBlockInfo}"); y += 20;
        }
        
        // Desenha um crosshair no centro
        Vector2 center = new Vector2(Screen.width / 2, Screen.height / 2);
        float size = 20;
        GUI.color = isLimitActive ? Color.red : Color.white;
        GUI.DrawTexture(new Rect(center.x - 1, center.y - size/2, 2, size), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x - size/2, center.y - 1, size, 2), Texture2D.whiteTexture);
    }
    
    // Desenha visualizações na cena
    void OnDrawGizmos()
    {
        if (!showVisualIndicators || !Application.isPlaying) return;
        
        if (isLimitActive && rotationLimiter != null)
        {
            // Desenha um arco mostrando os limites de rotação
            Gizmos.color = limitIndicatorColor;
            
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            
            // Desenha linhas indicando o limite
            float limitAngleRad = currentAngleLimit * Mathf.Deg2Rad;
            Vector3 leftLimit = (forward * Mathf.Cos(-limitAngleRad) + right * Mathf.Sin(-limitAngleRad)).normalized;
            Vector3 rightLimit = (forward * Mathf.Cos(limitAngleRad) + right * Mathf.Sin(limitAngleRad)).normalized;
            
            Gizmos.DrawRay(transform.position, leftLimit * 5f);
            Gizmos.DrawRay(transform.position, rightLimit * 5f);
            
            // Desenha a direção de interesse
            Gizmos.color = centerIndicatorColor;
            Gizmos.DrawRay(transform.position, directionToInterest * 3f);
            Gizmos.DrawSphere(transform.position + directionToInterest * 3f, 0.1f);
        }
    }

    public void SetSimulationActive(bool active)
    {
        isSimulationActive = active;
        Debug.Log($"Simulação {(active ? "ativada" : "desativada")}");
    }

    public void SetMovementIntensity(float intensity)
    {
        movementIntensity = Mathf.Clamp01(intensity);
        Debug.Log($"Intensidade do movimento ajustada para: {movementIntensity}");
    }

    public void OnBlockActivated(float angle)
    {
        isLimitActive = true;
        currentAngleLimit = angle;
        Debug.Log($"Simulador: Bloco ativado com ângulo {angle}°");
        
        // Reseta o movimento para evitar transições bruscas
        targetRotation = cameraTransform.localEulerAngles;
        ResetNoiseOffset();
    }

    public void OnBlockDeactivated()
    {
        isLimitActive = false;
        currentAngleLimit = 0f;
        Debug.Log("Simulador: Bloco desativado");
        
        // Reseta o movimento para evitar transições bruscas
        targetRotation = cameraTransform.localEulerAngles;
        ResetNoiseOffset();
    }
} 