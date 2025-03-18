using UnityEngine;
using System.Collections;
using TMPro;

public class DiagnosticUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private VRManager vrManager;
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private bool showDebugInfo = true;
    
    private ConnectionHelper connectionHelper;
    private VideoRotationInfo rotationInfo;
    
    [System.Serializable]
    public class VideoRotationInfo
    {
        public float cameraRotationY;
        public float cameraRotationX;
        public float sphereRotationY;
        public float sphereRotationX;
        public float differenceY;
        public float differenceX;
        public bool isLocked;
        public float videoTime;
    }
    
    private void Start()
    {
        if (vrManager == null)
        {
            vrManager = FindObjectOfType<VRManager>();
        }
        
        if (vrManager != null)
        {
            connectionHelper = vrManager.GetComponent<ConnectionHelper>();
        }
        
        rotationInfo = new VideoRotationInfo();
        
        StartCoroutine(UpdateDebugInfo());
        
        if (debugText != null)
        {
            debugText.gameObject.SetActive(showDebugInfo);
        }
    }
    
    private IEnumerator UpdateDebugInfo()
    {
        while (true)
        {
            UpdateRotationInfo();
            UpdateDebugText();
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    private void UpdateRotationInfo()
    {
        if (Camera.main == null || vrManager == null) return;
        
        Transform camera = Camera.main.transform;
        Transform videoSphere = vrManager.transform.Find("VideoSphere");
        
        if (camera != null && videoSphere != null)
        {
            // Obtém as rotações em graus
            rotationInfo.cameraRotationY = camera.eulerAngles.y;
            rotationInfo.cameraRotationX = camera.eulerAngles.x;
            rotationInfo.sphereRotationY = videoSphere.eulerAngles.y;
            rotationInfo.sphereRotationX = videoSphere.eulerAngles.x;
            
            // Calcula as diferenças (normalizadas entre -180 e 180)
            rotationInfo.differenceY = Mathf.DeltaAngle(rotationInfo.cameraRotationY, rotationInfo.sphereRotationY);
            rotationInfo.differenceX = Mathf.DeltaAngle(rotationInfo.cameraRotationX, rotationInfo.sphereRotationX);
            
            // Verifica se o SmoothOrbitFollower está ativo (bloqueio)
            SmoothOrbitFollower follower = videoSphere.GetComponent<SmoothOrbitFollower>();
            rotationInfo.isLocked = follower != null && follower.enabled;
            
            // Obtém o tempo do vídeo se estiver reproduzindo
            UnityEngine.Video.VideoPlayer videoPlayer = vrManager.GetComponentInChildren<UnityEngine.Video.VideoPlayer>();
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                rotationInfo.videoTime = (float)videoPlayer.time;
            }
        }
    }
    
    private void UpdateDebugText()
    {
        if (debugText == null || !showDebugInfo) return;
        
        string connectionStatus = "Desconectado";
        if (connectionHelper != null && connectionHelper.IsConnected)
        {
            connectionStatus = "Conectado";
        }
        
        string lockStatus = rotationInfo.isLocked ? "Bloqueado" : "Livre";
        
        string debugInfo = $"Status: {connectionStatus}\n" +
                          $"Rotação: {lockStatus}\n" +
                          $"Tempo: {rotationInfo.videoTime:F2}s\n\n" +
                          $"Câmera Y: {rotationInfo.cameraRotationY:F1}°\n" +
                          $"Câmera X: {rotationInfo.cameraRotationX:F1}°\n" +
                          $"Esfera Y: {rotationInfo.sphereRotationY:F1}°\n" +
                          $"Esfera X: {rotationInfo.sphereRotationX:F1}°\n\n" +
                          $"Diff Y: {rotationInfo.differenceY:F1}°\n" +
                          $"Diff X: {rotationInfo.differenceX:F1}°";
        
        debugText.text = debugInfo;
    }
    
    public void ToggleDebugInfo()
    {
        showDebugInfo = !showDebugInfo;
        
        if (debugText != null)
        {
            debugText.gameObject.SetActive(showDebugInfo);
        }
    }
} 