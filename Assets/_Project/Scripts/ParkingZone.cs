using UnityEngine;
using System.Collections.Generic;

public class ParkingZone : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject completionUIPanel;

    private HashSet<Collider> collidersInside = new HashSet<Collider>();

    void Start()
    {
        if (completionUIPanel != null)
        {
            completionUIPanel.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ExcavatorMovement movement = other.GetComponentInParent<ExcavatorMovement>();
        if (movement != null)
        {
            collidersInside.Add(other);
            UpdateUI();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (collidersInside.Contains(other))
        {
            collidersInside.Remove(other);
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (completionUIPanel != null)
        {
            // Clean up any destroyed colliders
            collidersInside.RemoveWhere(c => c == null);
            completionUIPanel.SetActive(collidersInside.Count > 0);
        }
    }
}
