using UnityEngine;
using UnityEditor;

public class SimulationTest : EditorWindow
{
    [MenuItem("Aparato/Testar Simulação")]
    public static void ShowWindow()
    {
        GetWindow<SimulationTest>("Teste de Simulação");
    }

    private VideoRotationControl rotationControl;
    private CameraMovementSimulator simulator;
    private string videoName = "video.mp4";
    private float angle = 75f;
    private Vector2 scrollPosition;
    private bool showDebug = true;

    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Configuração da Simulação", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Campos para configuração
        videoName = EditorGUILayout.TextField("Nome do Vídeo", videoName);
        angle = EditorGUILayout.Slider("Ângulo de Bloqueio", angle, 30f, 120f);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Componentes", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Referências dos componentes
        rotationControl = EditorGUILayout.ObjectField("Rotation Control", rotationControl, typeof(VideoRotationControl), true) as VideoRotationControl;
        simulator = EditorGUILayout.ObjectField("Simulator", simulator, typeof(CameraMovementSimulator), true) as CameraMovementSimulator;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Configurações do Simulador", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (simulator != null)
        {
            simulator.movementIntensity = EditorGUILayout.Slider("Intensidade do Movimento", simulator.movementIntensity, 0.1f, 2f);
            simulator.movementSpeed = EditorGUILayout.Slider("Velocidade do Movimento", simulator.movementSpeed, 0.1f, 5f);
            simulator.focusOnInterestAreas = EditorGUILayout.Toggle("Focar em Áreas de Interesse", simulator.focusOnInterestAreas);
            simulator.showVisualIndicators = EditorGUILayout.Toggle("Mostrar Indicadores Visuais", simulator.showVisualIndicators);
        }

        EditorGUILayout.Space(20);

        // Botões de ação
        if (GUILayout.Button("Configurar Bloqueios de Teste"))
        {
            ConfigureTestBlocks();
        }

        if (GUILayout.Button("Iniciar Simulação"))
        {
            StartSimulation();
        }

        if (GUILayout.Button("Parar Simulação"))
        {
            StopSimulation();
        }

        showDebug = EditorGUILayout.Foldout(showDebug, "Debug Info");
        if (showDebug)
        {
            EditorGUI.indentLevel++;
            if (rotationControl != null)
            {
                EditorGUILayout.LabelField("Vídeo Atual:", rotationControl.currentVideoTitleID);
                EditorGUILayout.LabelField("Bloco Atual:", rotationControl.currentTimeBlockInfo);
                EditorGUILayout.LabelField("Controle Ativo:", rotationControl.IsRotationControlEnabled.ToString());
            }
            if (simulator != null && simulator.rotationLimiter != null)
            {
                EditorGUILayout.LabelField("Limite Ativo:", simulator.rotationLimiter.IsLimitActive.ToString());
                EditorGUILayout.LabelField("Ângulo Atual:", simulator.rotationLimiter.angle.ToString("F1") + "°");
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndScrollView();
    }

    private void ConfigureTestBlocks()
    {
        if (rotationControl == null)
        {
            Debug.LogError("VideoRotationControl não encontrado!");
            return;
        }

        // Limpa blocos existentes
        rotationControl.videoBlocks.Clear();

        // Cria um bloco de teste
        var block = new VideoRotationControl.VideoBlock
        {
            videoTitle = videoName,
            angle = angle
        };

        // Adiciona alguns blocos de tempo para teste
        block.blockTimes.Add(new VideoRotationControl.BlockTime { startTime = 5, endTime = 15 });
        block.blockTimes.Add(new VideoRotationControl.BlockTime { startTime = 25, endTime = 35 });
        block.blockTimes.Add(new VideoRotationControl.BlockTime { startTime = 45, endTime = 55 });

        // Adiciona o bloco à lista
        rotationControl.videoBlocks.Add(block);

        Debug.Log($"Blocos de teste configurados para o vídeo {videoName} com ângulo {angle}°");
    }

    private void StartSimulation()
    {
        if (simulator == null || rotationControl == null)
        {
            Debug.LogError("Simulator ou VideoRotationControl não encontrados!");
            return;
        }

        // Configura as referências
        simulator.rotationController = rotationControl;
        if (rotationControl.cameraLimiter != null)
        {
            simulator.rotationLimiter = rotationControl.cameraLimiter;
        }

        // Ativa o controle de rotação
        rotationControl.SetRotationControlEnabled(true);

        Debug.Log("Simulação iniciada!");
    }

    private void StopSimulation()
    {
        if (rotationControl != null)
        {
            rotationControl.SetRotationControlEnabled(false);
        }

        Debug.Log("Simulação parada!");
    }
} 