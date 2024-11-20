using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 1;
    public float direction;
    int layerMask;
    RaycastHit2D raycast;
    
    private void Awake() {
        layerMask = LayerMask.GetMask("Walls");
        // transform.localScale = new Vector3(1,1,1);
    }

    private void FixedUpdate() {
        transform.position += Vector3.right * (speed * direction);

        raycast = Physics2D.Raycast(transform.position, Vector2.right*direction, Mathf.Infinity, layerMask);

        if(raycast.collider != null && raycast.distance == 0) {
            Destroy(gameObject);
        }
    }
    
    
}
