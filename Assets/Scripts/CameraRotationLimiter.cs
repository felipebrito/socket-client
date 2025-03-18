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
    private float currentRotationY;
    private bool referencesInitialized = false;
    
    // Cache para cálculos
    private static readonly Vector3 RotationAxis = Vector3.up;
    private Quaternion targetRotation;
    
    // Para ajudar com o debug
    private float lastAngleLimit = 0;

    void Awake()
    {
        // Cache da referência ao transform para evitar acessos repetidos
        _transform = transform;
    }

    void Start()
    {
        InitializeReferences();
        initialRotation = _transform.rotation;
    }

    // Inicializa referências automaticamente se necessário
    private void InitializeReferences()
    {
        if (sphereTransform == null)
        {
            // Tenta encontrar automaticamente - geralmente é o pai ou um objeto relacionado
            sphereTransform = _transform.parent;
            if (sphereTransform != null)
            {
                Debug.LogWarning("CameraRotationLimiter: Sphere Transform foi atribuído automaticamente ao pai. Verifique se está correto.");
            }
            else
            {
                Debug.LogError("CameraRotationLimiter: Sphere Transform não foi encontrado. O limitador não funcionará corretamente.");
                return;
            }
        }

        if (videoPlayer == null)
        {
            // Tenta encontrar o VideoPlayer no pai ou no atual GameObject
            videoPlayer = GetComponentInParent<VideoPlayer>();
            if (videoPlayer == null && sphereTransform != null)
            {
                videoPlayer = sphereTransform.GetComponentInChildren<VideoPlayer>();
            }
            
            if (videoPlayer == null)
            {
                Debug.LogWarning("CameraRotationLimiter: VideoPlayer não encontrado automaticamente. Algumas funções podem não funcionar.");
            }
        }

        referencesInitialized = true;
    }

    void Update()
    {
        // Garante que as referências estão inicializadas
        if (!EnsureReferences())
        {
            return;
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

        // Se o limitador estiver ativo, aplica a limitação
        if (IsLimitActive)
        {
            // Obtém a rotação atual da câmera
            Vector3 currentRotation = _transform.localEulerAngles;
            float currentYaw = currentRotation.y;
            if (currentYaw > 180) currentYaw -= 360;

            // Se a rotação exceder o limite
            if (Mathf.Abs(currentYaw) > angle)
            {
                // Calcula a rotação alvo
                float clampedYaw = Mathf.Clamp(currentYaw, -angle, angle);
                Quaternion targetRotation = Quaternion.Euler(
                    currentRotation.x,
                    clampedYaw,
                    currentRotation.z
                );

                // Aplica a rotação suave
                _transform.rotation = Quaternion.Lerp(
                    _transform.rotation,
                    targetRotation,
                    resetSpeed * Time.deltaTime
                );

                // Log detalhado quando a limitação é aplicada
                Debug.Log($"🔄 Aplicando limitação de rotação - Ângulo atual: {currentYaw:F1}°, Limite: ±{angle:F1}°, Velocidade: {resetSpeed:F1}");
            }
        }
    }

    private bool EnsureReferences()
    {
        if (!referencesInitialized)
        {
            InitializeReferences();
            if (!referencesInitialized)
            {
                Debug.LogError("CameraRotationLimiter: Referências não inicializadas corretamente.");
                return false;
            }
        }
        return true;
    }
}
