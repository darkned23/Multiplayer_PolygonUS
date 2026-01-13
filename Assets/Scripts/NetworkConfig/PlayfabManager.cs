using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using TMPro; 
using System.Collections.Generic;

public class PlayfabManager : MonoBehaviour
{
    // Singleton: Para poder acceder a este script desde el objeto 3D fácilmente
    public static PlayfabManager instance;

    [Header("UI")]
    public TMP_Text goldText; // Arrastra tu texto de UI aquí en el Inspector

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        Login();
    }

    // --- AUTENTICACIÓN ---
    void Login()
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = true
        };
        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnError);
    }

    void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("✅ Login Correcto");
        GetVirtualCurrencies(); // 2. Al loguear, pedimos el saldo actual
    }

    // --- OBTENER SALDO INICIAL ---
    void GetVirtualCurrencies()
    {
        PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(),
        result => {
            int coins = result.VirtualCurrency.ContainsKey("GD") ? result.VirtualCurrency["GD"] : 0;
            UpdateUI(coins);
        }, OnError);
    }

    // --- SOLICITUD AL SERVIDOR (GRANT GOLD) ---
    public void GrantGold()
    {
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "grantGold",
            GeneratePlayStreamEvent = true
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnCloudScriptSuccess, OnError);
    }

    void OnCloudScriptSuccess(ExecuteCloudScriptResult result)
    {
        if (result.Error != null)
        {
            Debug.LogError("❌ Error JS: " + result.Error.Message);
            return;
        }

        // 3. Leer la respuesta del CloudScript (newBalance) para actualizar la UI
        // PlayFab devuelve un JsonObject, lo convertimos a Diccionario para leerlo fácil
        var jsonResult = (PlayFab.Json.JsonObject)result.FunctionResult;

        if (jsonResult.TryGetValue("newBalance", out object balance))
        {
            // Convertimos el objeto a int y actualizamos UI
            UpdateUI(System.Convert.ToInt32(balance));
            Debug.Log("💰 Oro actualizado: " + balance);
        }
    }

    void OnError(PlayFabError error)
    {
        Debug.LogError("⛔ Error API: " + error.GenerateErrorReport());
    }

    // --- ACTUALIZAR UI ---
    void UpdateUI(int amount)
    {
        if (goldText != null)
            goldText.text = "Oro: " + amount.ToString();
    }
}