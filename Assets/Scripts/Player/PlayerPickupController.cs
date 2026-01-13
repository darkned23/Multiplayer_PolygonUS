using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

// Aseguramos que el componente PlayerInput exista, ya que lo necesitamos para leer el botón de interacción.
[RequireComponent(typeof(PlayerInput))]
public class PlayerPickupController : MonoBehaviourPun
{
    [Header("Configuración de Interacción")]
    [Tooltip("Radio de la esfera de detección para encontrar objetos agarrables.")]
    [SerializeField] private float pickupRadius = 2f;

    [Tooltip("Capas que se considerarán objetos interactuables.")]
    [SerializeField] private LayerMask interactLayer;

    [Tooltip("El Transform vacío donde se ubicará el objeto al ser agarrado.")]
    [SerializeField] private Transform handPoint;

    [Tooltip("Fuerza con la que se lanzará el objeto.")]
    [SerializeField] private float throwForce = 15f; // Aumentado un poco para compensar la masa/drag del objeto

    // Estado interno
    private NetworkGrabbable currentObject; // Referencia al objeto que tenemos en la mano actualmente
    private PlayerInput playerInput;

    private void Start()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();

        // CONFIGURACIÓN CRÍTICA DE RED:
        // Renombramos el punto de agarre a "HandMount".
        // Esto es necesario porque el script 'NetworkGrabbable' usa una búsqueda recursiva
        // (FindHandRecursive) buscando un objeto con este nombre exacto para emparentarse.
        if (handPoint != null)
        {
            handPoint.name = "HandMount";
        }
    }

    private void Update()
    {
        // 1. GUARDIA DE PROPIEDAD
        // Solo el dueño de este personaje puede procesar inputs de interacción.
        if (!photonView.IsMine) return;

        // 2. DETECCIÓN DE INPUT
        // Usamos "WasPressedThisFrame" para evitar que se ejecute múltiples veces si se mantiene el botón.
        if (playerInput.actions["Interact"].WasPressedThisFrame())
        {
            if (currentObject == null)
            {
                // Si tengo las manos vacías -> Intento agarrar algo
                TryPickup();
            }
            else
            {
                // Si tengo algo en la mano -> Lo lanzo
                ThrowItem();
            }
        }
    }

    /// <summary>
    /// Intenta encontrar el objeto 'NetworkGrabbable' más cercano dentro del radio de interacción.
    /// </summary>
    private void TryPickup()
    {
        // Lanzamos una esfera invisible para detectar colisionadores en la capa 'interactLayer'
        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRadius, interactLayer);

        NetworkGrabbable closest = null;
        float minDst = float.MaxValue;

        // Iteramos sobre todo lo que tocamos para encontrar el más cercano
        foreach (Collider col in colliders)
        {
            // Verificamos si el objeto tiene el script necesario
            NetworkGrabbable grabbable = col.GetComponent<NetworkGrabbable>();

            if (grabbable != null)
            {
                float dst = Vector3.Distance(transform.position, col.transform.position);

                // Algoritmo simple de "vecino más cercano"
                if (dst < minDst)
                {
                    minDst = dst;
                    closest = grabbable;
                }
            }
        }

        // Si encontramos un objeto válido...
        if (closest != null)
        {
            currentObject = closest;

            // LLAMADA A LA RED:
            // Enviamos nuestro ViewID. El objeto usará este ID para encontrar nuestro PhotonView,
            // buscar el "HandMount" y emparentarse a él mediante un RPC.
            currentObject.Pickup(photonView.ViewID);
        }
    }

    /// <summary>
    /// Calcula la fuerza y libera el objeto actual.
    /// </summary>
    private void ThrowItem()
    {
        if (currentObject != null)
        {
            // 1. CÁLCULO FÍSICO
            // Calculamos un vector hacia adelante y un poco hacia arriba para crear un arco parabólico.
            Vector3 forceVector = (transform.forward + Vector3.up * 0.2f).normalized * throwForce;

            // 2. SINCRONIZACIÓN DE LANZAMIENTO
            // Llamamos a Drop en el objeto. Esto ejecutará un RPC en todos los clientes
            // que reactivará las físicas, soltará el objeto y le aplicará la velocidad (LinearVelocity).
            currentObject.Drop(forceVector);

            // 3. LIMPIEZA LOCAL
            // Olvidamos la referencia inmediatamente.
            currentObject = null;
        }
    }

#if UNITY_EDITOR
    // Opcional: Para dibujar el radio de interacción en el editor y ver qué estamos tocando
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
#endif
}