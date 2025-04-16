using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class SimpleImageIncrementer : MonoBehaviour
{
    [SerializeField, Tooltip("ARTrackedImageManager component from the scene")]
    private ARTrackedImageManager trackedImageManager;

    [SerializeField, Tooltip("Base API URL for increment endpoint")]
    private string baseApiUrl = "http://192.168.88.234:5000/increment/";

    [SerializeField, Tooltip("Reference to the UserRegister component")]
    private UserRegister userRegister;

    // To avoid multiple calls for the same image during a session
    private HashSet<string> incrementedReferences = new HashSet<string>();

    private string userId;

    void Start()
    {
        if (userRegister == null)
        {
            Debug.LogError("UserRegister component is not assigned.");
        }
        else
        {
            userId = userRegister.GetUserId();
        }
    }

    void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var image in eventArgs.added)
        {
            ProcessImage(image);
        }
        foreach (var image in eventArgs.updated)
        {
            ProcessImage(image);
        }
    }

    private void ProcessImage(ARTrackedImage image)
    {
        if (image.trackingState == TrackingState.Tracking)
        {
            string refName = image.referenceImage.name;
            if (!incrementedReferences.Contains(refName))
            {
                incrementedReferences.Add(refName);
                StartCoroutine(CallIncrement(refName));
            }
        }
    }

    private IEnumerator CallIncrement(string refName)
    {
        // Prepare the form data including the user_id
        WWWForm form = new WWWForm();
        form.AddField("user_id", userId);

        string postUrl = baseApiUrl + refName;
        UnityWebRequest request = UnityWebRequest.Post(postUrl, form);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error calling increment for " + refName + ": " + request.error);
        }
        else
        {
            Debug.Log("Increment for " + refName + " was successful.");
        }
    }
}
