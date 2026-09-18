using System;
using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>A pooled tourist that enters, requests one photo point at its pivot, then exits after resolution.</summary>
    public sealed class TouristBehaviour : MonoBehaviour
    {
        private const float ArrivalDistance = 0.01f;

        private enum RouteState
        {
            Inactive,
            Entering,
            WaitingForPhotoResolution,
            Leaving,
        }

        [SerializeField] private Transform photoPivot;

        private SpriteRenderer[] spriteRenderers;
        private RouteState state;
        private Vector3 waitPosition;
        private Vector3 exitPosition;
        private float moveSpeed;
        private PhotoPointManager photoPointManager;
        private PhotoPointView activePhotoPoint;
        private Action<TouristBehaviour> completedCallback;

        private void Awake()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void OnDisable()
        {
            state = RouteState.Inactive;
            activePhotoPoint = null;
            completedCallback = null;
        }

        public void BeginRoute(
            Vector3 spawnPosition,
            Vector3 nextWaitPosition,
            Vector3 nextExitPosition,
            float movementSpeed,
            PhotoPointManager manager,
            Action<TouristBehaviour> routeCompletedCallback)
        {
            transform.position = spawnPosition;
            waitPosition = nextWaitPosition;
            exitPosition = nextExitPosition;
            moveSpeed = Mathf.Max(0f, movementSpeed);
            photoPointManager = manager;
            completedCallback = routeCompletedCallback;
            activePhotoPoint = null;
            state = RouteState.Entering;
            SetFacing(waitPosition.x - spawnPosition.x);
        }

        public void ConfigurePrototype(Transform pivot)
        {
            photoPivot = pivot;
        }

        private void Update()
        {
            switch (state)
            {
                case RouteState.Entering:
                    if (MoveTowards(waitPosition))
                        BeginWaitingForPhoto();
                    break;

                case RouteState.Leaving:
                    if (MoveTowards(exitPosition))
                        CompleteRoute();
                    break;
            }
        }

        private void BeginWaitingForPhoto()
        {
            state = RouteState.WaitingForPhotoResolution;
            if (photoPointManager == null)
            {
                BeginLeaving();
                return;
            }

            Vector2 photoPosition = photoPivot != null ? photoPivot.position : transform.position;
            activePhotoPoint = photoPointManager.SpawnAtPosition(photoPosition, OnPhotoPointResolved);
            if (activePhotoPoint == null)
                BeginLeaving();
        }

        private void OnPhotoPointResolved(PhotoPointResult result)
        {
            activePhotoPoint = null;
            if (state == RouteState.WaitingForPhotoResolution)
                BeginLeaving();
        }

        private void BeginLeaving()
        {
            state = RouteState.Leaving;
            SetFacing(exitPosition.x - transform.position.x);
        }

        private bool MoveTowards(Vector3 destination)
        {
            Vector3 position = transform.position;
            float horizontalDelta = destination.x - position.x;
            if (Mathf.Abs(horizontalDelta) > ArrivalDistance)
                SetFacing(horizontalDelta);

            transform.position = Vector3.MoveTowards(position, destination, moveSpeed * Time.deltaTime);
            return Vector3.SqrMagnitude(transform.position - destination) <= ArrivalDistance * ArrivalDistance;
        }

        private void CompleteRoute()
        {
            if (state == RouteState.Inactive)
                return;

            state = RouteState.Inactive;
            Action<TouristBehaviour> callback = completedCallback;
            completedCallback = null;
            callback?.Invoke(this);
        }

        private void SetFacing(float horizontalDelta)
        {
            if (Mathf.Approximately(horizontalDelta, 0f) || spriteRenderers == null)
                return;

            bool movingRight = horizontalDelta > 0f;
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                    spriteRenderers[i].flipX = !movingRight;
            }
        }
    }
}
