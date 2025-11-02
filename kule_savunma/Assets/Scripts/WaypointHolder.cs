using UnityEngine;

public class WaypointHolder : MonoBehaviour
{
    public Transform[] waypoints;
    // Otomatik atanan yol/koridor kimliği
    [HideInInspector]
    public int laneId = -1;

    // Statik kayıt tüm WaypointHolder örnekleri için yardımcı fonksiyonlar sağlar
    private static System.Collections.Generic.List<WaypointHolder> _allHolders = new System.Collections.Generic.List<WaypointHolder>();

    void Awake()
    {
        // Kaydet
        _allHolders.Add(this);
        // Basit artan id
        laneId = _allHolders.Count - 1;
    }

    void OnDestroy()
    {
        _allHolders.Remove(this);
    }

    public static int FindLaneIdByWaypoints(Transform[] wps)
    {
        if (wps == null) return -1;
        foreach (var holder in _allHolders)
        {
            if (holder == null) continue;
            var hw = holder.waypoints;
            if (hw == null) continue;
            if (hw.Length != wps.Length) continue;
            bool equal = true;
            for (int i = 0; i < hw.Length; i++)
            {
                if (hw[i] != wps[i]) { equal = false; break; }
            }
            if (equal) return holder.laneId;
        }
        return -1;
    }

    public static int FindClosestLane(Vector3 position)
    {
        int bestId = -1;
        float bestDist = float.MaxValue;
        foreach (var holder in _allHolders)
        {
            if (holder == null || holder.waypoints == null || holder.waypoints.Length == 0) continue;
            // Use average position of waypoints as lane center
            Vector3 avg = Vector3.zero;
            foreach (var w in holder.waypoints) avg += w.position;
            avg /= holder.waypoints.Length;
            float d = Vector3.Distance(position, avg);
            if (d < bestDist)
            {
                bestDist = d;
                bestId = holder.laneId;
            }
        }
        return bestId;
    }
}
