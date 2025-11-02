using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyTankPrefab;
    public GameObject enemyWarriorPrefab;
    public GameObject enemyArcherPrefab;
    public GameObject enemyMagePrefab;

    // Her bir öğe bir yol(waypoint) grubu olacak. Inspector'da yolları buraya atayın.
    public WaypointHolder[] spawnPaths; // Düşman spawn path'leri (ör: lane başları)
    // Sahnedeki oyuncu kalesini inspector'dan atayabilirsiniz. Eğer boşsa EnemyCharacter kendi aramasını yapar.
    public GameObject playerCastle;
    public float spawnInterval = 5f;

    private float timer = 0f;
    private int _lastChosenPath = -1;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnRandomEnemy();
            timer = 0f;
        }
    }

    void SpawnRandomEnemy()
    {
        if (spawnPaths == null || spawnPaths.Length == 0) return;

        // Rastgele bir yol seç (mümkünse bir öncekiyle aynı olmayan)
        if (spawnPaths.Length == 0) return;
        int chosenIndex = Random.Range(0, spawnPaths.Length);
        // Eğer birden fazla path varsa, öncekiyle aynı olmamaya çalış
        if (spawnPaths.Length > 1)
        {
            int attempts = 0;
            while (chosenIndex == _lastChosenPath && attempts < 5)
            {
                chosenIndex = Random.Range(0, spawnPaths.Length);
                attempts++;
            }
        }

        _lastChosenPath = chosenIndex;
        WaypointHolder chosenPath = spawnPaths[chosenIndex];
        if (chosenPath == null || chosenPath.waypoints == null || chosenPath.waypoints.Length == 0) return;

        GameObject prefab = GetRandomEnemyPrefab();
        if (prefab == null) return;

        // Spawn'u yolun rastgele bir waypoint'indan yap (path boyunca değişiklik olsun)
        int startIndex = Random.Range(0, chosenPath.waypoints.Length);
        Transform startWp = chosenPath.waypoints[startIndex];
        Vector3 spawnPos = startWp != null ? startWp.position : chosenPath.transform.position;

        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
        EnemyCharacter enemyScript = enemy.GetComponent<EnemyCharacter>();
        if (enemyScript != null)
        {
            enemyScript.SetWaypoints(chosenPath.waypoints);
            enemyScript.SetStartingWaypointIndex(startIndex);
            // set lane id from chosen path if available
            enemyScript.laneId = chosenPath.laneId;
            // Eğer spawner'da playerCastle atanmışsa düşmana set et
            if (playerCastle != null)
            {
                enemyScript.SetPlayerTower(playerCastle);
            }
        }
    }

    GameObject GetRandomEnemyPrefab()
    {
        int rand = Random.Range(0, 4);
        return rand switch
        {
            0 => enemyTankPrefab,
            1 => enemyWarriorPrefab,
            2 => enemyArcherPrefab,
            3 => enemyMagePrefab,
            _ => null
        };
    }
}
