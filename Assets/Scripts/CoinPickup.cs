using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    // Asegúrate de que tu Player tenga el Tag "Player"
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("💎 Objeto recogido, solicitando oro...");

            // Llamamos a la función GrantGold del Manager
            if (PlayfabManager.instance != null)
            {
                PlayfabManager.instance.GrantGold();
            }

            // Destruimos este objeto 3D inmediatamente
            Destroy(gameObject);
        }
    }
}