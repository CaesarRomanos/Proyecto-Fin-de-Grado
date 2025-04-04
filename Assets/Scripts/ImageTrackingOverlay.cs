using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ImageTrackingOverlay : MonoBehaviour
{
    [SerializeField, Tooltip("Componente ARTrackedImageManager de la escena.")]
    private ARTrackedImageManager trackedImageManager;

    [SerializeField, Tooltip("Prefab con la imagen de reconstrucción para fecha gótica")]
    private GameObject overlayPrefab1;

    [SerializeField, Tooltip("Prefab con la imagen de reconstrucción para soldado")]
    private GameObject overlayPrefab2;

    [SerializeField, Tooltip("Prefab con la imagen de reconstrucción para monje")]
    private GameObject overlayPrefab3;

    [SerializeField, Tooltip("Factor para reducir el tamaño del overlay respecto al tamaño de la imagen de referencia.")]
    private float scaleFactor = 0.1f;

    // Almacena el overlay instanciado para cada ARTrackedImage.
    private Dictionary<ARTrackedImage, GameObject> overlays = new Dictionary<ARTrackedImage, GameObject>();

    // Para cada pool, se guarda la referencia del tracked image que actualmente “dirige” el overlay.
    private Dictionary<string, ARTrackedImage> activePoolImages = new Dictionary<string, ARTrackedImage>();

    // Definición de pools: cada uno se identifica por el nombre de referencia.
    private List<string> datePool = new List<string> { "irlDate" };
    private List<string> soldierPool = new List<string> { "irlSoldier" };
    private List<string> monkPool = new List<string> { "irlMonk" };

    void OnEnable() {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable() {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // Procesamos las imágenes agregadas y actualizadas para actualizar candidatos.
        foreach (ARTrackedImage image in eventArgs.added)
        {
            ProcessTrackedImage(image);
        }
        foreach (ARTrackedImage image in eventArgs.updated)
        {
            ProcessTrackedImage(image);
        }
        foreach (ARTrackedImage image in eventArgs.removed)
        {
            RemoveOverlay(image);
        }
        // Para cada pool, seleccionamos el candidato activo y actualizamos (o creamos) el overlay.
        UpdatePoolOverlays();
    }

    // Para cada tracked image en Tracking, se guarda como candidato del pool correspondiente.
    void ProcessTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.trackingState != TrackingState.Tracking)
            return;
        string poolKey = GetPoolKey(trackedImage.referenceImage.name);
        if (string.IsNullOrEmpty(poolKey))
            return;

        // En este ejemplo, simplemente reemplazamos el candidato por el último en Tracking.
        activePoolImages[poolKey] = trackedImage;
    }

    // Según el nombre, devuelve el pool al que pertenece.
    string GetPoolKey(string refName)
    {
        if (datePool.Contains(refName))
            return "irlDate";
        else if (soldierPool.Contains(refName))
            return "irlSoldier";
        else if (monkPool.Contains(refName))
            return "irlMonk";
        return null;
    }

    // Revisa para cada pool si el tracked image candidato difiere del que ya está generando el overlay.
    // Si es distinto, se elimina el overlay anterior y se crea uno nuevo.
    void UpdatePoolOverlays()
    {
        foreach (string poolKey in new string[] { "irlDate", "irlSoldier", "irlMonk" })
        {
            ARTrackedImage candidate = null;
            activePoolImages.TryGetValue(poolKey, out candidate);

            // Si no hay candidato para este pool, eliminamos cualquier overlay existente.
            if (candidate == null)
            {
                List<ARTrackedImage> keysToRemove = new List<ARTrackedImage>();
                foreach (var kvp in overlays)
                {
                    if (GetPoolKey(kvp.Key.referenceImage.name) == poolKey)
                        keysToRemove.Add(kvp.Key);
                }
                foreach (var key in keysToRemove)
                    RemoveOverlay(key);
                continue;
            }

            // Verificamos si ya existe un overlay para este candidato.
            if (!overlays.ContainsKey(candidate))
            {
                // Eliminamos overlays asociados a otras imágenes de este pool.
                List<ARTrackedImage> keysToRemovePool = new List<ARTrackedImage>();
                foreach (var kvp in overlays)
                {
                    if (GetPoolKey(kvp.Key.referenceImage.name) == poolKey && kvp.Key != candidate)
                        keysToRemovePool.Add(kvp.Key);
                }
                foreach (var key in keysToRemovePool)
                    RemoveOverlay(key);

                // Creamos el overlay para el candidato actual.
                CreateOverlay(candidate, poolKey);
            }
            else
            {
                // Si ya existe, actualizamos su posición y escala.
                UpdateOverlay(candidate);
            }
        }
    }

    // Crea el overlay para el tracked image del pool dado.
    void CreateOverlay(ARTrackedImage trackedImage, string poolKey)
    {
        GameObject prefab = null;
        if (poolKey == "irlDate")
            prefab = overlayPrefab1;
        else if (poolKey == "irlSoldier")
            prefab = overlayPrefab2;
        else if (poolKey == "irlMonk")
            prefab = overlayPrefab3;

        if (prefab == null)
            return;

        Vector3 spawnPosition = trackedImage.transform.position;
        GameObject overlayInstance = Instantiate(prefab, spawnPosition, Quaternion.Euler(90f, 0f, 0f));
        if (!overlayInstance.GetComponent<Collider>())
            overlayInstance.AddComponent<BoxCollider>();
        overlays[trackedImage] = overlayInstance;
    }

    // Actualiza la transformación del overlay asociado al tracked image.
    void UpdateOverlay(ARTrackedImage trackedImage)
    {
        if (!overlays.ContainsKey(trackedImage))
            return;

        GameObject overlayInstance = overlays[trackedImage];
        overlayInstance.transform.position = trackedImage.transform.position;
        overlayInstance.transform.rotation = trackedImage.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
        Vector2 size = trackedImage.size;
        overlayInstance.transform.localScale = new Vector3(size.x * scaleFactor, size.y * scaleFactor, 1f);
    }

    // Elimina el overlay de la imagen y libera su pool.
    void RemoveOverlay(ARTrackedImage trackedImage)
    {
        if (overlays.ContainsKey(trackedImage))
        {
            Destroy(overlays[trackedImage]);
            overlays.Remove(trackedImage);
        }
        string poolKey = GetPoolKey(trackedImage.referenceImage.name);
        if (!string.IsNullOrEmpty(poolKey) && activePoolImages.ContainsKey(poolKey) && activePoolImages[poolKey] == trackedImage)
        {
            activePoolImages.Remove(poolKey);
        }
    }
}
