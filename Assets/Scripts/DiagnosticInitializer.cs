using UnityEngine;
using UnityEngine.SceneManagement;

// Adicione essa diretiva para os trechos que usam o SDK do Oculus
#if USING_OCULUS_SDK
using Oculus.VR;
#endif

/// <summary>
/// Adiciona automaticamente os componentes de diagnóstico ao iniciar a cena.
/// Este script deve ser adicionado a um GameObject na cena inicial.
/// </summary>
public class DiagnosticInitializer : MonoBehaviour
{
    [Tooltip("Se verdadeiro, cria automaticamente a interface de diagnóstico na inicialização")]
    public bool createOnStart = true;
    
    [Tooltip("Se verdadeiro, cria a interface apenas se ocorrer um erro")]
    public bool createOnError = true;
    
    [Tooltip("Se verdadeiro, permite ativar diagnóstico com botão do controle")]
    public bool enableEmergencyButton = true;
    
    [Tooltip("Número de vezes que o botão B ou Y precisa ser pressionado em 3 segundos")]
    public int buttonPressCount = 3;
    
    private float buttonTimer = 0f;
    private int currentPressCount = 0;
    private DiagnosticUI diagUI;
    private bool showingDiagnostic = false;
    
    void Awake()
    {
        // Não destruir ao carregar novas cenas
        DontDestroyOnLoad(gameObject);
        
        // Registrar para detecção de erros
        Application.logMessageReceived += OnLogMessage;
    }
    
    void Start()
    {
        if (createOnStart)
        {
            CreateDiagnosticUI();
        }
    }
    
    void Update()
    {
        // Verificar botão de emergência (botão B/Y do Quest ou tecla D no teclado)
        if (enableEmergencyButton)
        {
            CheckEmergencyButton();
        }
    }
    
    // Detecta pressionamento do botão de emergência
    void CheckEmergencyButton()
    {
        bool buttonPressed = false;
        
        // Verificar controle VR Oculus - Botão B ou Y
        #if UNITY_ANDROID && !UNITY_EDITOR && USING_OCULUS_SDK
        // Verifica botão B (mão direita) ou Y (mão esquerda)
        if (OVRInput.GetDown(OVRInput.Button.Two) || OVRInput.GetDown(OVRInput.Button.Four))
        {
            buttonPressed = true;
        }
        #else
        // No editor ou outras plataformas, usar tecla D
        if (Input.GetKeyDown(KeyCode.D))
        {
            buttonPressed = true;
        }
        #endif
        
        // Processar pressionamento
        if (buttonPressed)
        {
            // Se o timer expirou, reinicia a contagem
            if (buttonTimer <= 0)
            {
                currentPressCount = 1;
                buttonTimer = 3.0f; // 3 segundos para pressionar múltiplas vezes
                Debug.Log("Botão de emergência: 1º toque");
            }
            else
            {
                // Incrementa contador de pressionamentos
                currentPressCount++;
                Debug.Log($"Botão de emergência: {currentPressCount}º toque");
                
                // Verifica se atingiu o número necessário
                if (currentPressCount >= buttonPressCount)
                {
                    ToggleDiagnosticUI();
                    // Reset
                    currentPressCount = 0;
                    buttonTimer = 0;
                }
            }
        }
        
        // Atualiza o timer
        if (buttonTimer > 0)
        {
            buttonTimer -= Time.deltaTime;
            if (buttonTimer <= 0)
            {
                // Timer expirou sem completar sequência
                currentPressCount = 0;
            }
        }
    }
    
    // Alterna a exibição do diagnóstico
    void ToggleDiagnosticUI()
    {
        if (showingDiagnostic)
        {
            HideDiagnosticUI();
        }
        else
        {
            ShowDiagnosticUI();
        }
    }
    
    // Mostra a interface de diagnóstico
    void ShowDiagnosticUI()
    {
        DiagnosticUI diagInstance = FindObjectOfType<DiagnosticUI>();
        if (diagInstance != null)
        {
            diagInstance.ShowDiagnostic();
            showingDiagnostic = true;
            Debug.Log("Interface de diagnóstico ativada via botão de emergência");
        }
        else
        {
            CreateDiagnosticUI();
            showingDiagnostic = true;
        }
    }
    
    // Oculta a interface de diagnóstico
    void HideDiagnosticUI()
    {
        DiagnosticUI diagInstance = FindObjectOfType<DiagnosticUI>();
        if (diagInstance != null)
        {
            diagInstance.HideDiagnostic();
            showingDiagnostic = false;
            Debug.Log("Interface de diagnóstico desativada via botão de emergência");
        }
    }
    
    // Detectar mensagens de erro
    void OnLogMessage(string logString, string stackTrace, LogType type)
    {
        if (createOnError && (type == LogType.Error || type == LogType.Exception))
        {
            // Criar interface de diagnóstico se ocorrer erro crítico
            CreateDiagnosticUI();
        }
    }
    
    void CreateDiagnosticUI()
    {
        // Verificar se já existe uma interface de diagnóstico
        DiagnosticUI existingUI = FindObjectOfType<DiagnosticUI>();
        if (existingUI == null)
        {
            // Criar novo objeto para a interface
            GameObject diagObj = new GameObject("DiagnosticUI");
            diagUI = diagObj.AddComponent<DiagnosticUI>();
            
            // Não destruir ao carregar novas cenas
            DontDestroyOnLoad(diagObj);
            
            Debug.Log("Interface de diagnóstico criada automaticamente.");
        }
        else
        {
            diagUI = existingUI;
        }
        
        // Mostrar a interface
        if (diagUI != null)
        {
            diagUI.ShowDiagnostic();
            showingDiagnostic = true;
        }
    }
    
    public static void ShowDiagnostic()
    {
        DiagnosticUI.ShowDiagnosticUI();
    }
    
    void OnDestroy()
    {
        // Remover listener ao destruir
        Application.logMessageReceived -= OnLogMessage;
    }
} 