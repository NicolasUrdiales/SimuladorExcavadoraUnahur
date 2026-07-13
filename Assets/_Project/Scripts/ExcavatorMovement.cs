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

    [Header("Animacion de Orugas (auto-detectadas)")]
    public float trackScrollSpeed = 2f;
    public float wheelRotationSpeed = 360f;

    // Componentes
    private Rigidbody rb;

    // Inputs suavizados
    private float currentSpeed;
    private float currentRotation;
    private float targetSpeed;
    private float targetRotation;

    // Velocidades de cada oruga
    private float leftTrackSpeed;
    private float rightTrackSpeed;

    // Auto-detectados
    private Renderer trackRenderer;
    private Material trackMaterial;
    private Transform[] wheelTransforms;
    private float trackOffset;

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

        // Asegurar que el root tenga collider
        if (root.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider col = root.gameObject.AddComponent<BoxCollider>();
            Bounds bounds = new Bounds(root.position, Vector3.zero);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);
            col.center = root.InverseTransformPoint(bounds.center);
            col.size = bounds.size;
        }
    }

    void Start()
    {
        AutoDetectParts();
    }

    private void AutoDetectParts()
    {
        Transform root = GetExcavatorRoot();

        // Buscar el renderer de orugas/tracks (Wheel)
        Transform wheel = FindChildRecursive(root, "Wheel");
        if (wheel != null)
        {
            trackRenderer = wheel.GetComponent<Renderer>();
            if (trackRenderer != null)
            {
                trackMaterial = trackRenderer.material; // crea instancia
                Debug.Log("[ExcavatorMovement] Track encontrado: " + wheel.name);
            }
        }

        // Buscar Inside_Wheels y sus hijos como ruedas para animar
        Transform insideWheels = FindChildRecursive(root, "Inside_Wheels");
        if (insideWheels != null)
        {
            wheelTransforms = insideWheels.GetComponentsInChildren<Transform>();
            Debug.Log("[ExcavatorMovement] Ruedas encontradas: " + wheelTransforms.Length);
        }
    }

    void Update()
    {
        ReadInput();
        CalculateTrackSpeeds();
        AnimateTracks();
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
        // El frente del modelo apunta en el eje X (flecha roja) del root Rigidbody
        Vector3 movimiento = rb.transform.right * forwardSpeed * maxSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movimiento);
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

    private void AnimateTracks()
    {
        float avgSpeed = (leftTrackSpeed + rightTrackSpeed) * 0.5f;

        // Scroll UV de las orugas
        if (trackMaterial != null)
        {
            trackOffset += avgSpeed * trackScrollSpeed * Time.deltaTime;
            trackMaterial.mainTextureOffset = new Vector2(trackOffset, 0f);
        }

        // Girar ruedas visualmente
        if (wheelTransforms != null)
        {
            float rotAmount = avgSpeed * wheelRotationSpeed * Time.deltaTime;
            foreach (Transform w in wheelTransforms)
            {
                if (w != null && w != transform && w != rb.transform)
                    w.Rotate(Vector3.right, rotAmount, Space.Self);
            }
        }
    }

    private Transform GetExcavatorRoot()
    {
        Transform current = transform;
        Transform bestRoot = transform;
        while (current != null)
        {
            if (current.name.Contains("Bull Dozer") || current.name.Contains("Excavator") || current.GetComponent<Rigidbody>() != null)
            {
                bestRoot = current;
            }
            current = current.parent;
        }
        return bestRoot;
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

    void OnDestroy()
    {
        if (trackMaterial != null) Destroy(trackMaterial);
    }

    public float GetLeftTrackSpeed() => leftTrackSpeed;
    public float GetRightTrackSpeed() => rightTrackSpeed;
}
