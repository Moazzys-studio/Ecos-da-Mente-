using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class eco_movimento : MonoBehaviour
{
public float verticalSpeed = 10f;       
public float maxVerticalSpeed = 15f;    
public LayerMask groundLayer;           
public float groundCheckDistance = 0.6f;
public float rotationDelay = 0.1f;      
public float rotationDuration = 0.25f;  

private Rigidbody rb;
private bool movingUp = false; 
private Coroutine rotationCoroutine;

void Start()
{
    rb = GetComponent<Rigidbody>();
    rb.useGravity = false; // usamos nosso controle manual
    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    rb.freezeRotation = true;
}

void Update()
{
    // Clique do mouse PC OU toque na tela
    if (Input.GetMouseButtonDown(0))
    {
        movingUp = !movingUp;
        ApplyVerticalVelocity();
        StartSmoothRotation();  // <<< rotação suave com delay
    }
}

void ApplyVerticalVelocity()
{
    float vy = movingUp ? verticalSpeed : -verticalSpeed;
    Vector3 v = rb.velocity;
    v.x = 0f;
    v.z = 0f;
    v.y = Mathf.Clamp(vy, -maxVerticalSpeed, maxVerticalSpeed);
    rb.velocity = v;
}

void StartSmoothRotation()
{
    // Se já estiver rotacionando, para para não bugar
    if (rotationCoroutine != null)
        StopCoroutine(rotationCoroutine);

    rotationCoroutine = StartCoroutine(RotateSmoothly());
}

IEnumerator RotateSmoothly()
{
    // Delay antes de começar a girar
    yield return new WaitForSeconds(rotationDelay);

    float targetZ = movingUp ? 180f : 0f;
    Quaternion startRot = transform.rotation;
    Quaternion endRot = Quaternion.Euler(0f, 90f, targetZ);

    float time = 0f;

    while (time < rotationDuration)
    {
        time += Time.deltaTime;
        transform.rotation = Quaternion.Lerp(startRot, endRot, time / rotationDuration);
        yield return null;
    }

    transform.rotation = endRot; // garante rotação final precisa
}

void FixedUpdate()
{
    // Se estiver encostado no chão e movendo pra baixo, zera velocidade
    if (!movingUp && IsGroundedBelow())
    {
        Vector3 v = rb.velocity;
        v.y = 0f;
        rb.velocity = v;
    }

    // Se estiver encostado no teto e movendo pra cima, zera velocidade
    if (movingUp && IsGroundedAbove())
    {
        Vector3 v = rb.velocity;
        v.y = 0f;
        rb.velocity = v;
    }
}

bool IsGroundedBelow()
{
    // verifica colisão logo abaixo
    return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
}

bool IsGroundedAbove()
{
    // verifica colisão logo acima
    return Physics.Raycast(transform.position, Vector3.up, groundCheckDistance, groundLayer);
}
}
