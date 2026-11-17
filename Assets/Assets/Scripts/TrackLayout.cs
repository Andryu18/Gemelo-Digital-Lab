using UnityEngine;

// Geometry of the circuit: a stadium shape (two straights joined by two half circles).
// The road is the area within halfWidth of the center line.
public class TrackLayout : MonoBehaviour
{
    public float straightLength = 8f;       // length of each straight in meters
    public float curveRadius = 3.5f;        // radius of the center line in the curves
    public float halfWidth = 1.4f;          // half of the road width

    // Distance from a world point to the center line of the road
    public float OffsetFromCenterLine(Vector3 worldPoint)
    {
        Vector3 p = transform.InverseTransformPoint(worldPoint);

        // Distance to the segment that the half circles are centered on
        float qx = Mathf.Max(Mathf.Abs(p.x) - straightLength * 0.5f, 0f);
        float d = Mathf.Sqrt(qx * qx + p.z * p.z);

        return Mathf.Abs(d - curveRadius);
    }

    // True when the point is inside the road limits
    public bool IsOnRoad(Vector3 worldPoint)
    {
        return OffsetFromCenterLine(worldPoint) <= halfWidth;
    }
}
