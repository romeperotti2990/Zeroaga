using UnityEngine;

public class Projectile : MonoBehaviour
{
    float speed = 40f;
    float lifetime = 1.5f;

    void Start()
    {
        // add colider stuff lol
        Destroy(gameObject, lifetime);
    }
    void Update()
    {
        transform.position += transform.up * speed * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // handle hit 
        //Destroy(gameObject);
    }

}
