using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TargetBehaviour : MonoBehaviour
{
    private ScanningTherapyManager manager;
    private Camera arCamera;

    private Vector3 startWorld;
    private Vector3 endWorld;

    private float speed;     // meters/sec
    private float lifetime;  // seconds

    private float spawnTime;
    private bool resolved;

    public void InitWorldPath(
        ScanningTherapyManager manager,
        Camera arCamera,
        Vector3 startWorldPos,
        Vector3 endWorldPos,
        float speedMetersPerSec,
        float lifeSeconds)
    {
        this.manager = manager;
        this.arCamera = arCamera;

        startWorld = startWorldPos;
        endWorld = endWorldPos;

        speed = Mathf.Max(0.01f, speedMetersPerSec);
        lifetime = Mathf.Max(0.1f, lifeSeconds);

        spawnTime = Time.time;
        resolved = false;

        var col = GetComponent<Collider>();
        col.isTrigger = true;

        transform.position = startWorld;
    }

    private void Update()
    {
        if (resolved) return;
        if (manager == null) { resolved = true; return; }

        float alive = Time.time - spawnTime;
        if (alive >= lifetime)
        {
            ResolveMiss();
            return;
        }

        // Deterministic progress along the line at constant speed:
        float dist = Vector3.Distance(startWorld, endWorld);
        if (dist < 0.0001f)
        {
            transform.position = endWorld;
        }
        else
        {
            float travel = alive * speed;
            float t = Mathf.Clamp01(travel / dist);
            transform.position = Vector3.Lerp(startWorld, endWorld, t);
        }

        HandleTap();
    }

    private void HandleTap()
    {
        if (resolved || arCamera == null) return;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
            TryHitAt(Input.mousePosition);
#endif

        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                TryHitAt(t.position);
        }
    }

    private void TryHitAt(Vector2 screenPos)
    {
        if (resolved || arCamera == null) return;

        Ray ray = arCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            // safest check: compare to THIS transform root
            if (hit.collider != null && hit.collider.transform == transform)
                ResolveHit();
        }
    }

    private void ResolveHit()
    {
        if (resolved) return;
        resolved = true;

        float rt = Time.time - spawnTime;
        manager.ReportHit(rt);
        Destroy(gameObject);
    }

    private void ResolveMiss()
    {
        if (resolved) return;
        resolved = true;

        manager.ReportMiss();
        Destroy(gameObject);
    }

    public void ForceStop()
    {
        resolved = true;
        enabled = false;
    }
}