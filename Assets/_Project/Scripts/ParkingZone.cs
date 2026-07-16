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

            // Disable controls to stop the player from driving
            movement.enabled = false;

            // Find and disable the arm controls as well
            ExcavatorArm arm = movement.GetComponentInChildren<ExcavatorArm>() ?? movement.GetComponentInParent<ExcavatorArm>();
            if (arm != null)
            {
                arm.enabled = false;
            }

            // Freeze the physics of the excavator to stop it immediately
            Rigidbody rb = movement.GetComponent<Rigidbody>() ?? movement.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
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
