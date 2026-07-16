using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimiento realista de excavadora de orugas (skid steering).
/// Se adapta automaticamente si se coloca en el root o en un hijo.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ExcavatorMovement : MonoBehaviour
{
    [Header("Velocidades de Orugas")]
    public float maxSpeed = 3.5f;
    public float pivotRotationSpeed = 35f;
    public float turnRotationSpeed = 25f;

    [Header("Inercia y Peso")]
    public float accelerationTime = 1.2f;
    public float brakeTime = 0.8f;
    public float rotationSmoothing = 8f;


    // Componentes
    private Rigidbody rb;

    // El Bull Dozer 2 tiene el FBX rotado: su frente es el eje X local (right).
    // El Excavator Pack tiene rotacion identity: su frente es el eje Z (forward).
    private bool useRightAsForward;

    // Inputs suavizados
    private float currentSpeed;
    private float currentRotation;
    private float targetSpeed;
    private float targetRotation;

    // Velocidades de cada oruga
    private float leftTrackSpeed;
    private float rightTrackSpeed;

    void Awake()
    {
        Transform root = GetExcavatorRoot();
        
        // Si el script esta en un hijo (como Body.003), configuramos la fisica en el root
        if (gameObject != root.gameObject)
        {
            Rigidbody localRb = GetComponent<Rigidbody>();
            if (localRb != null)
            {
                localRb.isKinematic = true;
                localRb.useGravity = false;
            }
            
            rb = root.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = root.gameObject.AddComponent<Rigidbody>();
            }
        }
        else
        {
            rb = GetComponent<Rigidbody>();
        }

        rb.mass = 5000f;
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.linearDamping = 2f;
        rb.angularDamping = 5f;

        // Detectar eje de avance segun el modelo
        useRightAsForward = IsRootBullDozer(root);

        // Asegurar que el root tenga collider
        if (root.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider col = root.gameObject.AddComponent<BoxCollider>();
            Bounds bounds = new Bounds(root.position, Vector3.zero);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                foreach (Renderer r in renderers)
                    bounds.Encapsulate(r.bounds);
                col.center = root.InverseTransformPoint(bounds.center);
                col.size = bounds.size;
            }
            else
            {
                col.size = new Vector3(2, 2, 4);
            }
        }
    }

    void Start()
    {
    }


    void Update()
    {
        ReadInput();
        CalculateTrackSpeeds();
    }

    void FixedUpdate()
    {
        ApplyMovement();
        ApplyRotation();
    }

    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float rawForward = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
            rawForward = 1f;
        else if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
            rawForward = -1f;

        float rawTurn = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
            rawTurn = 1f;
        else if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
            rawTurn = -1f;

        targetSpeed = rawForward;
        targetRotation = rawTurn;
    }

    private void CalculateTrackSpeeds()
    {
        float accelRate = 1f / accelerationTime;
        float brakeRate = 1f / brakeTime;

        if (Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed))
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);
        else
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, brakeRate * Time.deltaTime);

        currentRotation = Mathf.Lerp(currentRotation, targetRotation, rotationSmoothing * Time.deltaTime);

        leftTrackSpeed = Mathf.Clamp(currentSpeed + currentRotation, -1f, 1f);
        rightTrackSpeed = Mathf.Clamp(currentSpeed - currentRotation, -1f, 1f);
    }

    private void ApplyMovement()
    {
        float forwardSpeed = (leftTrackSpeed + rightTrackSpeed) * 0.5f;
        // Bull Dozer 2 usa X (right); Excavator Pack usa Z (forward)
        Vector3 dir = useRightAsForward ? rb.transform.right : rb.transform.forward;
        rb.MovePosition(rb.position + dir * forwardSpeed * maxSpeed * Time.fixedDeltaTime);
    }

    private void ApplyRotation()
    {
        float trackDifference = (leftTrackSpeed - rightTrackSpeed) * 0.5f;
        if (Mathf.Abs(trackDifference) < 0.01f) return;

        bool isMoving = Mathf.Abs(currentSpeed) > 0.1f;
        float rotSpeed = isMoving ? turnRotationSpeed : pivotRotationSpeed;

        float giro = trackDifference * rotSpeed * Time.fixedDeltaTime;
        Quaternion nuevaRotacion = rb.rotation * Quaternion.Euler(0f, giro, 0f);
        rb.MoveRotation(nuevaRotacion);
    }


    private Transform GetExcavatorRoot()
    {
        Transform current = transform;
        Transform bestRoot = transform;
        while (current != null)
        {
            if (current.name.Contains("Bull Dozer") ||
                current.name.Contains("Excavator") ||
                current.name.Contains("Caterpillar") ||
                current.GetComponent<Rigidbody>() != null)
            {
                bestRoot = current;
            }
            current = current.parent;
        }
        return bestRoot;
    }

    /// <summary>Devuelve true si el root es el modelo Bull Dozer 2 (frente en eje X).</summary>
    private bool IsRootBullDozer(Transform root)
    {
        foreach (Transform child in root)
            if (child.name == "Body.003" || child.name == "Main_Forks") return true;
        return false;
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }


    public float GetLeftTrackSpeed() => leftTrackSpeed;
    public float GetRightTrackSpeed() => rightTrackSpeed;
}
