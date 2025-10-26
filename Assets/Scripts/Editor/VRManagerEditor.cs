using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor personalizado para o VRManager.
/// Interface simplificada para configuração básica.
/// </summary>
[CustomEditor(typeof(VRManager))]
public class VRManagerEditor : Editor
{
    // Referência ao VRManager
    private VRManager vrManager;
    
    // Flags para seções expandidas
    private bool showVideoSettings = true;
    private bool showStorageSettings = true;
    private bool showFadeSettings = true;
    private bool showNetworkSettings = true;
    private bool showUISettings = true;
    private bool showConnectionSettings = true;
    private bool showDebugSettings = true;
    
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
        
        // Adicionar seção personalizada para informações do sistema
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Informações do Sistema VR", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        // Status da conexão WebSocket
        EditorGUILayout.LabelField("Status da Conexão:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Servidor: {vrManager.serverUri}");
        EditorGUILayout.LabelField($"Modo Offline: {(vrManager.offlineMode ? "Sim" : "Não")}");
        
        EditorGUILayout.Space(5);
        
        // Informações do vídeo atual
        EditorGUILayout.LabelField("Vídeo Atual:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Arquivo: {vrManager.videoFiles[0]}");
        EditorGUILayout.LabelField($"Tocando: {(vrManager.isPlaying ? "Sim" : "Não")}");
        
        EditorGUILayout.Space(5);
        
        // Botões de controle (apenas no modo Play)
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Controles de Teste:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Testar Conexão"))
            {
                vrManager.TestConnection();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play Vídeo"))
            {
                vrManager.PlayVideo("Pierre_Final.mp4");
            }
            if (GUILayout.Button("Pausar"))
            {
                vrManager.PauseVideo();
            }
            if (GUILayout.Button("Parar"))
            {
                vrManager.StopVideo();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Testar Play"))
            {
                vrManager.TestPlayVideo("Pierre_Final.mp4");
            }
            if (GUILayout.Button("Forçar Play"))
            {
                vrManager.ForcePlayVideo("Pierre_Final.mp4");
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("Controles de teste disponíveis apenas no modo Play", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical();
        
        // Aplicar alterações
        serializedObject.ApplyModifiedProperties();
        
        // Se houve alguma alteração, marca o objeto como sujo para salvar
        if (GUI.changed)
        {
            EditorUtility.SetDirty(vrManager);
        }
    }
} 