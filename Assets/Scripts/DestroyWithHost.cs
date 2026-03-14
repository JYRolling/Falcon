using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to a GameObject to ensure referenced objects are destroyed when this host is destroyed.
/// - Configure specific GameObjects or Transforms in the inspector.
/// - Optionally destroy objects found by tag at runtime.
/// - Optionally apply a short delay before destroying each object.
/// </summary>
public class DestroyWithHost : MonoBehaviour
{
    [Header("Explicit objects to destroy")]
    [Tooltip("Drag GameObjects you want destroyed when this host is destroyed.")]
    [SerializeField] private GameObject[] objectsToDestroy;

    [Header("Optional: destroy by tag")]
    [Tooltip("If set, all active objects with these tags will be destroyed on host destruction.")]
    [SerializeField] private string[] destroyTags;

    [Header("Options")]
    [Tooltip("If true, objects that are children of this host WILL NOT be destroyed (they get destroyed with host automatically).")]
    [SerializeField] private bool skipChildren = true;

    [Tooltip("Delay (seconds) before destroying each referenced object. 0 = immediate.")]
    [SerializeField] private float destroyDelay = 0f;

    /// <summary>
    /// Call when the host is destroyed. Will attempt to destroy configured objects.
    /// </summary>
    private void OnDestroy()
    {
        // Destroy explicit references
        if (objectsToDestroy != null)
        {
            foreach (var go in objectsToDestroy)
            {
                if (go == null) continue;
                if (skipChildren && go.transform.IsChildOf(transform)) continue;
                DestroySafe(go);
            }
        }

        // Destroy objects by tag
        if (destroyTags != null && destroyTags.Length > 0)
        {
            foreach (var tag in destroyTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                var found = GameObject.FindGameObjectsWithTag(tag);
                foreach (var go in found)
                {
                    if (go == null) continue;
                    if (skipChildren && go.transform.IsChildOf(transform)) continue;
                    DestroySafe(go);
                }
            }
        }
    }

    private void DestroySafe(GameObject go)
    {
        if (go == null) return;
        if (destroyDelay > 0f)
            Destroy(go, destroyDelay);
        else
            Destroy(go);
    }

    // Public helpers for runtime control
    public void AddObjectToDestroy(GameObject go)
    {
        if (go == null) return;
        var list = new List<GameObject>(objectsToDestroy ?? new GameObject[0]);
        if (!list.Contains(go)) list.Add(go);
        objectsToDestroy = list.ToArray();
    }

    public void RemoveObjectToDestroy(GameObject go)
    {
        if (go == null || objectsToDestroy == null) return;
        var list = new List<GameObject>(objectsToDestroy);
        if (list.Remove(go)) objectsToDestroy = list.ToArray();
    }
}