using UnityEngine;

public class TestDamage : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GameObject castle = GameObject.FindGameObjectWithTag("Castle");
            if (castle != null)
            {
                castle.GetComponent<Health>().TakeDamage(10);
                Debug.Log("Kale caný: " + castle.GetComponent<Health>().GetCurrentHealth());
            }
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            GameObject enemyCastle = GameObject.FindGameObjectWithTag("EnemyCastle");
            if (enemyCastle != null)
            {
                enemyCastle.GetComponent<Health>().TakeDamage(10);
                Debug.Log("Düþman kulesi caný: " + enemyCastle.GetComponent<Health>().GetCurrentHealth());
            }
        }
    }
}