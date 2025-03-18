using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Video;
using System.Linq;

[CustomEditor(typeof(VideoRotationControl))]
public class VideoRotationControlEditor : Editor
{
    private VideoRotationControl rotationControl;
    private VideoPlayer videoPlayer;
    private int selectedVideoIndex = 0;
    private bool showTestTools = false;
    private string testVideoName = "rio.mp4";
    private Vector2 scrollPosition;
    
    private void OnEnable()
    {
        rotationControl = (VideoRotationControl)target;
        videoPlayer = rotationControl.videoPlayer;
    }
    
    public override void OnInspectorGUI()
    {
        // Desenha o cabeçalho padrão
        AparatoEditorUtils.DrawHeader();
        
        EditorGUILayout.Space(5);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 14;
        EditorGUILayout.LabelField("Configurador de Bloqueios de Vídeo 360°", titleStyle);
        
        // Desenha as propriedades padrão
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "videoBlocks");
        
        EditorGUILayout.Space(10);
        
        // Seção de configuração de vídeos
        EditorGUILayout.LabelField("Configuração de Vídeos", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // Lista de blocos de vídeo
        SerializedProperty videoBlocksProperty = serializedObject.FindProperty("videoBlocks");
        EditorGUI.indentLevel++;
        
        for (int i = 0; i < videoBlocksProperty.arraySize; i++)
        {
            SerializedProperty videoBlockProperty = videoBlocksProperty.GetArrayElementAtIndex(i);
            SerializedProperty videoTitleProperty = videoBlockProperty.FindPropertyRelative("videoTitle");
            SerializedProperty blockTimesProperty = videoBlockProperty.FindPropertyRelative("blockTimes");
            SerializedProperty angleProperty = videoBlockProperty.FindPropertyRelative("angle");
            
            EditorGUILayout.BeginVertical("box");
            
            // Título do vídeo e botão de remoção
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(videoTitleProperty, new GUIContent("Vídeo"));
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                videoBlocksProperty.DeleteArrayElementAtIndex(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
            
            // Ângulo máximo
            EditorGUILayout.PropertyField(angleProperty, new GUIContent("Ângulo Máximo"));
            
            // Lista de blocos de tempo
            EditorGUILayout.LabelField("Blocos de Tempo", EditorStyles.boldLabel);
            
            for (int j = 0; j < blockTimesProperty.arraySize; j++)
            {
                SerializedProperty blockTimeProperty = blockTimesProperty.GetArrayElementAtIndex(j);
                SerializedProperty startTimeProperty = blockTimeProperty.FindPropertyRelative("startTime");
                SerializedProperty endTimeProperty = blockTimeProperty.FindPropertyRelative("endTime");
                
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.LabelField($"Bloco {j + 1}", GUILayout.Width(50));
                
                // Campos de tempo formatados como mm:ss
                string startFormatted = FormatTime((float)startTimeProperty.doubleValue);
                string endFormatted = FormatTime((float)endTimeProperty.doubleValue);
                
                EditorGUI.BeginChangeCheck();
                startFormatted = EditorGUILayout.TextField(startFormatted, GUILayout.Width(50));
                EditorGUILayout.LabelField("até", GUILayout.Width(30));
                endFormatted = EditorGUILayout.TextField(endFormatted, GUILayout.Width(50));
                
                if (EditorGUI.EndChangeCheck())
                {
                    startTimeProperty.doubleValue = ParseTimeFormat(startFormatted);
                    endTimeProperty.doubleValue = ParseTimeFormat(endFormatted);
                }
                
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    blockTimesProperty.DeleteArrayElementAtIndex(j);
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            // Botão para adicionar novo bloco de tempo
            if (GUILayout.Button("Adicionar Bloco de Tempo"))
            {
                blockTimesProperty.InsertArrayElementAtIndex(blockTimesProperty.arraySize);
                SerializedProperty newBlockProperty = blockTimesProperty.GetArrayElementAtIndex(blockTimesProperty.arraySize - 1);
                newBlockProperty.FindPropertyRelative("startTime").doubleValue = 0;
                newBlockProperty.FindPropertyRelative("endTime").doubleValue = 5;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUI.indentLevel--;
        
        // Botão para adicionar novo vídeo
        if (GUILayout.Button("Adicionar Novo Vídeo"))
        {
            videoBlocksProperty.InsertArrayElementAtIndex(videoBlocksProperty.arraySize);
            SerializedProperty newVideoBlock = videoBlocksProperty.GetArrayElementAtIndex(videoBlocksProperty.arraySize - 1);
            newVideoBlock.FindPropertyRelative("videoTitle").stringValue = "";
            newVideoBlock.FindPropertyRelative("angle").floatValue = 75f;
            SerializedProperty blockTimes = newVideoBlock.FindPropertyRelative("blockTimes");
            blockTimes.ClearArray();
        }
        
        EditorGUILayout.EndScrollView();
        
        // Ferramentas de teste
        EditorGUILayout.Space(10);
        showTestTools = EditorGUILayout.Foldout(showTestTools, "Ferramentas de Teste", true);
        
        if (showTestTools)
        {
            EditorGUILayout.BeginVertical("box");
            
            testVideoName = EditorGUILayout.TextField("Nome do Vídeo", testVideoName);
            
            if (GUILayout.Button("Testar Vídeo"))
            {
                if (videoPlayer != null)
                {
                    string videoPath = System.IO.Path.Combine(Application.streamingAssetsPath, testVideoName);
                    videoPlayer.url = videoPath;
                    videoPlayer.Play();
                    rotationControl.SetRotationControlEnabled(true);
                }
            }
            
            if (GUILayout.Button("Parar Teste"))
            {
                if (videoPlayer != null)
                {
                    videoPlayer.Stop();
                    rotationControl.SetRotationControlEnabled(false);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // Desenha o rodapé padrão
        AparatoEditorUtils.DrawFooter();
        
        serializedObject.ApplyModifiedProperties();
        
        // Se houver mudanças, força a atualização dos lookups
        if (GUI.changed)
        {
            rotationControl.SendMessage("PrepareBlockLookups");
        }
    }
    
    // Converte segundos para formato mm:ss
    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60);
        int remainingSeconds = Mathf.FloorToInt(seconds % 60);
        return string.Format("{0:00}:{1:00}", minutes, remainingSeconds);
    }
    
    // Converte formato mm:ss para segundos
    private float ParseTimeFormat(string timeFormat)
    {
        string[] parts = timeFormat.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
        {
            return minutes * 60 + seconds;
        }
        return 0;
    }
} 