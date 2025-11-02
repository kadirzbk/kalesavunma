using UnityEngine;

using System.Collections;
using System.Collections.Generic;

public class Health : MonoBehaviour
{
    public int maxHealth = 100; // Maksimum can
    private int currentHealth;

    [Header("Visual")]
    public Color damageColor = Color.red;
    public float damageFlashDuration = 0.15f;

    private SpriteRenderer[] _spriteRenderers;
    private Renderer[] _meshRenderers;
    private Dictionary<Renderer, Color[]> _originalColors = new Dictionary<Renderer, Color[]>();

    void Awake()
    {
        // maxHealth doğrulama
        if (maxHealth <= 0)
        {
            maxHealth = 100;
            Debug.LogWarning($"❌ {gameObject.name} için maxHealth sıfır veya negatif! Varsayılan: 100");
        }
        currentHealth = maxHealth; // Başlangıçta can ful
        // Cache renderers for damage flash
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        _meshRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (var r in _meshRenderers)
        {
            if (r == null) continue;
            var mats = r.materials;
            Color[] cols = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].HasProperty("_Color")) cols[i] = mats[i].color;
                else cols[i] = Color.white;
            }
            _originalColors[r] = cols;
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0)
        {
            Debug.LogWarning($"❌ Negatif hasar değeri! Hasar: {damage} ({gameObject.name})");
            return;
        }

        int oldHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
    Debug.Log($"💥 {gameObject.name} {damage} hasar aldı! ({oldHealth} -> {currentHealth})");

    // Start damage flash coroutine
    try { StartCoroutine(DamageFlash()); } catch { }

        if (currentHealth <= 0)
        {
            // Kale veya düşman kulesi yok edildiğinde
            if (gameObject.CompareTag("Castle"))
            {
                Debug.Log("🏰 Kale yok edildi! Yenilgi!");
                GameManager.instance?.GameOver(false); // Yenilgi bildir
            }
            else if (gameObject.CompareTag("EnemyCastle"))
            {
                Debug.Log("🏆 Düşman kalesi yok edildi! ZAFER!");
                GameManager.instance?.GameOver(true); // Zafer bildir
            }
        }
    }

    IEnumerator DamageFlash()
    {
        // SpriteRenderers
        foreach (var sr in _spriteRenderers)
        {
            if (sr == null) continue;
            sr.color = damageColor;
        }

        // Mesh/Renderer
        foreach (var r in _meshRenderers)
        {
            if (r == null) continue;
            var mats = r.materials; // creates instances
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].HasProperty("_Color")) mats[i].color = damageColor;
            }
        }

        yield return new WaitForSeconds(damageFlashDuration);

        // Revert sprite colors (to white - if you need original tints, we can store them)
        foreach (var sr in _spriteRenderers)
        {
            if (sr == null) continue;
            sr.color = Color.white;
        }

        // Revert mesh renderer colors
        foreach (var kv in _originalColors)
        {
            var r = kv.Key;
            if (r == null) continue;
            var mats = r.materials;
            var cols = kv.Value;
            for (int i = 0; i < mats.Length && i < cols.Length; i++)
            {
                if (mats[i].HasProperty("_Color")) mats[i].color = cols[i];
            }
        }
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }

    public bool IsAlive()
    {
        return currentHealth > 0;
    }
}