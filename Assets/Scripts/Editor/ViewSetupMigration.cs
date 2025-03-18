using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;

public class ViewSetupMigration : EditorWindow
{
    private Vector2 scrollPosition;
    private List<GameObject> viewSetupObjects = new List<GameObject>();
    private bool analyzed = false;
    
    [MenuItem("▸aparato◂/Migrar de ViewSetup")]
    public static void ShowWindow()
    {
        GetWindow<ViewSetupMigration>("Migração de ViewSetup");
    }
    
    private void OnEnable()
    {
        analyzed = false;
        viewSetupObjects.Clear();
    }
    
    private void OnGUI()
    {
        // Desenha o cabeçalho padrão
        AparatoEditorUtils.DrawHeader();
        
        EditorGUILayout.LabelField("Migração de ViewSetup para Novo Sistema", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Esta ferramenta migra objetos que usam o componente ViewSetup para o novo sistema de limitação de rotação.\n\n" +
            "O sistema ViewSetup é obsoleto e deve ser substituído pelo CameraRotationLimiter e VideoRotationControl.", 
            MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("Analisar Cena", GUILayout.Height(30)))
        {
            AnalyzeScene();
        }
        
        EditorGUILayout.Space(10);
        
        if (analyzed)
        {
            EditorGUILayout.LabelField("Resultados da Análise:", EditorStyles.boldLabel);
            
            if (viewSetupObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("Não foram encontrados objetos com o componente ViewSetup na cena atual.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Encontrados {viewSetupObjects.Count} objetos com ViewSetup.", MessageType.Warning);
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                
                foreach (var obj in viewSetupObjects)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                    
                    if (GUILayout.Button("Selecionar", GUILayout.Width(100)))
                    {
                        Selection.activeGameObject = obj;
                    }
                    
                    if (GUILayout.Button("Migrar", GUILayout.Width(100)))
                    {
                        MigrateObject(obj);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.Space(10);
                
                if (GUILayout.Button("Migrar Todos", GUILayout.Height(30)))
                {
                    MigrateAllObjects();
                }
            }
        }
        
        // Desenha o rodapé padrão
        AparatoEditorUtils.DrawFooter();
    }
    
    private void AnalyzeScene()
    {
        viewSetupObjects.Clear();
        
        // Encontra todos os objetos na cena atual com o componente ViewSetup
        var viewSetups = FindObjectsOfType<MonoBehaviour>().Where(mb => mb.GetType().Name == "ViewSetup");
        
        foreach (var setup in viewSetups)
        {
            viewSetupObjects.Add(setup.gameObject);
        }
        
        analyzed = true;
        Repaint();
    }
    
    private void MigrateObject(GameObject obj)
    {
        Undo.RegisterCompleteObjectUndo(obj, "Migrate from ViewSetup");
        
        // Adiciona os novos componentes
        bool needsCameraLimiter = obj.GetComponentInChildren<CameraRotationLimiter>() == null;
        bool needsVideoControl = obj.GetComponentInChildren<VideoRotationControl>() == null;
        
        if (needsCameraLimiter)
        {
            // Tenta encontrar câmera associada
            Camera camera = obj.GetComponentInChildren<Camera>();
            GameObject cameraObj = camera != null ? camera.gameObject : obj;
            
            CameraRotationLimiter limiter = Undo.AddComponent<CameraRotationLimiter>(cameraObj);
            limiter.resetSpeed = 2f;
            limiter.angle = 75f;
            
            // Tenta encontrar VideoPlayer
            UnityEngine.Video.VideoPlayer videoPlayer = obj.GetComponentInChildren<UnityEngine.Video.VideoPlayer>();
            if (videoPlayer != null)
            {
                limiter.videoPlayer = videoPlayer;
            }
            
            // Tenta encontrar esfera do vídeo
            MonoBehaviour viewSetup = obj.GetComponent<MonoBehaviour>();
            if (viewSetup != null)
            {
                System.Type viewSetupType = viewSetup.GetType();
                var videoSphereField = viewSetupType.GetField("videoPlayerObject");
                if (videoSphereField != null)
                {
                    limiter.sphereTransform = videoSphereField.GetValue(viewSetup) as Transform;
                }
            }
            
            Debug.Log($"Adicionado CameraRotationLimiter ao objeto {cameraObj.name}");
        }
        
        if (needsVideoControl)
        {
            VideoRotationControl control = Undo.AddComponent<VideoRotationControl>(obj);
            
            // Tenta encontrar CameraRotationLimiter
            CameraRotationLimiter limiter = obj.GetComponentInChildren<CameraRotationLimiter>();
            if (limiter != null)
            {
                control.cameraLimiter = limiter;
                control.videoPlayer = limiter.videoPlayer;
            }
            
            Debug.Log($"Adicionado VideoRotationControl ao objeto {obj.name}");
        }
        
        // Remove o ViewSetup
        MonoBehaviour viewSetupComponent = obj.GetComponent<MonoBehaviour>();
        if (viewSetupComponent != null && viewSetupComponent.GetType().Name == "ViewSetup")
        {
            Undo.DestroyObjectImmediate(viewSetupComponent);
            Debug.Log($"Removido ViewSetup do objeto {obj.name}");
        }
        
        // Atualiza a lista
        viewSetupObjects.Remove(obj);
        if (viewSetupObjects.Count == 0)
        {
            analyzed = false;
        }
        
        Repaint();
    }
    
    private void MigrateAllObjects()
    {
        List<GameObject> objectsToMigrate = new List<GameObject>(viewSetupObjects);
        
        foreach (var obj in objectsToMigrate)
        {
            MigrateObject(obj);
        }
        
        analyzed = false;
        EditorUtility.DisplayDialog("Migração Concluída", 
            "Todos os objetos foram migrados com sucesso do ViewSetup para o novo sistema.", "OK");
        
        Repaint();
    }
} 