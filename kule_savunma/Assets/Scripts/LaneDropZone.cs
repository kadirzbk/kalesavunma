using UnityEngine;

public class LaneDropZone : MonoBehaviour
{
    [Header("Lane Ayarları")]
    public string laneName = "Lane1";
    
    [Header("Görsel Ayarlar")]
    public Color validDropColor = Color.green;
    public Color invalidDropColor = Color.red;
    public Color defaultColor = Color.white;
    
    private Renderer laneRenderer;
    private bool isHighlighted = false;

    void Start()
    {
        laneRenderer = GetComponent<Renderer>();
        if (laneRenderer == null)
        {
            Debug.LogWarning($"❌ {gameObject.name} için Renderer bulunamadı!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Sürükle bırak sırasında highlight için
        if (other.CompareTag("Player") || other.GetComponent<Character>() != null)
        {
            HighlightLane(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<Character>() != null)
        {
            HighlightLane(false);
        }
    }

    void HighlightLane(bool highlight)
    {
        if (laneRenderer != null && !isHighlighted)
        {
            if (highlight)
            {
                laneRenderer.material.color = validDropColor;
                isHighlighted = true;
            }
            else
            {
                laneRenderer.material.color = defaultColor;
                isHighlighted = false;
            }
        }
    }

    public bool IsValidDropZone()
    {
        return !string.IsNullOrEmpty(laneName);
    }

    void OnDrawGizmos()
    {
        // Lane sınırlarını göster
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
        
        // Lane ismini göster
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}

