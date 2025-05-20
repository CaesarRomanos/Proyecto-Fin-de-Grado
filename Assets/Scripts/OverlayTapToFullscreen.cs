// ImageTrackingOverlayManager.cs
// Manages AR image tracking overlays, creating, updating, and fixing them in fullscreen mode.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ImageTrackingOverlayManager : MonoBehaviour
{
    [Header("AR Setup")]
    [Tooltip("ARTrackedImageManager component for tracking reference images.")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;

    [Tooltip("Prefab for the gothic date overlay (irlDate).")]
    [SerializeField] private GameObject overlayPrefab1;
    [Tooltip("Prefab for the soldier overlay (irlSoldier).")]
    [SerializeField] private GameObject overlayPrefab2;
    [Tooltip("Prefab for the monk overlay (irlMonk).")]
    [SerializeField] private GameObject overlayPrefab3;

    [Tooltip("Scale factor to apply to AR overlays.")]
    [SerializeField] private float scaleFactor = 0.1f;

    [Header("UI and Fixed Overlay Setup")]
    [Tooltip("Button to toggle pinning/unpinning the overlay in fullscreen.")]
    [SerializeField] private Button fullScreenButton;
    [Tooltip("AR camera used for world-to-screen coordinate conversion.")]
    [SerializeField] private Camera arCamera;
    [Tooltip("Distance from camera at which to display the pinned overlay in world units.")]
    [SerializeField] private float displayDistance = 2f;

    // Maps each tracked image to its AR overlay instance
    private Dictionary<ARTrackedImage, GameObject> overlays = new Dictionary<ARTrackedImage, GameObject>();
    // Tracks currently active reference image per category pool
    private Dictionary<string, ARTrackedImage> activePoolImages = new Dictionary<string, ARTrackedImage>();

    // Pool definitions by reference image name
    private List<string> datePool = new List<string> { "irlDate" };
    private List<string> soldierPool = new List<string> { "irlSoldier" };
    private List<string> monkPool = new List<string> { "irlMonk" };

    // Holds the one pinned overlay when in fullscreen mode
    private GameObject fixedOverlay = null;

    void OnEnable()
    {
        if (trackedImageManager)
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;

        if (fullScreenButton)
        {
            fullScreenButton.gameObject.SetActive(false);
            fullScreenButton.onClick.AddListener(OnFullScreenButtonClicked);
        }

        if (arCamera == null)
            arCamera = Camera.main;
    }

    void OnDisable()
    {
        if (trackedImageManager)
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        if (fullScreenButton)
            fullScreenButton.onClick.RemoveListener(OnFullScreenButtonClicked);
    }

    // Skip processing image events if an overlay is pinned fullscreen
    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        if (fixedOverlay != null)
            return;

        foreach (var image in eventArgs.added)
            ProcessImage(image);
        foreach (var image in eventArgs.updated)
            ProcessImage(image);
        foreach (var image in eventArgs.removed)
            RemoveOverlay(image);

        UpdateOverlays();
    }

    // Registers the latest valid tracked image per pool
    void ProcessImage(ARTrackedImage image)
    {
        if (image.trackingState != TrackingState.Tracking)
            return;

        string poolKey = GetPoolKey(image.referenceImage.name);
        if (string.IsNullOrEmpty(poolKey))
            return;

        activePoolImages[poolKey] = image;
    }

    // Determines which pool a reference name belongs to
    string GetPoolKey(string refName)
    {
        if (datePool.Contains(refName)) return "irlDate";
        if (soldierPool.Contains(refName)) return "irlSoldier";
        if (monkPool.Contains(refName)) return "irlMonk";
        return null;
    }

    // Creates or updates overlays for each pool based on the latest tracked image
    void UpdateOverlays()
    {
        foreach (string poolKey in new string[] { "irlDate", "irlSoldier", "irlMonk" })
        {
            ARTrackedImage candidate = activePoolImages.ContainsKey(poolKey) ? activePoolImages[poolKey] : null;
            if (candidate == null)
            {
                // Remove overlays for this pool if no candidate
                foreach (var kvp in new List<ARTrackedImage>(overlays.Keys))
                {
                    if (GetPoolKey(kvp.referenceImage.name) == poolKey)
                        RemoveOverlay(kvp);
                }
                continue;
            }

            if (!overlays.ContainsKey(candidate))
            {
                // Remove old pool overlay and create a new one
                foreach (var kvp in new List<ARTrackedImage>(overlays.Keys))
                {
                    if (GetPoolKey(kvp.referenceImage.name) == poolKey)
                        RemoveOverlay(kvp);
                }
                CreateOverlay(candidate, poolKey);
                if (fullScreenButton) fullScreenButton.gameObject.SetActive(true);
            }
            else
            {
                // Update existing overlay transform and scale
                UpdateOverlay(candidate);
            }
        }
    }

    // Instantiates the correct prefab overlay for the tracked image
    void CreateOverlay(ARTrackedImage image, string poolKey)
    {
        GameObject prefab = null;
        if (poolKey == "irlDate")      prefab = overlayPrefab1;
        else if (poolKey == "irlSoldier") prefab = overlayPrefab2;
        else if (poolKey == "irlMonk")   prefab = overlayPrefab3;
        if (prefab == null) return;

        // Instantiate with a 90° X rotation so it lays flat on the tracked surface
        GameObject instance = Instantiate(prefab, image.transform.position, Quaternion.Euler(90f, 0f, 0f));
        if (!instance.GetComponent<Collider>())
            instance.AddComponent<BoxCollider>();

        instance.tag = "Overlay"; // Tag for easy cleanup
        overlays[image] = instance;
    }

    // Updates the overlay's position, rotation, and scale to match the tracked image
    void UpdateOverlay(ARTrackedImage image)
    {
        if (!overlays.ContainsKey(image)) return;

        GameObject instance = overlays[image];
        instance.transform.position = image.transform.position;
        instance.transform.rotation = image.transform.rotation * Quaternion.Euler(90f, 0f, 0f);

        Vector2 size = image.size;
        instance.transform.localScale = new Vector3(size.x * scaleFactor, size.y * scaleFactor, 1f);
    }

    // Removes an overlay when its tracked image is removed
    void RemoveOverlay(ARTrackedImage image)
    {
        if (overlays.ContainsKey(image))
        {
            Destroy(overlays[image]);
            overlays.Remove(image);
        }

        string poolKey = GetPoolKey(image.referenceImage.name);
        if (!string.IsNullOrEmpty(poolKey) && activePoolImages.ContainsKey(poolKey) && activePoolImages[poolKey] == image)
        {
            activePoolImages.Remove(poolKey);
        }
    }

    // Handles pinning/unpinning overlay to fullscreen when the button is clicked
    void OnFullScreenButtonClicked()
    {
        if (fixedOverlay == null)
        {
            // Pin the first available pool overlay
            ARTrackedImage candidate = null;
            string selectedPool = null;
            foreach (string pool in new string[] { "irlDate", "irlSoldier", "irlMonk" })
            {
                if (activePoolImages.ContainsKey(pool))
                {
                    candidate = activePoolImages[pool];
                    selectedPool = pool;
                    break;
                }
            }
            if (candidate != null && overlays.ContainsKey(candidate))
            {
                fixedOverlay = overlays[candidate];
                overlays.Remove(candidate);
                activePoolImages.Remove(selectedPool);
                SetFixedOverlay(fixedOverlay);
            }
        }
        else
        {
            // Unpin: destroy all overlays and hide button
            DestroyAllOverlays();
            fixedOverlay = null;
            if (fullScreenButton) fullScreenButton.gameObject.SetActive(false);
        }
    }

    // Destroys all overlay GameObjects in scene and clears tracking dictionaries
    void DestroyAllOverlays()
    {
        foreach (var overlay in new List<GameObject>(overlays.Values))
        {
            if (overlay) Destroy(overlay);
        }
        overlays.Clear();
        activePoolImages.Clear();

        if (fixedOverlay)
        {
            Destroy(fixedOverlay);
            fixedOverlay = null;
        }

        // Also destroy any leftover objects tagged as "Overlay"
        GameObject[] allOverlays = GameObject.FindGameObjectsWithTag("Overlay");
        foreach (GameObject ovr in allOverlays)
        {
            if (ovr) Destroy(ovr);
        }
    }

    // Configures the pinned overlay to face the camera, center it, and scale to 90% of screen width
    void SetFixedOverlay(GameObject overlay)
    {
        // Compute world position at screen center with specified distance
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, displayDistance);
        Vector3 worldCenter = arCamera.ScreenToWorldPoint(screenCenter);
        overlay.transform.position = worldCenter;

        // Rotate overlay to face the camera
        Vector3 dir = overlay.transform.position - arCamera.transform.position;
        overlay.transform.rotation = Quaternion.LookRotation(dir, arCamera.transform.up);

        // Calculate target width (90% of screen width in world units)
        float halfFOV = arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float screenHeightWorld = 2f * displayDistance * Mathf.Tan(halfFOV);
        float screenWidthWorld = screenHeightWorld * arCamera.aspect;
        float targetWidth = screenWidthWorld * 0.9f;

        // Measure current overlay width via BoxCollider or Renderer bounds
        float currentWidth = 1f;
        BoxCollider bc = overlay.GetComponent<BoxCollider>();
        if (bc != null)
            currentWidth = bc.size.x * overlay.transform.lossyScale.x;
        else
        {
            Renderer rend = overlay.GetComponent<Renderer>();
            if (rend != null)
                currentWidth = rend.bounds.size.x;
        }

        // Scale uniformly to reach target width
        float scaleScale = targetWidth / currentWidth;
        overlay.transform.localScale *= scaleScale;
    }
}
