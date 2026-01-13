using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent (typeof(PhotonTransformView))]
[RequireComponent(typeof(PhotonRigidbodyView))]
public class NetworkGrabbable : MonoBehaviourPun, IPunOwnershipCallbacks
{
    // Referencias a componentes
    private Rigidbody rb;
    private Collider col;
    private PhotonTransformView pTransformView;
    private PhotonRigidbodyView pRigidbodyView;

    // Control de lag para el lanzador
    private int lastThrowerID = -1;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        pTransformView = GetComponent<PhotonTransformView>();
        pRigidbodyView = GetComponent<PhotonRigidbodyView>();
    }

    void OnEnable()
    {
        // Nos suscribimos a los eventos de Photon (para saber cuándo cambia el dueño)
        PhotonNetwork.AddCallbackTarget(this);
    }

    void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    /// <summary>
    /// Llamado por el PlayerPickupController cuando intenta agarrar este objeto.
    /// </summary>
    public void Pickup(int playerViewID)
    {
        // 1. Solicitamos ser el dueño de la física de este objeto
        photonView.RequestOwnership();

        // 2. Avisamos a todos que el objeto ha sido agarrado
        photonView.RPC("RPC_SetHeldState", RpcTarget.AllBuffered, playerViewID);
    }

    /// <summary>
    /// Llamado por el PlayerPickupController al lanzar el objeto.
    /// </summary>
    public void Drop(Vector3 velocityToApply)
    {
        // Enviamos la orden de soltar a todos, incluyendo quién lo lanzó y con qué fuerza
        photonView.RPC("RPC_Release", RpcTarget.AllBuffered, transform.position, transform.rotation, velocityToApply, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    /// <summary>
    /// GESTOR DE FÍSICAS DE RED.
    /// Esta función es el corazón de la solución para evitar el "Jitter" y los errores de Kinematic.
    /// </summary>
    /// <param name="isActive">True = Sincronizar con red. False = Moverse localmente (mano).</param>
    private void SetNetworkPhysics(bool isActive)
    {
        // 1. Limpiamos la lista de observación.
        // Si la lista está vacía, Photon NO intenta leer ni escribir en el Rigidbody.
        photonView.ObservedComponents.Clear();

        if (isActive)
        {
            // Volvemos a añadir los componentes para que se sincronicen
            if (pTransformView != null) photonView.ObservedComponents.Add(pTransformView);
            if (pRigidbodyView != null) photonView.ObservedComponents.Add(pRigidbodyView);
        }

        // 2. Apagamos/Encendemos los componentes visuales.
        // Si no los apagamos, el PhotonTransformView intentará interpolar hacia la posición vieja,
        // peleando contra la mano del jugador.
        if (pTransformView != null) pTransformView.enabled = isActive;
        if (pRigidbodyView != null) pRigidbodyView.enabled = isActive;
    }

    [PunRPC]
    private void RPC_SetHeldState(int playerViewID)
    {
        // 1. Apagamos la red: El objeto ahora es "mudo", solo sigue a la mano.
        SetNetworkPhysics(false);

        // 2. Física local: Kinematic para que no caiga y sin colisión para no estorbar.
        rb.isKinematic = true;
        col.enabled = false;

        // 3. Emparentamiento: Buscamos la mano del jugador que nos agarró.
        PhotonView playerView = PhotonView.Find(playerViewID);
        if (playerView != null)
        {
            Transform hand = FindHandRecursive(playerView.transform, "HandMount");
            if (hand != null)
            {
                transform.SetParent(hand);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }
    }

    [PunRPC]
    private void RPC_Release(Vector3 startPos, Quaternion startRot, Vector3 velocity, int throwerID)
    {
        lastThrowerID = throwerID;

        // Desvinculamos de la mano
        transform.SetParent(null);
        transform.position = startPos;
        transform.rotation = startRot;

        // Reactivamos físicas reales
        rb.isKinematic = false;
        col.enabled = true;

        // Aplicamos la velocidad inicial (Unity 6 usa linearVelocity)
        rb.linearVelocity = velocity;

        // --- LÓGICA ANTI-LAG (PREDICCIÓN) ---
        bool amITheThrower = (PhotonNetwork.LocalPlayer.ActorNumber == throwerID);

        if (amITheThrower && !photonView.IsMine)
        {
            // CASO A: Soy el que lanzó, pero Photon aún no confirma que soy el dueño.
            // Mantengo la sincronización APAGADA para que mi simulación local mande 
            // y el objeto no se "teletransporte" hacia atrás.
            SetNetworkPhysics(false);
        }
        else
        {
            // CASO B: Soy un espectador o el dueño confirmado.
            // Enciendo la sincronización para ver la trayectoria real.
            SetNetworkPhysics(true);
        }
    }

    // --- CALLBACKS DE PROPIEDAD DE PHOTON ---

    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        if (targetView != this.photonView) return;

        // Si Photon me confirma que soy el dueño...
        if (photonView.IsMine)
        {
            // Solo reactivo la sincronización si el objeto está volando (no es kinematic).
            // Si todavía lo tengo en la mano, DEBE seguir apagado.
            if (!rb.isKinematic)
            {
                SetNetworkPhysics(true);
            }
        }
    }

    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer) { }

    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
    {
        // Red de seguridad: Si falla la transferencia y está volando, reactivamos para no romperlo.
        if (targetView == this.photonView && !rb.isKinematic)
        {
            SetNetworkPhysics(true);
        }
    }

    // Utilidad para encontrar el hueso de la mano en la jerarquía del jugador
    private Transform FindHandRecursive(Transform current, string name)
    {
        if (current.name == name) return current;
        foreach (Transform child in current)
        {
            Transform found = FindHandRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}