using UnityEngine;
using System.Collections;
using System.IO;

/// <summary>
/// Script de emergência para garantir que a aplicação não fique presa 
/// na tela de loading (três bolinhas brancas piscando).
/// Este componente é uma última medida de segurança.
/// </summary>
public class EmergencyStartup : MonoBehaviour
{
    [Header("Configurações")]
    [Tooltip("Tempo máximo em segundos que a aplicação pode ficar na tela de loading")]
    public float maxLoadingTime = 60f;
    [Tooltip("Se verdadeiro, inicia automaticamente um vídeo se o tempo máximo for atingido")]
    public bool autoStartVideoOnTimeout = true;
    [Tooltip("Nome do vídeo a ser iniciado em caso de emergência")]
    public string emergencyVideo = "rio.mp4";
    
    [Header("Estado")]
    [SerializeField] private float appUptime = 0f;
    [SerializeField] private bool emergencyActivated = false;
    [SerializeField] private string appState = "Inicializando";
    
    private VRManager vrManager;
    private DiagnosticUI diagnosticUI;
    private ConnectionHelper connectionHelper;
    
    void Start()
    {
        Debug.Log("🚨 Sistema de emergência iniciando...");
        
        // Agendar verificação após um atraso inicial
        Invoke(nameof(CheckComponents), 5f);
        
        // Iniciar verificação de emergência
        StartCoroutine(EmergencyCheckRoutine());
    }
    
    void Update()
    {
        appUptime += Time.deltaTime;
    }
    
    // Verifica se todos os componentes necessários estão presentes
    void CheckComponents()
    {
        vrManager = FindObjectOfType<VRManager>();
        diagnosticUI = FindObjectOfType<DiagnosticUI>();
        connectionHelper = FindObjectOfType<ConnectionHelper>();
        
        // Criar diagnóstico se não existir
        if (diagnosticUI == null)
        {
            Debug.LogWarning("DiagnosticUI não encontrado - criando novo componente");
            GameObject diagObj = new GameObject("DiagnosticUI");
            diagnosticUI = diagObj.AddComponent<DiagnosticUI>();
        }
        
        // Criar connection helper se não existir
        if (connectionHelper == null)
        {
            Debug.LogWarning("ConnectionHelper não encontrado - criando novo componente");
            GameObject helperObj = new GameObject("ConnectionHelper");
            connectionHelper = helperObj.AddComponent<ConnectionHelper>();
            
            if (vrManager != null)
            {
                connectionHelper.vrManager = vrManager;
            }
        }
        
        Debug.Log("✅ Verificação de componentes concluída");
    }
    
    // Verificação periódica para garantir que a aplicação não fique presa
    IEnumerator EmergencyCheckRoutine()
    {
        // Espera inicial
        yield return new WaitForSeconds(15f);
        
        appState = "Monitorando aplicação";
        
        while (true)
        {
            // Verificar se VRManager está presente
            if (vrManager == null)
            {
                vrManager = FindObjectOfType<VRManager>();
            }
            
            // Verificar se aplicação está presa na tela de loading
            if (vrManager != null && !vrManager.isPlaying && appUptime > maxLoadingTime && !emergencyActivated)
            {
                Debug.LogWarning($"🚨 ALERTA! Aplicação presa na tela de loading por {appUptime:F0} segundos");
                
                // Ativar procedimento de emergência
                ActivateEmergencyProcedure();
                emergencyActivated = true;
            }
            
            yield return new WaitForSeconds(5f);
        }
    }
    
    // Procedimento de emergência para sair da tela de loading
    void ActivateEmergencyProcedure()
    {
        Debug.Log("🚨 Iniciando procedimento de emergência!");
        appState = "Procedimento de emergência ativado";
        
        // 1. Mostrar interface de diagnóstico
        if (diagnosticUI != null)
        {
            diagnosticUI.ShowDiagnostic();
        }
        
        // 2. Forçar modo offline
        if (vrManager != null)
        {
            vrManager.offlineMode = true;
            Debug.Log("Mode offline ativado");
        }
        
        // 3. Se configurado, iniciar vídeo automaticamente
        if (autoStartVideoOnTimeout)
        {
            StartCoroutine(AutoStartVideo());
        }
    }
    
    // Inicia um vídeo automaticamente após verificação
    IEnumerator AutoStartVideo()
    {
        Debug.Log("🎬 Preparando para iniciar vídeo de emergência...");
        yield return new WaitForSeconds(2f);
        
        // Verificar se já temos um vídeo em reprodução
        if (vrManager != null && !vrManager.isPlaying)
        {
            // Verificar se o vídeo de emergência existe
            string videoToPlay = emergencyVideo;
            
            try
            {
                // Tentar encontrar um vídeo disponível
                string externalPath = Path.Combine("/sdcard", "Download");
                if (Directory.Exists(externalPath))
                {
                    string emergencyFilePath = Path.Combine(externalPath, emergencyVideo);
                    
                    if (File.Exists(emergencyFilePath))
                    {
                        Debug.Log($"✅ Vídeo de emergência encontrado: {emergencyVideo}");
                    }
                    else
                    {
                        // Procurar qualquer vídeo mp4
                        string[] mp4Files = Directory.GetFiles(externalPath, "*.mp4");
                        if (mp4Files.Length > 0)
                        {
                            videoToPlay = Path.GetFileName(mp4Files[0]);
                            Debug.Log($"✅ Usando vídeo alternativo: {videoToPlay}");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Erro ao verificar vídeos: {e.Message}");
            }
            
            // Forçar início do vídeo
            Debug.Log($"🚨 EMERGÊNCIA: Forçando início do vídeo {videoToPlay}");
            vrManager.ForcePlayVideo(videoToPlay);
            
            // Esconder interface de diagnóstico após iniciar o vídeo
            yield return new WaitForSeconds(5f);
            if (diagnosticUI != null && vrManager.isPlaying)
            {
                diagnosticUI.HideDiagnostic();
            }
        }
    }
} 