using UnityEngine;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public float currentEnergy = 100f;
    public int maxEnergy = 200;
    public float energyRegenRate = 50f;
    public TextMeshProUGUI energyText;
    private int energyUpgradeCount = 0; // Upgrade sayacı

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("GameManager initialized, instance set.");
        }
        else
        {
            Debug.LogWarning("Another GameManager instance found, destroying this one.");
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("GameManager Start called. Initial energy: " + currentEnergy);
        UpdateEnergyUI();
        Time.timeScale = 1f;
    }

    void Update()
    {
        float energyIncrement = energyRegenRate * Time.deltaTime;
        currentEnergy = Mathf.Clamp(currentEnergy + energyIncrement, 0, maxEnergy);
        UpdateEnergyUI();
    }

    public bool SpendEnergy(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("❌ Geçersiz enerji miktarı: " + amount);
            return false;
        }
        if (currentEnergy >= amount)
        {
            currentEnergy -= amount;
            UpdateEnergyUI();
            Debug.Log($"⚡ Enerji harcandı: {amount}, Kalan enerji: {currentEnergy:F1}");
            return true;
        }
        else
        {
            Debug.Log($"⚡ Yetersiz enerji! Gerekli: {amount}, Mevcut: {currentEnergy:F1}");
            if (energyText != null)
            {
                StartCoroutine(ShowInsufficientEnergyWarning());
            }
            return false;
        }
    }

    void UpdateEnergyUI()
    {
        if (energyText != null)
        {
            energyText.text = "Enerji: " + Mathf.FloorToInt(currentEnergy) + "/" + maxEnergy;
        }
        else
        {
            Debug.LogWarning("EnergyText bileşeni atanmamış!");
        }
    }

    IEnumerator ShowInsufficientEnergyWarning()
    {
        if (energyText != null)
        {
            Color originalColor = energyText.color;
            energyText.color = Color.red;
            yield return new WaitForSeconds(0.5f);
            energyText.color = originalColor;
            Debug.Log("Yetersiz enerji uyarısı gösterildi.");
        }
    }

    public void UpgradeEnergy()
    {
        // Upgrade maliyeti = 50 + (energyUpgradeCount * 50)
        int upgradeCost = 50 + (energyUpgradeCount * 50);

        if (currentEnergy >= upgradeCost)
        {
            currentEnergy -= upgradeCost;
            maxEnergy += 50;
            currentEnergy = Mathf.Min(currentEnergy + 50, maxEnergy); // Yükseltme sırasında enerji ekle
            energyUpgradeCount++;
            UpdateEnergyUI();
            Debug.Log($"⚡ Enerji yükselttikldi! (Maliyet: {upgradeCost}, Yeni Max: {maxEnergy}, Upgrade #{energyUpgradeCount})");
        }
        else
        {
            Debug.Log($"❌ Yetersiz enerji! Gerekli: {upgradeCost}, Mevcut: {currentEnergy:F1}");
            if (energyText != null)
            {
                StartCoroutine(ShowInsufficientEnergyWarning());
            }
        }
    }

    public void ResetGame()
    {
        currentEnergy = 100f;
        maxEnergy = 200; // Reset to base max energy
        energyUpgradeCount = 0; // Reset upgrade count
        Time.timeScale = 1f;
        UpdateEnergyUI();
        Debug.Log("🔄 Oyun sıfırlandı!");
    }

    public void GameOver(bool victory)
    {
        Time.timeScale = 0f;
        Debug.Log(victory ? "🏆 Zafer! Düşman kulesi yok edildi!" : "😔 Yenilgi! Kendi kalen yok edildi!");
        // Burada zafer/yenilgi ekranını çağırabilirsiniz
    }
}