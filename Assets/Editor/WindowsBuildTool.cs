using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.IO.Compression;

public class WindowsBuildTool : EditorWindow
{
    private string buildPath = "Builds/Windows";
    private string executableName = "SocketCliente";
    private bool developmentBuild = false;
    private bool buildScenes = true;
    private bool zipAfterBuild = false;

    [MenuItem("▸aparato◂/Build/Windows")]
    public static void ShowWindow()
    {
        GetWindow<WindowsBuildTool>("Windows Build Tool");
    }

    private void OnGUI()
    {
        // Exibe o logo no início da janela
        Rect logoRect = new Rect(10, 10, position.width - 20, 60);
        AparatoMenuHelper.DrawLogo(logoRect);
        
        GUILayout.Space(70); // Espaço para o logo
        
        GUILayout.Label("Windows Build Settings", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        // Build Path
        EditorGUILayout.BeginHorizontal();
        buildPath = EditorGUILayout.TextField("Build Path:", buildPath);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string path = EditorUtility.SaveFolderPanel("Choose Build Location", buildPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                // Convert to a relative path if possible
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
                buildPath = path;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // Executable Name
        executableName = EditorGUILayout.TextField("Executable Name:", executableName);
        
        EditorGUILayout.Space();
        
        // Build options
        developmentBuild = EditorGUILayout.Toggle("Development Build", developmentBuild);
        buildScenes = EditorGUILayout.Toggle("Include All Enabled Scenes", buildScenes);
        zipAfterBuild = EditorGUILayout.Toggle("Zip After Build", zipAfterBuild);
        
        EditorGUILayout.Space();
        
        // Build button
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Build Windows Standalone", GUILayout.Height(30)))
        {
            BuildWindowsStandalone();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "This tool builds a Windows standalone without changing your current platform settings in the project.", 
            MessageType.Info);
            
        // Adicionar o rodapé com a assinatura
        GUILayout.FlexibleSpace();
        GUILayout.Label("▸codex || aparato®", EditorStyles.centeredGreyMiniLabel);
    }

    private void BuildWindowsStandalone()
    {
        try
        {
            // Save current platform
            BuildTarget originalTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup originalTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            
            // Prepare build options
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            
            // Set scenes
            if (buildScenes)
            {
                buildPlayerOptions.scenes = GetEnabledScenes();
            }
            else
            {
                buildPlayerOptions.scenes = new string[] { SceneManager.GetActiveScene().path };
            }
            
            // Set location
            string fullPath = Path.Combine(buildPath, executableName + ".exe");
            buildPlayerOptions.locationPathName = fullPath;
            
            // Set target
            buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
            buildPlayerOptions.targetGroup = BuildTargetGroup.Standalone;
            
            // Set options
            buildPlayerOptions.options = BuildOptions.None;
            if (developmentBuild)
            {
                buildPlayerOptions.options |= BuildOptions.Development;
            }
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            
            // Build
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;
            
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
                
                // Create ZIP if requested
                if (zipAfterBuild)
                {
                    CreateZipFromBuild(fullPath);
                }
                
                // Show in explorer
                EditorUtility.RevealInFinder(fullPath);
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError("Build failed");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error building Windows standalone: " + e.Message);
        }
    }
    
    private string[] GetEnabledScenes()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        System.Collections.Generic.List<string> enabledScenes = new System.Collections.Generic.List<string>();
        
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.enabled)
            {
                enabledScenes.Add(scene.path);
            }
        }
        
        return enabledScenes.ToArray();
    }
    
    private void CreateZipFromBuild(string exePath)
    {
        try
        {
            string buildFolder = Path.GetDirectoryName(exePath);
            string zipFileName = Path.Combine(Directory.GetParent(buildFolder).FullName, executableName + ".zip");
            
            Debug.Log("Creating ZIP archive: " + zipFileName);
            
            // Delete existing zip file if it exists
            if (File.Exists(zipFileName))
            {
                File.Delete(zipFileName);
            }
            
            // Create the zip file - usando o namespace completo para resolver a ambiguidade
            ZipFile.CreateFromDirectory(buildFolder, zipFileName, System.IO.Compression.CompressionLevel.Optimal, false);
            
            Debug.Log("ZIP archive created successfully: " + zipFileName);
            
            // Show in explorer
            EditorUtility.RevealInFinder(zipFileName);
        }
        catch (Exception e)
        {
            Debug.LogError("Error creating ZIP file: " + e.Message);
            
            // Fallback message if System.IO.Compression not available
            if (e.Message.Contains("ZipFile"))
            {
                EditorUtility.DisplayDialog("ZIP Creation Failed", 
                    "ZIP creation failed. Make sure you're using .NET 4.x in your project settings.\n\n" +
                    "Edit > Project Settings > Player > Other Settings > Configuration > API Compatibility Level should be set to .NET 4.x", 
                    "OK");
            }
        }
    }
} 