using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class SessionTimer : MonoBehaviour
{
    private float sessionStartTime;
    [SerializeField, Tooltip("URL base for the session time update endpoint")]
    private string updateTimeUrl = "http://192.168.88.234:5000/updateTime/";

    private string userId;
    private UserRegister userRegister;

    void Start()
    {
        // Record the session start time
        sessionStartTime = Time.time;

        // Find the UserRegister component in the scene
        userRegister = FindObjectOfType<UserRegister>();
        if (userRegister != null)
        {
            userId = userRegister.GetUserId();
        }
        else
        {
            Debug.LogError("UserRegister component not found in the scene.");
        }
    }

    void OnApplicationQuit()
    {
        // Calculate session duration
        float sessionDuration = Time.time - sessionStartTime;
        StartCoroutine(SendSessionTime(sessionDuration));
    }

    private IEnumerator SendSessionTime(float duration)
    {
        string url = updateTimeUrl + userId;
        WWWForm form = new WWWForm();
        // Send the session time as a string
        form.AddField("session_time", duration.ToString());

        UnityWebRequest request = UnityWebRequest.Post(url, form);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error updating session time: " + request.error);
        }
        else
        {
            Debug.Log("Session time updated successfully: " + duration + " seconds.");
        }
    }
}
