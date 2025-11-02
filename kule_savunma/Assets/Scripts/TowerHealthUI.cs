using UnityEngine;
using TMPro;

public class TowerHealthUI : MonoBehaviour
{
    public TextMeshProUGUI towerHealthText;
    private GameObject enemyTower;
    private Health towerHealth;

    void Start()
    {
        enemyTower = GameObject.FindGameObjectWithTag("EnemyCastle");
        if (enemyTower != null)
        {
            towerHealth = enemyTower.GetComponent<Health>();
            UpdateTowerHealthUI();
        }
        else
        {
            Debug.LogWarning("🏰 EnemyCastle bulunamadı!");
            UpdateTowerDestroyedUI();
            enabled = false; // Script'i devre dışı bırak
        }
    }

    void Update()
    {
        if (enemyTower != null && towerHealth != null)
        {
            UpdateTowerHealthUI();
        }
    }

    void UpdateTowerHealthUI()
    {
        if (towerHealthText != null && towerHealth != null)
        {
            int currentHealth = towerHealth.GetCurrentHealth();
            int maxHealth = towerHealth.maxHealth;
            towerHealthText.text = "Düşman Kulesi: " + currentHealth + "/" + maxHealth;

            // Kule canı azaldığında renk değiştir
            if (currentHealth < maxHealth * 0.3f)
            {
                towerHealthText.color = Color.red;
            }
            else if (currentHealth < maxHealth * 0.6f)
            {
                towerHealthText.color = Color.yellow;
            }
            else
            {
                towerHealthText.color = Color.white;
            }
        }
    }

    void UpdateTowerDestroyedUI()
    {
        if (towerHealthText != null)
        {
            towerHealthText.text = "Düşman Kulesi: YOK EDİLDİ!";
            towerHealthText.color = Color.red;
        }
    }
}