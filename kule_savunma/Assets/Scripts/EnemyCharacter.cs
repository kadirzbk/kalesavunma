using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyType { EnemyTank, EnemyWarrior, EnemyArcher, EnemyMage }

public class EnemyCharacter : MonoBehaviour
{
    [Header("Düşman Karakter Özellikleri")]
    public EnemyType enemyType;
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
    private bool isAttackingPlayer = false;
    private GameObject playerTower;
    private Health towerHealth;
    private float attackStartTime = 0f;
    private float maxAttackTime = 300f; // artırıldı: uzun sürede otomatik yok etme yerine manuel temizleme tercih edilir
    // Rigidbody cache (eğer prefabta Rigidbody varsa düşmenin/tilt'in önüne geçmek için kullanılır)
    private Rigidbody _rb;
    // Hedeflenen oyuncu (karakter) ile savaşı sürdürmek için cache
    private GameObject currentTargetPlayer;
    private bool isAttackingUnit = false;
    // Bu düşmanın bulunduğu yol/koridor id'si (WaypointHolder.laneId ile eşleşir)
    public int laneId = -1;

    [Header("Hasar efektleri")]
    public Color damageColor = Color.red;
    public float damageFlashDuration = 0.15f;

    [Header("Combat Tuning")]
    [SerializeField] private float chaseSpeedMultiplier = 1.6f; // hedef kovalamada hız çarpanı
    [SerializeField] private float rangeTolerance = 1.5f;        // menzile girme toleransı (Z)
    private float _lastChaseZDist = float.MaxValue;

    private SpriteRenderer[] _spriteRenderers;
    private Renderer[] _meshRenderers;
    private Dictionary<Renderer, Color[]> _originalColors = new Dictionary<Renderer, Color[]>();
    [Header("DEBUG")]
    public bool forceContactDamageForDebug = false; // If true, bypass cooldown to force damage for testing

    void Start()
    {
        InitializeEnemy();
        FindPlayerTower();
        ValidateWaypoints();
        // Rigidbody varsa yerçekmesini kapat ve X/Z eksenindeki rotasyonları kilitle
        // Ayrıca isKinematic=true yaparak fiziksel itiş/çarpışma etkilerini engelle ve hareketi script ile kontrol et
        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rb.useGravity = false;
            _rb.isKinematic = true;
            _rb.constraints = _rb.constraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
        // Cache renderer components for damage flash
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
        // Eğer laneId henüz atanmadıysa, waypoints üzerinden veya en yakın lane üzerinden tespit et
        if (laneId < 0)
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                int found = WaypointHolder.FindLaneIdByWaypoints(waypoints);
                if (found >= 0) laneId = found;
            }

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

            Debug.Log($"🔢 Enemy auto laneId = {laneId} for {gameObject.name}");
        }
    }

    void InitializeEnemy()
    {
        SetEnemyStats();
        // Güvenlik: inspector'da yanlış değerleri toparla
        if (attackRange < 0.2f) attackRange = 2f;
        if (attackSpeed <= 0f) attackSpeed = 1f;
        if (moveSpeed <= 0f) moveSpeed = 1f;
        Debug.Log($"👹 {gameObject.name} ({enemyType}) başlatıldı - HP:{health} DMG:{damage}");
    }

    void SetEnemyStats()
    {
        switch (enemyType)
        {
            case EnemyType.EnemyTank:
                maxHealth = health = 150; damage = 15; attackRange = 2f; attackSpeed = 0.8f; break;
            case EnemyType.EnemyWarrior:
                maxHealth = health = 80; damage = 20; attackRange = 1.5f; attackSpeed = 1.2f; break;
            case EnemyType.EnemyArcher:
                maxHealth = health = 40; damage = 25; attackRange = 8f; attackSpeed = 1f; break;
            case EnemyType.EnemyMage:
                maxHealth = health = 35; damage = 18; attackRange = 6f; attackSpeed = 0.9f; break;
            default:
                Debug.LogError($"❌ Bilinmeyen düşman tipi: {enemyType}");
                break;
        }
    }

    void FindPlayerTower()
    {
        // Eğer önceden SetPlayerTower ile atanmışsa onu kullan
        if (playerTower != null)
        {
            towerHealth = playerTower.GetComponent<Health>();
            if (towerHealth == null)
            {
                Debug.LogError($"❌ {playerTower.name} objesinde Health component'i yok!");
                enabled = false;
                return;
            }
            Debug.Log($"🏰 Oyuncu kalesi (önceden atanmış) bulundu: {playerTower.name}");
            return;
        }

        // Oyuncu kalesini bulmaya çalış
        // Önce tag ile
        playerTower = GameObject.FindGameObjectWithTag("Castle");
        // Eğer tag ile birden fazla bulunuyorsa tercih etmek için isim kontrolü
        if (playerTower != null && playerTower.name.ToLower().Contains("enemy"))
        {
            // Bulunan Castle objesi 'enemy' içeriyorsa muhtemelen kendi taraftır; aramaya devam
            playerTower = null;
        }

        if (playerTower == null)
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.ToLower().Contains("player") && obj.name.ToLower().Contains("castle"))
                {
                    playerTower = obj;
                    break;
                }
            }
        }

        if (playerTower == null)
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if ((obj.name.ToLower().Contains("kale") || obj.name.ToLower().Contains("tower")) && !obj.name.ToLower().Contains("enemy"))
                {
                    playerTower = obj;
                    break;
                }
            }
        }

        if (playerTower == null)
        {
            Debug.LogWarning("🏰 Uyarı: Oyuncu kalesi bulunamadı! Inspector'dan spawner.playerCastle atayabilirsiniz. Düşman bekleyecek ve daha sonra tekrar arayacak.");
            // Don't disable the component; allow enemy to continue moving and retry finding the tower later.
            return;
        }

        towerHealth = playerTower.GetComponent<Health>();
        if (towerHealth == null)
        {
            Debug.LogError($"❌ {playerTower.name} objesinde Health component'i yok!");
            enabled = false;
            return;
        }

        Debug.Log($"🏰 Oyuncu kalesi bulundu: {playerTower.name}");
    }

    // Spawner veya diğer sistemler tarafından atamak için
    public void SetPlayerTower(GameObject tower)
    {
        playerTower = tower;
        if (playerTower != null)
        {
            towerHealth = playerTower.GetComponent<Health>();
            if (towerHealth == null)
            {
                Debug.LogError($"❌ {playerTower.name} objesinde Health component'i yok! SetPlayerTower() kullanıldı.");
            }
            else
            {
                Debug.Log($"🏰 SetPlayerTower ile atandı: {playerTower.name}");
            }
        }
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
        // Oyuncu kalesi kontrolü
        if (playerTower == null || towerHealth == null)
        {
            Debug.LogWarning($"⚠️ {enemyType}: Oyuncu kalesi kayboldu! Yeniden aranıyor...");
            FindPlayerTower();
            if (playerTower == null)
            {
                // Bulunamadıysa öldürme; sadece uyar ve devam et. Daha sonra tekrar aranacak.
                Debug.LogWarning($"⚠️ {enemyType}: Oyuncu kalesi hâlâ bulunamadı. Düşman bekleyecek ve yola devam edecek.");
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
            if (currentTargetPlayer == null)
            {
                // Target lost or destroyed - resume moving
                isAttackingUnit = false;
                isMoving = true;
            }
            else
            {
                AttackPlayer(currentTargetPlayer);
            }
        }
        else if (isAttackingPlayer)
        {
            AttackPlayerTower();
        }
    }

    void MoveAlongPath()
    {
        // Oyuncu karakter kontrolü - önce oyuncu karakterlerle savaş
        GameObject nearestPlayer = FindNearestPlayer();
        // Calculate aggroRange in outer scope so fallbacks can reuse it
        float aggroRange = Mathf.Max(attackRange * 3f, 5f);
        if (enemyType == EnemyType.EnemyTank || enemyType == EnemyType.EnemyWarrior)
        {
            aggroRange = Mathf.Max(attackRange * 4f, 6f);
        }

        // Eğer yakın bir same-lane oyuncu varsa onu hedefle (sadece Z ekseni mesafesi ile)
        if (nearestPlayer != null)
        {
            float zDistToPlayer = Mathf.Abs(transform.position.z - nearestPlayer.transform.position.z);
            Debug.Log($"🔎 Enemy {gameObject.name} found player {nearestPlayer.name} (lane enemy={laneId}, player={nearestPlayer.GetComponent<Character>()?.laneId ?? -1}), zDist={zDistToPlayer:F2}, aggroRangeZ={aggroRange:F2}");
            if (zDistToPlayer <= aggroRange)
            {
                isMoving = false;
                isAttackingUnit = true;
                currentTargetPlayer = nearestPlayer;
                attackStartTime = Time.time;
                Debug.Log($"⚔️ {enemyType} locked target {nearestPlayer.name} at zDist {zDistToPlayer:F2}");
                return;
            }
            else
            {
                Debug.Log($"ℹ️ {enemyType} sees player but out of Z aggro range: {zDistToPlayer:F2} > {aggroRange:F2}");
            }
        }

        // Fallback: aynı lane içindeki tüm Character'ları tarayıp Z mesafesine göre yakın olanı seç
        float contactRange = (enemyType == EnemyType.EnemyTank || enemyType == EnemyType.EnemyWarrior)
            ? Mathf.Max(aggroRange, attackRange * 4f)
            : Mathf.Max(aggroRange, attackRange * 3f);
        Character[] allPlayers = FindObjectsOfType<Character>();
        GameObject best = null;
        float bestDist = float.MaxValue;
        foreach (var p in allPlayers)
        {
            if (p == null || !p.IsAlive) continue;
            // Sadece aynı lane ve Z ekseni mesafesi
            if (p.laneId != this.laneId) continue;
            float d = Mathf.Abs(transform.position.z - p.transform.position.z);
            if (d <= contactRange && d < bestDist)
            {
                bestDist = d;
                best = p.gameObject;
            }
        }

        if (best != null)
        {
            Debug.Log($"🛰️ {enemyType} contact-detect {best.name} at zDist {bestDist:F2} (contactRangeZ={contactRange:F2})");
            isMoving = false;
            isAttackingUnit = true;
            currentTargetPlayer = best;
            attackStartTime = Time.time;
            Debug.Log($"⚔️ {enemyType} contact-locked target {best.name} at zDist {bestDist:F2}");
            return;
        }

        if(playerTower != null && towerHealth != null)
        {
            float distanceToTower = Vector3.Distance(transform.position, playerTower.transform.position);
            bool isRangedEnemy = (enemyType == EnemyType.EnemyArcher || enemyType == EnemyType.EnemyMage);

            if (distanceToTower <= attackRange)
            {
                // Hareketi durdur
                isMoving = false;
                isAttackingPlayer = true;
                attackStartTime = Time.time;

                // Saldırıyı başlat
                return;
            }
        }

        // Oyuncu kalesi kontrolü - hareket sırasında da kontrol et
        if (playerTower != null && towerHealth != null)
        {
            float distanceToTower = Vector3.Distance(transform.position, playerTower.transform.position);
            bool isRangedEnemy = (enemyType == EnemyType.EnemyArcher || enemyType == EnemyType.EnemyMage);
            
            Debug.Log($"🔍 {enemyType} - Kale mesafesi: {distanceToTower:F2}, Saldırı menzili: {attackRange:F2}, Uzaktan: {isRangedEnemy}");
            
            // Uzaktan saldıran düşmanlar için menzil kontrolü
            if (isRangedEnemy && distanceToTower <= attackRange)
            {
                Debug.Log($"🏹 {enemyType} menzile girdi! Hareket durduruluyor, saldırıya başlıyor...");
                isMoving = false;
                isAttackingPlayer = true;
                attackStartTime = Time.time;
                return;
            }
            
            // Yakın dövüş düşmanları için de menzil kontrolü
            if (!isRangedEnemy && distanceToTower <= attackRange)
            {
                Debug.Log($"⚔️ {enemyType} menzile girdi! Hareket durduruluyor, saldırıya başlıyor...");
                isMoving = false;
                isAttackingPlayer = true;
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
        Vector3 forwardDir = zDiff >= 0f ? Vector3.forward : Vector3.back;
        transform.rotation = Quaternion.LookRotation(forwardDir);

        // Hareket: sadece Z ekseninde değişiklik yap
        // Use transform.position for movement to avoid physics collisions pushing the enemy backwards
        Vector3 newPos = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        transform.position = newPos;

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
        // Eğer oyuncu kalesi bulunamazsa yok etme; bekle veya tekrar ara
        if (playerTower == null || towerHealth == null)
        {
            Debug.LogWarning($"❌ {gameObject.name} hedefe ulaştı ama oyuncu kalesi bulunamadı! Düşman bekliyor ve tekrar arayacak.");
            // İlerde tekrar aramak için isMoving false bırak
            return;
        }

        isAttackingPlayer = true;
        attackStartTime = Time.time;
        Debug.Log($"🎯 {gameObject.name} hedefe ulaştı! Oyuncu kalesine saldırı başlıyor...");
    }

    void AttackPlayerTower()
    {
        if (playerTower == null || towerHealth == null)
        {
            Debug.LogError($"❌ {gameObject.name}: Kale veya Health component'i bulunamadı!");
            return;
        }

        // Timeout kontrolü
        if (Time.time - attackStartTime > maxAttackTime)
        {
            Debug.Log($"⏰ {enemyType} çok uzun süre saldırıyor! Zaman aşıldı, saldırı sıfırlanıyor...");
            // Önceki davranış: Die() --- bu durumda düşman gereksiz yere yok ediliyordu.
            // Şimdi saldırı zamanlayıcısını sıfırlayıp yeniden deneyeceğiz.
            attackStartTime = Time.time;
        }

        float distanceToTower = Vector3.Distance(transform.position, playerTower.transform.position);
        bool isRangedEnemy = (enemyType == EnemyType.EnemyArcher || enemyType == EnemyType.EnemyMage);
        // Ranged enemies should try to stand at a desired Z (towerZ +/- attackRange) before attacking.
        if (isRangedEnemy)
        {
            float towerZ = playerTower.transform.position.z;
            float zDiff = towerZ - transform.position.z;
            float desiredZ = zDiff >= 0f ? towerZ - attackRange : towerZ + attackRange;
            // If not at desired Z (within tolerance), move toward it
            if (Mathf.Abs(transform.position.z - desiredZ) > 0.2f)
            {
                Debug.Log($"🏹 {enemyType} yaklaşma: currentZ={transform.position.z:F2}, desiredZ={desiredZ:F2}, towerZ={towerZ:F2}");
                Vector3 targetPositionZ = new Vector3(transform.position.x, transform.position.y, desiredZ);
                transform.position = Vector3.MoveTowards(transform.position, targetPositionZ, moveSpeed * Time.deltaTime);
                return;
            }
            // else fall through to attack if in range
        }

        if (distanceToTower <= attackRange)
        {
            if (attackCooldown <= 0f)
            {
                towerHealth.TakeDamage(damage);
                attackCooldown = 1f / attackSpeed;
            }
        }
        else
        {
            if (!isRangedEnemy)
            {
                // Melee düşman yaklaşsın: sadece Z ekseninde ilerle
                float targetZ = playerTower.transform.position.z;
                Vector3 currentPos = transform.position;
                Vector3 targetPosition = new Vector3(currentPos.x, currentPos.y, targetZ);

                // Rotate to face +Z or -Z
                float zDiff = targetZ - currentPos.z;
                Vector3 forwardDir = zDiff >= 0f ? Vector3.forward : Vector3.back;
                transform.rotation = Quaternion.LookRotation(forwardDir);

                Vector3 newPos = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                transform.position = newPos;
            }
            else
            {
                // Ranged düşman: hedef menzile gelene kadar Z ekseninde yaklaşsın, sonra sabit kalsın ve saldırsın
                float towerZ = playerTower.transform.position.z;
                float currentZ = transform.position.z;
                float zDiff = towerZ - currentZ;

                // İdeal konum: kuleye attackRange uzaklıkta durmak
                float desiredZ = zDiff >= 0f ? towerZ - attackRange : towerZ + attackRange;

                // Yalnızca Z ekseninde ilerle
                Vector3 targetPositionZ = new Vector3(transform.position.x, transform.position.y, desiredZ);

                // Dönüşü ayarla (ileriye/geriye)
                Vector3 forwardDir = zDiff >= 0f ? Vector3.forward : Vector3.back;
                transform.rotation = Quaternion.LookRotation(forwardDir);

                // Yaklaş (sadece Z)
                transform.position = Vector3.MoveTowards(transform.position, targetPositionZ, moveSpeed * Time.deltaTime);
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
        Debug.Log($"💀 {enemyType} öldü! Düşman yok ediliyor...");
        isMoving = false;
        isAttackingPlayer = false;
        enabled = false;
        
        if (gameObject != null)
        {
            Debug.Log($"🗑️ {enemyType} tamamen yok edildi!");
            Destroy(gameObject);
        }
    }

    // Public API
    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints ?? throw new System.ArgumentNullException(nameof(newWaypoints));
        currentWaypointIndex = 0;
        isMoving = true;
        isAttackingPlayer = false;
        Debug.Log($"🗺️ Waypoints güncellendi: {waypoints.Length} adet");
        // laneId otomatik tespiti
        int found = WaypointHolder.FindLaneIdByWaypoints(waypoints);
        if (found >= 0) laneId = found;
        Debug.Log($"🔢 laneId set to {laneId}");
    }

    // Yeni public API: yol üzerinde hangi waypoint'ten başlayacağını ayarlamak için kullan
    public void SetStartingWaypointIndex(int startingIndex)
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"⚠️ {gameObject.name}: Waypoints tanımlı değil, startingIndex ayarlanamadı.");
            return;
        }

        if (startingIndex < 0 || startingIndex >= waypoints.Length)
        {
            Debug.LogWarning($"⚠️ {gameObject.name}: Geçersiz startingIndex ({startingIndex}). Varsayılan 0 kullanılıyor.");
            currentWaypointIndex = 0;
        }
        else
        {
            currentWaypointIndex = startingIndex;
            // Eğer spawn pozisyonunu waypoint'e taşımak isterseniz bunu spawner yapmalı.
            Debug.Log($"📍 {gameObject.name} yol üzerinde {currentWaypointIndex}. waypoint'ten başlatıldı");
        }

        isMoving = true;
        isAttackingPlayer = false;
    }

    public bool IsAlive => health > 0;
    public int CurrentHealth => health;
    public int MaxHealth => maxHealth;
    public float HealthPercentage => (float)health / maxHealth;

    // Oyuncu karakter bulma ve saldırı sistemi
    GameObject FindNearestPlayer()
    {
        Character[] players = FindObjectsOfType<Character>();
        if (players.Length == 0) return null;

        GameObject nearestPlayer = null;
        float nearestZDist = float.MaxValue;

        foreach (Character player in players)
        {
            if (player == null || !player.IsAlive) continue;
            // Sadece aynı lane içindekileri dikkate al
            if (player.laneId != this.laneId) continue;
            float zDist = Mathf.Abs(transform.position.z - player.transform.position.z);
            if (zDist < nearestZDist)
            {
                nearestZDist = zDist;
                nearestPlayer = player.gameObject;
            }
        }

        return nearestPlayer;
    }

    void AttackPlayer(GameObject player)
    {
        if (player == null)
        {
            isAttackingUnit = false;
            currentTargetPlayer = null;
            isMoving = true;
            return;
        }

        Character playerScript = player.GetComponent<Character>();
        if (playerScript == null || !playerScript.IsAlive)
        {
            isAttackingUnit = false;
            currentTargetPlayer = null;
            isMoving = true;
            return;
        }

        // Saldırı ve menzil kontrolü: sadece Z ekseni mesafesi
        float zDistToPlayer = Mathf.Abs(transform.position.z - player.transform.position.z);
        Debug.Log($"🔔 AttackPlayer: {gameObject.name} -> {player.name}, zDist={zDistToPlayer:F2}, attackRangeZ={attackRange:F2}, cooldown={attackCooldown:F2}");

        if (zDistToPlayer <= attackRange + rangeTolerance)
        {
            bool forced = forceContactDamageForDebug;
            if (attackCooldown <= 0f || forced)
            {
                if (forced && attackCooldown > 0f)
                {
                    Debug.Log($"🔧 {enemyType} debug-bypass cooldown to force damage on {player.name}");
                }

                int oldHealth = playerScript.CurrentHealth;
                Debug.Log($"🔫 {enemyType} attacking {player.name}: oldHP={oldHealth}, dmg={damage}");
                playerScript.TakeDamage(damage);
                int newHealth = playerScript.CurrentHealth;

                Debug.Log($"💥 {enemyType} -> {playerScript.characterType}: {damage} hasar! ({oldHealth} -> {newHealth})");
                // If forced, give a tiny cooldown so it's not continuous spam; otherwise use normal cooldown
                attackCooldown = forced ? 0.1f : 1f / attackSpeed;
            }
            else
            {
                Debug.Log($"⏳ {enemyType} attack on cooldown: {attackCooldown:F2}s left");
            }

            if (!playerScript.IsAlive)
            {
                // Hedef öldü, saldırıyı sonlandır ve yola devam et
                Debug.Log($"☠️ Target {player.name} died, resuming movement");
                isAttackingUnit = false;
                currentTargetPlayer = null;
                isMoving = true;
            }
        }
        else
        {
            // Oyuncuya yaklaş (sadece Z ekseninde) - kovalamada biraz daha hızlı git
            float targetZ = player.transform.position.z;
            Vector3 currentPos = transform.position;
            Vector3 targetPosition = new Vector3(currentPos.x, currentPos.y, targetZ);

            float zDiff = targetZ - currentPos.z;
            Vector3 forwardDir = zDiff >= 0f ? Vector3.forward : Vector3.back;
            transform.rotation = Quaternion.LookRotation(forwardDir);

            float chaseSpeed = moveSpeed * Mathf.Max(1f, chaseSpeedMultiplier);
            Vector3 newPos = Vector3.MoveTowards(transform.position, targetPosition, chaseSpeed * Time.deltaTime);
            transform.position = newPos;
            // Debug z-mesafesi kapanıyor mu takip et
            float newZDist = Mathf.Abs(transform.position.z - player.transform.position.z);
            Debug.Log($"🏃‍♂️ ChaseZ: {gameObject.name} zDist {zDistToPlayer:F2} -> {newZDist:F2} (speed {chaseSpeed:F2})");

            // Çok yakına geldiysek jitteri engellemek için Z'yi eşitle
            if (newZDist <= rangeTolerance * 0.5f)
            {
                Vector3 snap = transform.position;
                snap.z = player.transform.position.z;
                transform.position = snap;
            }

            // Eğer zDist büyüyorsa (kaçırıyoruz), geçici olarak hızlandır
            if (newZDist > _lastChaseZDist + 0.05f)
            {
                float boost = chaseSpeed * 0.5f * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, boost);
            }
            _lastChaseZDist = newZDist;
        }
    }
}
