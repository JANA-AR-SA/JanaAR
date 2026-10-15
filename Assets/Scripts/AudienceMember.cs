using System.Collections;
using UnityEngine;

public enum AudienceAnimationType
{
    SittingIdle,        // Sitting Idle.fbx
    SittingOnce,        // Sitting.fbx (plays once then blends to Sitting Idle)
    MaleSittingPose,    // Male Sitting Pose.fbx
    FemaleSittingPose,  // Female Sitting Pose.fbx
    SittingTalking,     // Sitting Talking.fbx
    SittingTalking1,    // Sitting Talking (1).fbx
    SittingLaughing,    // Sitting Laughing.fbx
    SittingDisbelief,   // Sitting Disbelief.fbx
    SittingClap,        // Sitting Clap.fbx
    LeaveRoomSequence   // Sit To Stand.fbx -> Walking Turn 180.fbx -> Walking.fbx
}

[RequireComponent(typeof(AudioSource))]
public class AudienceMember : MonoBehaviour
{
    [Header("Default Base Posture")]
    [Tooltip("The initial sitting animation this character starts in on the ImageTarget.")]
    public AudienceAnimationType basePosture = AudienceAnimationType.SittingIdle;

    [Header("Smooth Blending Settings")]
    [Range(0.15f, 0.8f)] public float crossFadeDuration = 0.35f;

    [Header("Exact Animator State Names")]
    public string stateSittingIdle = "Sitting Idle";
    public string stateSittingOnce = "Sitting";
    public string stateMalePose = "Male Sitting Pose";
    public string stateFemalePose = "Female Sitting Pose";
    public string stateTalking0 = "Sitting Talking";
    public string stateTalking1 = "Sitting Talking (1)";
    public string stateLaughing = "Sitting Laughing";
    public string stateDisbelief = "Sitting Disbelief";
    public string stateClap = "Sitting Clap";
    public string stateSitToStand = "Sit To Stand";
    public string stateTurn180 = "Walking Turn 180";
    public string stateWalking = "Walking";

    [Header("3D Audio Clips")]
    public AudioClip laughClip;
    public AudioClip whisperClip;
    public AudioClip disbeliefSighClip;
    public AudioClip clapLoopClip;
    public AudioClip chairStandClip;
    public AudioClip footstepsLoopClip;

    [Header("Leave Sequence Settings (Local to ImageTarget)")]
    public float standUpDuration = 2.0f;
    public float turn180Duration = 1.5f;
    [Tooltip("Walking speed relative to the ImageTarget scale.")]
    public float walkSpeed = 1.4f;
    public float walkRotationSpeed = 4.0f;

    private Animator anim;
    private AudioSource audioSource;
    private Coroutine currentActionCoroutine;

    // Strictly Local Coordinates relative to the Vuforia ImageTarget / Parent
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialAnimChildLocalPos;
    private Quaternion initialAnimChildLocalRot;
    private AudienceAnimationType initialBasePosture;

    private bool isLeaving = false;
    private bool isBusyWithAction = false;

    public bool IsBusy => isBusyWithAction || isLeaving;
    public bool IsLeaving => isLeaving;

    void Awake()
    {
        // Save local coordinates relative to the ImageTarget parent ONLY
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialBasePosture = basePosture;

        Animator[] allAnimators = GetComponentsInChildren<Animator>(true);
        foreach (var a in allAnimators)
        {
            if (a.runtimeAnimatorController != null)
            {
                anim = a;
                break;
            }
        }

        if (anim == null && allAnimators.Length > 0)
        {
            anim = allAnimators[0];
        }

        if (anim != null)
        {
            initialAnimChildLocalPos = anim.transform.localPosition;
            initialAnimChildLocalRot = anim.transform.localRotation;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 1.2f;
        audioSource.maxDistance = 12.0f;
        audioSource.playOnAwake = false;
    }

    void OnEnable()
    {
        // Whenever enabled on the Vuforia ImageTarget, snap strictly to local chair coordinates
        RestoreLocalChairTransform();

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.speed = Random.Range(0.94f, 1.05f);

            if (basePosture == AudienceAnimationType.SittingOnce)
            {
                currentActionCoroutine = StartCoroutine(PlaySitDownOnceRoutine());
            }
            else
            {
                PlayBasePostureImmediate();
            }
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        currentActionCoroutine = null;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
    }

    // Called during Reset to guarantee local alignment with the chair on the ImageTarget
    public void RestoreLocalChairTransform()
    {
        isLeaving = false;
        isBusyWithAction = false;
        basePosture = initialBasePosture;

        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;

        if (anim != null && anim.transform != transform)
        {
            anim.transform.localPosition = initialAnimChildLocalPos;
            anim.transform.localRotation = initialAnimChildLocalRot;
        }
    }

    private bool HasValidController()
    {
        if (anim == null || anim.runtimeAnimatorController == null)
        {
            Debug.LogError($"[{gameObject.name}] Missing AnimatorController! Please assign 'AudienceController' to the Animator component on '{gameObject.name}'.");
            return false;
        }
        return true;
    }

    private void PlayBasePostureImmediate()
    {
        if (!HasValidController()) return;

        string targetState = GetStateName(basePosture);
        float randomOffset = (basePosture == AudienceAnimationType.SittingIdle) ? Random.Range(0f, 1f) : 0f;
        anim.Play(targetState, 0, randomOffset);
    }

    public void ReturnToBasePostureSmooth()
    {
        if (isLeaving || !HasValidController()) return;
        isBusyWithAction = false;

        AudienceAnimationType restType = (basePosture == AudienceAnimationType.SittingOnce)
            ? AudienceAnimationType.SittingIdle
            : basePosture;

        anim.CrossFadeInFixedTime(GetStateName(restType), crossFadeDuration);
    }

    private IEnumerator PlaySitDownOnceRoutine()
    {
        isBusyWithAction = true;
        anim.Play(stateSittingOnce, 0, 0f);
        yield return null;

        float clipLength = GetCurrentClipLength(2.2f);
        yield return new WaitForSeconds(Mathf.Max(0.2f, clipLength - crossFadeDuration));

        basePosture = AudienceAnimationType.SittingIdle;
        ReturnToBasePostureSmooth();
    }

    public void PerformAction(AudienceAnimationType actionType, float customDuration = 0f, Transform exitPoint = null)
    {
        if (isLeaving || !HasValidController() || !gameObject.activeInHierarchy) return;

        if (actionType == AudienceAnimationType.LeaveRoomSequence)
        {
            if (currentActionCoroutine != null) StopCoroutine(currentActionCoroutine);
            currentActionCoroutine = StartCoroutine(LeaveRoomRoutineLocalToTarget(exitPoint));
            return;
        }

        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);
        }

        currentActionCoroutine = StartCoroutine(ExecuteActionRoutine(actionType, customDuration));
    }

    private IEnumerator ExecuteActionRoutine(AudienceAnimationType actionType, float customDuration)
    {
        isBusyWithAction = true;
        string stateName = GetStateName(actionType);

        anim.CrossFadeInFixedTime(stateName, crossFadeDuration);
        PlayActionAudio(actionType);

        yield return null;

        float waitDuration = customDuration;
        if (waitDuration <= 0f)
        {
            waitDuration = GetCurrentClipLength(3.2f);
        }

        yield return new WaitForSeconds(Mathf.Max(0.3f, waitDuration - crossFadeDuration));

        StopLoopingAudio();
        ReturnToBasePostureSmooth();
    }

    private float GetCurrentClipLength(float fallbackLength)
    {
        if (!HasValidController()) return fallbackLength;

        AnimatorClipInfo[] nextClips = anim.GetNextAnimatorClipInfo(0);
        if (nextClips != null && nextClips.Length > 0 && nextClips[0].clip != null)
        {
            return nextClips[0].clip.length;
        }

        AnimatorClipInfo[] currClips = anim.GetCurrentAnimatorClipInfo(0);
        if (currClips != null && currClips.Length > 0 && currClips[0].clip != null)
        {
            return currClips[0].clip.length;
        }

        return fallbackLength;
    }

    private void PlayActionAudio(AudienceAnimationType actionType)
    {
        StopLoopingAudio();

        switch (actionType)
        {
            case AudienceAnimationType.SittingLaughing:
                if (laughClip != null) audioSource.PlayOneShot(laughClip);
                break;

            case AudienceAnimationType.SittingDisbelief:
                if (disbeliefSighClip != null) audioSource.PlayOneShot(disbeliefSighClip);
                break;

            case AudienceAnimationType.SittingTalking:
            case AudienceAnimationType.SittingTalking1:
                if (whisperClip != null)
                {
                    audioSource.clip = whisperClip;
                    audioSource.loop = true;
                    audioSource.volume = 0.7f;
                    audioSource.Play();
                }
                break;

            case AudienceAnimationType.SittingClap:
                if (clapLoopClip != null)
                {
                    audioSource.clip = clapLoopClip;
                    audioSource.loop = true;
                    audioSource.volume = 0.75f;
                    audioSource.PlayDelayed(Random.Range(0.05f, 0.3f));
                }
                break;
        }
    }

    private void StopLoopingAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
    }

    // Executes the Leave Sequence strictly in the Local Space of the Vuforia ImageTarget
    private IEnumerator LeaveRoomRoutineLocalToTarget(Transform exitPoint)
    {
        isLeaving = true;
        isBusyWithAction = true;
        StopLoopingAudio();
        anim.speed = 1.0f;

        // 1. Sit To Stand
        anim.CrossFadeInFixedTime(stateSitToStand, crossFadeDuration);
        if (chairStandClip != null)
        {
            audioSource.PlayOneShot(chairStandClip, 0.85f);
        }

        yield return new WaitForSeconds(Mathf.Max(0.2f, standUpDuration - crossFadeDuration));

        // 2. Turn 180 degrees in Local Space of the ImageTarget
        anim.CrossFadeInFixedTime(stateTurn180, crossFadeDuration);
        Quaternion startLocalRot = transform.localRotation;
        Quaternion endLocalRot = startLocalRot * Quaternion.Euler(0f, 180f, 0f);
        float turnElapsed = 0f;

        while (turnElapsed < turn180Duration)
        {
            turnElapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, turnElapsed / turn180Duration);
            transform.localRotation = Quaternion.Slerp(startLocalRot, endLocalRot, t);
            yield return null;
        }

        // 3. Walk along the ImageTarget local surface toward exitPoint
        anim.CrossFadeInFixedTime(stateWalking, crossFadeDuration);
        if (footstepsLoopClip != null)
        {
            audioSource.clip = footstepsLoopClip;
            audioSource.loop = true;
            audioSource.volume = 0.6f;
            audioSource.Play();
        }

        if (exitPoint != null)
        {
            while (true)
            {
                // Recalculate target position in parent's local space every frame so AR camera movement never affects walking
                Vector3 targetLocalPos = (transform.parent != null)
                    ? transform.parent.InverseTransformPoint(exitPoint.position)
                    : exitPoint.localPosition;

                targetLocalPos.y = transform.localPosition.y;

                if (Vector3.Distance(transform.localPosition, targetLocalPos) <= 0.08f)
                    break;

                Vector3 localDir = (targetLocalPos - transform.localPosition).normalized;
                localDir.y = 0f;

                if (localDir != Vector3.zero)
                {
                    Quaternion targetLocalRot = Quaternion.LookRotation(localDir, Vector3.up);
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRot, Time.deltaTime * walkRotationSpeed);
                }

                transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetLocalPos, walkSpeed * Time.deltaTime);
                yield return null;
            }
        }

        StopLoopingAudio();
        gameObject.SetActive(false);
    }

    private string GetStateName(AudienceAnimationType type)
    {
        switch (type)
        {
            case AudienceAnimationType.SittingIdle: return stateSittingIdle;
            case AudienceAnimationType.SittingOnce: return stateSittingOnce;
            case AudienceAnimationType.MaleSittingPose: return stateMalePose;
            case AudienceAnimationType.FemaleSittingPose: return stateFemalePose;
            case AudienceAnimationType.SittingTalking: return stateTalking0;
            case AudienceAnimationType.SittingTalking1: return stateTalking1;
            case AudienceAnimationType.SittingLaughing: return stateLaughing;
            case AudienceAnimationType.SittingDisbelief: return stateDisbelief;
            case AudienceAnimationType.SittingClap: return stateClap;
            default: return stateSittingIdle;
        }
    }
}