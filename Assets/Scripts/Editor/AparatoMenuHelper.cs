using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using System.IO;
using System.Reflection;
using System.Linq;

// Menu principal para as ferramentas da aparato
public static class AparatoMenuHelper
{
    [MenuItem("▸aparato◂/Sobre")]
    public static void ShowAbout()
    {
        EditorUtility.DisplayDialog("▸aparato◂ Toolkit",
            "Ferramentas de desenvolvimento para experiências VR/AR da aparato.\n\n" +
            "Versão: 1.2.0\n" +
            "© aparato - www.aparato.com.br",
            "OK");
    }
    
    #region Ferramentas de Vídeo 360

    [MenuItem("▸aparato◂/Criar Player de Vídeo 360")]
    public static void Create360VideoPlayer()
    {
        // Utiliza reflection para encontrar a classe Video360PlayerCreator em qualquer namespace
        System.Type creatorType = GetTypeFromAssemblies("Video360PlayerCreator");
        if (creatorType != null)
        {
            MethodInfo method = creatorType.GetMethod("CreateVideoPlayer", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, null);
                return;
            }
        }
        
        // Se não encontrou a classe, informa ao usuário
        Debug.LogError("Não foi possível encontrar a classe Video360PlayerCreator. Verifique se o script está no projeto.");
        EditorUtility.DisplayDialog("Erro", "Não foi possível encontrar a ferramenta de criação de player 360. Verifique se todos os scripts necessários estão instalados.", "OK");
    }
    
    [MenuItem("▸aparato◂/Adicionar Simulador de Câmera")]
    public static void AddCameraSimulator()
    {
        // Verifica se já existe uma câmera com o simulador
        var existingSimulator = Object.FindObjectOfType<CameraMovementSimulator>();
        if (existingSimulator != null)
        {
            // Seleciona o objeto existente
            Selection.activeGameObject = existingSimulator.gameObject;
            EditorGUIUtility.PingObject(existingSimulator.gameObject);
            EditorUtility.DisplayDialog("Simulador Existente", "Um simulador de câmera já existe na cena.", "OK");
            return;
        }
        
        // Procura pela câmera principal
        Camera mainCamera = Camera.main;
        GameObject cameraObject = null;
        
        if (mainCamera != null)
        {
            cameraObject = mainCamera.gameObject;
        }
        else
        {
            // Cria uma nova câmera se não existir
            cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
        
        // Adiciona o simulador
        CameraMovementSimulator simulator = cameraObject.AddComponent<CameraMovementSimulator>();
        
        // Tenta encontrar e associar os componentes relacionados
        simulator.rotationController = Object.FindObjectOfType<VideoRotationControl>();
        simulator.rotationLimiter = Object.FindObjectOfType<CameraRotationLimiter>();
        
        // Seleciona o objeto
        Selection.activeGameObject = cameraObject;
        
        Debug.Log("Simulador de câmera adicionado com sucesso.");
        EditorUtility.DisplayDialog("Simulador Adicionado", 
            "O simulador de câmera foi adicionado à câmera principal.\n\n" +
            "Ele irá simular o movimento natural do usuário e mostrará indicadores visuais durante os bloqueios de rotação.", 
            "OK");
    }
    
    [MenuItem("▸aparato◂/Configurar Bloqueios de Vídeo")]
    public static void OpenVideoBlockingTool()
    {
        // Procura por um VideoRotationControl na cena
        var rotationControl = Object.FindObjectOfType<VideoRotationControl>();
        if (rotationControl == null)
        {
            // Se não encontrou, pergunta se deseja criar um
            if (EditorUtility.DisplayDialog("VideoRotationControl não encontrado",
                "Nenhum VideoRotationControl foi encontrado na cena. Deseja criar um novo?",
                "Sim", "Não"))
            {
                // Cria um novo objeto com VideoRotationControl
                GameObject controllerObj = new GameObject("VideoRotationControl");
                rotationControl = controllerObj.AddComponent<VideoRotationControl>();
                
                // Tenta encontrar e associar componentes relacionados
                rotationControl.videoPlayer = Object.FindObjectOfType<VideoPlayer>();
                rotationControl.cameraLimiter = Object.FindObjectOfType<CameraRotationLimiter>();
                
                // Seleciona o novo objeto
                Selection.activeGameObject = controllerObj;
                EditorGUIUtility.PingObject(controllerObj);
            }
            else
            {
                return;
            }
        }
        
        // Seleciona o objeto com VideoRotationControl
        Selection.activeObject = rotationControl;
        
        // Abre o Inspector
        EditorApplication.ExecuteMenuItem("Window/General/Inspector");
    }
    
    #endregion
    
    #region Ferramentas de Configuração
    
    [MenuItem("▸aparato◂/Configurar para Meta Quest 3")]
    public static void SetupForMetaQuest3()
    {
        // Utiliza reflection para encontrar a classe MetaQuestSetupTool em qualquer namespace
        System.Type setupToolType = GetTypeFromAssemblies("MetaQuestSetupTool");
        if (setupToolType != null)
        {
            MethodInfo method = setupToolType.GetMethod("SetupProject", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, null);
                return;
            }
        }
        
        // Se não encontrou a classe, informa ao usuário
        Debug.LogError("Não foi possível encontrar a classe MetaQuestSetupTool. Verifique se o script está no projeto.");
        EditorUtility.DisplayDialog("Erro", "Não foi possível encontrar a ferramenta de configuração para Meta Quest 3. Verifique se todos os scripts necessários estão instalados.", "OK");
    }
    
    #endregion
    
    #region Utilidades
    
    [MenuItem("▸aparato◂/Utilidades/Abrir Pasta do Projeto")]
    public static void OpenProjectFolder()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        EditorUtility.RevealInFinder(path);
    }
    
    [MenuItem("▸aparato◂/Utilidades/Localizar Logs")]
    public static void OpenLogFolder()
    {
        string logPath = Path.Combine(Application.persistentDataPath, "Logs");
        if (!Directory.Exists(logPath))
        {
            Directory.CreateDirectory(logPath);
        }
        EditorUtility.RevealInFinder(logPath);
    }
    
    [MenuItem("▸aparato◂/Utilidades/Visualizar Configurações de Rotação")]
    public static void OpenRotationDebugUI()
    {
        RotationDebugUI.ShowWindow();
    }
    
    #endregion
    
    // Método helper para encontrar um tipo em qualquer assembly carregado
    private static System.Type GetTypeFromAssemblies(string typeName)
    {
        // Primeiro tenta encontrar no assembly atual
        var type = System.Type.GetType(typeName);
        if (type != null)
            return type;
        
        // Procura em todos os assemblies carregados
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
            if (type != null)
                return type;
        }
        
        return null;
    }
} 