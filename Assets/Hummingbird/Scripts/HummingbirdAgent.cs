using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using System;
using Unity.MLAgents.Sensors;
using UnityEngine.Rendering.Universal.Internal;

/// <summary>
/// a hummingbird machine learning agent
/// </summary>
public class HummingbirdAgent : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 2f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;

    [Tooltip("Speed to rotate around the up aixs")]
    public float yawSpeed = 100f;

    [Tooltip("Transform at the tip of the beak")]
    public Transform beakTip;

    [Tooltip("Then agent's camera")]
    public Camera agentCamera;

    [Tooltip("wheter this is training mode or gameplay mode")]
    public bool trainingMode;

    new private Rigidbody rigidbody;

    private FlowerArea flowerArea;

    private Flower nearestFlower;

    private float smoothPitchChange = 0f;
    private float smoothYawChange = 0f;

    private const float MaxPitchAngle = 80f;
    private const float MaxYawAngle = 80f;

    // maximum distance from the beak tip tho accept nectar collision
    private const float BeakTipRadius = 0.008f;

    // whether the agent is frozen
    private bool frozen = false;

    /// <summary>
    /// the amount of nectar the agent has obtained this episode
    /// </summary>
    public float NectarObtained { get; private set; }

    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();

        // if not training mode, no max step, play forever
        if (!trainingMode)
        {
            MaxStep = 0;
        }
    }

    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            // only reset flowers in training when there is one agent per area
            flowerArea.ResetFlowers();
        }
        NectarObtained = 0f;

        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        bool inFrontOfFlower = true;
        if (trainingMode)
        {
            // spawn in front of flower 50% of the time during training
            inFrontOfFlower = UnityEngine.Random.value > 0.5f;
        }

        // move the agent to a new random position
        MoveToSafeRandomPosition(inFrontOfFlower);
    }

    /// <summary>
    /// called when and action is received from either the player input or the neural network
    ///
    /// vectorAction[i] represents:
    /// Index 0: move vector x(+1 = right,  -1 = left)
    /// Index 1: move vector y(+1 = up,  -1 = down)
    /// Index 2: move vector z(+1 = forward,  -1 = backword)
    /// Index 3: pitch angle(+1 = pitch up,  -1 = pitch down)
    /// Index 4: yaw angle(+1 = turn right,  -1 = turn left)
    /// </summary>
    /// <param name="vectorAction">the actions to take</param>
    public override void OnActionReceived(float[] vectorAction)
    {
        // don't take tactions if frozen
        if (frozen)
        {
            return;
        }

        // calculate movement vector
        Vector3 move = new Vector3(vectorAction[0], vectorAction[1], vectorAction[2]);

        // add force in the direction of the move vector
        rigidbody.AddForce(move * moveForce);

        Vector3 rotationVector = transform.rotation.eulerAngles;

        // calculate pitch and yaw rotation
        float pitchChange = vectorAction[3];
        float yawChange = vectorAction[4];

        // calculate smooth rotation changes
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);

        // calculate new pitch and yaw based on smoothed values
        // clamp pitch to avoid flipping upside down
        float pitch = rotationVector.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f)
        {
            pitch -= 360f;
        }
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);

        float yaw = rotationVector.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// collect vector observations from the environment
    /// </summary>
    /// <param name="sensor"></param>
    public override void CollectObservations(VectorSensor sensor)
    {
        // if nearestFlower is null, observe an empty array and return empty
        if (nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }

        // observe the agent's local rotation (4 observations)
        sensor.AddObservation(transform.localRotation.normalized);

        // get a vector from the beak tip to the nearest flower
        Vector3 toFlower = nearestFlower.FlowerCenterposition - beakTip.position;

        // observe a normalized vector pointing to the nearest flower (3 observations)
        sensor.AddObservation(toFlower.normalized);

        // observe a dot product that indicates whether the beak tip is in front of the flower
        //(+1 means that the beak tip is directly in front of the flower, -1 means directly behind)
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.FlowerUpVector.normalized));

        // observe adot product that indicated whether the beak is pointing toward the flower
        //(+1 means that the beak tip is pointing directly at the flower, -1 means directly away
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.FlowerUpVector.normalized));

        // observe the relative distance from the beak tip to the flower (1 observation)
        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);
    }

    /// <summary>
    /// when Behaviour type is set to "heuristic Only" on the agent's Behaviour Parameters,
    /// this function will be called.
    /// Its return values will be fed into <see cref="OnActionReceived(float[])"/> instead of using the neural network
    /// </summary>
    /// <param name="actionsOut">output action array</param>
    public override void Heuristic(float[] actionsOut)
    {
        // create placeholders for all movement/turning
        Vector3 forward = Vector3.zero;
        Vector3 right = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0;
        float yaw = 0;

        // convert keyboard inputs to movement and turning
        // all value should be between -1 and +1
        if (Input.GetKey(KeyCode.W))
            forward = transform.forward;
        else if (Input.GetKey(KeyCode.S))
            forward = -transform.forward;

        if (Input.GetKey(KeyCode.D))
            right = transform.right;
        else if (Input.GetKey(KeyCode.A))
            right = -transform.right;

        if (Input.GetKey(KeyCode.E))
            up = transform.up;
        else if (Input.GetKey(KeyCode.Q))
            up = -transform.up;

        if (Input.GetKey(KeyCode.UpArrow))
            pitch = 1f;
        else if (Input.GetKey(KeyCode.DownArrow))
            pitch = -1f;

        if (Input.GetKey(KeyCode.RightArrow))
            yaw = 1f;
        else if (Input.GetKey(KeyCode.LeftArrow))
            yaw = -1f;

        // combine the movement vectors and normalize them
        Vector3 combined = (forward + up + right).normalized;
        actionsOut[0] = combined.x;
        actionsOut[1] = combined.y;
        actionsOut[2] = combined.z;
        actionsOut[3] = pitch;
        actionsOut[4] = yaw;
    }

    /// <summary>
    /// prevent the agent from moving and taking actions
    /// </summary>
    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = true;
        rigidbody.Sleep();
    }

    /// <summary>
    /// resume agent movement and actions
    /// </summary>
    public void UnfreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = false;
        rigidbody.WakeUp();
    }

    /// <summary>
    /// move the agent to a safe random position (i.e. does not collide with anything)
    /// </summary>
    /// <param name="inFrontOfFlower"></param>
    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safePositionFound = false;

        // prevent an infinite loop
        int attemptsRemaining = 100;

        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        while (!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;

            if (inFrontOfFlower)
            {
                Flower randomFlower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];

                // position a bit in frount of the flower
                float distanceFromFlower = UnityEngine.Random.Range(.1f, .2f);
                potentialPosition = randomFlower.transform.position + randomFlower.FlowerUpVector * distanceFromFlower;

                // point beak at flower (bird's head is center of transfom)
                Vector3 toFlower = randomFlower.FlowerCenterposition - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                float height = UnityEngine.Random.Range(1.2f, 2.5f);
                float radius = UnityEngine.Random.Range(2f, 7f);

                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);
                potentialPosition = flowerArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;

                float pitch = UnityEngine.Random.Range(-60f, 60f);
                float yaw = UnityEngine.Random.Range(-180f, 180f);
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // check to see if agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);
            safePositionFound = colliders.Length == 0;
        }
        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.Flowers)
        {
            if (nearestFlower == null && flower.HasNectar)
            {
                nearestFlower = flower;
            }
            else if (flower.HasNectar)
            {
                float distanceToFlower = Vector3.Distance(flower.transform.position, beakTip.position);
                float distanceToCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                if (!nearestFlower.HasNectar || distanceToCurrentNearestFlower > distanceToFlower)
                {
                    nearestFlower = flower;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    private void TriggerEnterOrStay(Collider collider)
    {
        // check if agent is colliding with nectar
        if (collider.CompareTag("Nectar"))
        {
            Vector3 closentPointToBeckTip = collider.ClosestPoint(beakTip.position);

            // check if the closent collision point is close to the beak tip
            // a collision with anything but the beak tip should not count

            if (Vector3.Distance(beakTip.position, closentPointToBeckTip) < BeakTipRadius)
            {
                // look up the flower for this nectar collider
                Flower flower = flowerArea.GetFlowerFromNectar(collider);

                float nectarReceived = flower.Feed(.01f);

                // keep trace of nectar obtained
                NectarObtained += nectarReceived;

                if (trainingMode)
                {
                    // calculate reward for getting nectar
                    float bonus = .02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -nearestFlower.FlowerUpVector.normalized));
                    AddReward(.01f + bonus);
                }

                // if flower is empty, update the nearest flower
                if (!flower.HasNectar)
                {
                    UpdateNearestFlower();
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("Boundary"))
        {
            // collided with the area boundary, give a negative reward
            AddReward(-.5f);
        }
    }

    private void Update()
    {
        // draw a line from the beak tip to the nearest flower
        if (nearestFlower != null)
        {
            Debug.DrawLine(beakTip.position, nearestFlower.FlowerCenterposition, Color.green);
        }
    }

    private void FixedUpdate()
    {
        // avoid scenario that the opponent stole the nectar before the agent reaching it
        if (nearestFlower != null && !nearestFlower.HasNectar)
        {
            UpdateNearestFlower();
        }
    }
}