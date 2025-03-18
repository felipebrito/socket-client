using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Video;

public class Video360PlayerCreator : EditorWindow
{
    private string videoPlayerName = "Video360Player";
    private string videoPath = "";
    private bool autoPlay = true;
    private bool loop = true;
    private VideoRenderMode renderMode = VideoRenderMode.RenderTexture;
    private float sphereRadius = 50f;
    private bool createUI = true;
    
    [MenuItem("▸aparato◂/Meta Quest 3/Criar videoplayer 360")]
    public static void ShowWindow()
    {
        GetWindow<Video360PlayerCreator>("360° Video Player Creator");
    }
    
    private void OnGUI()
    {
        // Exibe o logo no início da janela
        Rect logoRect = new Rect(10, 10, position.width - 20, 60);
        AparatoMenuHelper.DrawLogo(logoRect);
        
        GUILayout.Space(70); // Espaço para o logo
        
        GUILayout.Label("360° Video Player Creator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Esta ferramenta cria um player de vídeo 360° otimizado para Meta Quest 3", MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        videoPlayerName = EditorGUILayout.TextField("Nome do Player:", videoPlayerName);
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        videoPath = EditorGUILayout.TextField("Caminho do Vídeo (opcional):", videoPath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFilePanel("Selecionar Vídeo", "", "mp4,mov,m4v");
            if (!string.IsNullOrEmpty(path))
            {
                videoPath = path;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        autoPlay = EditorGUILayout.Toggle("Auto Play:", autoPlay);
        loop = EditorGUILayout.Toggle("Loop:", loop);
        renderMode = (VideoRenderMode)EditorGUILayout.EnumPopup("Render Mode:", renderMode);
        
        EditorGUILayout.Space(5);
        
        sphereRadius = EditorGUILayout.Slider("Raio da Esfera:", sphereRadius, 10f, 100f);
        createUI = EditorGUILayout.Toggle("Criar UI de Controle:", createUI);
        
        EditorGUILayout.Space(20);
        
        if (GUILayout.Button("Criar Player de Vídeo 360°", GUILayout.Height(40)))
        {
            CreateVideo360Player();
        }
        
        // Add footer at the end of OnGUI method
        EditorGUILayout.Space(10);
        GUILayout.FlexibleSpace();
        GUILayout.Label("▸codex || aparato®", EditorStyles.centeredGreyMiniLabel);
    }
    
    private void CreateVideo360Player()
    {
        // Verificar se a pasta Prefabs existe
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/360VideoPlayer"))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "360VideoPlayer");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        
        // Criar objeto pai
        GameObject playerObject = new GameObject(videoPlayerName);
        
        // Adicionar componente de vídeo
        VideoPlayer videoPlayer = playerObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = autoPlay;
        videoPlayer.isLooping = loop;
        videoPlayer.renderMode = renderMode;
        
        if (renderMode == VideoRenderMode.RenderTexture)
        {
            // Criar uma render texture para o vídeo
            string rtPath = "Assets/Materials/Video360RenderTexture.renderTexture";
            RenderTexture rt = null;
            
            if (File.Exists(rtPath))
            {
                rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            }
            else
            {
                rt = new RenderTexture(4096, 2048, 24);
                rt.name = "Video360RenderTexture";
                AssetDatabase.CreateAsset(rt, rtPath);
            }
            
            videoPlayer.targetTexture = rt;
        }
        
        // Tentar carregar o vídeo se o caminho for fornecido
        if (!string.IsNullOrEmpty(videoPath))
        {
            videoPlayer.url = videoPath;
        }
        
        // Criar a esfera para projeção do vídeo
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "VideoSphere";
        sphere.transform.SetParent(playerObject.transform);
        sphere.transform.localScale = new Vector3(sphereRadius, sphereRadius, sphereRadius);
        
        // Inverter as normais da esfera para que o vídeo seja exibido na parte interna
        Mesh mesh = sphere.GetComponent<MeshFilter>().mesh;
        mesh.InvertNormals();
        
        // Criar ou carregar o material para o vídeo
        Material videoMaterial = null;
        string materialPath = "Assets/Materials/Video360Material.mat";
        
        if (File.Exists(materialPath))
        {
            videoMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }
        else
        {
            videoMaterial = new Material(Shader.Find("Unlit/Texture"));
            videoMaterial.name = "Video360Material";
            AssetDatabase.CreateAsset(videoMaterial, materialPath);
        }
        
        // Configurar o material para usar a render texture
        if (renderMode == VideoRenderMode.RenderTexture)
        {
            videoMaterial.mainTexture = videoPlayer.targetTexture;
        }
        
        // Aplicar o material à esfera
        sphere.GetComponent<Renderer>().material = videoMaterial;
        
        // Configurar o vídeo player para renderizar diretamente no material se não estiver usando render texture
        if (renderMode == VideoRenderMode.MaterialOverride)
        {
            videoPlayer.targetMaterialRenderer = sphere.GetComponent<Renderer>();
            videoPlayer.targetMaterialProperty = "_MainTex";
        }
        
        // Adicionar Audio Source
        AudioSource audioSource = playerObject.AddComponent<AudioSource>();
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);
        
        // Criar UI de controle se solicitado
        if (createUI)
        {
            CreateVideoControlUI(playerObject);
        }
        
        // Adicionar script de controle básico se não existir
        if (!File.Exists("Assets/Scripts/Video360Controller.cs"))
        {
            CreateVideo360ControllerScript();
        }
        
        // Adicionar o script de controle
        playerObject.AddComponent(System.Type.GetType("Video360Controller"));
        
        // Criar um prefab
        string prefabPath = "Assets/Prefabs/360VideoPlayer/" + videoPlayerName + ".prefab";
        
        // Salvar o prefab
        PrefabUtility.SaveAsPrefabAsset(playerObject, prefabPath);
        
        // Destruir o objeto de cena
        DestroyImmediate(playerObject);
        
        // Notificar o usuário
        EditorUtility.DisplayDialog("Player de Vídeo 360° Criado", 
            "O player de vídeo 360° foi criado com sucesso!\n\n" +
            "Você pode encontrar o prefab em: " + prefabPath, "OK");
        
        // Selecionar o prefab no Project
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }
    
    private void CreateVideoControlUI(GameObject parent)
    {
        // Este método criaria uma UI básica para controlar o vídeo
        // Verificar se o pacote TextMeshPro está instalado
        bool hasTMP = false;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name.Contains("Unity.TextMeshPro"))
            {
                hasTMP = true;
                break;
            }
        }
        
        if (!hasTMP)
        {
            Debug.LogWarning("TextMeshPro não encontrado. UI de controle simplificada será criada.");
            EditorUtility.DisplayDialog("TextMeshPro Recomendado", 
                "Para uma UI melhor, instale o TextMeshPro via Package Manager.", "OK");
        }
        
        // Criar um Canvas para a UI
        GameObject canvasObject = new GameObject("ControlUI");
        canvasObject.transform.SetParent(parent.transform);
        
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        canvasObject.transform.localPosition = new Vector3(0, 0, 5);
        canvasObject.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        
        // Criar um painel de fundo
        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform);
        
        UnityEngine.UI.Image panelImage = panelObject.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);
        
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.sizeDelta = new Vector2(300, 200);
        panelRect.anchoredPosition = Vector2.zero;
        
        // Criar botões Play/Pause
        GameObject playButton = CreateButton(panelObject, "PlayButton", "Play", new Vector2(0, 50));
        GameObject pauseButton = CreateButton(panelObject, "PauseButton", "Pause", new Vector2(0, 0));
        GameObject stopButton = CreateButton(panelObject, "StopButton", "Stop", new Vector2(0, -50));
    }
    
    private GameObject CreateButton(GameObject parent, string name, string text, Vector2 position)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent.transform);
        
        UnityEngine.UI.Button button = buttonObject.AddComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Image buttonImage = buttonObject.AddComponent<UnityEngine.UI.Image>();
        button.targetGraphic = buttonImage;
        
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(160, 30);
        
        // Adicionar texto
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform);
        
        UnityEngine.UI.Text buttonText = textObject.AddComponent<UnityEngine.UI.Text>();
        buttonText.text = text;
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.black;
        
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        return buttonObject;
    }
    
    private void CreateVideo360ControllerScript()
    {
        string scriptPath = "Assets/Scripts";
        
        // Criar pasta Scripts se não existir
        if (!AssetDatabase.IsValidFolder(scriptPath))
        {
            AssetDatabase.CreateFolder("Assets", "Scripts");
        }
        
        // Conteúdo do script
        string scriptContent = @"using UnityEngine;
using UnityEngine.Video;

public class Video360Controller : MonoBehaviour
{
    private VideoPlayer videoPlayer;
    private bool isPlaying = false;
    
    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
        {
            Debug.LogError(""VideoPlayer component not found!"");
            return;
        }
        
        isPlaying = videoPlayer.playOnAwake;
        
        // Adicionar listener para quando o vídeo terminar
        videoPlayer.loopPointReached += OnVideoEnd;
        
        // Configurar controles VR
        SetupVRControls();
    }
    
    void Update()
    {
        // Controles de teclado (para testes no editor)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TogglePlayPause();
        }
        
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            StopVideo();
        }
        
        // Retornar 10 segundos
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            videoPlayer.time = Mathf.Max(0, videoPlayer.time - 10);
        }
        
        // Avançar 10 segundos
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            videoPlayer.time = Mathf.Min(videoPlayer.length, videoPlayer.time + 10);
        }
    }
    
    public void TogglePlayPause()
    {
        if (isPlaying)
        {
            PauseVideo();
        }
        else
        {
            PlayVideo();
        }
    }
    
    public void PlayVideo()
    {
        videoPlayer.Play();
        isPlaying = true;
    }
    
    public void PauseVideo()
    {
        videoPlayer.Pause();
        isPlaying = false;
    }
    
    public void StopVideo()
    {
        videoPlayer.Stop();
        isPlaying = false;
    }
    
    private void OnVideoEnd(VideoPlayer vp)
    {
        isPlaying = false;
        
        if (!videoPlayer.isLooping)
        {
            Debug.Log(""Video playback completed."");
        }
    }
    
    private void SetupVRControls()
    {
        // Configuração de controles VR aqui
        // Isto varia dependendo do sistema de input usado (XR Interaction Toolkit, Oculus Integration, etc.)
        
        #if UNITY_ANDROID && !UNITY_EDITOR
        // Código específico para dispositivos Oculus/Meta Quest
        OVRManager.display.displayFrequency = 72.0f; // Ou outro valor desejado: 72, 80, 90, 120
        #endif
    }
    
    // Método para permitir que outros scripts possam carregar um novo vídeo
    public void LoadVideo(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath))
        {
            Debug.LogError(""Video path is empty or null!"");
            return;
        }
        
        StopVideo();
        videoPlayer.url = videoPath;
        PlayVideo();
    }
}";
        
        // Escrever o arquivo do script
        string fullPath = Path.Combine(scriptPath, "Video360Controller.cs");
        File.WriteAllText(fullPath, scriptContent);
        AssetDatabase.ImportAsset(fullPath);
    }
}

// Extensão para inverter normais da mesh (útil para esferas de vídeo 360)
public static class MeshExtension
{
    public static void InvertNormals(this Mesh mesh)
    {
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = -normals[i];
        }
        mesh.normals = normals;

        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int temp = triangles[i];
            triangles[i] = triangles[i + 2];
            triangles[i + 2] = temp;
        }
        mesh.triangles = triangles;
    }
} 