using UnityEngine;
using UnityEditor;
using System.IO;

public class RemoveViewSetupMenu : EditorWindow
{
    private string viewSetupPath = "Assets/Scripts/ViewSetup.cs";
    private bool fileExists = false;
    
    [MenuItem("▸aparato◂/Remover ViewSetup")]
    public static void ShowWindow()
    {
        GetWindow<RemoveViewSetupMenu>("Remover ViewSetup");
    }
    
    private void OnEnable()
    {
        fileExists = File.Exists(viewSetupPath);
    }
    
    private void OnGUI()
    {
        // Desenha o cabeçalho padrão
        AparatoEditorUtils.DrawHeader();
        
        EditorGUILayout.LabelField("Remover Script ViewSetup Obsoleto", EditorStyles.boldLabel);
        
        EditorGUILayout.Space(10);
        
        if (fileExists)
        {
            EditorGUILayout.HelpBox(
                "O arquivo ViewSetup.cs foi encontrado e pode ser removido.\n\n" +
                "Este arquivo é obsoleto e foi substituído pelas classes CameraRotationLimiter e VideoRotationControl.\n\n" +
                "Use a ferramenta de migração para garantir que todos os objetos que usam ViewSetup sejam atualizados antes de remover o script.",
                MessageType.Warning);
                
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Abrir Ferramenta de Migração", GUILayout.Height(30)))
            {
                ViewSetupMigration.ShowWindow();
            }
            
            EditorGUILayout.Space(15);
            
            EditorGUILayout.HelpBox(
                "ATENÇÃO: Remover o arquivo sem migrar os objetos pode causar erros no projeto.",
                MessageType.Error);
                
            if (GUILayout.Button("Remover ViewSetup.cs", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Confirmar Exclusão", 
                    "Tem certeza que deseja remover permanentemente o arquivo ViewSetup.cs?\n\nEsta ação não pode ser desfeita.",
                    "Sim, Remover", "Cancelar"))
                {
                    RemoveViewSetupFile();
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "O arquivo ViewSetup.cs não foi encontrado no projeto.\n\n" +
                "O projeto já está atualizado para usar o novo sistema de rotação de câmera.",
                MessageType.Info);
                
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Abrir Ferramenta de Migração", GUILayout.Height(30)))
            {
                ViewSetupMigration.ShowWindow();
            }
        }
        
        // Desenha o rodapé padrão
        AparatoEditorUtils.DrawFooter();
    }
    
    private void RemoveViewSetupFile()
    {
        if (File.Exists(viewSetupPath))
        {
            // Remove o arquivo .cs
            AssetDatabase.DeleteAsset(viewSetupPath);
            
            // Remove também o arquivo .meta associado
            string metaPath = viewSetupPath + ".meta";
            if (File.Exists(metaPath))
            {
                AssetDatabase.DeleteAsset(metaPath);
            }
            
            AssetDatabase.Refresh();
            fileExists = false;
            Debug.Log("ViewSetup.cs removido com sucesso");
            
            EditorUtility.DisplayDialog(
                "Arquivo Removido", 
                "O arquivo ViewSetup.cs foi removido com sucesso.\n\n" +
                "Certifique-se de que todas as cenas foram atualizadas para usar o novo sistema.",
                "OK");
        }
    }
} 