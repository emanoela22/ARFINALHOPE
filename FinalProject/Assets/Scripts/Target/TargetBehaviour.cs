using UnityEngine;

public class TargetBehaviour : MonoBehaviour
{
    private ScanningTherapyManager manager;
    private Camera arCamera;

    private Vector3 endPos;
    private float speed;
    private float maxLifeTime;

    private float spawnTime;
    private bool resolved;

    public void Init(
        ScanningTherapyManager manager,
        Vector3 startPos,
        Vector3 endPos,
        float speed,
        float maxLifeTime,
        Camera arCamera)
    {
        this.manager = manager;
        this.endPos = endPos;
        this.speed = speed;
        this.maxLifeTime = maxLifeTime;
        this.arCamera = arCamera;

        spawnTime = Time.time;
        resolved = false;

        transform.position = startPos;
    }

    private void Update()
    {
        if (resolved) return;

        // Move target
        transform.position = Vector3.MoveTowards(transform.position, endPos, speed * Time.deltaTime);

        // Expire
        if (Time.time - spawnTime >= maxLifeTime)
        {
            ResolveMiss();
            return;
        }

        // Tap detection
        HandleTouchTap();
    }

    private void HandleTouchTap()
    {
#if UNITY_EDITOR
        // Editor click support
        if (Input.GetMouseButtonDown(0))
        {
            TryHitAtScreenPos(Input.mousePosition);
        }
#else
        if (Input.touchCount <= 0) return;

        Touch t = Input.GetTouch(0);
        if (t.phase == TouchPhase.Began)
        {
            TryHitAtScreenPos(t.position);
        }
#endif
    }

    private void TryHitAtScreenPos(Vector2 screenPos)
    {
        if (arCamera == null) return;

        Ray ray = arCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                ResolveHit();
            }
        }
    }

    private void ResolveHit()
    {
        if (resolved) return;
        resolved = true;

        float reaction = Time.time - spawnTime;
        manager.ReportHit(reaction);
        Destroy(gameObject);
    }

    private void ResolveMiss()
    {
        if (resolved) return;
        resolved = true;

        manager.ReportMiss();
        Destroy(gameObject);
    }
}
