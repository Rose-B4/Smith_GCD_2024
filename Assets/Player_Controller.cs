using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

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
	protected BoxCollider2D boxCollider;
	protected SpriteRenderer spriteRenderer;
	protected Rigidbody2D rb;
	//---------------------------------------------------------------

	// serialized constant variables---------------------------------
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
	//---------------------------------------------------------------

	// state variables-----------------------------------------------
	protected int remainingCoyoteTime;
	protected int remainingDashes;
	protected int timeSinceLastDash = 60;
	protected float downwardsAcceleration;
	protected float targetFallSpeed;
	protected bool isGrounded;
	protected bool canJump;
	protected bool currentlyDashing;
	protected bool shouldFindGround = true;
	[Tooltip("This is only serialized so that velocity can be seen in the editor, don't change its value")]
	[SerializeField] protected Vector2 velocity = new(0,0);
	//---------------------------------------------------------------

	// input containers----------------------------------------------
	protected FrameInput frameInput = new();
	protected List<BufferedInput> bufferedInputs = new();
	//---------------------------------------------------------------
#endregion

#region Unity Methods
	void Start() {
		QualitySettings.vSyncCount = 0; // turn off v-sync
		Application.targetFrameRate = 60; // set the max fps

		rb = GetComponent<Rigidbody2D>();
		boxCollider = GetComponent<BoxCollider2D>();
		spriteRenderer = GetComponent<SpriteRenderer>();

		floorLayerMask = LayerMask.GetMask("Floor");
	}

	// Update is called once per frame
	void Update() {
		GetInput(); // this MUST come first in the list since it dictates everything that will happen below
		RayCast();
		ProcessJump();
		ProcessGravity();
		ProcessMovement();
		ProcessAnimation();
		ProcessDash(); // this or ProcessAttack MUST come last in the list because it can modify the players input
		ProcessAttack();

		rb.velocity = velocity;

		RemoveExpiredInputsFromBuffer();
		if(Input.GetKey(KeyCode.Escape)) {
			Application.Quit();
		}
	}

	void OnDrawGizmos() { // this method allows ray casts and velocities to be visible in the editor 
		if(!Application.isPlaying) { return; }
		Gizmos.color = Color.white;
		Gizmos.DrawRay(transform.position, velocity*2);

		Gizmos.color = Color.cyan;
		Vector3[] positions = new Vector3[] {bottomCenter, rayToGround.centroid};
		Gizmos.DrawLineStrip(positions, false);
	}
#endregion

#region Input
	void GetInput() {
		frameInput = new FrameInput{
			// TODO: Change out for new input package______________________________________________
			JumpDown = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space), // checks if the jump button was pressed this frame
			JumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.Space), // checks if the jump button is being held
			Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), // gets the user's directional input
			AttackPressed = Input.GetButtonDown("Attack"),
			DashPressed = Input.GetButtonDown("Dash")
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
		remainingCoyoteTime --;
		canJump = isGrounded || (remainingCoyoteTime > 0); // if the player is grounded or has remaining coyote time, they can jump

		if(frameInput.JumpDown && (!canJump || (distanceToLeftWall > distanceToWallJump && distanceToRightWall > distanceToWallJump))) {
			bufferedInputs.Add(new BufferedInput{InputType = "jump", FramesUntilDropped = inputBufferFrames}); // add a jump to the input buffer
		}

		for(int i = 0; i < bufferedInputs.Count; i++){ // go through each input in the buffer
			if(bufferedInputs[i].InputType.Equals("jump")) { // if there is a jump input buffered and the player can jump this frame
				frameInput.JumpDown = true; // make the player input jump
			}
		}

		if(!frameInput.JumpDown) { return; } // if the player didn't press jump or have a buffered jump, return

		if(canJump) {
			Jump();
		}
		else if(rayToLeftWall.distance < distanceToWallJump && rayToRightWall.distance < distanceToWallJump) {
			WallJump(0);
		}
		else if(rayToLeftWall.distance < distanceToWallJump) {
			WallJump(1);
		}
		else if(rayToRightWall.distance < distanceToWallJump) {
			WallJump(-1);
		}
	}

	void StartJump() {
		for(int i=0; i < bufferedInputs.Count; i++) { // go through the input buffer and remove all jump inputs
			if(bufferedInputs[i].InputType.Equals("jump")) {
				bufferedInputs[i] = new BufferedInput{FramesUntilDropped = -1}; //replace this buffered input with one that will be cleared at the end of the frame
			}
		}
		Instantiate(jumpParticle, transform.position, Quaternion.Euler(new Vector3(0, 0, 180)));
		currentlyDashing = false;
		shouldFindGround = false;
		StartCoroutine(JumpStartTimer()); // this was implemented so that the game wouldn't eat the player's jump if they were slightly clipped into the ground
		remainingCoyoteTime = -1; // remove the player's ability to coyote time jump until they touch the ground again
	}

	void Jump() {
		StartJump();
		velocity.y = jumpPower;
		isGrounded = false;
	}

	void WallJump(int direction) {
		StartJump();
		velocity.y = wallJumpPower;
		velocity.x = wallJumpPushBack * direction;
		isGrounded = false;
	}

	IEnumerator JumpStartTimer() { // this was implemented so that the game wouldn't eat the player's jump if they were slightly clipped into the ground
		for(int i=0; i<3; i++) {
			yield return new WaitForEndOfFrame(); // wait 3 frames before checking for ground again
		}
		shouldFindGround = true;
	}

#endregion

#region Physics
	void RayCast() {
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
			return; // and don't do any more gravity calculations
		}

		if(currentlyDashing) { return; }

		downwardsAcceleration = gravity; // initialize the amount the player's downward velocity should increase by
		targetFallSpeed = maxFallSpeed;
		
		if(velocity.y < 0 && ((rayToLeftWall.distance < colliderOffset && frameInput.Move.x < 0) || (rayToRightWall.distance < colliderOffset && frameInput.Move.x > 0))) {
			targetFallSpeed = maxWallSlideVelocity;
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
		if(!currentlyDashing) {
			velocity.x = Mathf.MoveTowards(velocity.x, frameInput.Move.x * walkSpeed, walkAcceleration);
		}
		if((rayToRightWall.distance < colliderOffset && velocity.x > 0) || (rayToLeftWall.distance < colliderOffset && velocity.x < 0)) { // if the player is walking into a wall
			velocity.x = 0;
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
		timeSinceLastDash ++;
		if(isGrounded) { remainingDashes = numDashes; } // if the player is on the ground, give them their dashes back
		if(!frameInput.DashPressed) { return; } // if the player didn't dash, don't dash
		if(remainingDashes <= 0) { return; } // if the player is out of dashes, don't dash
		if(currentlyDashing) { return; } // if the player is already dashing, don't dash
		if(timeSinceLastDash < timeBetweenDashes) { return ; }

		timeSinceLastDash = 0;
		StartCoroutine(Dash());
	}

	IEnumerator Dash() {
		Instantiate(jumpParticle, transform.position, Quaternion.Euler(new Vector3(0,0,270)));
		currentlyDashing = true;
		remainingDashes --;
		
		if(frameInput.Move.x == 0) { // if the player is not inputting a direction
			frameInput.Move.x = 1; // force an inputted direction for this frame only
			if(spriteRenderer.flipX) { // if they are facing left
				frameInput.Move.x = -1; // make the input go left
			}
		}

		float preDashXInput = frameInput.Move.x;
		// Vector2 storedVelocity = velocity; // store the current velocity of the player

		for(int i=0; i<dashStartPause; i++) { // pause for a few frames at the start of the dash to add weight to it
			velocity = Vector2.zero;
			yield return new WaitForEndOfFrame();
		}
		// Instantiate(dashGhost, transform.parent); // spawn the dash ghost sprite
		// velocity = storedVelocity; // resume movement
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
		// canDashTechnique = true;
		Vector2 colliderSize = boxCollider.size;
		boxCollider.size = new Vector2(boxCollider.size.x, boxCollider.size.y/3);
		for (int i=0; i<dashFrames; i++) { // the number of frames the player should be dashing for
			if (currentlyDashing == false) { // if the dash ends prematurely
				boxCollider.size = colliderSize;
				yield break; // end the function
			}
			yield return new WaitForEndOfFrame();
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
	// 		yield return new WaitForEndOfFrame();
	// 	}
	// 	// Instantiate(dashGhost, transform.parent); // spawn the dash ghost sprite
	// 	velocity = storedVelocity; // resume movement

	// 	StartCoroutine(DashTimer());
	// }
	// ----------------------------------------------------------------------------------------------------------------
#endregion

#region Attack
	void ProcessAttack() {
		if(!frameInput.AttackPressed) { return; } // if the player didn't press the attack button, don'd attack

		if(frameInput.Move.x == 0) {
			frameInput.Move.x = spriteRenderer.flipX ? -1 : 1;
		}
		GameObject attack = Instantiate(attackObject, transform);
		attack.transform.localScale = new Vector3(frameInput.Move.x, 1, 1);
		StartCoroutine(AttackTimer(attack));
	}

	IEnumerator AttackTimer(GameObject attack) {
		yield return new WaitForSeconds(0.2f);
		Destroy(attack);
	}
#endregion

#region Input Structs
	public struct FrameInput { // a struct that stores all of the needed inputs this frame
		public bool JumpDown; // did the player press jump this frame?
		public bool JumpHeld; // is the player currently holding the jump button
		public Vector2 Move; // what movements did the player input
		public bool AttackPressed; // did the player press the attack button this frame?
		public bool DashPressed; // did the player press the dash button this frame?
	}

	public struct BufferedInput { // a struct for storing buffered inputs
		public string InputType; // a string to represent what type of input the player made
		public int FramesUntilDropped; // an int representing how many ticks remain before the input is removed from the buffer 
	}
#endregion
}
