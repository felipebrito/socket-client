using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class GitHubManager : EditorWindow
{
    private Vector2 scrollPosition;
    private bool isGitInstalled = false;
    private bool isRepositoryInitialized = false;
    private string gitStatus = "";
    private string gitBranch = "";
    private string commitMessage = "Update project files";
    private string remoteName = "origin";
    private string remoteUrl = "";
    private string newBranchName = "";
    private string logOutput = "";

    // Commit selection options
    private bool commitAllChanges = true;
    private List<string> changedFiles = new List<string>();
    private List<bool> selectedFiles = new List<bool>();
    
    // For GitHub authentication
    private string username = "";
    private string token = "";
    
    // UI States
    private bool showCredentials = false;
    private bool showCreateRepo = false;
    private string newRepoName = "";
    private bool newRepoPrivate = true;
    private string newRepoDescription = "";
    
    // For branch management
    private List<string> branches = new List<string>();
    private int selectedBranchIndex = 0;

    [MenuItem("▸aparato◂/Github/Avançado (completo)")]
    public static void ShowWindow()
    {
        GetWindow<GitHubManager>("GitHub Manager");
    }

    private void OnEnable()
    {
        CheckGitInstallation();
        if (isGitInstalled)
        {
            CheckGitRepository();
            RefreshGitStatus();
            GetCurrentBranch();
            GetRemoteUrl();
            GetBranches();
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // Exibe o logo no início da janela
        Rect logoRect = new Rect(10, 10, position.width - 20, 60);
        AparatoMenuHelper.DrawLogo(logoRect);
        
        GUILayout.Space(70); // Espaço para o logo
        
        GUILayout.Label("GitHub Repository Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (!isGitInstalled)
        {
            EditorGUILayout.HelpBox("Git is not installed or not in the system PATH. Please install Git to use this tool.", MessageType.Error);
            if (GUILayout.Button("Check Again"))
            {
                CheckGitInstallation();
            }
            EditorGUILayout.EndScrollView();
            return;
        }

        // Repository Status Section
        DrawRepositoryStatusSection();
        
        // Credentials Section
        DrawCredentialsSection();
        
        // Repository Creation Section
        if (showCreateRepo)
        {
            DrawCreateRepositorySection();
        }
        
        EditorGUILayout.Space();
        
        if (isRepositoryInitialized)
        {
            // Change Management Section
            DrawChangesSection();
            
            EditorGUILayout.Space();
            
            // Branch Management Section
            DrawBranchSection();
            
            EditorGUILayout.Space();
            
            // Remote Management Section
            DrawRemoteSection();
        }
        else
        {
            DrawInitializeRepositorySection();
        }
        
        EditorGUILayout.Space();
        
        // Output Log
        DrawLogSection();
        
        EditorGUILayout.EndScrollView();

        // Add footer with signature at the very end of OnGUI
        GUILayout.FlexibleSpace();
        GUILayout.Label("▸codex || aparato®", EditorStyles.centeredGreyMiniLabel);
    }
    
    #region UI Sections
    
    private void DrawRepositoryStatusSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Repository Status", EditorStyles.boldLabel);
        
        if (isRepositoryInitialized)
        {
            EditorGUILayout.LabelField("Status: ", "Git repository initialized");
            EditorGUILayout.LabelField("Current Branch: ", gitBranch);
            
            if (!string.IsNullOrEmpty(remoteUrl))
            {
                EditorGUILayout.LabelField("Remote URL: ", remoteUrl);
            }
            else
            {
                EditorGUILayout.LabelField("Remote URL: ", "No remote configured");
            }
            
            if (GUILayout.Button("Refresh Status"))
            {
                RefreshGitStatus();
                GetCurrentBranch();
                GetRemoteUrl();
                GetBranches();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Status: ", "No Git repository initialized");
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawCredentialsSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        showCredentials = EditorGUILayout.Foldout(showCredentials, "GitHub Credentials", true);
        
        if (showCredentials)
        {
            username = EditorGUILayout.TextField("Username:", username);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Token:");
            token = EditorGUILayout.PasswordField(token);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox("These credentials will be used for GitHub operations. The token should have the 'repo' scope.", MessageType.Info);
            
            if (GUILayout.Button("Test Credentials"))
            {
                TestGitHubCredentials();
            }
            
            EditorGUILayout.Space();
            
            if (!showCreateRepo && GUILayout.Button("Create New GitHub Repository"))
            {
                showCreateRepo = true;
            }
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawCreateRepositorySection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Create New GitHub Repository", EditorStyles.boldLabel);
        
        newRepoName = EditorGUILayout.TextField("Repository Name:", newRepoName);
        newRepoDescription = EditorGUILayout.TextField("Description:", newRepoDescription);
        newRepoPrivate = EditorGUILayout.Toggle("Private Repository", newRepoPrivate);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Repository"))
        {
            CreateGitHubRepository();
        }
        
        if (GUILayout.Button("Cancel"))
        {
            showCreateRepo = false;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawInitializeRepositorySection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Initialize Repository", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Initialize Git Repository"))
        {
            InitializeRepository();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawChangesSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Changes and Commits", EditorStyles.boldLabel);
        
        // Git status display
        EditorGUILayout.LabelField("Git Status:");
        EditorGUILayout.TextArea(gitStatus, GUILayout.Height(100));
        
        EditorGUILayout.Space();
        
        // File selection
        commitAllChanges = EditorGUILayout.Toggle("Commit All Changes", commitAllChanges);
        
        if (!commitAllChanges && changedFiles.Count > 0)
        {
            EditorGUILayout.LabelField("Select files to commit:");
            
            for (int i = 0; i < changedFiles.Count; i++)
            {
                selectedFiles[i] = EditorGUILayout.Toggle(changedFiles[i], selectedFiles[i]);
            }
        }
        
        // Commit message
        commitMessage = EditorGUILayout.TextField("Commit Message:", commitMessage);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Refresh Changes"))
        {
            RefreshGitStatus();
            UpdateChangedFilesList();
        }
        
        if (GUILayout.Button("Stage Changes"))
        {
            StageChanges();
        }
        
        EditorGUILayout.EndHorizontal();
        
        if (GUILayout.Button("Commit Changes"))
        {
            CommitChanges();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawBranchSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Branch Management", EditorStyles.boldLabel);
        
        // Current branch info
        EditorGUILayout.LabelField("Current Branch:", gitBranch);
        
        // Branch selection
        if (branches.Count > 0)
        {
            EditorGUILayout.LabelField("Switch to Branch:");
            selectedBranchIndex = EditorGUILayout.Popup(selectedBranchIndex, branches.ToArray());
            
            if (GUILayout.Button("Switch Branch"))
            {
                SwitchBranch(branches[selectedBranchIndex]);
            }
        }
        
        // Create new branch
        EditorGUILayout.Space();
        GUILayout.Label("Create New Branch", EditorStyles.boldLabel);
        newBranchName = EditorGUILayout.TextField("New Branch Name:", newBranchName);
        
        if (GUILayout.Button("Create Branch"))
        {
            CreateNewBranch();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawRemoteSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Remote Repository", EditorStyles.boldLabel);
        
        remoteName = EditorGUILayout.TextField("Remote Name:", remoteName);
        
        if (string.IsNullOrEmpty(remoteUrl))
        {
            remoteUrl = EditorGUILayout.TextField("Remote URL:", remoteUrl);
            
            if (GUILayout.Button("Add Remote"))
            {
                AddRemote();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Remote URL:", remoteUrl);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Pull"))
            {
                PullFromRemote();
            }
            
            if (GUILayout.Button("Push"))
            {
                PushToRemote();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawLogSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Command Output", EditorStyles.boldLabel);
        
        EditorGUILayout.TextArea(logOutput, GUILayout.Height(100));
        
        if (GUILayout.Button("Clear Log"))
        {
            logOutput = "";
        }
        
        EditorGUILayout.EndVertical();
    }
    
    #endregion
    
    #region Git Operations
    
    private void CheckGitInstallation()
    {
        try
        {
            string output = RunGitCommand("--version", out int exitCode);
            isGitInstalled = exitCode == 0 && output.Contains("git version");
            if (isGitInstalled)
            {
                AppendToLog("Git is installed: " + output);
            }
            else
            {
                AppendToLog("Git is not installed or not in the system PATH.");
            }
        }
        catch (Exception e)
        {
            isGitInstalled = false;
            AppendToLog("Error checking Git installation: " + e.Message);
        }
    }
    
    private void CheckGitRepository()
    {
        try
        {
            string output = RunGitCommand("rev-parse --is-inside-work-tree", out int exitCode);
            isRepositoryInitialized = exitCode == 0 && output.Trim() == "true";
            
            if (isRepositoryInitialized)
            {
                AppendToLog("Git repository is initialized.");
            }
            else
            {
                AppendToLog("No Git repository initialized in this directory.");
            }
        }
        catch (Exception e)
        {
            isRepositoryInitialized = false;
            AppendToLog("Error checking Git repository: " + e.Message);
        }
    }
    
    private void InitializeRepository()
    {
        try
        {
            string output = RunGitCommand("init", out int exitCode);
            
            if (exitCode == 0)
            {
                isRepositoryInitialized = true;
                AppendToLog("Git repository initialized successfully.");
                RefreshGitStatus();
                GetCurrentBranch();
            }
            else
            {
                AppendToLog("Failed to initialize Git repository: " + output);
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error initializing Git repository: " + e.Message);
        }
    }
    
    private void RefreshGitStatus()
    {
        if (!isRepositoryInitialized) return;
        
        try
        {
            gitStatus = RunGitCommand("status", out int exitCode);
            
            if (exitCode != 0)
            {
                AppendToLog("Failed to get Git status.");
            }
            
            UpdateChangedFilesList();
        }
        catch (Exception e)
        {
            AppendToLog("Error getting Git status: " + e.Message);
        }
    }
    
    private void UpdateChangedFilesList()
    {
        changedFiles.Clear();
        selectedFiles.Clear();
        
        try
        {
            string output = RunGitCommand("status --porcelain", out int exitCode);
            
            if (exitCode == 0)
            {
                using (StringReader reader = new StringReader(output))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && line.Length > 3)
                        {
                            string filePath = line.Substring(3);
                            changedFiles.Add(filePath);
                            selectedFiles.Add(true);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error updating changed files list: " + e.Message);
        }
    }
    
    private void GetCurrentBranch()
    {
        if (!isRepositoryInitialized) return;
        
        try
        {
            gitBranch = RunGitCommand("rev-parse --abbrev-ref HEAD", out int exitCode).Trim();
            
            if (exitCode != 0 || string.IsNullOrEmpty(gitBranch))
            {
                gitBranch = "unknown";
            }
        }
        catch (Exception e)
        {
            gitBranch = "unknown";
            AppendToLog("Error getting current branch: " + e.Message);
        }
    }
    
    private void GetRemoteUrl()
    {
        if (!isRepositoryInitialized) return;
        
        try
        {
            remoteUrl = RunGitCommand($"config --get remote.{remoteName}.url", out int exitCode).Trim();
            
            if (exitCode != 0)
            {
                remoteUrl = "";
            }
        }
        catch (Exception e)
        {
            remoteUrl = "";
            AppendToLog("Error getting remote URL: " + e.Message);
        }
    }
    
    private void GetBranches()
    {
        if (!isRepositoryInitialized) return;
        
        try
        {
            branches.Clear();
            
            string output = RunGitCommand("branch", out int exitCode);
            
            if (exitCode == 0)
            {
                using (StringReader reader = new StringReader(output))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.StartsWith("*"))
                        {
                            // Current branch
                            line = line.Substring(1).Trim();
                            branches.Add(line);
                            selectedBranchIndex = branches.Count - 1;
                        }
                        else if (!string.IsNullOrEmpty(line))
                        {
                            branches.Add(line);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error getting branches: " + e.Message);
        }
    }
    
    private void StageChanges()
    {
        if (!isRepositoryInitialized) return;
        
        try
        {
            string command;
            
            if (commitAllChanges)
            {
                command = "add -A";
            }
            else
            {
                StringBuilder sb = new StringBuilder("add");
                
                for (int i = 0; i < changedFiles.Count; i++)
                {
                    if (selectedFiles[i])
                    {
                        sb.Append(" \"").Append(changedFiles[i]).Append("\"");
                    }
                }
                
                command = sb.ToString();
                
                if (command == "add")
                {
                    AppendToLog("No files selected for staging.");
                    return;
                }
            }
            
            string output = RunGitCommand(command, out int exitCode);
            
            if (exitCode == 0)
            {
                AppendToLog("Changes staged successfully.");
                RefreshGitStatus();
            }
            else
            {
                AppendToLog("Failed to stage changes: " + output);
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error staging changes: " + e.Message);
        }
    }
    
    private void CommitChanges()
    {
        if (!isRepositoryInitialized) return;
        
        if (string.IsNullOrWhiteSpace(commitMessage))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a commit message.", "OK");
            return;
        }
        
        try
        {
            // First stage changes if needed
            StageChanges();
            
            string output = RunGitCommand($"commit -m \"{commitMessage}\"", out int exitCode);
            
            if (exitCode == 0)
            {
                AppendToLog("Changes committed successfully.");
                RefreshGitStatus();
            }
            else
            {
                AppendToLog("Failed to commit changes: " + output);
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error committing changes: " + e.Message);
        }
    }
    
    private void AddRemote()
    {
        if (!isRepositoryInitialized) return;
        
        if (string.IsNullOrWhiteSpace(remoteName) || string.IsNullOrWhiteSpace(remoteUrl))
        {
            EditorUtility.DisplayDialog("Error", "Please enter both remote name and URL.", "OK");
            return;
        }
        
        try
        {
            string output = RunGitCommand($"remote add {remoteName} {remoteUrl}", out int exitCode);
            
            if (exitCode == 0)
            {
                AppendToLog($"Remote '{remoteName}' added successfully.");
                GetRemoteUrl();
            }
            else
            {
                AppendToLog("Failed to add remote: " + output);
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error adding remote: " + e.Message);
        }
    }
    
    private void PullFromRemote()
    {
        if (!isRepositoryInitialized) return;
        
        try
        {
            string output = RunGitCommand($"pull {remoteName} {gitBranch}", out int exitCode);
            
            if (exitCode == 0)
            {
                AppendToLog("Pull completed successfully.");
                RefreshGitStatus();
            }
            else
            {
                AppendToLog("Failed to pull: " + output);
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error pulling from remote: " + e.Message);
        }
    }
    
    private void PushToRemote()
    {
        if (!isRepositoryInitialized) return;
        
        try
        {
            string output = RunGitCommand($"push -u {remoteName} {gitBranch}", out int exitCode);
            
            if (exitCode == 0)
            {
                AppendToLog("Push completed successfully.");
            }
            else
            {
                AppendToLog("Failed to push: " + output);
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error pushing to remote: " + e.Message);
        }
    }
    
    private void CreateNewBranch()
    {
        if (!isRepositoryInitialized) return;
        
        if (string.IsNullOrWhiteSpace(newBranchName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a branch name.", "OK");
            return;
        }
        
        try
        {
            string output = RunGitCommand($"checkout -b {newBranchName}", out int exitCode);
            
            if (exitCode == 0)
            {
                AppendToLog($"Branch '{newBranchName}' created successfully.");
                GetCurrentBranch();
                GetBranches();
                newBranchName = "";
            }
            else
            {
                AppendToLog("Failed to create branch: " + output);
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error creating branch: " + e.Message);
        }
    }
    
    private void SwitchBranch(string branchName)
    {
        if (!isRepositoryInitialized) return;
        
        try
        {
            string output = RunGitCommand($"checkout {branchName}", out int exitCode);
            
            if (exitCode == 0)
            {
                AppendToLog($"Switched to branch '{branchName}'.");
                GetCurrentBranch();
                RefreshGitStatus();
            }
            else
            {
                AppendToLog("Failed to switch branch: " + output);
            }
        }
        catch (Exception e)
        {
            AppendToLog("Error switching branch: " + e.Message);
        }
    }
    
    private void TestGitHubCredentials()
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
        {
            EditorUtility.DisplayDialog("Error", "Please enter both username and token.", "OK");
            return;
        }
        
        AppendToLog("Testing GitHub credentials...");
        
        // Create a simple authenticated request to GitHub API
        System.Net.WebClient client = new System.Net.WebClient();
        client.Headers.Add("User-Agent", "Unity GitHub Manager");
        client.Headers.Add("Authorization", "token " + token);
        
        try
        {
            string response = client.DownloadString("https://api.github.com/user");
            AppendToLog("GitHub credentials are valid.");
            EditorUtility.DisplayDialog("Success", "GitHub credentials are valid.", "OK");
        }
        catch (Exception e)
        {
            AppendToLog("GitHub credentials validation failed: " + e.Message);
            EditorUtility.DisplayDialog("Error", "GitHub credentials validation failed: " + e.Message, "OK");
        }
    }
    
    private void CreateGitHubRepository()
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
        {
            EditorUtility.DisplayDialog("Error", "Please enter GitHub credentials.", "OK");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(newRepoName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a repository name.", "OK");
            return;
        }
        
        AppendToLog("Creating GitHub repository...");
        
        string requestBody = "{\"name\":\"" + newRepoName + "\",\"private\":" + newRepoPrivate.ToString().ToLower() + 
                             ",\"description\":\"" + newRepoDescription + "\"}";
        
        System.Net.WebClient client = new System.Net.WebClient();
        client.Headers.Add("User-Agent", "Unity GitHub Manager");
        client.Headers.Add("Authorization", "token " + token);
        client.Headers.Add("Content-Type", "application/json");
        
        try
        {
            string response = client.UploadString("https://api.github.com/user/repos", requestBody);
            AppendToLog("GitHub repository created successfully.");
            
            // Set the remote URL
            remoteUrl = "https://github.com/" + username + "/" + newRepoName + ".git";
            
            // If repository is already initialized, add the remote
            if (isRepositoryInitialized)
            {
                AddRemote();
            }
            
            showCreateRepo = false;
            EditorUtility.DisplayDialog("Success", "GitHub repository created successfully.", "OK");
        }
        catch (Exception e)
        {
            AppendToLog("Failed to create GitHub repository: " + e.Message);
            EditorUtility.DisplayDialog("Error", "Failed to create GitHub repository: " + e.Message, "OK");
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    private string RunGitCommand(string arguments, out int exitCode)
    {
        Process process = new Process();
        process.StartInfo.FileName = "git";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        
        StringBuilder output = new StringBuilder();
        StringBuilder error = new StringBuilder();
        
        process.OutputDataReceived += (sender, e) => {
            if (e.Data != null)
                output.AppendLine(e.Data);
        };
        
        process.ErrorDataReceived += (sender, e) => {
            if (e.Data != null)
                error.AppendLine(e.Data);
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        
        exitCode = process.ExitCode;
        
        string result = output.ToString();
        if (exitCode != 0)
        {
            result = error.ToString();
        }
        
        return result;
    }
    
    private void AppendToLog(string message)
    {
        logOutput = message + "\n" + logOutput;
        
        if (logOutput.Length > 10000)
        {
            logOutput = logOutput.Substring(0, 10000);
        }
        
        UnityEngine.Debug.Log("[GitHub Manager] " + message);
    }
    
    #endregion
} 