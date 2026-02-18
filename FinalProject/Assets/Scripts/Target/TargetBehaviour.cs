using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

[RequireComponent(typeof(Collider))]
public class TargetBehaviour : MonoBehaviour
{
    private ScanningTherapyManager manager;
    private Camera arCamera;

    private float distance;
    private float speed;
    private float maxLifeTime;

    private float spawnTime;
    private bool resolved;

    private float t01;
    private float startOffsetX;
    private float endOffsetX;
    private float heightOffset;

    [Header("Smoothing")]
    [SerializeField] private float followSmoothing = 25f; // increase if jittery
    private Vector3 smoothedPos;
    private bool hasSmoothed;

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
    }

    public void InitCameraRelative(
        ScanningTherapyManager manager,
        Camera arCamera,
        float distanceMeters,
        float startOffsetXMeters,
        float endOffsetXMeters,
        float heightOffsetMeters,
        float speedMetersPerSec,
        float maxLifeTimeSeconds)
    {
        this.manager = manager;
        this.arCamera = arCamera;

        distance = Mathf.Max(0.3f, distanceMeters);
        startOffsetX = startOffsetXMeters;
        endOffsetX = endOffsetXMeters;
        heightOffset = heightOffsetMeters;

        speed = Mathf.Max(0.01f, speedMetersPerSec);
        maxLifeTime = maxLifeTimeSeconds;

        spawnTime = Time.time;
        resolved = false;
        t01 = 0f;

        // Collider must exist for Physics.Raycast taps
        var col = GetComponent<Collider>();
        col.isTrigger = false;

        // IMPORTANT: no physics jitter
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Vector3 desired = ComputeWorldPos(startOffsetX);
        smoothedPos = desired;
        hasSmoothed = true;
        transform.position = desired;
    }

    private void Update()
    {
        if (resolved) return;
        if (arCamera == null) { ResolveMiss(); return; }

        // Move along path (m/s -> normalized t)
        float pathLen = Mathf.Max(0.05f, Mathf.Abs(endOffsetX - startOffsetX));
        float tSpeed = speed / pathLen;
        t01 = Mathf.Clamp01(t01 + tSpeed * Time.deltaTime);

        float curOffsetX = Mathf.Lerp(startOffsetX, endOffsetX, t01);
        Vector3 desiredPos = ComputeWorldPos(curOffsetX);

        if (!hasSmoothed) { smoothedPos = desiredPos; hasSmoothed = true; }
        smoothedPos = Vector3.Lerp(smoothedPos, desiredPos, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));
        transform.position = smoothedPos;

        if (Time.time - spawnTime >= maxLifeTime) { ResolveMiss(); return; }

        HandleTap();
    }

    private Vector3 ComputeWorldPos(float offsetX)
    {
        Transform camT = arCamera.transform;

        Vector3 right = camT.right.normalized;
        Vector3 up = camT.up.normalized;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized;
        if (forwardFlat.sqrMagnitude < 0.0001f) forwardFlat = camT.forward.normalized;

        return camT.position + forwardFlat * distance + right * offsetX + up * heightOffset;
    }

    private void HandleTap()
    {
#if ENABLE_INPUT_SYSTEM
        // New input system (Android builds love this)
        if (Touch.activeTouches.Count > 0)
        {
            var t = Touch.activeTouches[0];
            if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                TryHitAtScreenPos(t.screenPosition);
        }
#else
        // Old input system fallback
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                TryHitAtScreenPos(t.position);
        }
#endif

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
            TryHitAtScreenPos(Input.mousePosition);
#endif
    }

    private void TryHitAtScreenPos(Vector2 screenPos)
    {
        if (resolved || arCamera == null) return;

        Ray ray = arCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            var tb = hit.collider ? hit.collider.GetComponentInParent<TargetBehaviour>() : null;
            if (tb == this) ResolveHit();
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
}
