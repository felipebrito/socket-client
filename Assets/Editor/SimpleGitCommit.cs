using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public class SimpleGitCommit : EditorWindow
{
    private string commitMessage = "";
    private bool isProcessing = false;
    private string statusOutput = "";
    private Vector2 scrollPosition;
    private bool autoAddFiles = true;
    private bool autoPush = true;
    private string outputMessage = "";
    private bool showFullOutput = false;
    private GUIStyle richTextStyle;

    [MenuItem("▸aparato◂/Github/Commit simples")]
    public static void ShowWindow()
    {
        GetWindow<SimpleGitCommit>("Git Commit");
    }

    private void OnGUI()
    {
        if (richTextStyle == null)
        {
            richTextStyle = new GUIStyle(EditorStyles.textArea);
            richTextStyle.richText = true;
            richTextStyle.wordWrap = true;
        }

        // Exibe o logo no início da janela
        Rect logoRect = new Rect(10, 10, position.width - 20, 60);
        AparatoMenuHelper.DrawLogo(logoRect);
        
        GUILayout.Space(70); // Espaço para o logo

        GUILayout.Label("Commit Simplificado", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Ferramenta para realizar commits Git de forma simplificada.", MessageType.Info);

        EditorGUILayout.Space(10);

        // Opções
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Opções:", EditorStyles.boldLabel, GUILayout.Width(60));
        autoAddFiles = EditorGUILayout.ToggleLeft("Adicionar arquivos automaticamente", autoAddFiles);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(60));
        autoPush = EditorGUILayout.ToggleLeft("Push automático após commit", autoPush);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Mensagem de commit
        GUILayout.Label("Mensagem de Commit:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Digite um resumo conciso das alterações realizadas.", MessageType.None);
        
        commitMessage = EditorGUILayout.TextField(commitMessage, GUILayout.Height(50));

        EditorGUILayout.Space(10);

        // Status do Git
        if (!isProcessing)
        {
            if (GUILayout.Button("Atualizar Status do Git", GUILayout.Height(30)))
            {
                UpdateGitStatus();
            }
        }

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Status do Git:", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
        EditorGUILayout.TextArea(statusOutput, richTextStyle);
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(commitMessage);
        
        if (GUILayout.Button("Realizar Commit", GUILayout.Height(40)))
        {
            PerformCommit();
        }
        
        GUI.enabled = true;

        // Output da operação
        if (!string.IsNullOrEmpty(outputMessage))
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Resultado da Operação:", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            showFullOutput = EditorGUILayout.ToggleLeft("Mostrar Detalhes", showFullOutput, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
            
            if (showFullOutput)
            {
                EditorGUILayout.TextArea(outputMessage, GUILayout.Height(100));
            }
            else
            {
                bool success = !outputMessage.Contains("error") && !outputMessage.Contains("fatal");
                EditorGUILayout.HelpBox(success ? "Operação realizada com sucesso!" : "Ocorreu um erro. Clique em 'Mostrar Detalhes'.", 
                    success ? MessageType.Info : MessageType.Error);
            }
            
            EditorGUILayout.EndVertical();
        }

        if (isProcessing)
        {
            EditorGUILayout.HelpBox("Processando...", MessageType.Info);
        }

        // Footer com marca
        GUILayout.FlexibleSpace();
        GUILayout.Label("▸codex || aparato®", EditorStyles.centeredGreyMiniLabel);
    }

    private async void UpdateGitStatus()
    {
        isProcessing = true;
        statusOutput = "Carregando status...";
        
        string result = await RunGitCommandAsync("status");
        
        // Formatar saída para visualização melhor
        StringBuilder formattedOutput = new StringBuilder();
        
        if (result.Contains("Changes not staged for commit"))
        {
            formattedOutput.AppendLine("<color=yellow><b>Arquivos Modificados (não adicionados):</b></color>");
            string[] lines = result.Split('\n');
            bool inChangesSection = false;
            
            foreach (var line in lines)
            {
                if (line.Contains("Changes not staged for commit"))
                {
                    inChangesSection = true;
                    continue;
                }
                else if (line.Contains("Changes to be committed") || line.Contains("Untracked files"))
                {
                    inChangesSection = false;
                }
                
                if (inChangesSection && line.Trim().StartsWith("modified:"))
                {
                    formattedOutput.AppendLine("  <color=orange>" + line.Trim() + "</color>");
                }
            }
        }
        
        if (result.Contains("Changes to be committed"))
        {
            formattedOutput.AppendLine("<color=green><b>Arquivos Prontos para Commit:</b></color>");
            string[] lines = result.Split('\n');
            bool inChangesSection = false;
            
            foreach (var line in lines)
            {
                if (line.Contains("Changes to be committed"))
                {
                    inChangesSection = true;
                    continue;
                }
                else if (line.Contains("Changes not staged") || line.Contains("Untracked files"))
                {
                    inChangesSection = false;
                }
                
                if (inChangesSection && (line.Trim().StartsWith("modified:") || line.Trim().StartsWith("new file:")))
                {
                    formattedOutput.AppendLine("  <color=green>" + line.Trim() + "</color>");
                }
            }
        }
        
        if (result.Contains("Untracked files"))
        {
            formattedOutput.AppendLine("<color=blue><b>Arquivos Não Rastreados:</b></color>");
            string[] lines = result.Split('\n');
            bool inUntrackedSection = false;
            
            foreach (var line in lines)
            {
                if (line.Contains("Untracked files"))
                {
                    inUntrackedSection = true;
                    continue;
                }
                else if (line.Trim() == "")
                {
                    inUntrackedSection = false;
                }
                
                if (inUntrackedSection && !line.Contains("(use \"git add") && !line.Contains("Untracked files"))
                {
                    string trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                        formattedOutput.AppendLine("  <color=blue>" + trimmedLine + "</color>");
                }
            }
        }
        
        if (result.Contains("nothing to commit"))
        {
            formattedOutput.AppendLine("<color=gray>Nenhuma alteração para commit.</color>");
        }
        
        if (string.IsNullOrEmpty(result) || result.Contains("fatal") || result.Contains("error"))
        {
            formattedOutput.AppendLine("<color=red>Erro ao obter status do Git. Verifique se este é um repositório Git válido.</color>");
            formattedOutput.AppendLine("<color=red>" + result + "</color>");
        }
        
        statusOutput = formattedOutput.ToString();
        
        if (string.IsNullOrEmpty(statusOutput.Trim()))
        {
            statusOutput = "Não foi possível obter o status do Git. Verifique o console para mais detalhes.";
        }
        
        isProcessing = false;
        Repaint();
    }

    private async void PerformCommit()
    {
        if (string.IsNullOrEmpty(commitMessage))
        {
            outputMessage = "A mensagem de commit não pode estar vazia.";
            return;
        }

        isProcessing = true;
        outputMessage = "";
        StringBuilder output = new StringBuilder();
        
        try
        {
            // Adicionar todos os arquivos se a opção estiver habilitada
            if (autoAddFiles)
            {
                output.AppendLine("Adicionando arquivos...");
                string addResult = await RunGitCommandAsync("add -A");
                output.AppendLine(addResult);
            }

            // Realizar o commit
            output.AppendLine("Realizando commit...");
            string commitResult = await RunGitCommandAsync($"commit -m \"{commitMessage}\"");
            output.AppendLine(commitResult);

            // Push automático se a opção estiver habilitada
            if (autoPush)
            {
                output.AppendLine("Realizando push...");
                string pushResult = await RunGitCommandAsync("push");
                output.AppendLine(pushResult);
            }

            // Atualizar o status após as operações
            await Task.Delay(500); // Pequeno delay para garantir que o Git atualizou
            UpdateGitStatus();
        }
        catch (System.Exception ex)
        {
            output.AppendLine("Erro durante a operação: " + ex.Message);
            UnityEngine.Debug.LogError("Erro ao realizar commit: " + ex.Message);
        }
        
        outputMessage = output.ToString();
        isProcessing = false;
        Repaint();
    }

    private async Task<string> RunGitCommandAsync(string arguments)
    {
        string projectPath = Path.GetDirectoryName(Application.dataPath);
        
        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = projectPath
        };

        StringBuilder output = new StringBuilder();
        
        try
        {
            using (Process process = Process.Start(processInfo))
            {
                // Ler a saída padrão
                using (StreamReader reader = process.StandardOutput)
                {
                    string result = await reader.ReadToEndAsync();
                    output.Append(result);
                }
                
                // Ler a saída de erro
                using (StreamReader reader = process.StandardError)
                {
                    string error = await reader.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(error))
                    {
                        output.AppendLine("Erro: " + error);
                    }
                }
                
                // Substituir WaitForExitAsync por uma alternativa que funciona em versões mais antigas do .NET
                await Task.Run(() => {
                    process.WaitForExit();
                });
            }
        }
        catch (System.Exception ex)
        {
            output.AppendLine("Exceção ao executar comando Git: " + ex.Message);
            UnityEngine.Debug.LogError("Erro ao executar comando Git: " + ex.Message);
        }
        
        return output.ToString();
    }
} 