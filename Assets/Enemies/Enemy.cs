using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    protected Vector2 bottomLeft => new Vector2(transform.position.x - transform.localScale.x/2, transform.position.y - transform.localScale.y);
    protected Vector2 bottomRight => new Vector2(transform.position.x + transform.localScale.x/2, transform.position.y - transform.localScale.y);
    protected Vector2 centerLeft => new Vector2(transform.position.x - transform.localScale.x/2, transform.position.y);
    protected Vector2 centerRight => new Vector2(transform.position.x + transform.localScale.x/2, transform.position.y);

    protected RaycastHit2D leftFloorRay;
    protected RaycastHit2D rightFloorRay;
    protected RaycastHit2D leftWallRay;
    protected RaycastHit2D rightWallRay;

    protected int layerMask;

    [SerializeField] protected int health = 5;
    [SerializeField] protected int damage = 1;
    [SerializeField] protected float moveSpeed;
    [SerializeField] protected GameObject deathParticle;

    [SerializeField] Vector2 velocity;
    Vector2 storedVelocity;
    protected bool isPaused;
    protected bool wasPausedLastFrame;

    protected Rigidbody2D rb;
    protected SpriteRenderer sprite;
    protected Player_Controller player;

    virtual protected void Start() {
        velocity.x = moveSpeed;
        gameObject.layer = LayerMask.NameToLayer("Enemy");
        layerMask = LayerMask.GetMask("Walls");
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        player = FindAnyObjectByType<Player_Controller>();
    }

    virtual protected void FixedUpdate() {
        leftFloorRay = Physics2D.Raycast(bottomLeft, Vector2.down, Mathf.Infinity, layerMask);
        rightFloorRay = Physics2D.Raycast(bottomRight, Vector2.down, Mathf.Infinity, layerMask);
        leftWallRay = Physics2D.Raycast(centerLeft, Vector2.left, Mathf.Infinity, layerMask);
        rightWallRay = Physics2D.Raycast(centerRight, Vector2.right, Mathf.Infinity, layerMask);


        if ((leftFloorRay.distance > 0.1f && rightFloorRay.distance < 0.1f && velocity.x < 0) ||
            (rightFloorRay.distance > 0.1f && leftFloorRay.distance < 0.1f && velocity.x > 0) ||
            (leftWallRay.distance < 0.1f && velocity.x < 0 ) ||
            (rightWallRay.distance < 0.1f && velocity.x > 0))
        {
            velocity.x *= -1;
            sprite.flipX = ! sprite.flipX;
        }

        isPaused = player.inImpactFrames;
        if(!isPaused && !wasPausedLastFrame) {
            rb.velocity = new Vector3(velocity.x, rb.velocity.y) / (60*Time.deltaTime);
        }
        else if(!isPaused && wasPausedLastFrame) {
            rb.velocity = storedVelocity;
        }
        else if(isPaused && !wasPausedLastFrame) {
            storedVelocity = rb.velocity;
            rb.velocity = Vector3.zero;
        }
        else if(isPaused && wasPausedLastFrame) {
            rb.velocity = Vector3.zero;
        }

        wasPausedLastFrame = isPaused;
    }

    virtual protected void OnTriggerStay2D(Collider2D other) {
        if(other.tag == "Player_Attack") {
            Instantiate(deathParticle, transform.position, Quaternion.Euler(0,0,180));
            Destroy(gameObject);
        }
        else if(other.tag == "Player") {
            other.GetComponent<Player_Controller>().TakeDamage(damage);
        }
    }
}
