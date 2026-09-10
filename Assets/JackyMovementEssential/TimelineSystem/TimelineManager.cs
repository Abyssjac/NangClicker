using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector))]
public class TimelineManager : MonoBehaviour
{
    public static TimelineManager Instance { get; private set; }

    [SerializeField] private PlayableDirector playableDirector;
    [SerializeField] private CameraMode cutsceneCameraMode = CameraMode.Cutscene;
    [SerializeField] private bool restartDirectorBeforePlay = true;
    [SerializeField] private bool restoreCameraOnStop = true;

    private bool isPlayingTimeline;

    public bool IsPlayingTimeline => isPlayingTimeline;

    private void Reset()
    {
        playableDirector = GetComponent<PlayableDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (playableDirector == null)
        {
            playableDirector = GetComponent<PlayableDirector>();
        }
    }

    private void OnEnable()
    {
        if (playableDirector != null)
        {
            playableDirector.stopped += HandleDirectorStopped;
        }
    }

    private void OnDisable()
    {
        if (playableDirector != null)
        {
            playableDirector.stopped -= HandleDirectorStopped;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (isPlayingTimeline && restoreCameraOnStop && AllCameraManager.Instance != null)
            {
                AllCameraManager.Instance.EndCameraOverride();
            }

            Instance = null;
        }
    }

    public bool PlayTimeline(PlayableAsset timelineAsset)
    {
        if (playableDirector == null)
        {
            Debug.LogWarning($"{nameof(TimelineManager)} has no PlayableDirector assigned.", this);
            return false;
        }

        if (timelineAsset == null)
        {
            Debug.LogWarning($"{nameof(TimelineManager)} cannot play a null timeline asset.", this);
            return false;
        }

        if (isPlayingTimeline || playableDirector.state == PlayState.Playing)
        {
            Debug.LogWarning($"{nameof(TimelineManager)} is already playing a timeline.", this);
            return false;
        }

        if (AllCameraManager.Instance == null)
        {
            Debug.LogWarning($"{nameof(TimelineManager)} cannot find an {nameof(AllCameraManager)} instance.", this);
            return false;
        }

        if (!AllCameraManager.Instance.BeginCameraOverride(cutsceneCameraMode))
        {
            return false;
        }

        playableDirector.playableAsset = timelineAsset;

        if (restartDirectorBeforePlay)
        {
            playableDirector.time = 0d;
            playableDirector.Evaluate();
        }

        isPlayingTimeline = true;
        playableDirector.Play();
        return true;
    }

    public void StopTimeline()
    {
        if (playableDirector == null)
        {
            return;
        }

        playableDirector.Stop();
    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (!isPlayingTimeline || director != playableDirector)
        {
            return;
        }

        isPlayingTimeline = false;

        if (restoreCameraOnStop && AllCameraManager.Instance != null)
        {
            AllCameraManager.Instance.EndCameraOverride();
        }
    }
}
