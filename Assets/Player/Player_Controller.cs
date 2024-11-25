using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]

public class Player_Controller : MonoBehaviour
{
#region Class Variables
	protected float colliderOffset = 0.1f;
	protected float root2Over2 = math.sqrt(2)/2;

	// math and collision variables----------------------------------
	protected Vector2 bottomCenter => new Vector2(transform.position.x, transform.position.y-(transform.localScale.y*boxCollider.size.y/2));
	protected Vector2 topCenter => new Vector2(transform.position.x, transform.position.y+(transform.localScale.y*boxCollider.size.y/2));
	protected Vector2 leftCenter => new Vector2(transform.position.x-(transform.localScale.x*boxCollider.size.x/2), transform.position.y);
	protected Vector2 rightCenter => new Vector2(transform.position.x+(transform.localScale.x*boxCollider.size.x/2), transform.position.y);
	protected Vector2 verticalRaySize => new Vector2(transform.localScale.x*boxCollider.size.x, colliderOffset);
	// protected int wallLayerMask;
	protected int floorLayerMask;
	protected RaycastHit2D rayToGround;
	protected RaycastHit2D rayToCeiling;
	protected RaycastHit2D rayToLeftWall;
	protected RaycastHit2D rayToRightWall;
	protected float distanceToGround;
	protected float distanceToCeiling;
	protected float distanceToLeftWall;
	protected float distanceToRightWall;
	//---------------------------------------------------------------

	// components----------------------------------------------------
	[SerializeField] protected BoxCollider2D boxCollider;
	protected SpriteRenderer spriteRenderer;
	protected Rigidbody2D rb;
	//---------------------------------------------------------------

	// serialized constant variables---------------------------------
	[Header("Combat")]
		[SerializeField] protected int health = 3;
		[SerializeField] protected int meleeDamage = 2;
		[SerializeField] protected int rangedDamage = 1;
		[Tooltip("The number of frames the game should freeze for when the player takes damage")]
		[SerializeField] protected int impactFrames = 10;
		[Tooltip("The number of frames the player should freeze for when shooting a projectile")]
		[SerializeField] protected int projectileImpactFrames = 15;
		[Tooltip("The number of frames the player must wait before shooting again")]
		[SerializeField] protected int timeBetweenShots = 15;
		[Tooltip("The number of frames the player is invulnerable for after taking damage")]
		[SerializeField] protected int invulnerabilityFrames = 75;
	[Header("Movement")]
		[Tooltip("The speed at which the player walks, bigger number means they walk faster")]
		[SerializeField] [Range(0,10)] protected float walkSpeed = 4;
		[Tooltip("The speed at which the player accelerates when walking on the ground, bigger number means they accelerate faster")]
		[SerializeField] [Range(0,3)] protected float walkAcceleration = 1;
	[Header("Jump & Gravity")]
		[Tooltip("The upwards force that is applied to the player when they jump, bigger number means higher jump")]
		[SerializeField] protected float jumpPower = 22;
		[Tooltip("The upwards force that is applied to the player when they wall jump, bigger number means higher jump")]
		[SerializeField] protected float wallJumpPower = 18;
		[Tooltip("The horizontal force that is applied to the player when they wall jump, bigger number means they are pushed farther off the wall")]
		[SerializeField] protected float wallJumpPushBack = 18;
		[Tooltip("The maximum downwards velocity the player can have while falling, bigger number means falling faster")]
		[SerializeField] protected float maxFallSpeed = 20;
		[Tooltip("The downwards force that is applied to the player each frame they are in the air, bigger number means they accelerate downwards faster")]
		[SerializeField] protected float gravity = 1;
		[Tooltip("The amount gravity is multiplied by when the player is not holding the jump button, bigger number means they fall faster when not holding the jump button")]
		[SerializeField] protected float jumpNotHeldModifier = 3;
		[Tooltip("The range (from positive to negative) of Y velocities where the gravity is halved at the peak of the player's jump, bigger number means they are floatier at the peak of their jump")]
		[SerializeField] [Range(0,5)] float halfGravityVelocity = 2.5f;
		[Tooltip("The maximum speed the player will fall at when they are sliding down a wall, bigger number means falling faster")]
		[SerializeField] float maxWallSlideVelocity = 0.5f; 
	[Header("Dash")]
		[Tooltip("The number of dashes the player gets while in mid air")]
		[SerializeField] [Range(0,5)] protected int numDashes = 1;
		[Tooltip("The amount of velocity that is applied to the player when they dash, bigger number means faster dash")]
		[SerializeField] protected float dashVelocity = 10;
		[Tooltip("The number of frames the game pauses for when the player dashes, bigger number means longer pause")]
		[SerializeField] [Range(0,10)] protected int dashStartPause = 3;
		[Tooltip("The number of frames that the player has no gravity while dashing, bigger number means floatier dash")]
		[SerializeField] [Range(0,30)] protected int dashFrames = 10;
		[Tooltip("The number of frames the player has to wait between dashes, bigger number means more time between dashes")]
		[SerializeField] [Range(0,60)] protected int timeBetweenDashes = 30;
	[Header("Systems")]
		[Tooltip("The number of frames the player can still jump after leaving the ground, bigger number means they can jump longer after leaving the ground")]
		[SerializeField] [Range(0,20)] protected int coyoteTime = 5;
		[Tooltip("The number of frames that the game will store an input before the action can be executed, bigger number means they can push a button earlier than it is registered")]
		[SerializeField] [Range(0,20)] protected int inputBufferFrames = 5;
		[Tooltip("The distance away from the wall that the player can be and still wall jump, bigger number means they can wall jump from farther away")]
		[SerializeField] [Range(0,2)] protected float distanceToWallJump = 0.5f;
		[Tooltip("The dead zone for a joystick if a player is using a controller, bigger number means larger dead zone")]
		[SerializeField] [Range(0,0.5f)] protected float stickDeadZone = 0.01f;
	[Header("Misc")]
		[SerializeField] protected GameObject jumpParticle;
		[SerializeField] protected GameObject attackObject;
		[SerializeField] protected GameObject rangedAttackObject;
		[SerializeField] protected GameObject takeDamageParticle;
	//---------------------------------------------------------------

	// state variables-----------------------------------------------
	protected int remainingCoyoteTime;
	protected int remainingDashes;
	protected int timeSinceLastDash = 60;
	protected int timeSinceLastShot = 60;
	protected int timeSinceLastDamage;
	protected float downwardsAcceleration;
	protected float targetFallSpeed;
	protected bool isGrounded;
	protected bool canJump;
	protected bool currentlyDashing;
	public bool inImpactFrames;
	protected bool shouldFindGround = true;
	[Tooltip("This is only serialized so that velocity can be seen in the editor, don't change its value")]
	[SerializeField] protected Vector2 velocity = new(0,0);
	//---------------------------------------------------------------

	// input stuff---------------------------------------------------
	InputAction moveInput;
    InputAction jumpButton;
	InputAction dashButton;
	InputAction attackButton;
	InputAction shootButton;
	InputAction pauseButton;
	protected FrameInput frameInput = new();
	protected List<BufferedInput> bufferedInputs = new();
	//---------------------------------------------------------------
#endregion

#region Unity Methods
	void Start() {
		moveInput = InputSystem.actions.FindAction("Move");
		jumpButton = InputSystem.actions.FindAction("Jump");
		dashButton = InputSystem.actions.FindAction("Dash");
		attackButton = InputSystem.actions.FindAction("Attack");
		shootButton = InputSystem.actions.FindAction("Shoot");
		pauseButton = InputSystem.actions.FindAction("Pause");

		QualitySettings.vSyncCount = 0; // turn off v-sync
		Application.targetFrameRate = 60; // set the max fps

		rb = GetComponent<Rigidbody2D>();
		// boxCollider = GetComponent<BoxCollider2D>();
		spriteRenderer = GetComponentInChildren<SpriteRenderer>();

		floorLayerMask = LayerMask.GetMask("Walls");

		// Cursor.visible = false;
	}

	// Update is called once per frame
	void Update() {
		IncrementFrameCounter();
		// Debug.Log(health);
		GetInput(); // this MUST come first in the list since it dictates everything that will happen below
		RayCast(); // this MUST come second in the list since it changes how most of the following will work
		ProcessJump(); // this MUST come before ProcessGravity()
		ProcessGravity();
		ProcessMovement();
		ProcessDash(); // this, ProcessAttack(), or ProcessRangedAttack() MUST come last in the list because they can modify the players input
		ProcessAttack();
		ProcessRangedAttack();

		rb.velocity = velocity / (60*Time.deltaTime);

		RemoveExpiredInputsFromBuffer();
		ProcessAnimation();
		if(pauseButton.WasPressedThisFrame()) {
			Application.Quit();
		}
	}

	void OnDrawGizmos() { // this method allows ray casts and velocities to be visible in the editor 
		if(!Application.isPlaying) { return; }
		Gizmos.color = Color.white;
		Gizmos.DrawRay(transform.position, velocity); // draw a vector to the screen to represent the player's velocity

		Gizmos.color = Color.cyan;
		Vector3[] positions = new Vector3[] {bottomCenter, rayToGround.centroid};
		Gizmos.DrawLineStrip(positions, false); // draw a line to the point on the ground that the player is snapped to
	}
#endregion

	void IncrementFrameCounter() {
		timeSinceLastDamage ++;
		timeSinceLastDash ++;
		timeSinceLastShot ++;
	}

#region Input
	void GetInput() {
		frameInput = new FrameInput{
			JumpDown = jumpButton.WasPressedThisFrame(), // checks if the jump button was pressed this frame
			JumpHeld = jumpButton.IsPressed(), // checks if the jump button is being held
			Move = moveInput.ReadValue<Vector2>(), // gets the user's directional input
			AttackPressed = attackButton.WasPressedThisFrame(),
			RangedAttackPressed = shootButton.WasPressedThisFrame(),
			DashPressed = dashButton.WasPressedThisFrame()
		};
		frameInput.Move.x = Mathf.Abs(frameInput.Move.x) < stickDeadZone ? 0 : Mathf.Sign(frameInput.Move.x); // this line and the one below it add dead zones for joysticks and make inputs locked to 8 directions
		frameInput.Move.y = Mathf.Abs(frameInput.Move.y) < stickDeadZone ? 0 : Mathf.Sign(frameInput.Move.y);
	}

	void RemoveExpiredInputsFromBuffer() {
		for(int i=0; i < bufferedInputs.Count; i++){ // reduce the remaining frames on each buffered input by 1
			bufferedInputs[i] = new BufferedInput{InputType = bufferedInputs[i].InputType, FramesUntilDropped = bufferedInputs[i].FramesUntilDropped - 1};
			
		}
		while(true) { // clear out expired buffered inputs
			if(bufferedInputs.Count == 0) { return; } // no need to clear buffered inputs if there aren't any
			if(bufferedInputs[0].FramesUntilDropped > 0) { return; } // if the oldest input has not yet expired, end the method
			bufferedInputs.Remove(bufferedInputs[0]); // clear the oldest buffered input
		}
	}
#endregion

#region Jump
	void ProcessJump() {
		remainingCoyoteTime --; // reduce the amount of remaining coyote time the player has
		canJump = isGrounded || (remainingCoyoteTime > 0); // if the player is grounded or has remaining coyote time, they can jump

		if(frameInput.JumpDown && (!canJump || (distanceToLeftWall > distanceToWallJump && distanceToRightWall > distanceToWallJump))) { // if the player input a jump but cant jump
			bufferedInputs.Add(new BufferedInput{InputType = "jump", FramesUntilDropped = inputBufferFrames}); // add a jump to the input buffer
		}

		for(int i = 0; i < bufferedInputs.Count; i++){ // go through each input in the buffer
			if(bufferedInputs[i].InputType.Equals("jump")) { // if there is a jump input buffered and the player can jump this frame
				frameInput.JumpDown = true; // make the player input jump
			}
		}

		if(!frameInput.JumpDown) { return; } // if the player didn't press jump or have a buffered jump, return

		if(canJump) { // if the player is on the ground or in coyote frames
			Jump(); // do a normal jump
		}
		else if(distanceToLeftWall < distanceToWallJump && distanceToRightWall < distanceToWallJump) { // if the player is sandwiched between two walls
			WallJump(0); // do a wall jump without any horizontal velocity
		}
		else if(distanceToLeftWall < distanceToWallJump) { // if the player has a wall to the left of them
			WallJump(1); // wall jump away from the wall
		}
		else if(distanceToRightWall < distanceToWallJump) { // if the player has a wall to the right of them
			WallJump(-1); // wall jump away from the wall
		}
	}

	void StartJump() { // this is called at the beginning of all jump methods, both normal and wall jumps
		for(int i=0; i < bufferedInputs.Count; i++) { // go through the input buffer and remove all jump inputs
			if(bufferedInputs[i].InputType.Equals("jump")) {
				bufferedInputs[i] = new BufferedInput{FramesUntilDropped = -1}; //replace this buffered input with one that will be cleared at the end of the frame
			}
		}
		Instantiate(jumpParticle, transform.position, Quaternion.Euler(new Vector3(0, 0, 180))); // spawn in a particle
		currentlyDashing = false; // disable the player's dash if they are currently dashing
		shouldFindGround = false;
		isGrounded = false; // tell the game that they are not on the ground
		StartCoroutine(JumpStartTimer()); // this was implemented so that the game wouldn't eat the player's jump if they were slightly clipped into the ground
		remainingCoyoteTime = -1; // remove the player's ability to coyote time jump until they touch the ground again
	}

	void Jump() {
		StartJump();
		velocity.y = jumpPower; // set the player to moving upwards
	}

	void WallJump(int direction) {
		StartJump();
		velocity.y = wallJumpPower; // set the player to moving upwards
		velocity.x = wallJumpPushBack * direction; // push them away from the wall in the desired direction
	}

	IEnumerator JumpStartTimer() { // this was implemented so that the game wouldn't eat the player's jump if they were slightly clipped into the ground
		for(int i=0; i<3; i++) {
			yield return new WaitForFixedUpdate(); // wait 3 frames before checking for ground again
		}
		shouldFindGround = true;
	}

#endregion

#region Physics
	void RayCast() { // this method does all of the collision checking for the character controller
		rayToCeiling = Physics2D.BoxCast(topCenter, verticalRaySize, 0, Vector2.up, colliderOffset, floorLayerMask);
		rayToGround = Physics2D.BoxCast(bottomCenter, verticalRaySize, 0, Vector2.down, 100, floorLayerMask);
		
		rayToLeftWall = Physics2D.Raycast(leftCenter, Vector2.left, 100, floorLayerMask);
		distanceToLeftWall = FindRayDistance(rayToLeftWall);
		rayToRightWall = Physics2D.Raycast(rightCenter, Vector2.right, 100, floorLayerMask);
		distanceToRightWall = FindRayDistance(rayToRightWall);
	}

	void ProcessGravity() {
		if(shouldFindGround) { // this if statement is so that the game wouldn't eat the player's jump if they were slightly clipped into the ground
			FindGround();
		}
		FindCeiling();

		if(isGrounded && !frameInput.JumpDown) { // if the player is on the ground and didn't press the jump button
			remainingCoyoteTime = coyoteTime; // reset their coyote time
			velocity.y = 0; // set their vertical velocity to 0
			transform.Translate(0, -distanceToGround, 0);
			return; // and don't do any more gravity calculations
		}

		if(currentlyDashing || inImpactFrames) { return; } // if the player is dashing or is being hit, don't process gravity

		downwardsAcceleration = gravity; // initialize the amount the player's downward velocity should increase by
		targetFallSpeed = maxFallSpeed;
		
		if(velocity.y < 0 && ((distanceToLeftWall < colliderOffset && frameInput.Move.x < 0) || (distanceToRightWall < colliderOffset && frameInput.Move.x > 0))) {
			targetFallSpeed = maxWallSlideVelocity; // if the player is wall sliding, decrease the target fall speed
		}
		else if(velocity.y <= halfGravityVelocity && velocity.y >= -halfGravityVelocity) { // if the player is at the peak of their jump
			downwardsAcceleration /= 2; // give them half velocity
		}
		else if(!frameInput.JumpHeld && velocity.y > 0) { // if the player is moving upwards and is not holding a jump
			downwardsAcceleration *= jumpNotHeldModifier; // increase the force of gravity
		}
		
		velocity.y = Mathf.MoveTowards(velocity.y, -targetFallSpeed, downwardsAcceleration);
		
	}

	void FindGround() {
		distanceToGround = FindRayDistance(rayToGround);
		isGrounded = distanceToGround < colliderOffset; // if the player is close enough to the ground, mark them as grounded
	}
	void FindCeiling() {
		distanceToCeiling = FindRayDistance(rayToCeiling);
		if(distanceToCeiling < colliderOffset && velocity.y > 0) { // if the player is touching a ceiling and is moving upwards
			velocity.y = 0; // set their vertical velocity to 0
		}
	}

	protected float FindRayDistance(RaycastHit2D ray) {
		float distance = 500; // this is so that it returns a massively large number if there is no ground at all below the player
		if(ray.collider != null) { // if there is ground below the player
			distance = ray.distance;
		}
		return distance;
	}
#endregion

#region Movement
	void ProcessMovement() {
		if(!currentlyDashing && !inImpactFrames) { // if the player isn't dashing or taking damage
			velocity.x = Mathf.MoveTowards(velocity.x, frameInput.Move.x * walkSpeed, walkAcceleration); // accelerate or decelerate them accordingly
		}
		if((distanceToRightWall < colliderOffset && velocity.x > 0) || (distanceToLeftWall < colliderOffset && velocity.x < 0)) { 
			velocity.x = 0; // if the player is walking into a wall, stop their velocity
		}
	}
#endregion

#region Animation
	void ProcessAnimation() {
		if(frameInput.Move.x > 0) {
			spriteRenderer.flipX = false;
		}
		else if(frameInput.Move.x < 0) {
			spriteRenderer.flipX = true;
		}
	}
#endregion

#region Dash
	void ProcessDash() {
		if(isGrounded) { remainingDashes = numDashes; } // if the player is on the ground, give them their dashes back
		if(distanceToLeftWall < distanceToWallJump || distanceToRightWall < distanceToWallJump) { remainingDashes = numDashes; } // if the player is wall sliding, give them their dashes back

		if(!frameInput.DashPressed) { return; } // if the player didn't dash, don't dash
		if(remainingDashes <= 0) { return; } // if the player is out of dashes, don't dash
		if(currentlyDashing) { return; } // if the player is already dashing, don't dash
		if(inImpactFrames) { return; } // if the player is currently taking damage, don't dash
		if(timeSinceLastDash < timeBetweenDashes) { return ; } // if the player has dashed too recently, don't dash

		timeSinceLastDash = 0;
		StartCoroutine(Dash());
	}

	IEnumerator Dash() {
		Instantiate(jumpParticle, transform.position, Quaternion.Euler(new Vector3(0,0,270))); // spawn in the particle
		currentlyDashing = true;
		remainingDashes --;
		
		if(frameInput.Move.x == 0) { // if the player is not inputting a direction
			frameInput.Move.x = spriteRenderer.flipX ? -1 : 1; // force an inputted direction for this frame only
		}

		float preDashXInput = frameInput.Move.x;
		// Vector2 storedVelocity = velocity; // store the current velocity of the player

		for(int i=0; i<dashStartPause; i++) { // pause for a few frames at the start of the dash to add weight to it
			velocity = Vector2.zero;
			yield return new WaitForFixedUpdate();
		}
		// Instantiate(dashGhost, transform.parent); // spawn the dash ghost sprite
		if(frameInput.Move.x == 0) {
			velocity.x = preDashXInput * dashVelocity;
		}
		else{
			velocity.x = frameInput.Move.x * dashVelocity;
		}
		velocity.y = 0;

		StartCoroutine(DashTimer());
	}
	IEnumerator DashTimer() {
		Vector2 colliderSize = boxCollider.size;
		boxCollider.size = new Vector2(boxCollider.size.x, boxCollider.size.y/3);
		for (int i=0; i<dashFrames; i++) { // the number of frames the player should be dashing for
			if (currentlyDashing == false) { // if the dash ends prematurely
				boxCollider.size = colliderSize;
				yield break; // end the function
			}
			yield return new WaitForFixedUpdate();
		}
		currentlyDashing = false; // end the dash
		boxCollider.size = colliderSize;
	}

	// Below is a version of the dash method that allows for directional dashing --------------------------------------

	// 	IEnumerator DirectionalDash() {
	// 	currentlyDashing = true;
	// 	remainingDashes --;
		
	// 	if(frameInput.Move.x == 0 && frameInput.Move.y == 0) { // if the player is not inputting a direction
	// 		frameInput.Move.x = 1; // force an inputted direction for this frame only
	// 		if(spriteRenderer.flipX) { // if they are facing left
	// 			frameInput.Move.x = -1; // make the input go left
	// 		}
	// 	}

	// 	float dashAmount = dashVelocity; // store the amount the player should dash
	// 	if (frameInput.Move.x != 0 && frameInput.Move.y != 0) { // if the player is dashing at an angle
	// 		dashAmount *= root2Over2; // reduce the dash amount by sqrt(2)/2 because of Pythagorean Theorem
	// 	}

	// 	if (frameInput.Move.y < 0 && Mathf.Sign(frameInput.Move.x) == Mathf.Sign(velocity.x) && !isGrounded) { // if the player dashes down at an angle
	// 		velocity.x += frameInput.Move.x * dashAmount; // add the speed instead of setting it
	// 	}
	// 	else {
	// 		velocity.x = frameInput.Move.x * dashAmount;
	// 	}

	// 	velocity.y = frameInput.Move.y * dashAmount;
	// 	velocity.y = 0;

	// 	Vector2 storedVelocity = velocity; // store the current velocity of the player
	// 	for(int i=0; i<dashStartPause; i++) { // pause for a few frames at the start of the dash to add weight to it
	// 		velocity = Vector2.zero;
	// 		yield return new WaitForFixedUpdate();
	// 	}
	// 	// Instantiate(dashGhost, transform.parent); // spawn the dash ghost sprite
	// 	velocity = storedVelocity; // resume movement

	// 	StartCoroutine(DashTimer());
	// }
	// ----------------------------------------------------------------------------------------------------------------
#endregion

#region Attack
	void ProcessAttack() {
		if(!frameInput.AttackPressed) { return; } // if the player didn't press the attack button, don't attack
		if(currentlyDashing || inImpactFrames) { return; } // if the player is dashing or is taking damage, don't attack

		if(frameInput.Move.x == 0) { // if the player is not inputting a direction
			frameInput.Move.x = spriteRenderer.flipX ? -1 : 1; // force an input based on which direction the character is facing
		}
		GameObject attack = Instantiate(attackObject, transform); // spawn in the attack object
		attack.transform.localScale = new Vector3(frameInput.Move.x, 1, 1); // flip the attack object to be facing the same direction as the player
		StartCoroutine(AttackTimer(attack));
	}

	IEnumerator AttackTimer(GameObject attack) {
		yield return new WaitForSeconds(0.2f);
		Destroy(attack);
	}

	void ProcessRangedAttack() {
		if (!frameInput.RangedAttackPressed) { return; } // check if the player inputted a ranged attack
		if (timeSinceLastShot < timeBetweenShots) { return; } // check if the player has shot too recently

		if(frameInput.Move.x == 0) { // if the player is not inputting a direction
			frameInput.Move.x = spriteRenderer.flipX ? -1 : 1; // set them to facing either left or right
		}
		timeSinceLastShot = 0; // reset the counter keeping track of when they last used a ranged attack

		GameObject projectile = Instantiate(rangedAttackObject, transform.position, new Quaternion()); // spawn in the projectile
		projectile.GetComponent<Projectile>().direction = frameInput.Move.x; // set the direction the projectile is facing
		// StartCoroutine(ImpactFrames(projectileImpactFrames));
	}
#endregion

#region Take Damage
	public void TakeDamage(int damage) { // this method is called by enemies when they come into contact with the player
		if(timeSinceLastDamage < invulnerabilityFrames) { return; } // check if the player is currently invulnerable
		health -= damage;
		timeSinceLastDamage = 0;
		StartCoroutine(ImpactFrames(impactFrames)); // freeze the game for a few frames to add a powerful feeling to the attack
		Instantiate(takeDamageParticle, transform.position, Quaternion.Euler(Vector3.zero)); // spawn in the particles
		if(health <= 0) {
			// spriteRenderer.color = new Color(1,0,0);
			SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		}
	}

	IEnumerator ImpactFrames(int numFrames) {
		inImpactFrames = true;
		velocity = Vector2.zero;
		for(int i=0; i<numFrames; i++) {
			yield return new WaitForFixedUpdate();
		}
		inImpactFrames = false;
	}
#endregion

#region Input Structs
	public struct FrameInput { // a struct that stores all of the needed inputs this frame
		public bool JumpDown; // did the player press jump this frame?
		public bool JumpHeld; // is the player currently holding the jump button
		public Vector2 Move; // what movements did the player input
		public bool AttackPressed;
		public bool RangedAttackPressed;
		public bool DashPressed;
	}

	public struct BufferedInput { // a struct for storing buffered inputs
		public string InputType; // a string to represent what type of input the player made
		public int FramesUntilDropped; // an int representing how many ticks remain before the input is removed from the buffer 
	}
#endregion
}
