using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.Build;
using System.Xml;
using System;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
using Unity.XR.Oculus;
using UnityEditor.Android;

public class MetaQuestSetupTool : EditorWindow
{
    // Configurações gerais
    private bool setupXRSettings = true;
    private bool setupQualitySettings = true;
    private bool setupPlayerSettings = true;
    private bool setupPermissions = true;
    private bool setupManifest = true;
    
    // Configurações específicas de permissão
    private bool enableExternalStorageRead = true;
    private bool enableExternalStorageWrite = true;
    private bool enableInternetAccess = true;
    
    // Configurações de vídeo
    private bool optimizeFor360Video = true;
    private int targetFPS = 72;
    private bool useDynamicResolution = true;
    
    [MenuItem("▸aparato◂/Meta Quest 3/Configurar Meta Quest 3")]
    public static void ShowWindow()
    {
        GetWindow<MetaQuestSetupTool>("Meta Quest 3 Setup");
    }
    
    private void OnGUI()
    {
        // Exibe o logo no início da janela
        Rect logoRect = new Rect(10, 10, position.width - 20, 60);
        AparatoMenuHelper.DrawLogo(logoRect);
        
        GUILayout.Space(70); // Espaço para o logo
        
        GUILayout.Label("Meta Quest 3 Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Configure seu projeto para Meta Quest 3, com foco em vídeos 360°", MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        GUILayout.Label("Configurações a aplicar:", EditorStyles.boldLabel);
        setupXRSettings = EditorGUILayout.Toggle("Configurar XR Settings", setupXRSettings);
        setupQualitySettings = EditorGUILayout.Toggle("Configurar Quality Settings", setupQualitySettings);
        setupPlayerSettings = EditorGUILayout.Toggle("Configurar Player Settings", setupPlayerSettings);
        setupPermissions = EditorGUILayout.Toggle("Configurar Permissões", setupPermissions);
        setupManifest = EditorGUILayout.Toggle("Configurar Manifesto Android", setupManifest);
        
        EditorGUILayout.Space(10);
        
        GUILayout.Label("Permissões:", EditorStyles.boldLabel);
        enableExternalStorageRead = EditorGUILayout.Toggle("Leitura de Armazenamento Externo", enableExternalStorageRead);
        enableExternalStorageWrite = EditorGUILayout.Toggle("Escrita em Armazenamento Externo", enableExternalStorageWrite);
        enableInternetAccess = EditorGUILayout.Toggle("Acesso à Internet", enableInternetAccess);
        
        EditorGUILayout.Space(10);
        
        GUILayout.Label("Configurações de Vídeo 360°:", EditorStyles.boldLabel);
        optimizeFor360Video = EditorGUILayout.Toggle("Otimizar para Vídeo 360°", optimizeFor360Video);
        targetFPS = EditorGUILayout.IntSlider("Target FPS", targetFPS, 60, 90);
        useDynamicResolution = EditorGUILayout.Toggle("Usar Resolução Dinâmica", useDynamicResolution);
        
        EditorGUILayout.Space(20);
        
        if (GUILayout.Button("Aplicar Configurações", GUILayout.Height(40)))
        {
            ApplySettings();
        }

        // Add footer at the end of OnGUI method
        EditorGUILayout.Space(10);
        GUILayout.FlexibleSpace();
        GUILayout.Label("▸codex || aparato®", EditorStyles.centeredGreyMiniLabel);
    }
    
    private void ApplySettings()
    {
        if (setupXRSettings) ConfigureXRSettings();
        if (setupQualitySettings) ConfigureQualitySettings();
        if (setupPlayerSettings) ConfigurePlayerSettings();
        if (setupPermissions) ConfigurePermissions();
        if (setupManifest) ConfigureAndroidManifest();
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Verificar e exibir resumo das configurações
        VerifyConfiguration();
        
        Debug.Log("Configurações para Meta Quest 3 aplicadas com sucesso!");
        EditorUtility.DisplayDialog("Configuração Concluída", 
            "Todas as configurações para Meta Quest 3 foram aplicadas com sucesso!\n\n" +
            "Verifique o console para mais detalhes.", "OK");
    }
    
    private void ConfigureXRSettings()
    {
        Debug.Log("Configurando XR Settings para Meta Quest...");
        
        // Verificar se o pacote XR Management está instalado
        bool hasXRManagement = false;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name.Contains("Unity.XR.Management"))
            {
                hasXRManagement = true;
                break;
            }
        }
        
        if (!hasXRManagement)
        {
            Debug.LogWarning("XR Management não encontrado. Adicione o pacote 'XR Plugin Management' no Package Manager.");
            EditorUtility.DisplayDialog("Pacote XR Management Necessário", 
                "Você precisa instalar o pacote 'XR Plugin Management' no Package Manager.\n\n" +
                "Window > Package Manager > + > Add package by name > com.unity.xr.management", "OK");
            return;
        }
        
        // Verificar se o plugin Oculus está instalado
        bool hasOculusPlugin = false;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name.Contains("Unity.XR.Oculus"))
            {
                hasOculusPlugin = true;
                break;
            }
        }
        
        if (!hasOculusPlugin)
        {
            Debug.LogWarning("XR Plugin Oculus não encontrado. Adicione o pacote 'Oculus XR Plugin' no Package Manager.");
            EditorUtility.DisplayDialog("Pacote Oculus XR Plugin Necessário", 
                "Você precisa instalar o pacote 'Oculus XR Plugin' no Package Manager.\n\n" +
                "Window > Package Manager > + > Add package by name > com.unity.xr.oculus", "OK");
            return;
        }
        
        try
        {
            // Método atualizado para configurar XR Plugin Management utilizando a API correta
            // Obter as configurações XR existentes
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            
            // Se não houver configurações, crie-as
            if (generalSettings == null)
            {
                // Criar e configurar configurações globais XR para Android
                var settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                var settingsPath = "Assets/XR/Settings";
                
                if (!Directory.Exists(settingsPath))
                {
                    Directory.CreateDirectory(settingsPath);
                }
                
                AssetDatabase.CreateAsset(settings, $"{settingsPath}/Android XR Settings.asset");
                
                // Atualizar configurações gerais por plataforma
                var buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(buildTargetSettings, $"{settingsPath}/XRGeneralSettingsPerBuildTarget.asset");
                
                // Adicionar às configurações de build
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
                
                // Definir configurações para Android
                buildTargetSettings.SetSettingsForBuildTarget(BuildTargetGroup.Android, settings);
                generalSettings = settings;
            }
            
            // Verificar e configurar o gerenciador de XR
            var manager = generalSettings.Manager;
            if (manager == null)
            {
                var managersettings = ScriptableObject.CreateInstance<XRManagerSettings>();
                AssetDatabase.CreateAsset(managersettings, "Assets/XR/Settings/Oculus XR Manager Settings.asset");
                generalSettings.Manager = managersettings;
                manager = managersettings;
            }
            
            // Definir o plugin do Oculus como o provedor de XR ativo
            SetOculusXRPlugin(manager);
            
            EditorUtility.SetDirty(generalSettings);
            Debug.Log("XR Settings configurados com sucesso!");
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao configurar XR Settings: " + e.Message);
        }
    }
    
    private void SetOculusXRPlugin(XRManagerSettings manager)
    {
        // Verificar se já existe o loader do Oculus
        bool hasOculusLoader = false;
        var loaders = manager.activeLoaders;
        
        foreach (var loader in loaders)
        {
            if (loader != null && loader.GetType().Name.Contains("OculusLoader"))
            {
                hasOculusLoader = true;
                break;
            }
        }
        
        if (!hasOculusLoader)
        {
            // Adicionar o loader do Oculus
            // Abordagem alternativa para obter os loaders disponíveis
            // que funciona em diferentes versões do Unity
            Type oculusLoaderType = null;
            
            // Tentativa direta de obter o tipo
            oculusLoaderType = Type.GetType("Unity.XR.Oculus.OculusLoader, Unity.XR.Oculus");
            
            // Se não encontrou, tente buscar em todos os assemblies carregados
            if (oculusLoaderType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == "OculusLoader" && type.Namespace != null && type.Namespace.Contains("Unity.XR.Oculus"))
                        {
                            oculusLoaderType = type;
                            break;
                        }
                    }
                    if (oculusLoaderType != null) break;
                }
            }
            
            if (oculusLoaderType != null)
            {
                // Criar instância do loader e adicionar ao manager
                var loaderInstance = ScriptableObject.CreateInstance(oculusLoaderType) as XRLoader;
                if (loaderInstance != null)
                {
                    AssetDatabase.CreateAsset(loaderInstance, "Assets/XR/Settings/OculusLoader.asset");
                    
                    // Usar o método correto para atribuir loaders
                    SerializedObject serializedManager = new SerializedObject(manager);
                    SerializedProperty loadersProp = serializedManager.FindProperty("m_Loaders");
                    
                    loadersProp.ClearArray();
                    loadersProp.arraySize = 1;
                    SerializedProperty loaderProp = loadersProp.GetArrayElementAtIndex(0);
                    loaderProp.objectReferenceValue = loaderInstance;
                    
                    serializedManager.ApplyModifiedProperties();
                    
                    EditorUtility.SetDirty(manager);
                    Debug.Log("Oculus XR Loader adicionado com sucesso!");
                }
            }
            else
            {
                Debug.LogWarning("Não foi possível encontrar o tipo do OculusLoader. Verifique se o pacote está instalado corretamente.");
            }
        }
    }
    
    private void ConfigureQualitySettings()
    {
        Debug.Log("Configurando Quality Settings para Meta Quest...");
        
        try
        {
            // Configurações específicas para Quest
            QualitySettings.SetQualityLevel(0, true); // Usar qualidade mais baixa como base
            QualitySettings.vSyncCount = 0; // Desabilitar VSync (usar o VSync do Quest)
            QualitySettings.maxQueuedFrames = 2; // Bom para VR
            
            // Otimizar para vídeo 360
            if (optimizeFor360Video)
            {
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable; // Melhor para texturas em diferentes ângulos
                QualitySettings.antiAliasing = 4; // 4x MSAA
            }
            else
            {
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.antiAliasing = 0; // Sem MSAA para economizar performance
            }
            
            Application.targetFrameRate = targetFPS;
            
            Debug.Log("Quality Settings configurados com sucesso!");
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao configurar Quality Settings: " + e.Message);
        }
    }
    
    private void ConfigurePlayerSettings()
    {
        Debug.Log("Configurando Player Settings para Meta Quest...");
        
        try
        {
            // Configurações gerais do player para Android/Quest
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PlayerSettings.applicationIdentifier);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29; // Mínimo para Quest 3
            
            // Usando valor numérico para Android API Level 33, que pode não estar definido em algumas versões do Unity
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)33;
            
            // Configurações específicas para VR
            PlayerSettings.stereoRenderingPath = StereoRenderingPath.SinglePass; // Melhor performance
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            
            // Configurações para 64 bits
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            
            // Configurações de textura para vídeo 360
            if (optimizeFor360Video)
            {
                // Abordagem com tratamento para diferentes versões do Unity
                try
                {
                    // Tenta acessar APIs específicas de forma segura
                    var targetPropertyInfo = typeof(PlayerSettings.Android).GetProperty("androidETC2Fallback");
                    if (targetPropertyInfo != null)
                    {
                        // Se existe a propriedade, tenta definir o valor usando reflection
                        var fallbackEnum = System.Enum.Parse(targetPropertyInfo.PropertyType, "UseBuildSettings");
                        targetPropertyInfo.SetValue(null, fallbackEnum);
                    }

                    // Configurações de build para qualidade de textura
                    EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
                    
                    // Tenta definir a fallback para qualidade de textura nas configurações de build
                    var buildFallbackPropInfo = typeof(EditorUserBuildSettings).GetProperty("androidETC2Fallback");
                    if (buildFallbackPropInfo != null)
                    {
                        var qualityEnum = System.Enum.Parse(buildFallbackPropInfo.PropertyType, "Quality32Bit");
                        buildFallbackPropInfo.SetValue(null, qualityEnum);
                    }
                }
                catch (Exception textureEx)
                {
                    Debug.LogWarning("Não foi possível definir algumas configurações de textura: " + textureEx.Message);
                }
            }
            
            // Configurações de resolução dinâmica - usando formas alternativas para definições mais recentes
            if (useDynamicResolution)
            {
                PlayerSettings.runInBackground = true;
                
                // Configure Oculus específico através do OculusSettings (esse código depende da versão do Unity)
                // Este código tenta configurar resoluções dinâmicas
                var oculusSettings = GetOculusSettings();
                if (oculusSettings != null)
                {
                    // Tente definir propriedades do Oculus settings via reflection se necessário
                    try {
                        var settingsType = oculusSettings.GetType();
                        var dynamicScaleProperty = settingsType.GetProperty("eyeResolutionScale");
                        if (dynamicScaleProperty != null)
                        {
                            dynamicScaleProperty.SetValue(oculusSettings, 1.0f);
                        }
                    }
                    catch (Exception ex) {
                        Debug.LogWarning("Não foi possível configurar algumas propriedades do Oculus: " + ex.Message);
                    }
                }
            }
            
            // Outras configurações importantes
            PlayerSettings.Android.forceSDCardPermission = true;
            PlayerSettings.Android.forceInternetPermission = enableInternetAccess;
            
            Debug.Log("Player Settings configurados com sucesso!");
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao configurar Player Settings: " + e.Message);
        }
    }
    
    private ScriptableObject GetOculusSettings()
    {
        try {
            var oculusSettingsType = Type.GetType("Unity.XR.Oculus.OculusSettings, Unity.XR.Oculus.Editor");
            if (oculusSettingsType != null)
            {
                var settingsMethod = oculusSettingsType.GetMethod("GetSettings", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                
                if (settingsMethod != null)
                {
                    return settingsMethod.Invoke(null, null) as ScriptableObject;
                }
            }
        }
        catch (Exception ex) {
            Debug.LogWarning("Erro ao obter Oculus Settings: " + ex.Message);
        }
        return null;
    }
    
    private void ConfigurePermissions()
    {
        Debug.Log("Configurando permissões para Meta Quest...");
        
        try
        {
            // Criar arquivo de permissões se não existir
            string permissionsFile = "Assets/Plugins/Android/AndroidPermissions.cs";
            Directory.CreateDirectory(Path.GetDirectoryName(permissionsFile));
            
            string permissionsCode = @"
using UnityEngine;
using UnityEngine.Android;

public class AndroidPermissions : MonoBehaviour
{
    void Start()
    {
        RequestPermissions();
    }

    public void RequestPermissions()
    {
";
            
            if (enableExternalStorageRead)
            {
                permissionsCode += @"
        #if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
        }
        #endif
";
            }
            
            if (enableExternalStorageWrite)
            {
                permissionsCode += @"
        #if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageWrite);
        }
        #endif
";
            }
            
            permissionsCode += @"
    }
}
";
            
            File.WriteAllText(permissionsFile, permissionsCode);
            AssetDatabase.ImportAsset(permissionsFile);
            
            Debug.Log("Permissões configuradas com sucesso!");
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao configurar permissões: " + e.Message);
        }
    }
    
    private void ConfigureAndroidManifest()
    {
        Debug.Log("Configurando Android Manifest para Meta Quest...");
        
        try
        {
            // Caminho para o manifesto
            string manifestFolder = "Assets/Plugins/Android";
            string manifestPath = Path.Combine(manifestFolder, "AndroidManifest.xml");
            
            // Criar pasta se não existir
            if (!Directory.Exists(manifestFolder))
            {
                Directory.CreateDirectory(manifestFolder);
            }
            
            // Criar novo manifesto XML
            XmlDocument manifest = new XmlDocument();
            
            // Se o manifesto já existir, carregue-o
            if (File.Exists(manifestPath))
            {
                manifest.Load(manifestPath);
            }
            else
            {
                // Criar novo manifesto
                string baseManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.unity3d.player"">
  <application android:icon=""@drawable/app_icon"" android:label=""@string/app_name"">
    <activity android:name=""com.unity3d.player.UnityPlayerActivity"" android:label=""@string/app_name"" android:configChanges=""orientation|screenSize|keyboardHidden|keyboard"">
      <intent-filter>
        <action android:name=""android.intent.action.MAIN"" />
        <category android:name=""android.intent.category.LAUNCHER"" />
        <category android:name=""com.oculus.intent.category.VR"" />
      </intent-filter>
      <meta-data android:name=""unityplayer.UnityActivity"" android:value=""true"" />
      <meta-data android:name=""android.notch_support"" android:value=""true"" />
    </activity>
  </application>
</manifest>";
                
                manifest.LoadXml(baseManifest);
            }
            
            // Adicionar permissões
            XmlNode manifestNode = manifest.SelectSingleNode("/manifest");
            
            // Adicionar permissões específicas
            AddPermissionToManifest(manifest, manifestNode, "android.permission.INTERNET", enableInternetAccess);
            AddPermissionToManifest(manifest, manifestNode, "android.permission.READ_EXTERNAL_STORAGE", enableExternalStorageRead);
            AddPermissionToManifest(manifest, manifestNode, "android.permission.WRITE_EXTERNAL_STORAGE", enableExternalStorageWrite);
            
            // Configurações específicas para Quest
            XmlNode applicationNode = manifest.SelectSingleNode("/manifest/application");
            
            // Adicionar meta-data para Quest
            AddMetaDataToApplication(manifest, applicationNode, "com.oculus.supportedDevices", "quest|quest2|quest3");
            AddMetaDataToApplication(manifest, applicationNode, "com.oculus.handtracking.frequency", "HIGH");
            
            // Configurar activity para VR 
            XmlNode activityNode = manifest.SelectSingleNode("/manifest/application/activity");
            if (activityNode != null)
            {
                XmlAttribute configChanges = activityNode.Attributes["android:configChanges"];
                if (configChanges != null)
                {
                    configChanges.Value = "keyboard|keyboardHidden|navigation|orientation|screenLayout|screenSize|uiMode";
                }
                else
                {
                    XmlAttribute attr = manifest.CreateAttribute("android", "configChanges", "http://schemas.android.com/apk/res/android");
                    attr.Value = "keyboard|keyboardHidden|navigation|orientation|screenLayout|screenSize|uiMode";
                    activityNode.Attributes.Append(attr);
                }
                
                XmlAttribute resizeable = manifest.CreateAttribute("android", "resizeableActivity", "http://schemas.android.com/apk/res/android");
                resizeable.Value = "false";
                activityNode.Attributes.Append(resizeable);
            }
            
            // Salvar manifesto
            manifest.Save(manifestPath);
            AssetDatabase.ImportAsset(manifestPath);
            
            Debug.Log("Android Manifest configurado com sucesso!");
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao configurar Android Manifest: " + e.Message);
        }
    }
    
    private void AddPermissionToManifest(XmlDocument manifest, XmlNode manifestNode, string permission, bool shouldAdd)
    {
        if (!shouldAdd) return;
        
        bool permissionExists = false;
        XmlNodeList permissionNodes = manifest.SelectNodes("/manifest/uses-permission");
        
        foreach (XmlNode node in permissionNodes)
        {
            XmlAttribute nameAttr = node.Attributes["android:name"];
            if (nameAttr != null && nameAttr.Value == permission)
            {
                permissionExists = true;
                break;
            }
        }
        
        if (!permissionExists)
        {
            XmlElement permissionElement = manifest.CreateElement("uses-permission");
            XmlAttribute nameAttribute = manifest.CreateAttribute("android", "name", "http://schemas.android.com/apk/res/android");
            nameAttribute.Value = permission;
            permissionElement.Attributes.Append(nameAttribute);
            manifestNode.AppendChild(permissionElement);
        }
    }
    
    private void AddMetaDataToApplication(XmlDocument manifest, XmlNode applicationNode, string name, string value)
    {
        bool metaDataExists = false;
        XmlNodeList metaDataNodes = applicationNode.SelectNodes("meta-data");
        
        foreach (XmlNode node in metaDataNodes)
        {
            XmlAttribute nameAttr = node.Attributes["android:name"];
            if (nameAttr != null && nameAttr.Value == name)
            {
                XmlAttribute valueAttr = node.Attributes["android:value"];
                if (valueAttr != null)
                {
                    valueAttr.Value = value;
                }
                else
                {
                    XmlAttribute attr = manifest.CreateAttribute("android", "value", "http://schemas.android.com/apk/res/android");
                    attr.Value = value;
                    node.Attributes.Append(attr);
                }
                metaDataExists = true;
                break;
            }
        }
        
        if (!metaDataExists)
        {
            XmlElement metaDataElement = manifest.CreateElement("meta-data");
            
            XmlAttribute nameAttribute = manifest.CreateAttribute("android", "name", "http://schemas.android.com/apk/res/android");
            nameAttribute.Value = name;
            metaDataElement.Attributes.Append(nameAttribute);
            
            XmlAttribute valueAttribute = manifest.CreateAttribute("android", "value", "http://schemas.android.com/apk/res/android");
            valueAttribute.Value = value;
            metaDataElement.Attributes.Append(valueAttribute);
            
            applicationNode.AppendChild(metaDataElement);
        }
    }
    
    // Método para verificar se as configurações estão corretas
    private void VerifyConfiguration()
    {
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("=== RELATÓRIO DE CONFIGURAÇÃO PARA META QUEST 3 ===");
        
        // Verificar XR Settings
        bool xrConfigured = IsXRConfigured();
        report.AppendLine($"XR Management: {(xrConfigured ? "✓ Configurado" : "✗ Não configurado")}");
        
        // Verificar Player Settings
        bool vulkanConfigured = IsGraphicsAPIConfigured();
        bool archConfigured = Is64BitArchitectureConfigured();
        report.AppendLine($"Graphics API (Vulkan): {(vulkanConfigured ? "✓ Configurado" : "✗ Não configurado")}");
        report.AppendLine($"Arquitetura 64-bit: {(archConfigured ? "✓ Configurado" : "✗ Não configurado")}");
        
        // Verificar Android Manifest
        bool manifestExists = File.Exists("Assets/Plugins/Android/AndroidManifest.xml");
        report.AppendLine($"Android Manifest: {(manifestExists ? "✓ Existe" : "✗ Não encontrado")}");
        
        // Verificar Permissões
        bool permissionsExist = File.Exists("Assets/Plugins/Android/AndroidPermissions.cs");
        report.AppendLine($"Script de Permissões: {(permissionsExist ? "✓ Existe" : "✗ Não encontrado")}");
        
        report.AppendLine("\nRecomendações Adicionais:");
        if (!xrConfigured)
            report.AppendLine("- Configure o XR Management manualmente pelo menu Edit > Project Settings > XR Plugin Management");
        
        if (!vulkanConfigured)
            report.AppendLine("- Configure Vulkan como API gráfica no Player Settings");
        
        if (!archConfigured)
            report.AppendLine("- Configure ARM64 como arquitetura alvo no Player Settings");
        
        if (!manifestExists)
            report.AppendLine("- Crie um AndroidManifest.xml com as permissões necessárias");
        
        report.AppendLine("\n=== FIM DO RELATÓRIO ===");
        
        Debug.Log(report.ToString());
    }
    
    private bool IsXRConfigured()
    {
        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (generalSettings == null) return false;
        
        var manager = generalSettings.Manager;
        if (manager == null) return false;
        
        foreach (var loader in manager.activeLoaders)
        {
            if (loader != null && loader.GetType().Name.Contains("OculusLoader"))
                return true;
        }
        
        return false;
    }
    
    private bool IsGraphicsAPIConfigured()
    {
        var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        if (apis == null || apis.Length == 0) return false;
        
        return apis[0] == UnityEngine.Rendering.GraphicsDeviceType.Vulkan;
    }
    
    private bool Is64BitArchitectureConfigured()
    {
        return PlayerSettings.Android.targetArchitectures.HasFlag(AndroidArchitecture.ARM64);
    }
} 