using System;
using UnityEngine;

/// <summary>
/// A pooled road customer. It walks from one side of the workshop to a visit point,
/// waits, then continues through to the opposite side before returning to its owner.
/// </summary>
public sealed class CustomerBehaviour : MonoBehaviour
{
    private const float ArrivalDistance = 0.01f;

    private enum RouteState
    {
        Inactive,
        WalkingToVisit,
        WaitingAtVisit,
        WalkingToExit,
    }

    private SpriteRenderer[] spriteRenderers;
    private RouteState state;
    private Vector3 visitPosition;
    private Vector3 exitPosition;
    private float moveSpeed;
    private float waitTimer;
    private Action<CustomerBehaviour> onRouteCompleted;

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    /// <summary>
    /// Starts a new one-way shop visit. This is called every time a pooled customer is reused.
    /// </summary>
    public void BeginRoute(
        Vector3 spawnPosition,
        Vector3 nextVisitPosition,
        Vector3 nextExitPosition,
        float movementSpeed,
        Vector2 stayDurationRange,
        Action<CustomerBehaviour> completedCallback)
    {
        transform.position = spawnPosition;
        visitPosition = nextVisitPosition;
        exitPosition = nextExitPosition;
        moveSpeed = Mathf.Max(0f, movementSpeed);
        Vector2 normalizedStayRange = NormalizeRange(stayDurationRange);
        waitTimer = UnityEngine.Random.Range(normalizedStayRange.x, normalizedStayRange.y);
        onRouteCompleted = completedCallback;
        state = RouteState.WalkingToVisit;
        SetFacing(visitPosition.x - spawnPosition.x);
    }

    private void OnDisable()
    {
        state = RouteState.Inactive;
        onRouteCompleted = null;
    }

    private void Update()
    {
        switch (state)
        {
            case RouteState.WalkingToVisit:
                if (MoveTowards(visitPosition))
                    state = RouteState.WaitingAtVisit;
                break;

            case RouteState.WaitingAtVisit:
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f)
                {
                    state = RouteState.WalkingToExit;
                    SetFacing(exitPosition.x - transform.position.x);
                }
                break;

            case RouteState.WalkingToExit:
                if (MoveTowards(exitPosition))
                    CompleteRoute();
                break;
        }
    }

    private bool MoveTowards(Vector3 destination)
    {
        Vector3 position = transform.position;
        float deltaX = destination.x - position.x;
        if (Mathf.Abs(deltaX) > ArrivalDistance)
            SetFacing(deltaX);

        transform.position = Vector3.MoveTowards(position, destination, moveSpeed * Time.deltaTime);
        return Vector3.SqrMagnitude(transform.position - destination) <= ArrivalDistance * ArrivalDistance;
    }

    private void CompleteRoute()
    {
        if (state == RouteState.Inactive)
            return;

        state = RouteState.Inactive;
        Action<CustomerBehaviour> callback = onRouteCompleted;
        onRouteCompleted = null;
        callback?.Invoke(this);
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
