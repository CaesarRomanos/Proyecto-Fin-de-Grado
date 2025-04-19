// UserRegister.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class UserRegister : MonoBehaviour
{
    [Header("API URLs")]
    [SerializeField, Tooltip("URL base para registrar usuarios (termina con '/')")]
    private string registerUrl    = "http://192.168.88.234:5000/registerUser/";
    [SerializeField, Tooltip("URL base para terminar sesión (termina con '/')")]
    private string endSessionUrl  = "http://192.168.88.234:5000/endSession/";

    private string userId;
    private float  sessionStartTime;

    void Start()
    {
        userId           = SystemInfo.deviceUniqueIdentifier;
        sessionStartTime = Time.time;
        StartCoroutine(RegisterUser());
    }

    private IEnumerator RegisterUser()
    {
        string url = registerUrl + userId;
        using (UnityWebRequest req = UnityWebRequest.Post(url, new WWWForm()))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError("Error registrando usuario: " + req.error);
            else
                Debug.Log("Usuario registrado: " + userId);
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            float duration = Time.time - sessionStartTime;
            
            // Formulario
            WWWForm form = new WWWForm();
            form.AddField("user_id", userId);
            form.AddField("duration", duration.ToString());

            // Petición síncrona
            UnityWebRequest req = UnityWebRequest.Post(endSessionUrl + userId, form);
            req.SendWebRequest();
            // Bloquea hasta que termine
            while (!req.isDone) { }

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError("Error enviando duración de sesión: " + req.error);
            else
                Debug.Log($"Duración enviada: {duration:F1}s");
        }
    }

    // Si otros scripts necesitan el userId
    public string GetUserId() => userId;
}
