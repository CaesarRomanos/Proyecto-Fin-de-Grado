using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class SimpleImageIncrementer : MonoBehaviour
{
    [SerializeField, Tooltip("Componente ARTrackedImageManager de la escena.")]
    private ARTrackedImageManager trackedImageManager;

    [SerializeField, Tooltip("URL base de la API para incrementar")]
    private string baseApiUrl = "http://192.168.88.234:5000/increment/";

    // Referencia al script que registra al usuario
    [SerializeField, Tooltip("Referencia al componente que registra el usuario único")]
    private UserRegister userRegister;

    // Registro para evitar múltiples llamadas a la misma imagen en esta sesión
    private HashSet<string> incrementedReferences = new HashSet<string>();

    private string userId;

    void Start()
    {
        if (userRegister != null)
        {
            userId = userRegister.GetUserId();
        }
        else
        {
            Debug.LogError("No se ha asignado el componente UserRegister.");
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
        // Se crea un formulario con el user_id
        WWWForm form = new WWWForm();
        form.AddField("user_id", userId);

        string postUrl = baseApiUrl + refName;
        UnityWebRequest request = UnityWebRequest.Post(postUrl, form);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error al llamar a " + postUrl + ": " + request.error);
        }
    }
}
