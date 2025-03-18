using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using AparatoCustomAttributes;

public class CameraRotationLimiter : MonoBehaviour
{
    [Header("Configurações de Limite")]
    public bool IsLimitActive = false;
    public float resetSpeed = 2f; // Velocidade para resetar a esfera
    public float angle = 75f;     // Ângulo máximo de rotação permitido

    [Header("Referências")]
    public Transform sphereTransform; // Referência à esfera contendo a câmera
    public VideoPlayer videoPlayer;   // Referência ao player de vídeo

    // Cache de componentes e valores frequentemente acessados
    private Transform _transform;
    private Quaternion initialRotation;
    private bool referencesInitialized = false;
    private Vector3 lastRotation;
    private bool wasLimitActive = false;
    
    // Cache para cálculos
    private static readonly Vector3 RotationAxis = Vector3.up;
    private Quaternion targetRotation;
    
    // Para ajudar com o debug
    private float lastAngleLimit = 0;

    void Start()
    {
        _transform = transform;
        InitializeReferences();
        initialRotation = _transform.rotation;
        lastRotation = _transform.localEulerAngles;
    }

    // Inicializa referências automaticamente se necessário
    private void InitializeReferences()
    {
        if (sphereTransform == null)
        {
            sphereTransform = _transform.parent;
            if (sphereTransform != null)
            {
                Debug.Log("CameraRotationLimiter: Sphere Transform atribuído ao pai");
            }
            else
            {
                Debug.LogError("CameraRotationLimiter: Sphere Transform não encontrado!");
                return;
            }
        }

        if (videoPlayer == null)
        {
            videoPlayer = GetComponentInParent<VideoPlayer>();
            if (videoPlayer == null)
            {
                videoPlayer = FindObjectOfType<VideoPlayer>();
            }
            
            if (videoPlayer == null)
            {
                Debug.LogWarning("CameraRotationLimiter: VideoPlayer não encontrado");
            }
        }

        referencesInitialized = true;
        Debug.Log("CameraRotationLimiter inicializado");
    }

    void Update()
    {
        if (!referencesInitialized)
        {
            InitializeReferences();
            return;
        }

        // Detecta mudança no estado do limitador
        if (IsLimitActive != wasLimitActive)
        {
            wasLimitActive = IsLimitActive;
            if (IsLimitActive)
            {
                Debug.Log($"Limitador ativado - Ângulo: {angle}°");
            }
            else
            {
                Debug.Log("Limitador desativado");
            }
        }

        // Verifica se o vídeo está tocando ou se o limitador está ativo
        if (videoPlayer != null && (!videoPlayer.isPlaying && !IsLimitActive))
        {
            IsLimitActive = false;
            if (sphereTransform != null)
            {
                sphereTransform.gameObject.SetActive(false);
            }
            return;
        }

        // Controle de rotação no editor
        #if UNITY_EDITOR
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            _transform.Rotate(Vector3.up, -resetSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            _transform.Rotate(Vector3.up, resetSpeed * Time.deltaTime);
        }
        #endif

        // Atualiza a visibilidade da esfera
        if (sphereTransform != null)
        {
            sphereTransform.gameObject.SetActive(videoPlayer != null && videoPlayer.isPlaying);
        }

        // Se o limitador estiver ativo, aplica as restrições
        if (IsLimitActive)
        {
            // Obtém a rotação atual
            Vector3 currentRotation = transform.localEulerAngles;
            
            // Converte o ângulo para o intervalo -180 a 180
            float currentY = currentRotation.y;
            if (currentY > 180f) currentY -= 360f;
            
            // Aplica o limite de forma mais restritiva
            float limitedY = Mathf.Clamp(currentY, -angle, angle);
            
            // Se houver diferença, força a rotação para dentro dos limites
            if (!Mathf.Approximately(currentY, limitedY))
            {
                Debug.Log($"🔒 Forçando rotação: {currentY:F1}° -> {limitedY:F1}° (limite: ±{angle:F1}°)");
                currentRotation.y = limitedY;
                transform.localEulerAngles = currentRotation;
                
                // Força a esfera a ficar centralizada
                if (sphereTransform != null)
                {
                    Vector3 sphereRotation = sphereTransform.localEulerAngles;
                    sphereRotation.y = 0;
                    sphereTransform.localEulerAngles = sphereRotation;
                }
            }
            
            // Impede qualquer movimento adicional
            if (transform.parent != null)
            {
                transform.parent.localRotation = Quaternion.identity;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!IsLimitActive || !Application.isPlaying) return;

        // Desenha os limites de rotação
        Vector3 position = transform.position;
        float radius = 1f;

        // Desenha o arco dos limites
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireArc(
            position,
            Vector3.up,
            Quaternion.Euler(0, -angle, 0) * transform.forward,
            angle * 2,
            radius
        );

        // Desenha as linhas dos limites
        Gizmos.color = Color.red;
        Vector3 leftLimit = Quaternion.Euler(0, -angle, 0) * transform.forward * radius;
        Vector3 rightLimit = Quaternion.Euler(0, angle, 0) * transform.forward * radius;
        Gizmos.DrawLine(position, position + leftLimit);
        Gizmos.DrawLine(position, position + rightLimit);
    }
}
