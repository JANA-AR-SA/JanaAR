using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class TherapySessionManager : MonoBehaviour
{
    [System.Serializable]
    public class SpeechSegment
    {
        [TextArea(3, 5)]
        public string textToRead;
        public float durationInSeconds = 10f;
    }

    [System.Serializable]
    public class CustomAudienceEvent
    {
        public string eventName = "Audience Reaction";
        public float triggerTime = 3.5f;

        [Header("Primary Character (0 to 11)")]
        [Range(0, 11)] public int primaryCharacterIndex = 1;
        public AudienceAnimationType primaryAnimation = AudienceAnimationType.SittingTalking;
        public float primaryDuration = 4.5f;

        [Header("Overlapping Second Character (Mid-Animation Reaction)")]
        public bool triggerSecondCharacterMidWay = true;
        [Range(0, 11)] public int secondCharacterIndex = 2;
        public AudienceAnimationType secondAnimation = AudienceAnimationType.SittingTalking1;
        public float overlapDelaySeconds = 1.2f;
        public float secondDuration = 4.0f;

        [HideInInspector] public bool hasTriggered = false;
    }

    [Header("Start & Reset UI Buttons")]
    public Button startSessionButton;
    public Button resetSessionButton;
    public GameObject panel;

    [Header("Vuforia ImageTarget / Content Container")]
    [Tooltip("Drag your Vuforia ImageTarget GameObject (or the child container holding all 12 chairs and characters) here.")]
    public GameObject vuforiaTargetOrContainer;

    [Header("UI Button Click Sounds")]
    public AudioClip resetButtonClickSound;
    public AudioClip startButtonClickSound;

    [Header("UI Teleprompter")]
    public TextMeshProUGUI speechDisplayUI;
    public TextMeshProUGUI timerProgressUI;

    [Header("Audience Members (Assign All 12 Characters)")]
    public AudienceMember[] audience = new AudienceMember[12];
    [Tooltip("Make sure roomExitPoint is also a child of the ImageTarget so it moves with the target.")]
    public Transform roomExitPoint;

    [Header("1. Custom Inspector Events (Choose Who Does What: 0 to 11)")]
    public List<CustomAudienceEvent> customEvents = new List<CustomAudienceEvent>()
    {
        new CustomAudienceEvent {
            eventName = "Row 1: Overlapping Whisper & Reply",
            triggerTime = 3.5f,
            primaryCharacterIndex = 1,
            primaryAnimation = AudienceAnimationType.SittingTalking,
            primaryDuration = 4.5f,
            triggerSecondCharacterMidWay = true,
            secondCharacterIndex = 2,
            secondAnimation = AudienceAnimationType.SittingTalking1,
            overlapDelaySeconds = 1.2f,
            secondDuration = 4.0f
        },
        new CustomAudienceEvent {
            eventName = "Row 2: Disbelief Overlapped With Laugh",
            triggerTime = 8.0f,
            primaryCharacterIndex = 7,
            primaryAnimation = AudienceAnimationType.SittingDisbelief,
            primaryDuration = 0f,
            triggerSecondCharacterMidWay = true,
            secondCharacterIndex = 9,
            secondAnimation = AudienceAnimationType.SittingLaughing,
            overlapDelaySeconds = 1.0f,
            secondDuration = 0f
        },
        new CustomAudienceEvent {
            eventName = "Character Leaves + Mid-Action Reaction",
            triggerTime = 16.0f,
            primaryCharacterIndex = 5,
            primaryAnimation = AudienceAnimationType.LeaveRoomSequence,
            primaryDuration = 0f,
            triggerSecondCharacterMidWay = true,
            secondCharacterIndex = 10,
            secondAnimation = AudienceAnimationType.SittingDisbelief,
            overlapDelaySeconds = 1.3f,
            secondDuration = 0f
        }
    };

    [Header("2. Random Overlapping Crowd Generator")]
    public bool enableRandomOverlappingCrowd = true;
    public float minRandomInterval = 2.0f;
    public float maxRandomInterval = 4.5f;

    [Header("End of Session Applause")]
    public bool clapAtEndOfSession = true;
    public float applauseDuration = 6.0f;

    [Header("Speech Content")]
    public List<SpeechSegment> speechSegments = new List<SpeechSegment>()
    {
        new SpeechSegment {
            textToRead = "Good morning everyone. Today I will be discussing the impact of digital transformation on modern workplaces...",
            durationInSeconds = 10f
        },
        new SpeechSegment {
            textToRead = "As shown in recent studies, integrating intelligent systems significantly improves overall team productivity...",
            durationInSeconds = 12f
        },
        new SpeechSegment {
            textToRead = "In conclusion, thank you all for your time and attention. I would now like to open the floor for any questions.",
            durationInSeconds = 10f
        }
    };

    private AudioSource uiAudioSource;
    private bool sessionActive = false;

    private readonly AudienceAnimationType[] randomReactionPool = new AudienceAnimationType[]
    {
        AudienceAnimationType.SittingTalking,
        AudienceAnimationType.SittingTalking1,
        AudienceAnimationType.SittingLaughing,
        AudienceAnimationType.SittingDisbelief
    };

    void Awake()
    {
        uiAudioSource = GetComponent<AudioSource>();
        uiAudioSource.spatialBlend = 0f;
        uiAudioSource.playOnAwake = false;
    }

    void Start()
    {
        if (startSessionButton != null)
        {
            startSessionButton.onClick.RemoveListener(OnStartButtonClicked);
            startSessionButton.onClick.AddListener(OnStartButtonClicked);
            startSessionButton.gameObject.SetActive(true);
        }

        if (resetSessionButton != null)
        {
            resetSessionButton.onClick.RemoveListener(OnResetButtonClicked);
            resetSessionButton.onClick.AddListener(OnResetButtonClicked);
            resetSessionButton.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        if (speechDisplayUI != null)
            speechDisplayUI.text = "";

        if (timerProgressUI != null)
            timerProgressUI.text = "Waiting to start...";
    }

    public void OnStartButtonClicked()
    {
        if (startButtonClickSound != null && uiAudioSource != null)
        {
            uiAudioSource.PlayOneShot(startButtonClickSound);
        }

        StartTherapySession();
    }

    public void OnResetButtonClicked()
    {
        if (resetButtonClickSound != null && uiAudioSource != null)
        {
            uiAudioSource.PlayOneShot(resetButtonClickSound);
        }

        StopAllCoroutines();
        sessionActive = false;

        StartCoroutine(ResetVuforiaTargetAndStartImmediatelyRoutine());
    }

    private IEnumerator ResetVuforiaTargetAndStartImmediatelyRoutine()
    {
        if (vuforiaTargetOrContainer != null)
        {
            vuforiaTargetOrContainer.SetActive(false);
        }
        else
        {
            for (int i = 0; i < audience.Length; i++)
            {
                if (audience[i] != null)
                    audience[i].gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < audience.Length; i++)
        {
            if (audience[i] != null)
            {
                audience[i].gameObject.SetActive(true);
                audience[i].RestoreLocalChairTransform();
            }
        }

        yield return new WaitForSeconds(0.15f);

        if (vuforiaTargetOrContainer != null)
        {
            vuforiaTargetOrContainer.SetActive(true);
        }

        yield return null;

        // Re-enable all Renderers and Colliders (all 12 Chairs + 12 Characters)
        if (vuforiaTargetOrContainer != null)
        {
            Renderer[] allTargetRenderers = vuforiaTargetOrContainer.GetComponentsInChildren<Renderer>(true);
            foreach (var r in allTargetRenderers)
            {
                r.enabled = true;
            }

            Collider[] allTargetColliders = vuforiaTargetOrContainer.GetComponentsInChildren<Collider>(true);
            foreach (var c in allTargetColliders)
            {
                c.enabled = true;
            }
        }
        else
        {
            for (int i = 0; i < audience.Length; i++)
            {
                if (audience[i] != null)
                {
                    Renderer[] charRenderers = audience[i].GetComponentsInChildren<Renderer>(true);
                    foreach (var r in charRenderers)
                    {
                        r.enabled = true;
                    }
                }
            }
        }

        StartTherapySession();
    }

    public void StartTherapySession()
    {
        if (sessionActive) return;
        sessionActive = true;
        panel.gameObject.SetActive(true);

        if (startSessionButton != null)
            startSessionButton.gameObject.SetActive(false);

        if (resetSessionButton != null)
            resetSessionButton.gameObject.SetActive(true);

        foreach (var ev in customEvents)
        {
            ev.hasTriggered = false;
        }

        StartCoroutine(RunTeleprompterTimerRoutine());
        StartCoroutine(RunCustomEventsRoutine());

        if (enableRandomOverlappingCrowd)
        {
            StartCoroutine(RunRandomOverlappingCrowdRoutine());
        }
    }

    private IEnumerator RunTeleprompterTimerRoutine()
    {
        for (int i = 0; i < speechSegments.Count; i++)
        {
            if (speechDisplayUI != null)
                speechDisplayUI.text = speechSegments[i].textToRead;

            float remainingTime = speechSegments[i].durationInSeconds;

            while (remainingTime > 0f)
            {
                if (timerProgressUI != null)
                {
                    timerProgressUI.text = $"Time Remaining: {Mathf.CeilToInt(remainingTime)}s (Segment {i + 1}/{speechSegments.Count})";
                }

                remainingTime -= Time.deltaTime;
                yield return null;
            }
        }

        sessionActive = false;

        if (speechDisplayUI != null)
            speechDisplayUI.text = "";
        if (timerProgressUI != null)
            timerProgressUI.text = "Session Completed";

        if (clapAtEndOfSession)
        {
            TriggerAllAudienceClap();
        }
    }

    private IEnumerator RunCustomEventsRoutine()
    {
        float elapsed = 0f;

        while (sessionActive)
        {
            elapsed += Time.deltaTime;

            foreach (var ev in customEvents)
            {
                if (!ev.hasTriggered && elapsed >= ev.triggerTime)
                {
                    ev.hasTriggered = true;
                    StartCoroutine(ExecuteOverlappingEvent(ev));
                }
            }

            yield return null;
        }
    }

    private IEnumerator ExecuteOverlappingEvent(CustomAudienceEvent ev)
    {
        if (IsValidIndex(ev.primaryCharacterIndex))
        {
            float dur1 = GetDefaultDurationIfZero(ev.primaryAnimation, ev.primaryDuration);
            audience[ev.primaryCharacterIndex].PerformAction(ev.primaryAnimation, dur1, roomExitPoint);
        }

        if (ev.triggerSecondCharacterMidWay && IsValidIndex(ev.secondCharacterIndex))
        {
            yield return new WaitForSeconds(ev.overlapDelaySeconds);

            if (sessionActive && audience[ev.secondCharacterIndex] != null)
            {
                float dur2 = GetDefaultDurationIfZero(ev.secondAnimation, ev.secondDuration);
                audience[ev.secondCharacterIndex].PerformAction(ev.secondAnimation, dur2, roomExitPoint);
            }
        }
    }

    private IEnumerator RunRandomOverlappingCrowdRoutine()
    {
        while (sessionActive)
        {
            float waitTime = Random.Range(minRandomInterval, maxRandomInterval);
            yield return new WaitForSeconds(waitTime);

            if (!sessionActive) yield break;

            AudienceMember charA = GetRandomAvailableCharacter(-1);
            if (charA != null)
            {
                AudienceAnimationType animA = randomReactionPool[Random.Range(0, randomReactionPool.Length)];
                float durA = GetDefaultDurationIfZero(animA, 0f);
                charA.PerformAction(animA, durA, roomExitPoint);

                if (Random.value <= 0.70f)
                {
                    float midAnimationDelay = Random.Range(0.7f, 1.5f);
                    yield return new WaitForSeconds(midAnimationDelay);

                    if (!sessionActive) yield break;

                    AudienceMember charB = GetRandomAvailableCharacter(System.Array.IndexOf(audience, charA));
                    if (charB != null)
                    {
                        AudienceAnimationType animB = randomReactionPool[Random.Range(0, randomReactionPool.Length)];
                        float durB = GetDefaultDurationIfZero(animB, 0f);
                        charB.PerformAction(animB, durB, roomExitPoint);
                    }
                }
            }
        }
    }

    private AudienceMember GetRandomAvailableCharacter(int excludeIndex)
    {
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < audience.Length; i++)
        {
            if (i != excludeIndex && audience[i] != null && audience[i].gameObject.activeInHierarchy && !audience[i].IsBusy)
            {
                availableIndices.Add(i);
            }
        }

        if (availableIndices.Count == 0) return null;
        int chosenIndex = availableIndices[Random.Range(0, availableIndices.Count)];
        return audience[chosenIndex];
    }

    private float GetDefaultDurationIfZero(AudienceAnimationType type, float customDuration)
    {
        if (customDuration > 0f) return customDuration;

        if (type == AudienceAnimationType.SittingTalking || type == AudienceAnimationType.SittingTalking1)
        {
            return Random.Range(3.5f, 4.5f);
        }

        return 0f;
    }

    private void TriggerAllAudienceClap()
    {
        foreach (var member in audience)
        {
            if (member != null && member.gameObject.activeInHierarchy && !member.IsLeaving)
            {
                member.PerformAction(AudienceAnimationType.SittingClap, applauseDuration);
            }
        }
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < audience.Length && audience[index] != null;
    }
}