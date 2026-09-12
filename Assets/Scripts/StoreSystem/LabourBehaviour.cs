using UnityEngine;

/// <summary>
/// Moves one workshop labourer horizontally inside a Controller-provided work range.
/// The labourer owns no gameplay state; it only presents the level unlocked by StoreVisualController.
/// </summary>
public sealed class LabourBehaviour : MonoBehaviour
{
    private const float ArrivalDistance = 0.01f;

    private SpriteRenderer[] spriteRenderers;
    private float minimumX;
    private float maximumX;
    private float fixedY;
    private float moveSpeed;
    private float minimumTargetDistance;
    private Vector2 idleTimeRange;
    private float targetX;
    private float idleTimer;
    private bool hasTarget;
    private bool isInitialized;

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    /// <summary>
    /// Configures this worker from StoreVisualController. The position is immediately clamped into the supplied range.
    /// </summary>
    public void Initialize(
        float leftBoundaryX,
        float rightBoundaryX,
        float workingY,
        float movementSpeed,
        float requiredTargetDistance,
        Vector2 idleDurationRange)
    {
        minimumX = Mathf.Min(leftBoundaryX, rightBoundaryX);
        maximumX = Mathf.Max(leftBoundaryX, rightBoundaryX);
        fixedY = workingY;
        moveSpeed = Mathf.Max(0f, movementSpeed);
        minimumTargetDistance = Mathf.Max(0f, requiredTargetDistance);
        idleTimeRange = NormalizeRange(idleDurationRange);

        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, minimumX, maximumX);
        position.y = fixedY;
        transform.position = position;

        idleTimer = 0f;
        hasTarget = false;
        PickNextTarget();
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        if (idleTimer > 0f)
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
                PickNextTarget();

            return;
        }

        Vector3 position = transform.position;
        float deltaX = targetX - position.x;
        if (Mathf.Abs(deltaX) <= ArrivalDistance)
        {
            position.x = targetX;
            position.y = fixedY;
            transform.position = position;
            StartIdle();
            return;
        }

        SetFacing(deltaX);
        position.x = Mathf.MoveTowards(position.x, targetX, moveSpeed * Time.deltaTime);
        position.y = fixedY;
        transform.position = position;
    }

    private void PickNextTarget()
    {
        if (maximumX - minimumX <= ArrivalDistance)
        {
            targetX = minimumX;
            hasTarget = true;
            return;
        }

        float candidate = targetX;
        bool foundValidTarget = false;
        for (int i = 0; i < 8; i++)
        {
            candidate = Random.Range(minimumX, maximumX);
            if (!hasTarget || Mathf.Abs(candidate - targetX) >= minimumTargetDistance)
            {
                foundValidTarget = true;
                break;
            }
        }

        if (!foundValidTarget)
        {
            float fallbackDistance = Mathf.Min(
                Mathf.Max(minimumTargetDistance, (maximumX - minimumX) * 0.5f),
                maximumX - minimumX);
            float preferredDirection = targetX <= (minimumX + maximumX) * 0.5f ? 1f : -1f;
            candidate = Mathf.Clamp(targetX + preferredDirection * fallbackDistance, minimumX, maximumX);
        }

        targetX = candidate;
        hasTarget = true;
    }

    private void StartIdle()
    {
        idleTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
    }

    private void SetFacing(float horizontalDelta)
    {
        if (Mathf.Approximately(horizontalDelta, 0f))
            return;

        bool movingRight = horizontalDelta > 0f;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].flipX = !movingRight;
        }
    }

    private static Vector2 NormalizeRange(Vector2 range)
    {
        return new Vector2(Mathf.Max(0f, Mathf.Min(range.x, range.y)), Mathf.Max(0f, Mathf.Max(range.x, range.y)));
    }
}
