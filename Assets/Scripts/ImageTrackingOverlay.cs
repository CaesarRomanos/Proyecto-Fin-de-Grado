using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ImageTrackingOverlay : MonoBehaviour
{
    [SerializeField, Tooltip("Componente ARTrackedImageManager de la escena.")]
    private ARTrackedImageManager trackedImageManager;

    [SerializeField, Tooltip("Prefab que se mostrará sobre cualquier imagen reconocida.")]
    private GameObject overlayPrefab;

    [SerializeField, Tooltip("Factor para reducir el tamaño del overlay respecto al tamaño de la imagen de referencia.")]
    private float scaleFactor = 0.1f;

    // Mapea cada ARTrackedImage a su overlay instanciado
    private Dictionary<ARTrackedImage, GameObject> overlays = new Dictionary<ARTrackedImage, GameObject>();

    void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (ARTrackedImage image in eventArgs.added)
        {
            if (image.trackingState == TrackingState.Tracking)
                CreateOrUpdateOverlay(image);
        }

        foreach (ARTrackedImage image in eventArgs.updated)
        {
            if (image.trackingState == TrackingState.Tracking)
                CreateOrUpdateOverlay(image);
            else
                RemoveOverlay(image);
        }

        foreach (ARTrackedImage image in eventArgs.removed)
        {
            RemoveOverlay(image);
        }
    }

    void CreateOrUpdateOverlay(ARTrackedImage trackedImage)
    {
        if (!overlays.ContainsKey(trackedImage))
        {
            GameObject overlayInstance = Instantiate(overlayPrefab, trackedImage.transform.position, Quaternion.Euler(90f, 0f, 0f));
            if (!overlayInstance.GetComponent<Collider>())
                overlayInstance.AddComponent<BoxCollider>();
            overlays[trackedImage] = overlayInstance;
        }

        GameObject instance = overlays[trackedImage];
        instance.transform.position = trackedImage.transform.position;
        instance.transform.rotation = trackedImage.transform.rotation * Quaternion.Euler(90f, 0f, 0f);

        Vector2 size = trackedImage.size;
        instance.transform.localScale = new Vector3(size.x * scaleFactor, size.y * scaleFactor, 1f);
    }

    void RemoveOverlay(ARTrackedImage trackedImage)
    {
        if (overlays.ContainsKey(trackedImage))
        {
            Destroy(overlays[trackedImage]);
            overlays.Remove(trackedImage);
        }
    }
}
