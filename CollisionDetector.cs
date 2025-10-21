using UnityEngine;

public class CollisionDetector : MonoBehaviour
{
    public LayerMask detectionLayers; // Select layers from the inspector
    public bool IsColliding { get; private set; }

    public delegate void CollisionStateChangedAction(bool state);
    public event CollisionStateChangedAction OnCollisionStateChanged;

    private void OnTriggerEnter(Collider other)
    {
        if (IsInDetectionLayer(other.gameObject))
        {
            SetCollisionState(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInDetectionLayer(other.gameObject))
        {
            SetCollisionState(false);
        }
    }

    private bool IsInDetectionLayer(GameObject obj)
    {
        return (detectionLayers.value & (1 << obj.layer)) != 0;
    }

    void SetCollisionState(bool state)
    {
        IsColliding = state;
        OnCollisionStateChanged?.Invoke(IsColliding);
    }
}
