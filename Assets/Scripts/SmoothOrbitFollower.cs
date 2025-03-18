using UnityEngine;

public class SmoothOrbitFollower : MonoBehaviour
{
    public Transform target;
    public float followSpeed = 2.0f;
    public float maxDistance = 45.0f;
    public bool inverseRotation = true;
    public bool lockVertical = false;
    public bool lockHorizontal = false;
    
    private Quaternion targetRotation;
    private Quaternion initialRotation;
    private Vector3 initialForward;
    
    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("SmoothOrbitFollower: Target transform não definido!");
            enabled = false;
            return;
        }
        
        initialRotation = transform.rotation;
        initialForward = transform.forward;
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        // Calcula direção desejada
        Vector3 targetDirection = target.forward;
        
        // Verifica se precisamos de rotação inversa
        if (inverseRotation)
        {
            targetDirection = -targetDirection;
        }
        
        // Aplica restrições de rotação
        if (lockVertical)
        {
            targetDirection.y = initialForward.y;
        }
        
        if (lockHorizontal)
        {
            targetDirection.x = initialForward.x;
        }
        
        // Calcula a rotação alvo
        targetRotation = Quaternion.LookRotation(targetDirection);
        
        // Limita a rotação ao ângulo máximo
        float angle = Quaternion.Angle(initialRotation, targetRotation);
        if (angle > maxDistance)
        {
            targetRotation = Quaternion.RotateTowards(initialRotation, targetRotation, maxDistance);
        }
        
        // Aplica rotação suave
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followSpeed);
    }
    
    public void ResetRotation()
    {
        transform.rotation = initialRotation;
    }
} 