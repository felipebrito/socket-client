using UnityEngine;
using UnityEditor;

public static class AparatoMenuHelper
{
    private static Texture2D logoTexture;

    // Exibe informações sobre o Build do Windows
    [MenuItem("▸aparato◂/Build/Sobre builds")]
    public static void ShowBuildInfo()
    {
        EditorUtility.DisplayDialog("Ferramentas de Build", 
            "Ferramentas de Build v1.0\n\n" +
            "Esta ferramenta permite:\n\n" +
            "- Criar builds para Windows sem mudar a plataforma\n" +
            "- Configurar facilmente as builds para diferentes plataformas\n\n" +
            "Para mais informações, consulte a documentação.", 
            "OK");
    }
    
    // Menu para configurações do GitHub
    [MenuItem("▸aparato◂/Github/Configuração")]
    public static void ShowGitHubConfig()
    {
        EditorWindow.GetWindow<GitHubManager>("GitHub Config");
    }
    
    // Sobre as ferramentas
    [MenuItem("▸aparato◂/Sobre")]
    public static void ShowAboutWindow()
    {
        AboutWindow.ShowWindow();
    }

    // Helper para exibir o logo em qualquer janela
    public static void DrawLogo(Rect position)
    {
        if (logoTexture == null)
        {
            // Tenta carregar o logo de Resources
            logoTexture = Resources.Load<Texture2D>("Logo/aparatoLogo");
            
            // Se não encontrou, usa um fallback
            if (logoTexture == null)
            {
                Debug.LogWarning("Logo não encontrado em Resources/Logo/aparatoLogo. " +
                                 "Crie a pasta Resources/Logo e adicione sua imagem com nome aparatoLogo.");
            }
        }

        if (logoTexture != null)
        {
            GUI.DrawTexture(position, logoTexture, ScaleMode.ScaleToFit);
        }
    }

    // Janela "Sobre"
    public class AboutWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<AboutWindow>("Sobre ▸aparato◂");
            window.minSize = new Vector2(400, 300);
        }

        void OnGUI()
        {
            // Exibe logo no topo
            Rect logoRect = new Rect(10, 10, position.width - 20, 100);
            DrawLogo(logoRect);

            GUILayout.Space(110);
            GUILayout.Label("▸aparato◂ Tools Suite v1.0", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Estas ferramentas foram desenvolvidas para facilitar:\n" +
                "- Configuração de projetos para Meta Quest 3\n" +
                "- Gerenciamento de controle de versão com Git e GitHub\n" +
                "- Criação de players de vídeo 360° otimizados\n" +
                "- Build de aplicativos para diferentes plataformas", 
                MessageType.Info);

            GUILayout.FlexibleSpace();
            GUILayout.Label("▸codex || aparato®", EditorStyles.centeredGreyMiniLabel);
        }
    }
} 