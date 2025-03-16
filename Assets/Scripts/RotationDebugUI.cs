using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
public class RotationDebugUI : EditorWindow
{
    private static RotationDebugUI window;
    private VRManager vrManager;
    private Vector2 scrollPosition;
    private bool showDebugUI = true;
    private bool showCameraInfo = true;
    private bool showVideoSphereInfo = true;

    [MenuItem("Window/VR/Rotation Debug")]
    public static void ShowWindow()
    {
        window = GetWindow<RotationDebugUI>("VR Rotation Debug");
        window.minSize = new Vector2(300, 400);
        window.maxSize = new Vector2(500, 800);
    }

    void OnGUI()
    {
        if (vrManager == null)
        {
            vrManager = FindObjectOfType<VRManager>();
            if (vrManager == null)
            {
                EditorGUILayout.HelpBox("VRManager não encontrado na cena!", MessageType.Warning);
                return;
            }
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Debug Controls", EditorStyles.boldLabel);
            
            bool newShowDebug = EditorGUILayout.Toggle("Show Debug UI", vrManager.enableRotationDebug);
            if (newShowDebug != vrManager.enableRotationDebug)
            {
                Undo.RecordObject(vrManager, "Toggle Debug UI");
                vrManager.enableRotationDebug = newShowDebug;
                EditorUtility.SetDirty(vrManager);
            }

            bool newLockRotation = EditorGUILayout.Toggle("Lock Rotation", vrManager.isRotationLocked);
            if (newLockRotation != vrManager.isRotationLocked)
            {
                Undo.RecordObject(vrManager, "Toggle Rotation Lock");
                vrManager.isRotationLocked = newLockRotation;
                EditorUtility.SetDirty(vrManager);
            }
        }

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Rotation Limits", EditorStyles.boldLabel);

            float newVerticalAngle = EditorGUILayout.Slider("Max Vertical Angle", vrManager.maxVerticalAngle, 0, 180);
            if (newVerticalAngle != vrManager.maxVerticalAngle)
            {
                Undo.RecordObject(vrManager, "Change Vertical Angle");
                vrManager.maxVerticalAngle = newVerticalAngle;
                EditorUtility.SetDirty(vrManager);
            }

            float newHorizontalAngle = EditorGUILayout.Slider("Max Horizontal Angle", vrManager.maxHorizontalAngle, 0, 180);
            if (newHorizontalAngle != vrManager.maxHorizontalAngle)
            {
                Undo.RecordObject(vrManager, "Change Horizontal Angle");
                vrManager.maxHorizontalAngle = newHorizontalAngle;
                EditorUtility.SetDirty(vrManager);
            }
        }

        if (vrManager.enableRotationDebug)
        {
            EditorGUILayout.Space(10);
            showCameraInfo = EditorGUILayout.Foldout(showCameraInfo, "Camera Information", true);
            if (showCameraInfo)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    if (vrManager.mainCamera != null)
                    {
                        EditorGUILayout.LabelField("Active Camera:", vrManager.mainCamera.name);
                        EditorGUILayout.Vector3Field("Position", vrManager.mainCamera.transform.position);
                        EditorGUILayout.Vector3Field("Rotation", vrManager.mainCamera.transform.eulerAngles);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Camera não configurada!", MessageType.Warning);
                    }
                }
            }

            EditorGUILayout.Space(5);
            showVideoSphereInfo = EditorGUILayout.Foldout(showVideoSphereInfo, "VideoSphere Information", true);
            if (showVideoSphereInfo)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    if (vrManager.videoSphere != null)
                    {
                        EditorGUILayout.Vector3Field("Position", vrManager.videoSphere.position);
                        Vector3 angles = vrManager.videoSphere.localEulerAngles;
                        EditorGUILayout.LabelField($"X (Vertical): {angles.x:F1}°");
                        EditorGUILayout.LabelField($"Y (Horizontal): {angles.y:F1}°");
                        EditorGUILayout.LabelField($"Z: {angles.z:F1}°");

                        // Mostrar diferença entre câmera e VideoSphere
                        if (vrManager.mainCamera != null)
                        {
                            Vector3 relativeDiff = vrManager.mainCamera.transform.eulerAngles - angles;
                            EditorGUILayout.Space(5);
                            EditorGUILayout.LabelField("Diferença Relativa:", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField($"X: {relativeDiff.x:F1}°");
                            EditorGUILayout.LabelField($"Y: {relativeDiff.y:F1}°");
                            EditorGUILayout.LabelField($"Z: {relativeDiff.z:F1}°");
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("VideoSphere não encontrado!", MessageType.Warning);
                    }
                }
            }
        }

        EditorGUILayout.EndScrollView();

        // Auto-refresh a cada frame no editor
        Repaint();
    }
}
#endif 