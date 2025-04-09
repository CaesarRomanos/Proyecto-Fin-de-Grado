using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class ImageTrackingOverlayManager : MonoBehaviour
{
    [Header("Configuración AR")]
    [Tooltip("Componente ARTrackedImageManager de la escena.")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [Tooltip("Prefab para 'fecha gótica' (irlDate).")]
    [SerializeField] private GameObject overlayPrefab1;
    [Tooltip("Prefab para 'soldado' (irlSoldier).")]
    [SerializeField] private GameObject overlayPrefab2;
    [Tooltip("Prefab para 'monje' (irlMonk).")]
    [SerializeField] private GameObject overlayPrefab3;
    [Tooltip("Factor para reducir el tamaño del overlay en modo AR.")]
    [SerializeField] private float scaleFactor = 0.1f;

    [Header("Configuración UI y Overlay fijo")]
    [Tooltip("Botón del Canvas para fijar/eliminar el overlay.")]
    [SerializeField] private Button fullScreenButton;
    [Tooltip("Cámara AR para conversión de coordenadas.")]
    [SerializeField] private Camera arCamera;
    [Tooltip("Distancia (world units) para mostrar el overlay fijo.")]
    [SerializeField] private float displayDistance = 2f;

    // Seguimiento de overlays y de las imágenes activas (por pool)
    private Dictionary<ARTrackedImage, GameObject> overlays = new Dictionary<ARTrackedImage, GameObject>();
    private Dictionary<string, ARTrackedImage> activePoolImages = new Dictionary<string, ARTrackedImage>();

    // Pools de nombres de referencia
    private List<string> datePool = new List<string> { "irlDate" };
    private List<string> soldierPool = new List<string> { "irlSoldier" };
    private List<string> monkPool = new List<string> { "irlMonk" };

    // Overlay fijado (por botón).
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

    // Solo se procesa el tracking mientras no haya un overlay fijo.
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

    void ProcessImage(ARTrackedImage image)
    {
        if (image.trackingState != TrackingState.Tracking)
            return;

        string poolKey = GetPoolKey(image.referenceImage.name);
        if (string.IsNullOrEmpty(poolKey))
            return;

        activePoolImages[poolKey] = image;
    }

    string GetPoolKey(string refName)
    {
        if (datePool.Contains(refName))
            return "irlDate";
        if (soldierPool.Contains(refName))
            return "irlSoldier";
        if (monkPool.Contains(refName))
            return "irlMonk";
        return null;
    }

    // Crea o actualiza el overlay AR para cada pool.
    void UpdateOverlays()
    {
        foreach (string poolKey in new string[] { "irlDate", "irlSoldier", "irlMonk" })
        {
            ARTrackedImage candidate = activePoolImages.ContainsKey(poolKey) ? activePoolImages[poolKey] : null;
            if (candidate == null)
            {
                // Si no hay imagen para el pool, se eliminan los overlays asociados.
                foreach (var kvp in new List<ARTrackedImage>(overlays.Keys))
                {
                    if (GetPoolKey(kvp.referenceImage.name) == poolKey)
                        RemoveOverlay(kvp);
                }
                continue;
            }

            if (!overlays.ContainsKey(candidate))
            {
                // Elimina overlays antiguos de este pool y crea el nuevo.
                foreach (var kvp in new List<ARTrackedImage>(overlays.Keys))
                {
                    if (GetPoolKey(kvp.referenceImage.name) == poolKey)
                        RemoveOverlay(kvp);
                }
                CreateOverlay(candidate, poolKey);
                if (fullScreenButton)
                    fullScreenButton.gameObject.SetActive(true);
            }
            else
            {
                UpdateOverlay(candidate);
            }
        }
    }

    void CreateOverlay(ARTrackedImage image, string poolKey)
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

        Vector3 pos = image.transform.position;
        // Se instancia con rotación base para AR (90° en X) y se añade BoxCollider si no tiene.
        GameObject instance = Instantiate(prefab, pos, Quaternion.Euler(90f, 0f, 0f));
        if (!instance.GetComponent<Collider>())
            instance.AddComponent<BoxCollider>();
        // Asignar etiqueta para poder buscar todos posteriormente.
        instance.tag = "Overlay";

        overlays[image] = instance;
    }

    void UpdateOverlay(ARTrackedImage image)
    {
        if (!overlays.ContainsKey(image))
            return;
        GameObject instance = overlays[image];
        instance.transform.position = image.transform.position;
        instance.transform.rotation = image.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
        Vector2 size = image.size;
        instance.transform.localScale = new Vector3(size.x * scaleFactor, size.y * scaleFactor, 1f);
    }

    void RemoveOverlay(ARTrackedImage image)
    {
        if (overlays.ContainsKey(image))
        {
            Destroy(overlays[image]);
            overlays.Remove(image);
        }
        string poolKey = GetPoolKey(image.referenceImage.name);
        if (!string.IsNullOrEmpty(poolKey) &&
            activePoolImages.ContainsKey(poolKey) &&
            activePoolImages[poolKey] == image)
        {
            activePoolImages.Remove(poolKey);
        }
    }

    // Al pulsar el botón:
    // • Si no hay overlay fijo, se fija el overlay del primer pool disponible.
    // • Si ya hay overlay fijo, se destruyen TODOS los overlays para reiniciar.
    void OnFullScreenButtonClicked()
    {
        if (fixedOverlay == null)
        {
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
            DestroyAllOverlays();
            fixedOverlay = null;
            if (fullScreenButton)
                fullScreenButton.gameObject.SetActive(false);
        }
    }

    // Destruye TODOS los overlays existentes en la escena.
    void DestroyAllOverlays()
    {
        // Destruir overlays gestionados en el diccionario.
        foreach (var overlay in new List<GameObject>(overlays.Values))
        {
            if (overlay)
                Destroy(overlay);
        }
        overlays.Clear();
        activePoolImages.Clear();

        // Destruir overlay fijo, si existe.
        if (fixedOverlay)
        {
            Destroy(fixedOverlay);
            fixedOverlay = null;
        }

        // Adicionalmente, buscar y destruir cualquier GameObject con la etiqueta "Overlay".
        GameObject[] allOverlays = GameObject.FindGameObjectsWithTag("Overlay");
        foreach (GameObject ovr in allOverlays)
        {
            Destroy(ovr);
        }
    }

    // Configura el overlay fijo: lo centra en pantalla, lo orienta para mirar a la cámara y lo escala al 90% del ancho.
    void SetFixedOverlay(GameObject overlay)
    {
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, displayDistance);
        Vector3 worldCenter = arCamera.ScreenToWorldPoint(screenCenter);
        overlay.transform.position = worldCenter;

        // Orientar para que la cara frontal (local +Z) mire a la cámara.
        Vector3 dir = overlay.transform.position - arCamera.transform.position;
        overlay.transform.rotation = Quaternion.LookRotation(dir, arCamera.transform.up);

        float halfFOV = arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float screenHeightWorld = 2f * displayDistance * Mathf.Tan(halfFOV);
        float screenWidthWorld = screenHeightWorld * arCamera.aspect;
        float targetWidth = screenWidthWorld * 0.9f;  // 90% del ancho.

        // Obtener el ancho actual usando BoxCollider o Renderer.
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

        float sf = targetWidth / currentWidth;
        overlay.transform.localScale = overlay.transform.localScale * sf;
    }
}
