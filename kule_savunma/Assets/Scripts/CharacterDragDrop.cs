using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class CharacterDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Drag & Drop Ayarları")]
    public CharacterType characterType;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera eventCamera;

    [Header("Prefab Referansları (Inspector'dan ayarla!)")]
    [SerializeField] private GameObject tankPrefab;
    [SerializeField] private GameObject warriorPrefab;
    [SerializeField] private GameObject archerPrefab;
    [SerializeField] private GameObject magePrefab;

    [Header("Lane Waypoint'leri")]
    [SerializeField] private Transform[] lane1Waypoints;
    [SerializeField] private Transform[] lane2Waypoints;
    [SerializeField] private Transform[] lane3Waypoints;
    [SerializeField] private Transform[] lane4Waypoints;
    [SerializeField] private Transform[] lane5Waypoints;

    // Private değişkenler
    private CanvasGroup canvasGroup;
    private GameObject draggedObject;
    private RectTransform rectTransform;
    private GraphicRaycaster graphicRaycaster;
    private PointerEventData pointerEventData;

    // Cache için
    private static Dictionary<string, Transform[]> laneWaypointCache = new Dictionary<string, Transform[]>();

    void Awake()
    {
        // Component'leri cache et
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        graphicRaycaster = FindObjectOfType<GraphicRaycaster>();

        // Otomatik referans ayarla (gerekirse)
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (eventCamera == null) eventCamera = Camera.main;

        // Cache'i başlat
        InitializeWaypointCache();

        // Validasyon
        ValidateSetup();
    }

    void Start()
    {
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            Debug.LogWarning($"CanvasGroup otomatik eklendi: {gameObject.name}");
        }
    }

    void InitializeWaypointCache()
    {
        laneWaypointCache.Clear();

        laneWaypointCache["Lane1"] = lane1Waypoints;
        laneWaypointCache["Lane2"] = lane2Waypoints;
        laneWaypointCache["Lane3"] = lane3Waypoints;
        laneWaypointCache["Lane4"] = lane4Waypoints;
        laneWaypointCache["Lane5"] = lane5Waypoints;
    }

    void ValidateSetup()
    {
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();

        // Prefab kontrolleri
        if (tankPrefab == null) errors.Add("Tank Prefab eksik");
        if (warriorPrefab == null) errors.Add("Warrior Prefab eksik");
        if (archerPrefab == null) errors.Add("Archer Prefab eksik");
        if (magePrefab == null) errors.Add("Mage Prefab eksik");

        // Waypoint kontrolleri
        if (lane1Waypoints == null || lane1Waypoints.Length == 0) errors.Add("Lane1 waypoints eksik");
        if (lane2Waypoints == null || lane2Waypoints.Length == 0) errors.Add("Lane2 waypoints eksik");
        if (lane3Waypoints == null || lane3Waypoints.Length == 0) errors.Add("Lane3 waypoints eksik");
        if (lane4Waypoints == null || lane4Waypoints.Length == 0) errors.Add("Lane4 waypoints eksik");
        if (lane5Waypoints == null || lane5Waypoints.Length == 0) errors.Add("Lane5 waypoints eksik");

        // Canvas ve Camera kontrolleri
        if (canvas == null) errors.Add("Canvas referansı eksik");
        if (eventCamera == null) errors.Add("Event Camera referansı eksik");

        // GraphicRaycaster kontrolü
        if (graphicRaycaster == null) warnings.Add("GraphicRaycaster bulunamadı - UI raycast çalışmayabilir");

        if (errors.Count > 0)
        {
            Debug.LogError($"🚨 {gameObject.name} SETUP HATASI:\n" + string.Join("\n", errors));
            enabled = false;
        }
        else
        {
            Debug.Log($"✅ {gameObject.name} setup başarılı!");
            if (warnings.Count > 0)
            {
                Debug.LogWarning($"⚠️ UYARILAR:\n" + string.Join("\n", warnings));
            }
        }
    }

    #region IDragHandler Implementation

    public void OnBeginDrag(PointerEventData eventData)
    {
        pointerEventData = eventData;

        // Raycast engelini kapat
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.6f;

        // Dragged objeyi oluştur
        CreateDraggedObject();

        Debug.Log($"🎯 {characterType} sürüklenmeye başlandı");
    }

    void CreateDraggedObject()
    {
        draggedObject = new GameObject($"Dragged_{characterType}");
        draggedObject.transform.SetParent(canvas.transform, false);

        // Görsel
        Image draggedImage = draggedObject.AddComponent<Image>();
        Image sourceImage = GetComponent<Image>();
        if (sourceImage != null && sourceImage.sprite != null)
        {
            draggedImage.sprite = sourceImage.sprite;
        }
        draggedImage.raycastTarget = false; // UI etkileşimini engelle

        // RectTransform
        RectTransform draggedRect = draggedObject.GetComponent<RectTransform>();
        if (draggedRect == null)
        {
            draggedRect = draggedObject.AddComponent<RectTransform>();
        }
        draggedRect.sizeDelta = rectTransform.sizeDelta;
        draggedRect.anchorMin = Vector2.one * 0.5f;
        draggedRect.anchorMax = Vector2.one * 0.5f;
        draggedRect.pivot = Vector2.one * 0.5f;

        // CanvasGroup ekle
        CanvasGroup draggedGroup = draggedObject.AddComponent<CanvasGroup>();
        draggedGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggedObject != null)
        {
            // Mouse pozisyonuna taşı
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                eventCamera,
                out Vector2 localPoint
            );

            draggedObject.transform.localPosition = localPoint;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Temizlik
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        // Drop kontrolü
        if (draggedObject != null && IsValidDropZone(eventData))
        {
            // Enerji kontrolü - sadece geçerli drop zone'da
            int energyCost = GetEnergyCost();
            if (GameManager.instance != null && GameManager.instance.SpendEnergy(energyCost))
            {
                SpawnCharacter(eventData);
                Debug.Log($"✅ {characterType} başarıyla yerleştirildi!");
            }
            else
            {
                Debug.Log($"⚡ Yetersiz enerji! {characterType}: {energyCost} enerji gerekli");
            }
        }
        else
        {
            Debug.Log($"❌ {characterType} geçersiz bölgeye bırakıldı");
        }

        // Dragged objeyi yok et
        if (draggedObject != null)
        {
            Destroy(draggedObject);
            draggedObject = null;
        }

        pointerEventData = null;
    }

    #endregion

    bool IsValidDropZone(PointerEventData eventData)
    {
        // Önce UI raycast dene (daha hızlı)
        if (graphicRaycaster != null)
        {
            List<RaycastResult> results = new List<RaycastResult>();
            graphicRaycaster.Raycast(eventData, results);

            foreach (var result in results)
            {
                // LaneDropZone tag kontrolü - güvenli
                if (result.gameObject != null && result.gameObject.CompareTag("LaneDropZone"))
                {
                    LaneDropZone zone = result.gameObject.GetComponent<LaneDropZone>();
                    if (zone != null && !string.IsNullOrEmpty(zone.laneName) && laneWaypointCache.ContainsKey(zone.laneName))
                    {
                        return true;
                    }
                }
            }
        }

        // UI raycast başarısızsa 3D raycast dene
        if (eventCamera != null)
        {
            Ray ray = eventCamera.ScreenPointToRay(eventData.position);
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
            
            foreach (var hit in hits)
            {
                string laneTag = hit.collider.tag;
                if (laneWaypointCache.ContainsKey(laneTag))
                {
                    return true;
                }
            }
        }

        return false;
    }

    void SpawnCharacter(PointerEventData eventData)
    {
        // Hedef lane'i bul
        string targetLane = GetTargetLane(eventData);
        if (string.IsNullOrEmpty(targetLane))
        {
            Debug.LogError("❌ Hedef lane bulunamadı!");
            return;
        }

        // Prefab seç
        GameObject prefab = GetCharacterPrefab();
        if (prefab == null)
        {
            Debug.LogError($"❌ Prefab bulunamadı: {characterType}");
            return;
        }

        // Waypoint'leri al
        if (!laneWaypointCache.TryGetValue(targetLane, out Transform[] waypoints) || waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError($"❌ Waypoints bulunamadı: {targetLane}");
            return;
        }

        // Karakteri spawn et
        Vector3 spawnPos = GetSpawnPosition(eventData, targetLane);
        if (spawnPos == Vector3.zero)
        {
            Debug.LogError("❌ Spawn pozisyonu bulunamadı!");
            return;
        }

        GameObject character = Instantiate(prefab, spawnPos, Quaternion.identity);
        if (character == null)
        {
            Debug.LogError("❌ Karakter spawn edilemedi!");
            return;
        }

        // Character script'ini ayarla
        Character characterScript = character.GetComponent<Character>();
        if (characterScript != null)
        {
            characterScript.characterType = characterType;
            characterScript.SetWaypoints(waypoints);
            
            // Spawn pozisyonuna en yakın waypoint'i bul
            int closestWaypointIndex = FindClosestWaypointIndex(spawnPos, waypoints);
            if (closestWaypointIndex > 0)
            {
                // Karakteri en yakın waypoint'ten başlat
                characterScript.SetStartingWaypointIndex(closestWaypointIndex);
            }
            
            Debug.Log($"✅ {characterType} {targetLane} lane'inde spawn edildi! (Waypoint {closestWaypointIndex})");
        }
        else
        {
            Debug.LogError("❌ Character component'i prefab'da yok!");
            Destroy(character);
        }
    }

    string GetTargetLane(PointerEventData eventData)
    {
        // Önce UI raycast dene
        if (graphicRaycaster != null)
        {
            List<RaycastResult> results = new List<RaycastResult>();
            graphicRaycaster.Raycast(eventData, results);

            foreach (var result in results)
            {
                // LaneDropZone tag kontrolü - güvenli
                if (result.gameObject != null && result.gameObject.CompareTag("LaneDropZone"))
                {
                    LaneDropZone zone = result.gameObject.GetComponent<LaneDropZone>();
                    if (zone != null && !string.IsNullOrEmpty(zone.laneName) && laneWaypointCache.ContainsKey(zone.laneName))
                    {
                        return zone.laneName;
                    }
                }
            }
        }

        // UI raycast başarısızsa 3D raycast dene
        if (eventCamera != null)
        {
            Ray ray = eventCamera.ScreenPointToRay(eventData.position);
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
            
            foreach (var hit in hits)
            {
                string laneTag = hit.collider.tag;
                if (laneWaypointCache.ContainsKey(laneTag))
                {
                    return laneTag;
                }
            }
        }

        return null;
    }

    Vector3 GetSpawnPosition(PointerEventData eventData, string laneTag)
    {
        // Basit yaklaşım: Her zaman lane'in ilk waypoint'ine spawn et
        // Bu şekilde karakterler her zaman sağa doğru gider
        if (laneWaypointCache.TryGetValue(laneTag, out Transform[] waypoints) && waypoints.Length > 0)
        {
            Debug.Log($"📍 {characterType} {laneTag} lane'inin başlangıcına spawn ediliyor");
            return waypoints[0].position;
        }

        Debug.LogError($"❌ {laneTag} lane'i için waypoint bulunamadı!");
        return Vector3.zero;
    }

    GameObject GetCharacterPrefab()
    {
        return characterType switch
        {
            CharacterType.Tank => tankPrefab,
            CharacterType.Warrior => warriorPrefab,
            CharacterType.Archer => archerPrefab,
            CharacterType.Mage => magePrefab,
            _ => null
        };
    }

    int FindClosestWaypointIndex(Vector3 spawnPosition, Transform[] waypoints)
    {
        if (waypoints == null || waypoints.Length == 0) return 0;
        
        // Basit yaklaşım: Her zaman ilk waypoint'ten başla
        // Bu şekilde karakterler her zaman sağa doğru gider
        Debug.Log($"📍 {characterType} her zaman ilk waypoint'ten başlayacak (sağa doğru hareket için)");
        return 0;
    }
    
    int FindNearestWaypointIndex(Vector3 spawnPosition, Transform[] waypoints)
    {
        int closestIndex = 0;
        float closestDistance = Vector3.Distance(spawnPosition, waypoints[0].position);
        
        for (int i = 1; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null)
            {
                float distance = Vector3.Distance(spawnPosition, waypoints[i].position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }
        }
        
        return closestIndex;
    }

    #region Utility Methods

    int GetEnergyCost()
    {
        return characterType switch
        {
            CharacterType.Tank => 50,
            CharacterType.Warrior => 30,
            CharacterType.Archer => 20,
            CharacterType.Mage => 40,
            _ => 0
        };
    }

    #endregion
}