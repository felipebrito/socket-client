using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DiagnosticInitializer : MonoBehaviour
{
    [SerializeField] private GameObject diagnosticCanvasPrefab;
    [SerializeField] private bool enableOnStart = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.D;
    
    private GameObject diagnosticCanvas;
    private DiagnosticUI diagnosticUI;
    
    private void Start()
    {
        InitializeDiagnostic();
    }
    
    private void InitializeDiagnostic()
    {
        if (diagnosticCanvasPrefab != null)
        {
            // Instanciar o prefab do canvas de diagnóstico
            diagnosticCanvas = Instantiate(diagnosticCanvasPrefab, Vector3.zero, Quaternion.identity);
            diagnosticUI = diagnosticCanvas.GetComponentInChildren<DiagnosticUI>();
            
            // Configurar visibilidade inicial
            if (diagnosticCanvas.activeSelf != enableOnStart)
            {
                diagnosticCanvas.SetActive(enableOnStart);
            }
        }
        else
        {
            // Criar canvas e componentes necessários em runtime
            diagnosticCanvas = new GameObject("DiagnosticCanvas");
            Canvas canvas = diagnosticCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            CanvasScaler scaler = diagnosticCanvas.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;
            
            diagnosticCanvas.AddComponent<GraphicRaycaster>();
            
            // Criar o painel de fundo
            GameObject panel = new GameObject("DiagnosticPanel");
            panel.transform.SetParent(diagnosticCanvas.transform, false);
            
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(0.3f, 0.2f);
            panelRect.localPosition = new Vector3(0f, 0f, 0.5f);
            
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.7f);
            
            // Criar texto de debug
            GameObject textObj = new GameObject("DebugText");
            textObj.transform.SetParent(panel.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(0.28f, 0.18f);
            textRect.localPosition = Vector3.zero;
            
            TextMeshProUGUI debugText = textObj.AddComponent<TextMeshProUGUI>();
            debugText.fontSize = 0.01f;
            debugText.color = Color.white;
            debugText.alignment = TextAlignmentOptions.TopLeft;
            debugText.text = "Carregando diagnóstico...";
            
            // Adicionar componente DiagnosticUI
            diagnosticUI = panel.AddComponent<DiagnosticUI>();
            diagnosticUI.enabled = enableOnStart;
            
            // Configurar campo de texto
            diagnosticUI.GetType().GetField("debugText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(diagnosticUI, debugText);
            
            // Posicionar na frente da câmera
            if (Camera.main != null)
            {
                Transform camTransform = Camera.main.transform;
                diagnosticCanvas.transform.position = camTransform.position + camTransform.forward * 1.5f;
                diagnosticCanvas.transform.rotation = camTransform.rotation;
            }
            
            diagnosticCanvas.SetActive(enableOnStart);
        }
    }
    
    private void Update()
    {
        // Verificar tecla para alternar visibilidade
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleDiagnosticVisibility();
        }
        
        // Manter o canvas sempre visível no editor durante o desenvolvimento
        #if UNITY_EDITOR
        if (diagnosticCanvas != null && Camera.main != null)
        {
            Transform camTransform = Camera.main.transform;
            diagnosticCanvas.transform.position = camTransform.position + camTransform.forward * 1.5f;
            diagnosticCanvas.transform.rotation = camTransform.rotation;
        }
        #endif
    }
    
    public void ToggleDiagnosticVisibility()
    {
        if (diagnosticCanvas != null)
        {
            diagnosticCanvas.SetActive(!diagnosticCanvas.activeSelf);
            
            if (diagnosticUI != null)
            {
                diagnosticUI.enabled = diagnosticCanvas.activeSelf;
            }
        }
    }
} 