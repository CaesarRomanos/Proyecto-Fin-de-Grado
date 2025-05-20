// This script listens for ARTrackedImage events and sends a one-time increment
// call to a backend API whenever a new reference image is detected and tracked.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class SimpleImageIncrementer : MonoBehaviour
{
    [SerializeField, Tooltip("ARTrackedImageManager component for tracking AR images.")]
    private ARTrackedImageManager trackedImageManager;

    [SerializeField, Tooltip("Base API URL to increment when a target image is detected.")]
    private string baseApiUrl = "http://192.168.88.234:5000/increment/";

    [SerializeField, Tooltip("Reference to the UserRegister component for user ID retrieval.")]
    private UserRegister userRegister;

    // HashSet to store the names of reference images that have already been processed
    // Prevents sending duplicate increment requests for the same image within one session
    private HashSet<string> incrementedReferences = new HashSet<string>();

    // Unique device/user identifier obtained from UserRegister
    private string userId;

    void Start()
    {
        // Retrieve the userId from the UserRegister component or log an error if missing
        if (userRegister != null)
        {
            userId = userRegister.GetUserId();
        }
        else
        {
            Debug.LogError("UserRegister component is not assigned.");
        }
    }

    void OnEnable()
    {
        // Subscribe to ARTrackedImageManager's trackedImagesChanged event
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable()
    {
        // Unsubscribe to avoid memory leaks when the object is disabled
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    // Handles ARTrackedImage events for newly added or updated images.
    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // Process only new or updated tracked images
        foreach (var image in eventArgs.added)
        {
            ProcessImage(image);
        }
        foreach (var image in eventArgs.updated)
        {
            ProcessImage(image);
        }
    }

    // If the image is actively being tracked, initiates a backend increment request
    // for that reference image if not already done in this session.
    private void ProcessImage(ARTrackedImage image)
    {
        if (image.trackingState == TrackingState.Tracking)
        {
            string refName = image.referenceImage.name;
            // Only send one increment request per reference per session
            if (!incrementedReferences.Contains(refName))
            {
                incrementedReferences.Add(refName);
                StartCoroutine(CallIncrement(refName));
            }
        }
    }

    // Coroutine to send a POST request to the API increment endpoint,
    // including the user_id in the form data.
    private IEnumerator CallIncrement(string refName)
    {
        // Prepare form data with the user identifier
        WWWForm form = new WWWForm();
        form.AddField("user_id", userId);

        // Build the full endpoint URL by appending the reference image name
        string postUrl = baseApiUrl + refName;
        UnityWebRequest request = UnityWebRequest.Post(postUrl, form);

        // Await completion of the web request
        yield return request.SendWebRequest();

        // Check for request errors and log if any occur
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error calling " + postUrl + ": " + request.error);
        }
    }
}
