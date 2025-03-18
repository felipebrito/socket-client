using UnityEngine;
using UnityEditor;

/// <summary>
/// Classe de utilidades para manter a consistência visual das ferramentas da aparato
/// </summary>
public static class AparatoEditorUtils
{
    private static readonly string headerTexturePath = "Assets/Editor/Textures/aparato-header.png";
    private static readonly string footerTexturePath = "Assets/Editor/Textures/aparato-footer.png";
    private static Texture2D headerTexture;
    private static Texture2D footerTexture;
    
    private static void LoadTextures()
    {
        if (headerTexture == null)
            headerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(headerTexturePath);
            
        if (footerTexture == null)
            footerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(footerTexturePath);
    }
    
    /// <summary>
    /// Desenha o cabeçalho padrão das ferramentas da aparato
    /// </summary>
    public static void DrawHeader()
    {
        LoadTextures();
        
        if (headerTexture != null)
        {
            float aspectRatio = (float)headerTexture.width / headerTexture.height;
            float height = 60;
            Rect rect = GUILayoutUtility.GetRect(height * aspectRatio, height);
            GUI.DrawTexture(rect, headerTexture, ScaleMode.ScaleToFit);
        }
        
        EditorGUILayout.Space(10);
    }
    
    /// <summary>
    /// Desenha o rodapé padrão das ferramentas da aparato
    /// </summary>
    public static void DrawFooter()
    {
        LoadTextures();
        
        EditorGUILayout.Space(10);
        
        if (footerTexture != null)
        {
            float aspectRatio = (float)footerTexture.width / footerTexture.height;
            float height = 40;
            Rect rect = GUILayoutUtility.GetRect(height * aspectRatio, height);
            GUI.DrawTexture(rect, footerTexture, ScaleMode.ScaleToFit);
        }
    }
} 