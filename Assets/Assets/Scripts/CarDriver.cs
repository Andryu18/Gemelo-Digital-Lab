using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Moves the car at constant speed. Only the steering angle decides the path.
// It uses the bicycle model: yaw rate = speed / wheelBase * tan(steering angle).
public class CarDriver : MonoBehaviour
{
    [Header("References")]
    public SteeringTwin steering;           // gives the wheel angle
    public TrackLayout track;               // road limits
    public TMP_Text driveLabel;

    [Header("Vehicle")]
    public float speed = 1.2f;              // constant speed in m/s
    public float wheelBase = 0.9f;          // distance between axles in meters
    public float rearAxleZ = -0.45f;        // rear axle position along the rig forward axis
    public float halfLength = 0.65f;        // half length of the footprint used for the limits
    public float halfWidth = 0.35f;         // half width of the footprint
    public float maxWheelAngle = 30f;       // mechanical limit of the wheels in degrees

    [Header("Circuit")]
    public Transform startPoint;
    public float restartDelay = 2f;         // seconds shown after leaving the road

    bool crashed;
    float crashTime;
    int laps;
    float lastLocalX;

    void Start()
    {
        ResetCar();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            ResetCar();

        if (crashed)
        {
            if (Time.time - crashTime > restartDelay) ResetCar();
        }
        else
        {
            Drive(Time.deltaTime);
            CheckLimits();
        }

        UpdateLabel();
    }

    void Drive(float dt)
    {
        float wheel = Mathf.Clamp(steering.WheelAngle, -maxWheelAngle, maxWheelAngle);

        // The model turns about the rear axle
        Vector3 rear = transform.position + transform.forward * rearAxleZ;

        float yawRate = speed / wheelBase * Mathf.Tan(wheel * Mathf.Deg2Rad);
        transform.Rotate(0f, yawRate * Mathf.Rad2Deg * dt, 0f, Space.World);

        rear += transform.forward * speed * dt;
        transform.position = rear - transform.forward * rearAxleZ;

        CountLaps();
    }

    // A lap is counted each time the car crosses the middle of the start straight
    void CountLaps()
    {
        float x = track.transform.InverseTransformPoint(transform.position).x;
        float z = track.transform.InverseTransformPoint(transform.position).z;
        bool onStartStraight = z < 0f;

        if (onStartStraight && lastLocalX < 0f && x >= 0f) laps++;
        lastLocalX = x;
    }

    void CheckLimits()
    {
        // The four corners of the footprint must stay on the road
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = transform.TransformPoint(
                    new Vector3(sx * halfWidth, 0f, sz * halfLength + (rearAxleZ + wheelBase * 0.5f)));
                if (!track.IsOnRoad(corner))
                {
                    crashed = true;
                    crashTime = Time.time;
                    return;
                }
            }
        }
    }

    public void ResetCar()
    {
        crashed = false;
        laps = 0;
        transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
        lastLocalX = track.transform.InverseTransformPoint(transform.position).x;
    }

    void UpdateLabel()
    {
        if (!driveLabel) return;

        string status = crashed ? "OFF THE ROAD, restarting" : "Driving";
        driveLabel.text =
            $"Speed: {speed:0.0} m/s (constant)\n" +
            $"Wheels: {steering.WheelAngle:+0.0;-0.0;0.0}°\n" +
            $"Laps: {laps}\n{status}";
    }
}
