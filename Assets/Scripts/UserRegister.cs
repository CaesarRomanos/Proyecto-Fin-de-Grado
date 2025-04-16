using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class UserRegister : MonoBehaviour
{
    [SerializeField, Tooltip("URL base de la API para registrar usuarios")]
    private string registerUrl = "http://192.168.88.234:5000/registerUser/";

    private string userId;

    void Start()
    {
        // Obtiene un identificador único del dispositivo
        userId = SystemInfo.deviceUniqueIdentifier;
        StartCoroutine(RegisterUser());
    }

    private IEnumerator RegisterUser()
    {
        string url = registerUrl + userId;
        UnityWebRequest request = UnityWebRequest.Post(url, new WWWForm());
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error registrando usuario: " + request.error);
        }
        else
        {
            Debug.Log("Usuario registrado: " + userId);
        }
    }

    public string GetUserId()
    {
        return userId;
    }
}
