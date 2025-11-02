using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterType { Tank, Warrior, Archer, Mage }

public class Character : MonoBehaviour
{
    [Header("Karakter Özellikleri")]
    public CharacterType characterType;
    [SerializeField] private int maxHealth;
    [SerializeField] private int health;
    [SerializeField] private int damage;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Yol Sistemi")]
    public Transform[] waypoints;

    // Private değişkenler
    private int currentWaypointIndex = 0;
    private float attackCooldown = 0f;
    private bool isMoving = true;
    private bool isAttackingTower = false;
    private bool isAttackingUnit = false;
    private GameObject currentTargetEnemy;
    private GameObject enemyTower;
    private Health towerHealth; // Cache için
    private float attackStartTime = 0f; // Saldırı başlama zamanı
    private float maxAttackTime = 30f; // Maksimum saldırı süresi (30 saniye)
    // Bu karakterin bulunduğu yol/koridor id'si (WaypointHolder.laneId ile eşleşir)
    public int laneId = -1;
    [Header("Hasar efektleri")]
    public Color damageColor = Color.red;
    public float damageFlashDuration = 0.15f;

    private SpriteRenderer[] _spriteRenderers;
    private Renderer[] _meshRenderers;
    private Dictionary<Renderer, Color[]> _originalColors = new Dictionary<Renderer, Color[]>();

    void Start()
    {
        InitializeCharacter();
        FindEnemyTower();
        ValidateWaypoints();
        // Eğer laneId atanmadıysa, otomatik tespit et (waypoints varsa onlardan, yoksa en yakın lane)
        if (laneId < 0)
        {
            // Öncelik: inspector/waypoints üzerinden tespit
            if (waypoints != null && waypoints.Length > 0)
            {
                int found = WaypointHolder.FindLaneIdByWaypoints(waypoints);
                if (found >= 0) laneId = found;
            }

            // Eğer hala atanmadıysa, en yakın laneyi bul
            if (laneId < 0)
            {
                laneId = WaypointHolder.FindClosestLane(transform.position);
            }

            // Eğer hala -1 ise, default olarak lane 0 kullan
            if (laneId < 0)
            {
                laneId = 0;
                Debug.LogWarning($"⚠️ {gameObject.name} için lane bulunamadı, default lane 0 kullanıldı");
            }

            Debug.Log($"🔢 Character auto laneId = {laneId} for {gameObject.name}");
        }

    // Lane tespiti tamamlandı (zorla atama kaldırıldı)
            // Cache renderer components and original colors for damage flash
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

    void InitializeCharacter()
    {
        // Karakter özelliklerini ayarla
        SetCharacterStats();
        // Güvenlik: inspector'da yanlış değerleri toparla
        if (attackRange < 0.2f) attackRange = 2f;
        if (attackSpeed <= 0f) attackSpeed = 1f;
        if (moveSpeed <= 0f) moveSpeed = 1f;

        Debug.Log($"🚀 {gameObject.name} ({characterType}) başlatıldı - HP:{health} DMG:{damage}");
    }

    void SetCharacterStats()
    {
        switch (characterType)
        {
            case CharacterType.Tank:
                maxHealth = health = 200; damage = 20; attackRange = 3f; attackSpeed = 1.0f; break;
            case CharacterType.Warrior:
                maxHealth = health = 100; damage = 30; attackRange = 2f; attackSpeed = 1.5f; break;
            case CharacterType.Archer:
                maxHealth = health = 50; damage = 40; attackRange = 12f; attackSpeed = 1.2f; break;
            case CharacterType.Mage:
                maxHealth = health = 40; damage = 25; attackRange = 10f; attackSpeed = 1.0f; break;
            default:
                Debug.LogError($"❌ Bilinmeyen karakter tipi: {characterType}");
                break;
        }
    }

    void FindEnemyTower()
    {
        // Önce tag ile ara
        enemyTower = GameObject.FindGameObjectWithTag("EnemyCastle");
        
        // Tag ile bulunamazsa isim ile ara
        if (enemyTower == null)
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.ToLower().Contains("enemy") && obj.name.ToLower().Contains("castle"))
                {
                    enemyTower = obj;
                    break;
                }
            }
        }
        
        // Hala bulunamazsa isim ile ara
        if (enemyTower == null)
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.ToLower().Contains("kale") || obj.name.ToLower().Contains("tower"))
                {
                    enemyTower = obj;
                    break;
                }
            }
        }
        
        if (enemyTower == null)
        {
            Debug.LogError("🏰 KRİTİK HATA: Düşman kalesi bulunamadı! Tag: EnemyCastle veya isimde 'enemy', 'castle', 'kale', 'tower' içeren obje yok!");
            enabled = false; // Karakteri devre dışı bırak
            return;
        }

        towerHealth = enemyTower.GetComponent<Health>();
        if (towerHealth == null)
        {
            Debug.LogError($"❌ {enemyTower.name} objesinde Health component'i yok!");
            enabled = false;
            return;
        }

        Debug.Log($"🏰 Düşman kalesi bulundu: {enemyTower.name} (Tag: {enemyTower.tag})");
    }

    void ValidateWaypoints()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError($"🗺️ {gameObject.name} için waypoint tanımlanmamış!");
            enabled = false;
            return;
        }

        Debug.Log($"📍 {waypoints.Length} waypoint yüklendi");
    }

    void Update()
    {
        // Düşman kulesi kontrolü - sürekli kontrol et
        if (enemyTower == null || towerHealth == null)
        {
            Debug.LogWarning($"⚠️ {characterType}: Düşman kulesi kayboldu! Yeniden aranıyor...");
            FindEnemyTower();
            if (enemyTower == null)
            {
                Debug.LogError($"❌ {characterType}: Düşman kulesi bulunamadı! Karakter yok ediliyor...");
                Die();
                return;
            }
        }

        // Cooldown güncellemesi
        if (attackCooldown > 0f)
            attackCooldown -= Time.deltaTime;

        if (isMoving)
        {
            MoveAlongPath();
        }
        else if (isAttackingUnit)
        {
            if (currentTargetEnemy == null)
            {
                // Target lost/destroyed -> resume moving
                isAttackingUnit = false;
                isMoving = true;
            }
            else
            {
                AttackEnemy(currentTargetEnemy);
            }
        }
        else if (isAttackingTower)
        {
            AttackTower();
        }
    }

    void MoveAlongPath()
    {
        // Düşman karakter kontrolü - önce düşman karakterlerle savaş
        GameObject nearestEnemy = FindNearestEnemy();
        if (nearestEnemy != null)
        {
            float distanceToEnemy = Vector3.Distance(transform.position, nearestEnemy.transform.position);
            if (distanceToEnemy <= attackRange)
            {
                Debug.Log($"⚔️ {characterType} düşman karakter buldu! Savaşa başlıyor...");
                isMoving = false;
                isAttackingTower = false;
                isAttackingUnit = true;
                currentTargetEnemy = nearestEnemy;
                return;
            }
        }

        // Düşman kalesi kontrolü - hareket sırasında da kontrol et
        if (enemyTower != null && towerHealth != null)
        {
            float distanceToTower = Vector3.Distance(transform.position, enemyTower.transform.position);
            bool isRangedCharacter = (characterType == CharacterType.Archer || characterType == CharacterType.Mage);
            
            Debug.Log($"🔍 {characterType} - Kule mesafesi: {distanceToTower:F2}, Saldırı menzili: {attackRange:F2}, Uzaktan: {isRangedCharacter}");
            
            // Uzaktan saldıran karakterler için menzil kontrolü
            if (isRangedCharacter && distanceToTower <= attackRange)
            {
                Debug.Log($"🏹 {characterType} menzile girdi! Hareket durduruluyor, saldırıya başlıyor...");
                isMoving = false;
                isAttackingTower = true;
                attackStartTime = Time.time;
                return;
            }
            
            // Yakın dövüş karakterleri için de menzil kontrolü
            if (!isRangedCharacter && distanceToTower <= attackRange)
            {
                Debug.Log($"⚔️ {characterType} menzile girdi! Hareket durduruluyor, saldırıya başlıyor...");
                isMoving = false;
                isAttackingTower = true;
                attackStartTime = Time.time;
                return;
            }
        }

        if (currentWaypointIndex >= waypoints.Length)
        {
            ArriveAtDestination();
            return;
        }

        Transform targetWaypoint = waypoints[currentWaypointIndex];
        if (targetWaypoint == null)
        {
            Debug.LogError($"❌ Waypoint {currentWaypointIndex} null!");
            return;
        }

        // Hareket: sadece Z ekseninde ilerleyecek şekilde hedefin Z koordinatına git
        float targetZ = targetWaypoint.position.z;
        Vector3 currentPos = transform.position;
        Vector3 targetPosition = new Vector3(currentPos.x, currentPos.y, targetZ);

        // Yöne göre döndür: hedef daha büyük Z ise +Z, daha küçükse -Z
        float zDiff = targetZ - currentPos.z;
        if (Mathf.Abs(zDiff) > 0.001f)
        {
            Vector3 forwardDir = zDiff >= 0f ? Vector3.forward : Vector3.back;
            transform.rotation = Quaternion.LookRotation(forwardDir);
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        // Waypoint'e varış kontrolü (sadece Z farkına bak)
        if (Mathf.Abs(transform.position.z - targetZ) < 0.2f)
        {
            currentWaypointIndex++;
            Debug.Log($"➡️ Z ekseninde ilerliyor: Waypoint {currentWaypointIndex - 1}/{waypoints.Length} tamamlandı");
        }
    }

    void ArriveAtDestination()
    {
        isMoving = false;
        
        // Düşman kalesi var mı kontrol et
        if (enemyTower == null || towerHealth == null)
        {
            Debug.Log($"❌ {gameObject.name} hedefe ulaştı ama düşman kalesi bulunamadı! Karakter yok ediliyor...");
            Die();
            return;
        }
        
        // Uzaktan saldıran karakterler için özel kontrol
        bool isRangedCharacter = (characterType == CharacterType.Archer || characterType == CharacterType.Mage);
        float distanceToTower = Vector3.Distance(transform.position, enemyTower.transform.position);
        
        if (isRangedCharacter && distanceToTower <= attackRange)
        {
            // Uzaktan saldıran karakter zaten menzilde, hemen saldırıya başla
            isAttackingTower = true;
            attackStartTime = Time.time; // Saldırı başlama zamanını kaydet
            Debug.Log($"🏹 {gameObject.name} menzilde! Uzaktan saldırı başlıyor...");
        }
        else
        {
            // Normal karakter veya menzil dışında, saldırı moduna geç
            isAttackingTower = true;
            attackStartTime = Time.time; // Saldırı başlama zamanını kaydet
            Debug.Log($"🎯 {gameObject.name} hedefe ulaştı! Düşman kalesine saldırı başlıyor...");
        }
    }

    void AttackTower()
    {
        if (enemyTower == null || towerHealth == null)
        {
            Debug.LogError($"❌ {gameObject.name}: Kule veya Health component'i bulunamadı! enemyTower: {enemyTower != null}, towerHealth: {towerHealth != null}");
            return;
        }

        // Timeout kontrolü - çok uzun süre saldırıyorsa yok et
        if (Time.time - attackStartTime > maxAttackTime)
        {
            Debug.Log($"⏰ {characterType} çok uzun süre saldırıyor! Karakter yok ediliyor...");
            Die();
            return;
        }

        float distanceToTower = Vector3.Distance(transform.position, enemyTower.transform.position);
        Debug.Log($"🔍 {characterType} - Kule mesafesi: {distanceToTower:F2}, Saldırı menzili: {attackRange:F2}, Cooldown: {attackCooldown:F2}");

        // Uzaktan saldıran karakterler için özel mantık
        bool isRangedCharacter = (characterType == CharacterType.Archer || characterType == CharacterType.Mage);
        
        if (distanceToTower <= attackRange)
        {
            Debug.Log($"✅ {characterType} menzilde! Saldırı kontrolü...");
            
            // Cooldown kontrolü
            if (attackCooldown <= 0f)
            {
                // Saldırı!
                int oldHealth = towerHealth.GetCurrentHealth();
                towerHealth.TakeDamage(damage);
                int newHealth = towerHealth.GetCurrentHealth();
                
                Debug.Log($"💥 {characterType} SALDIRI! {damage} hasar verdi! ({oldHealth} -> {newHealth})");

                // Cooldown başlat
                attackCooldown = 1f / attackSpeed;
                Debug.Log($"⏰ Cooldown başlatıldı: {attackCooldown:F2} saniye");
            }
            else
            {
                Debug.Log($"⏳ {characterType} cooldown'da... {attackCooldown:F2} saniye kaldı");
            }

            // Kule yok oldu mu?
            if (towerHealth.GetCurrentHealth() <= 0)
            {
                Debug.Log("🏆 DÜŞMAN KALESİ YOK EDİLDİ! ZAFER!");
                GameManager.instance?.GameOver(true); // Zafer bildir
                isAttackingTower = false;
                Die(); // Karakteri de yok et
            }
        }
        else
        {
            // Uzaktan saldıran karakterler için özel mantık
            if (isRangedCharacter)
            {
                // Uzaktan saldıran karakterler kaleye yaklaşmaz, sadece Z yönüne dönsün
                float targetZ = enemyTower.transform.position.z;
                float zDiff = targetZ - transform.position.z;
                if (Mathf.Abs(zDiff) > 0.001f)
                {
                    Vector3 forwardDir = zDiff >= 0f ? Vector3.forward : Vector3.back;
                    transform.rotation = Quaternion.LookRotation(forwardDir);
                }

                Debug.Log($"🏹 {characterType} uzaktan saldırı pozisyonunda bekliyor... (Menzil: {distanceToTower:F1}/{attackRange:F1})");
            }
            else
            {
                // Yakın dövüş karakterleri kaleye yaklaş (sadece Z ekseninde)
                float targetZ = enemyTower.transform.position.z;
                Vector3 currentPos2 = transform.position;
                Vector3 targetPosition2 = new Vector3(currentPos2.x, currentPos2.y, targetZ);

                float zDiff2 = targetZ - currentPos2.z;
                if (Mathf.Abs(zDiff2) > 0.001f)
                {
                    Vector3 forwardDir = zDiff2 >= 0f ? Vector3.forward : Vector3.back;
                    transform.rotation = Quaternion.LookRotation(forwardDir);
                }

                transform.position = Vector3.MoveTowards(transform.position, targetPosition2, moveSpeed * Time.deltaTime);
                Debug.Log($"⚔️ {characterType} düşman kalesine yaklaşıyor... (Menzil: {distanceToTower:F1}/{attackRange:F1})");
            }
        }
    }

    public void TakeDamage(int damageAmount)
    {
        health = Mathf.Max(0, health - damageAmount);
        Debug.Log($"🩸 {gameObject.name} {damageAmount} hasar aldı! Kalan HP: {health}/{maxHealth}");

        // Play damage flash
        try { StartCoroutine(DamageFlash()); } catch { }

        if (health <= 0)
        {
            Die();
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

        // Revert sprite colors
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

    void Die()
    {
        Debug.Log($"💀 {characterType} öldü! Karakter yok ediliyor...");
        
        // Karakteri devre dışı bırak
        isMoving = false;
        isAttackingTower = false;
        enabled = false;
        
        // Hemen yok et - gecikme olmadan
        if (gameObject != null)
        {
            Debug.Log($"🗑️ {characterType} tamamen yok edildi!");
            Destroy(gameObject);
        }
    }

    // Public API
    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints ?? throw new System.ArgumentNullException(nameof(newWaypoints));
        currentWaypointIndex = 0;
        isMoving = true;
        isAttackingTower = false;
        Debug.Log($"🗺️ Waypoints güncellendi: {waypoints.Length} adet");
        // laneId otomatik tespiti
        int found = WaypointHolder.FindLaneIdByWaypoints(waypoints);
        if (found >= 0) laneId = found;
        Debug.Log($"🔢 Character laneId set to {laneId}");
    }

    public void SetStartingWaypointIndex(int startingIndex)
    {
        if (waypoints != null && startingIndex >= 0 && startingIndex < waypoints.Length)
        {
            currentWaypointIndex = startingIndex;
            Debug.Log($"📍 Karakter {startingIndex}. waypoint'ten başlatıldı");
        }
    }

    public bool IsAlive => health > 0;
    public int CurrentHealth => health;
    public int MaxHealth => maxHealth;
    public float HealthPercentage => (float)health / maxHealth;
    // Düşman karakter bulma ve saldırı sistemi
    GameObject FindNearestEnemy()
    {
        EnemyCharacter[] enemies = FindObjectsOfType<EnemyCharacter>();
        if (enemies.Length == 0) return null;

        GameObject nearestEnemy = null;
        float nearestDistance = float.MaxValue;

        foreach (EnemyCharacter enemy in enemies)
        {
            if (enemy != null && enemy.IsAlive)
            {
                // Sadece aynı lane içindekileri dikkate al
                if (enemy.laneId != this.laneId) continue;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEnemy = enemy.gameObject;
                }
            }
        }

        return nearestEnemy;
    }

    void AttackEnemy(GameObject enemy)
    {
        if (enemy == null) return;

        EnemyCharacter enemyScript = enemy.GetComponent<EnemyCharacter>();
        if (enemyScript == null || !enemyScript.IsAlive)
        {
            // Target is dead or invalid - resume moving
            isAttackingUnit = false;
            currentTargetEnemy = null;
            isMoving = true;
            return;
        }

        float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
        
        if (distanceToEnemy <= attackRange)
        {
            if (attackCooldown <= 0f)
            {
                int oldHealth = enemyScript.CurrentHealth;
                enemyScript.TakeDamage(damage);
                int newHealth = enemyScript.CurrentHealth;
                
                Debug.Log($"💥 {characterType} -> {enemyScript.enemyType}: {damage} hasar! ({oldHealth} -> {newHealth})");
                attackCooldown = 1f / attackSpeed;
            }
        }
        else
        {
            // Düşmana yaklaş (sadece Z ekseninde)
            float targetZ = enemy.transform.position.z;
            Vector3 currentPos = transform.position;
            Vector3 targetPosition = new Vector3(currentPos.x, currentPos.y, targetZ);

            float zDiff = targetZ - currentPos.z;
            if (Mathf.Abs(zDiff) > 0.001f)
            {
                Vector3 forwardDir = zDiff >= 0f ? Vector3.forward : Vector3.back;
                transform.rotation = Quaternion.LookRotation(forwardDir);
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }
    }

    // Gizmos
    void OnDrawGizmosSelected()
    {
        // Saldırı menzili
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Yol çizimi
        if (waypoints != null)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
            }

            // Mevcut waypoint
            if (currentWaypointIndex < waypoints.Length && waypoints[currentWaypointIndex] != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(waypoints[currentWaypointIndex].position, 0.5f);
            }
        }
    }

    void OnDrawGizmos()
    {
        // HP barı
        if (IsAlive)
        {
            Vector3 barPos = transform.position + Vector3.up * 2f;
            Gizmos.color = Color.green;
            Gizmos.DrawCube(barPos, new Vector3(HealthPercentage * 1.5f, 0.2f, 0));
        }
    }
}