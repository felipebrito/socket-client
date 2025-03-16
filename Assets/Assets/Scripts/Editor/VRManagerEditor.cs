using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor personalizado para o VRManager.
/// Facilita a configuração dos pontos de bloqueio.
/// </summary>
[CustomEditor(typeof(VRManager))]
public class VRManagerEditor : Editor
{
    // Referência ao VRManager
    private VRManager vrManager;
    // Seção expandida atual
    private string expandedSection = "";
    // Nome do vídeo atual para teste
    private string testVideoName = "rio.mp4";
    // Flag para mostrar ferramentas de teste
    private bool showTestTools = false;
    // Vista atual para teste
    private Vector2 testViewAngles = new Vector2(0, 0);
    
    private bool showVideoSettings = true;
    private bool showStorageSettings = true;
    private bool showFadeSettings = true;
    private bool showViewSettings = true;
    private bool showNetworkSettings = true;
    private bool showUISettings = true;
    private bool showConnectionSettings = true;
    private bool showDebugSettings = true;
    private bool showEditorSettings = true;
    private bool showLockSettings = true;
    
    // Inicialização
    private void OnEnable()
    {
        vrManager = (VRManager)target;
    }
    
    // Interface personalizada no Inspector
    public override void OnInspectorGUI()
    {
        // Serializar o objeto para podermos modificá-lo
        serializedObject.Update();
        
        // Desenhar os campos padrão do VRManager
        DrawDefaultInspector();
        
        // Adicionar seção personalizada para configuração de bloqueios
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Ferramentas de Configuração de Bloqueio", EditorStyles.boldLabel);
        
        // Botão para adicionar novo vídeo
        if (GUILayout.Button("Adicionar Novo Vídeo"))
        {
            AddNewVideo();
        }
        
        // Mostrar todos os vídeos configurados com botões de edição
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Vídeos Configurados:");
        
        for (int i = 0; i < vrManager.videoLockSettings.Count; i++)
        {
            var videoSetting = vrManager.videoLockSettings[i];
            
            // Verificar se esta seção está expandida
            bool isExpanded = (expandedSection == videoSetting.videoFileName);
            
            // Cabecalho com nome do vídeo e botões
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            if (GUILayout.Button(isExpanded ? "▼" : "►", GUILayout.Width(30)))
            {
                expandedSection = isExpanded ? "" : videoSetting.videoFileName;
            }
            
            EditorGUILayout.LabelField(videoSetting.videoFileName, EditorStyles.boldLabel);
            
            // Botão para remover vídeo
            if (GUILayout.Button("Remover", GUILayout.Width(80)))
            {
                if (EditorUtility.DisplayDialog("Confirmar Remoção", 
                    $"Tem certeza que deseja remover o vídeo '{videoSetting.videoFileName}'?", 
                    "Sim", "Cancelar"))
                {
                    vrManager.videoLockSettings.RemoveAt(i);
                    EditorUtility.SetDirty(vrManager);
                    break;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Se esta seção está expandida, mostrar todos os intervalos de bloqueio
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                
                // Mostrar e editar cada intervalo de bloqueio
                for (int j = 0; j < videoSetting.lockRanges.Count; j++)
                {
                    var lockRange = videoSetting.lockRanges[j];
                    
                    SerializedProperty rangeProperty = serializedObject.FindProperty($"videoLockSettings.{i}.lockRanges.{j}");
                    SerializedProperty startTimeProperty = rangeProperty.FindPropertyRelative("startTime");
                    SerializedProperty endTimeProperty = rangeProperty.FindPropertyRelative("endTime");
                    SerializedProperty maxAngleProperty = rangeProperty.FindPropertyRelative("maxAngle");
                    SerializedProperty resetSpeedProperty = rangeProperty.FindPropertyRelative("resetSpeed");

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Range {j + 1}", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(startTimeProperty, new GUIContent("Start Time (s)"));
                    EditorGUILayout.PropertyField(endTimeProperty, new GUIContent("End Time (s)"));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(maxAngleProperty, new GUIContent("Max Angle (°)"));
                    EditorGUILayout.PropertyField(resetSpeedProperty, new GUIContent("Reset Speed"));
                    EditorGUILayout.EndHorizontal();

                    // Ângulos alvo
                    EditorGUILayout.LabelField("Direção do Bloqueio (Coordenadas Esféricas):");
                    EditorGUI.indentLevel++;
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Yaw (Horizontal):", GUILayout.Width(120));
                    rangeProperty.FindPropertyRelative("targetYawAngle").floatValue = EditorGUILayout.Slider(rangeProperty.FindPropertyRelative("targetYawAngle").floatValue, 0f, 360f);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Pitch (Vertical):", GUILayout.Width(120));
                    rangeProperty.FindPropertyRelative("targetPitchAngle").floatValue = EditorGUILayout.Slider(rangeProperty.FindPropertyRelative("targetPitchAngle").floatValue, -90f, 90f);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUI.indentLevel--;
                    
                    // Botão para remover este intervalo
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Remover Intervalo", GUILayout.Width(150)))
                    {
                        videoSetting.lockRanges.RemoveAt(j);
                        EditorUtility.SetDirty(vrManager);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
                
                // Botão para adicionar novo intervalo
                if (GUILayout.Button("Add Range"))
                {
                    AddNewLockRange(videoSetting);
                }
                
                EditorGUI.indentLevel--;
            }
        }
        
        // Ferramentas de teste
        EditorGUILayout.Space(15);
        showTestTools = EditorGUILayout.Foldout(showTestTools, "Ferramentas de Teste", true);
        
        if (showTestTools)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            EditorGUILayout.LabelField("Testar Visualização em Ângulos Específicos:");
            
            // Campo para selecionar vídeo
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Vídeo:", GUILayout.Width(100));
            testVideoName = EditorGUILayout.TextField(testVideoName);
            EditorGUILayout.EndHorizontal();
            
            // Campos para ângulos
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Yaw (0-360):", GUILayout.Width(100));
            testViewAngles.x = EditorGUILayout.Slider(testViewAngles.x, 0f, 360f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pitch (-90 a 90):", GUILayout.Width(100));
            testViewAngles.y = EditorGUILayout.Slider(testViewAngles.y, -90f, 90f);
            EditorGUILayout.EndHorizontal();
            
            // Botão para testar ângulos atuais
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Testar Visualização", GUILayout.Width(150)))
            {
                TestViewAtAngles();
            }
            EditorGUILayout.EndHorizontal();
            
            // Ajuda sobre como usar
            EditorGUILayout.HelpBox("Para usar corretamente:\n" +
                "1. Entre no modo Play\n" +
                "2. Configure os ângulos desejados\n" +
                "3. Clique em 'Testar Visualização'\n" +
                "4. Observe a esfera do vídeo rotacionar para mostrar a direção alvo", MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        // Aplicar alterações
        serializedObject.ApplyModifiedProperties();
        
        // Se houve alguma alteração, marca o objeto como sujo para salvar
        if (GUI.changed)
        {
            EditorUtility.SetDirty(vrManager);
        }
    }
    
    // Adicionar um novo vídeo à lista
    private void AddNewVideo()
    {
        // Mostrar diálogo para inserir nome do arquivo
        string videoFileName = EditorInputDialog.Show("Adicionar Novo Vídeo", "Nome do arquivo de vídeo:", "video.mp4");
        
        if (!string.IsNullOrEmpty(videoFileName))
        {
            // Verificar se já existe
            foreach (var setting in vrManager.videoLockSettings)
            {
                if (setting.videoFileName == videoFileName)
                {
                    EditorUtility.DisplayDialog("Erro", $"Já existe um vídeo com o nome '{videoFileName}'", "OK");
                    return;
                }
            }
            
            // Criar nova configuração
            var newSetting = new VRManager.VideoLockSettings
            {
                videoFileName = videoFileName,
                lockRanges = new List<VRManager.LockTimeRange>()
            };
            
            // Adicionar à lista
            vrManager.videoLockSettings.Add(newSetting);
            
            // Expandir esta seção
            expandedSection = videoFileName;
            
            // Marcar como sujo para salvar
            EditorUtility.SetDirty(vrManager);
        }
    }
    
    // Testar visualização em ângulos específicos (apenas no modo Play)
    private void TestViewAtAngles()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Erro", "Esta função só está disponível no modo Play.", "OK");
            return;
        }
        
        // Encontrar ViewSetup
        ViewSetup viewSetup = Object.FindObjectOfType<ViewSetup>();
        if (viewSetup != null)
        {
            viewSetup.TestLookAtAngle(testViewAngles.x, testViewAngles.y);
        }
        else
        {
            EditorUtility.DisplayDialog("Erro", "Não foi encontrado um componente ViewSetup na cena.", "OK");
        }
    }

    private void AddNewLockRange(VRManager.VideoLockSettings videoSetting)
    {
        var newRange = new VRManager.LockTimeRange(0, 10)
        {
            maxAngle = 75f,
            resetSpeed = 2f
        };
        videoSetting.lockRanges.Add(newRange);
        EditorUtility.SetDirty(vrManager);
    }
}

// Classe auxiliar para diálogos de entrada
public class EditorInputDialog : EditorWindow
{
    public static string Show(string title, string message, string defaultText = "")
    {
        EditorInputDialog window = CreateInstance<EditorInputDialog>();
        window.titleContent = new GUIContent(title);
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
        window.message = message;
        window.inputText = defaultText;
        window.ShowModalUtility();
        return window.result;
    }
    
    private string message = "";
    private string inputText = "";
    private string result = "";
    
    private void OnGUI()
    {
        GUILayout.Label(message);
        inputText = EditorGUILayout.TextField(inputText);
        
        GUILayout.FlexibleSpace();
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Cancelar"))
        {
            result = "";
            Close();
        }
        if (GUILayout.Button("OK"))
        {
            result = inputText;
            Close();
        }
        GUILayout.EndHorizontal();
    }
} 