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
    
    // Variáveis privadas
    private Transform cameraTransform;
    private Vector3 targetRotation;
    private Vector3 noiseOffset;
    private bool isLimitActive = false;
    private Vector3 directionToInterest = Vector3.forward;
    private float currentAngleLimit = 0f;
    private bool isMovingToInterest = false;
    private float interestAreaProgress = 0f;
    
    // Constantes
    private const float NOISE_SCALE = 0.5f;
    private const float FOCUS_SPEED_MULTIPLIER = 3.0f;
    private const float MIN_DISTANCE_TO_TARGET = 5.0f;
    
    // Para debug
    private string currentBlockInfo = "";
    
    void Start()
    {
        cameraTransform = transform;
        
        // Inicializa valores aleatórios para cada direção
        noiseOffset = new Vector3(
            Random.Range(0f, 100f),
            Random.Range(0f, 100f),
            Random.Range(0f, 100f)
        );
        
        // Auto-detecção dos componentes
        if (rotationController == null)
        {
            rotationController = FindObjectOfType<VideoRotationControl>();
            if (rotationController == null)
            {
                Debug.LogWarning("VideoRotationControl não encontrado! A simulação não focará em áreas de interesse.");
            }
        }
        
        if (rotationLimiter == null && rotationController != null)
        {
            rotationLimiter = rotationController.cameraLimiter;
        }
        
        if (rotationLimiter == null)
        {
            rotationLimiter = GetComponentInChildren<CameraRotationLimiter>();
            if (rotationLimiter == null)
            {
                rotationLimiter = FindObjectOfType<CameraRotationLimiter>();
            }
        }
        
        // Iniciar a simulação
        StartCoroutine(SimulateRandomFocus());
    }
    
    void Update()
    {
        // Verifica se o controle de rotação está habilitado
        if (rotationController != null && !rotationController.IsRotationControlEnabled)
        {
            isLimitActive = false;
            return;
        }

        // Verifica se o limitador está ativo
        bool wasLimitActive = isLimitActive;
        isLimitActive = rotationLimiter != null && rotationLimiter.IsLimitActive;
        
        // Quando entra em um bloco de limitação
        if (!wasLimitActive && isLimitActive)
        {
            OnEnterLimitedArea();
        }
        // Quando sai de um bloco de limitação
        else if (wasLimitActive && !isLimitActive)
        {
            OnExitLimitedArea();
        }
        
        if (isLimitActive && focusOnInterestAreas)
        {
            // Quando em uma área limitada, foca no centro do interesse
            SimulateFocusedMovement();
        }
        else
        {
            // Movimento livre randomizado
            SimulateBrownianMovement();
        }
        
        // Aplica a rotação à câmera
        cameraTransform.localEulerAngles = targetRotation;
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
        Debug.Log("<color=cyan>Bloqueio Desativado:</color> Movimento livre restaurado");
    }
    
    // Coroutine para mudar aleatoriamente o foco durante longos períodos
    IEnumerator SimulateRandomFocus()
    {
        while (true)
        {
            // Espera um tempo aleatório antes de mudar o foco
            yield return new WaitForSeconds(Random.Range(5f, 15f));
            
            // Só muda o foco se estiver em uma área livre
            if (!isLimitActive && !isMovingToInterest)
            {
                noiseOffset = new Vector3(
                    Random.Range(0f, 100f),
                    Random.Range(0f, 100f),
                    0f
                );
            }
        }
    }
    
    // Desenha indicadores visuais na tela
    void OnGUI()
    {
        if (!showVisualIndicators || !isLimitActive) return;
        
        // Obtém o centro da tela
        Vector2 center = new Vector2(Screen.width / 2, Screen.height / 2);
        
        // Desenha o indicador central
        GUI.color = centerIndicatorColor;
        GUI.Box(new Rect(center.x - 15, center.y - 15, 30, 30), "");
        
        // Informações do bloqueio
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.UpperCenter;
        style.fontSize = 14;
        style.normal.textColor = centerIndicatorColor;
        
        GUI.Label(new Rect(0, 20, Screen.width, 30), 
            $"BLOQUEIO ATIVO: {currentBlockInfo} | Limite: ±{currentAngleLimit:F0}°", style);
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
} 