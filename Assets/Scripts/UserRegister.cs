// This script registers a new user with a backend API when the application starts,
// and sends the session duration when the application is paused.
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class UserRegister : MonoBehaviour
{
    [Header("API URLs")]
    [SerializeField, Tooltip("URL to register new users")]
    private string registerUrl    = "http://192.168.88.234:5000/registerUser/";
    [SerializeField, Tooltip("URL to end the session")]
    private string endSessionUrl  = "http://192.168.88.234:5000/endSession/";

    // Unique identifier for this device/user
    private string userId;
    // Timestamp when the session started
    private float  sessionStartTime;

    void Start()
    {
        // Retrieve the device's unique identifier
        userId = SystemInfo.deviceUniqueIdentifier;
        // Record the start time of the session
        sessionStartTime = Time.time;
        StartCoroutine(RegisterUser());
    }

    // Coroutine to send a POST request to register a new user.
    private IEnumerator RegisterUser()
    {
        // Append the userId to the register URL
        string url = registerUrl + userId;
        using (UnityWebRequest req = UnityWebRequest.Post(url, new WWWForm()))
        {
            // Wait for the request to complete
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError("User register error: " + req.error);
            else
                Debug.Log("User registered: " + userId);
        }
    }

    // Called when the application is paused or resumed.
    // When paused, calculates the session duration and sends it to the server.
    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            // Calculate the session duration in seconds
            float duration = Time.time - sessionStartTime;
            
            // Prepare form data for the POST request
            WWWForm form = new WWWForm();
            form.AddField("user_id", userId);
            form.AddField("duration", duration.ToString());

            // Create and send a synchronous POST request to submit session length
            UnityWebRequest req = UnityWebRequest.Post(endSessionUrl + userId, form);
            req.SendWebRequest();
            // Block until the request completes
            while (!req.isDone) { }

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError("Session length request error: " + req.error);
            else
                Debug.Log($"Session length sent: {duration:F1}s");
        }
    }

    /// Provides access to the stored userId for other scripts.
    public string GetUserId() => userId;
}
