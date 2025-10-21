using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Unity.VisualScripting;

public class SpeedMovementCharacterController : MonoBehaviour
{
    [HideInInspector] public Rigidbody rb;

    [Header("Movement")]
    public float baseSpeed = 8f;
    private bool isGrounded;

    [Header("Ground & Wall Checkers")]
    public CollisionDetectorRaycast bottomCollider;
    public CollisionDetectorRaycast rightCollider;
    public CollisionDetectorRaycast leftCollider;

    [Header("Jumping")]
    public float jumpForce = 10f;
    bool jumpInitiated = false;
    float lastSpeedBeforeTakeoff;

    [Header("Look Around")]
    public Transform cameraHolder;
    public float mouseSensitivity = 300f;
    public float verticalClampAngle = 90f;
    public float horizontalClampAngle = 90f;
    float verticalRotation = 0f;

    [Header("Sliding")]
    public float slideSpeedThreshold = 6f; // Minimum speed needed to begin sliding
    public float addedSlideSpeed = 3f; // Adding flat speed + also add a percentage of horizontal speed up to 66% of base speed
    public float slideSpeedDampening = 0.99f; // The speed will be multiplied by this every frame (to stop in ~3 seconds)
    public float keepSlidingSpeedThreshold = 3f; // As long as speed is above this, keep sliding
    public float slideSteeringPower = 0.5f; // How much you can steer around while sliding

    bool isSliding = false;
    bool slideInitiated = false;

    [Header("Wall Running")]
    public bool fallWhileWallRunning; // Slowly fall character while wall running
    public float keepWallRunningSpeedThreshold = 3f; // If speed drops below this, stop wall running
    public Transform playerCameraZRotator; // To be able to rotate the camera on the Z axis without affecting other rotations
    float wallRunStartingSpeed; // The speed that you begin wall running with, will be maintained while you keep wall running

    bool isWallRunning = false;
    bool onRightWall = false;
    // bool landingFromWallRunning;

    [Header("Cinema Mode (For YT Video)")]
    public GameObject playerCamera;
    public GameObject uiCanvas;
    bool cinemaMode; // Switch camera and hide UI

    [Header("Visual")]
    public GameObject playerVisual; // Used to rotate/tilt/move player model without affecting the colliders etc.

    void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) jumpInitiated = true;
        if (Input.GetKeyDown(KeyCode.LeftShift)) slideInitiated = true;

        LookUpAndDownWithCamera();
        RotateBodyHorizontally(); // On Update() so that its smoother

        //? Cinema mode is just for the YouTube video
        if (Input.GetKeyDown(KeyCode.C))
        {
            cinemaMode = !cinemaMode;
            playerCamera.SetActive(!cinemaMode);
            uiCanvas.SetActive(!cinemaMode);
        }
    }

    void FixedUpdate()
    {
        WallRun();
        Move();
        Jump();
        Slide();

        SetIsGrounded(bottomCollider.IsColliding);
    }

    void Move()
    {
        // Move with WASD (the direction)
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 movementDirection = transform.right * x + transform.forward * z;

        // If wall running & sliding, don't take input
        if (isSliding) return; // TODO: If moving in the opposite direction "cancel sliding", ...maybe
        if (isWallRunning) return;
        // if (landingFromWallRunning) return;

        // Otherwise, if no input, stop fast if grounded
        if (movementDirection == Vector3.zero)
        {
            // Dampen speed fast
            if (isGrounded) rb.velocity = rb.velocity * 0.6f;
            return;
        }

        //* Applying the Movement

        // Only consider horizontal velocity for movement (on ground OR in air)
        //? On the ground horizontal vel. is 0, in air if you add it then you can keep speeding up infinitely
        float horizontalSpeed = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
        float speedToApply = Mathf.Max(baseSpeed, horizontalSpeed); // If the player is going FASTER than the limit, cap it there

        // If in the air, keep the LAST speed before takeoff
        // //? Otherwise, the player can keep speeding up infinitely with gravity
        if (!isGrounded) speedToApply = lastSpeedBeforeTakeoff;

        // If the player is going over the limit, dampen it a bit
        if (speedToApply > baseSpeed) speedToApply *= isGrounded ? 0.985f : 0.99f; // Dampens harder while on the ground

        // The new velocity to apply
        Vector3 newVelocity = movementDirection.normalized * speedToApply;
        newVelocity.y = rb.velocity.y; // + (Physics.gravity.y * Time.fixedDeltaTime); // Keep the current horizontal speed

        //? If in the air we add force instead of modifying the velocity so that the gravity can do its thing
        if (isGrounded) rb.velocity = newVelocity;
        else rb.AddForce(movementDirection.normalized * speedToApply, ForceMode.Force);

        // If player is going too fast HORIZONTALLY in AIR => dampen HORIZONTAL speed
        //? Otherwise the speed applied above goes out of control
        if (!isGrounded && horizontalSpeed > baseSpeed)
        {
            Vector3 newHorizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            newHorizontalVelocity *= 0.98f;
            rb.velocity = new Vector3(newHorizontalVelocity.x, rb.velocity.y, newHorizontalVelocity.z);
        }
    }

    void Jump()
    {
        if (jumpInitiated)
        {
            jumpInitiated = false;

            if (!isGrounded) return;

            //? Using rb.velocity.magnitude as the last speed input, so if player runs into wall etc. momentum resets
            lastSpeedBeforeTakeoff = rb.velocity.magnitude;

            rb.velocity += Vector3.up * jumpForce;
        }
    }

    void RotateBodyHorizontally()
    {
        // Get mouse input for horizontal rotation
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;

        // Rotate the player horizontally
        transform.Rotate(0f, mouseX, 0f);

        // If sliding, allow steering slightly
        if (isSliding)
        {
            Vector3 newVelocity = rb.velocity;
            newVelocity = Quaternion.Euler(0, mouseX * slideSteeringPower, 0) * newVelocity;
            rb.velocity = newVelocity;
        }
    }

    void LookUpAndDownWithCamera()
    {
        // Get mouse input for vertical rotation
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Update vertical rotation
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalClampAngle, verticalClampAngle);

        // Apply vertical rotation to the camera holder
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    void SetIsGrounded(bool state)
    {
        isGrounded = state;
        if (PlayerStatesManager.instance) PlayerStatesManager.instance.SetGroundedState(isGrounded);
        if (!isGrounded && isSliding) StopSliding();
    }

    #region Sliding

    void Slide()
    {
        if (slideInitiated)
        {
            if (!isGrounded) return; // Don't cancel the state to slide as soon as you land

            // -- INITIATE --
            slideInitiated = false;

            // TODO If going backwards, or not moving, dont slide (not moving handled by speed threshold)

            // If already sliding... return;
            if (isSliding) return;

            // Can only slide if the "horizontal" speed (X & Z) is above a threshold
            float horizontalSpeed = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
            if (horizontalSpeed < slideSpeedThreshold) return;

            StartSliding();
        }

        // While sliding, slowly lose momentum until it ends
        if (isSliding)
        {
            // Dampen the speed
            Vector3 newVelocity = rb.velocity * slideSpeedDampening;

            // If the speed is still above the threshold, keep sliding
            if (newVelocity.magnitude > keepSlidingSpeedThreshold) rb.velocity = newVelocity;
            else StopSliding();
        }
    }

    void StartSliding()
    {
        slideInitiated = false;
        SetIsSliding(true);

        // Move camera down 1 unit
        cameraHolder.DOLocalMoveY(cameraHolder.localPosition.y - 0.4f, 0.2f);
        playerVisual.transform.DOLocalRotate(new Vector3(-20, 0, 0), 0.2f);
        playerVisual.transform.DOLocalMoveY(-0.2f, 0.2f);

        // Add bonus speed
        // TODO: Boost should "dampen" out as the players speed increases the max cap
        float currSpeedModifier = Mathf.Clamp(rb.velocity.magnitude / 40, 0, 1); // Maximum speed at 20
        float boost = addedSlideSpeed + Mathf.Lerp(0, addedSlideSpeed * 2f, currSpeedModifier); // Boost the speed (up to 40% more speed with Y speed)

        // Get direction, get speed, amplify speed, apply
        Vector3 direction = rb.velocity.normalized;

        //? Using rb.velocity.magnitude as the "base" speed, so if player runs into wall etc. momentum resets
        rb.velocity = direction * (rb.velocity.magnitude + boost);

        // Debug.Log($"SLIDE [START] (boost: {boost}, y speed: {Mathf.Abs(rb.velocity.y)} ");
    }

    void StopSliding()
    {
        SetIsSliding(false);

        // Move camera back up
        cameraHolder.DOLocalMoveY(cameraHolder.localPosition.y + 0.4f, 0.2f);
        playerVisual.transform.DOLocalRotate(new Vector3(0, 0, 0), 0.2f);
        playerVisual.transform.DOLocalMoveY(0f, 0.2f);

        // Debug.Log("SLIDE [END]");
    }

    void SetIsSliding(bool state)
    {
        isSliding = state;
        if (PlayerStatesManager.instance) PlayerStatesManager.instance.SetSlidingState(isSliding);
    }

    #endregion

    #region Wall Running

    void WallRun()
    {
        if (jumpInitiated && !isGrounded) // Must initiate jump, be off the ground, ...
        {
            if (isWallRunning) // If already wall running, jump off
            {
                int directionCount = 1; // Can be up to 3 directions, magnitude can be 1, 1.25, 1.5 depending

                // Jump off the wall
                Vector3 jumpDirection = Vector3.up;

                // If holding forward, add a force forward
                if (Input.GetAxisRaw("Vertical") > 0)
                {
                    jumpDirection += transform.forward;
                    directionCount++;
                }

                // If holding the horizontal direction AWAY from the wall, add that horizontal direction as well
                if (Input.GetAxisRaw("Horizontal") < 0 && onRightWall)
                {
                    jumpDirection += rightCollider.outHit.normal;
                    directionCount++;
                }
                else if (Input.GetAxisRaw("Horizontal") > 0 && !onRightWall)
                {
                    jumpDirection += leftCollider.outHit.normal;
                    directionCount++;
                }

                // Normalize (otherwise you can artifically buff up speed)
                float magnitude = 1 + (directionCount - 1) * 0.25f;
                jumpDirection = jumpDirection.normalized * magnitude;

                rb.AddForce(jumpDirection * jumpForce, ForceMode.Impulse);

                StopWallRunning();
            }
            else // Check to see if you can START wall running
            {
                // ... and have a wall in contact
                if (leftCollider.IsColliding) StartWallRunning(false);
                else if (rightCollider.IsColliding) StartWallRunning(true);
            }

            jumpInitiated = false;
        }

        if (isWallRunning)
        {
            //? Can't remove this (what happens when flat wall ends?)
            if (isGrounded)
            {
                StopWallRunning();
                return;
            }
            if (!leftCollider.IsColliding && !rightCollider.IsColliding)
            {
                // StartCoroutine(KeepWallRunMomentumUntilGrounded(rb.velocity));
                StopWallRunning();
                return;
            }

            //*  - THE DIRECITON -

            // Which wall? where is the collider?
            onRightWall = rightCollider.IsColliding; // temp
            var col = onRightWall ? rightCollider : leftCollider;
            Vector3 wallNormal = col.outHit.normal;

            // Direction to travel along
            Vector3 wallForward = Vector3.Cross(
                wallNormal,
                transform.up
            );

            // Ensure the forward direction aligns with the player's orientation (aka changing direction while running along the wall)
            if ((transform.forward - wallForward).magnitude > (transform.forward - -wallForward).magnitude)
            {
                wallForward = -wallForward;
            }

            // Current negative Y velocity
            float ySpeed = rb.velocity.y;

            // Apply the velocity
            rb.velocity = wallForward * wallRunStartingSpeed;

            // Threshold?
            if (rb.velocity.magnitude < keepWallRunningSpeedThreshold)
            {
                StopWallRunning();
                return;
            }

            // However negative is the Y speed, half it and add it to the Y speed
            if (fallWhileWallRunning && ySpeed < 0) rb.velocity += new Vector3(0, ySpeed * 0.75f, 0);

            // Add force TOWARDS the wall
            rb.AddForce(-wallNormal * 100, ForceMode.Force);
        }
    }

    // IEnumerator KeepWallRunMomentumUntilGrounded(Vector3 lastWallRunningVelocity)
    // {
    //     landingFromWallRunning = true;

    //     rb.velocity = lastWallRunningVelocity;

    //     // Wait until grounded
    //     while (!isGrounded)
    //     {
    //         yield return null;
    //     }

    //     landingFromWallRunning = false;
    // }

    void StartWallRunning(bool rightWall)
    {
        SetIsWallRunning(true);

        if (!fallWhileWallRunning) rb.useGravity = false;

        // Rotate camera Z 20 degrees away from wall
        playerCameraZRotator.DOLocalRotate(new Vector3(0, 0, rightWall ? 20 : -20), 0.2f);
        playerVisual.transform.DOLocalRotate(new Vector3(0, 0, rightWall ? 20 : -20), 0.2f);

        wallRunStartingSpeed = rb.velocity.magnitude;

        // Debug.Log($"WALL RUN [START] (spd: {wallRunStartingSpeed})");
    }

    void StopWallRunning()
    {
        SetIsWallRunning(false);

        if (!fallWhileWallRunning) rb.useGravity = true;

        // Rotate Z to 0
        playerCameraZRotator.DOLocalRotate(new Vector3(0, 0, 0), 0.2f); ;
        playerVisual.transform.DOLocalRotate(new Vector3(0, 0, 0), 0.2f);

        // Debug.Log("WALL RUN [STOP]");
    }

    void SetIsWallRunning(bool state)
    {
        isWallRunning = state;
        if (PlayerStatesManager.instance) PlayerStatesManager.instance.SetWallRunningState(isWallRunning);
    }

    #endregion
}
