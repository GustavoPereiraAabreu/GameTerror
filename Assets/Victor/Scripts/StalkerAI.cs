using UnityEngine;
using UnityEngine.AI;

public class StalkerAI : MonoBehaviour
{
    public enum BehaviorState { Patrol, Chase, Investigate, Search }
    public BehaviorState state = BehaviorState.Patrol;

    [Header("References")]
    public Transform player;
    public NavMeshAgent agent;
    public Transform[] patrolPoints;
    public AudioSource footstepAudio;
    public JumpscareController jumpscareController;

    [Header("Vision")]
    public float viewDistance = 12f;
    public float viewAngle = 100f;
    public LayerMask visionMask;
    public LayerMask obstacleMask;

    [Header("Hearing")]
    public float hearingRange = 10f;

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;

    [Header("Attack")]
    public float attackDistance = 2.2f;

    [Header("Footsteps")]
    public float maxFootstepDistance = 20f;
    public float minFootstepVolume = 0.05f;
    public float maxFootstepVolume = 0.8f;

    [Header("Timers")]
    public float patrolWaitTime = 2f;
    public float searchDuration = 7f;
    public float activationDelay = 0f;

    int currentPatrolPoint;

    float patrolWaitTimer;
    float searchTimer;
    float activationTimer;

    bool hasHeardNoise;
    bool isActive;
    bool hasTriggeredJumpscare;

    Vector3 investigationPosition;
    Vector3 lastKnownPlayerPosition;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (footstepAudio)
        {
            footstepAudio.loop = true;
            footstepAudio.volume = minFootstepVolume;
            footstepAudio.Play();
        }
    }

    void Update()
    {
        ActivationDelay();

        if (!isActive || hasTriggeredJumpscare)
            return;

        FootstepAudio();
        CheckPlayerVisibility();

        switch (state)
        {
            case BehaviorState.Patrol:
                Patrol();
                break;

            case BehaviorState.Chase:
                Chase();
                break;

            case BehaviorState.Investigate:
                Investigate();
                break;

            case BehaviorState.Search:
                Search();
                break;
        }
    }

    void ActivationDelay()
    {
        if (isActive)
            return;

        activationTimer += Time.deltaTime;
        agent.isStopped = true;

        if (activationTimer >= activationDelay)
        {
            isActive = true;
            agent.isStopped = false;
        }
    }

    void FootstepAudio()
    {
        if (!footstepAudio || !player)
            return;

        float distance = Vector3.Distance(transform.position, player.position);
        float volumeFactor = Mathf.Clamp01(1f - (distance / maxFootstepDistance));

        footstepAudio.volume = Mathf.Lerp(
            minFootstepVolume,
            maxFootstepVolume,
            volumeFactor
        );
    }

    void CheckPlayerVisibility()
    {
        Vector3 eyePosition = transform.position + Vector3.up * 1.6f;
        Vector3 directionToPlayer = (player.position - eyePosition).normalized;

        float distanceToPlayer =
            Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer > viewDistance)
            return;

        if (Vector3.Angle(transform.forward, directionToPlayer) > viewAngle * 0.5f)
            return;

        if (Physics.Raycast(
            eyePosition,
            directionToPlayer,
            out RaycastHit hit,
            viewDistance,
            visionMask))
        {
            bool playerDetected = hit.collider.CompareTag("Player");

            bool hasObstacle = Physics.Raycast(
                eyePosition,
                directionToPlayer,
                distanceToPlayer,
                obstacleMask);

            if (playerDetected && !hasObstacle)
            {
                lastKnownPlayerPosition = player.position;
                state = BehaviorState.Chase;
            }
        }
    }

    public void HearSound(Vector3 noisePosition)
    {
        if (Vector3.Distance(transform.position, noisePosition) <= hearingRange)
        {
            hasHeardNoise = true;
            investigationPosition = noisePosition;
            state = BehaviorState.Investigate;
        }
    }

    void Patrol()
    {
        agent.speed = patrolSpeed;

        if (patrolPoints.Length == 0)
            return;

        if (hasHeardNoise)
        {
            state = BehaviorState.Investigate;
            return;
        }

        if (agent.remainingDistance < 0.3f)
        {
            patrolWaitTimer += Time.deltaTime;

            if (patrolWaitTimer >= patrolWaitTime)
            {
                patrolWaitTimer = 0f;

                currentPatrolPoint =
                    Random.Range(0, patrolPoints.Length);

                agent.SetDestination(
                    patrolPoints[currentPatrolPoint].position
                );
            }
        }
    }

    void Chase()
    {
        agent.speed = chaseSpeed;

        float distanceToPlayer =
            Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackDistance)
        {
            if (!hasTriggeredJumpscare)
            {
                hasTriggeredJumpscare = true;

                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
                agent.enabled = false;

                jumpscareController.TriggerJumpscare();
            }

            return;
        }

        agent.isStopped = false;
        agent.SetDestination(player.position);

        lastKnownPlayerPosition = player.position;

        if (distanceToPlayer > viewDistance * 1.3f)
            state = BehaviorState.Investigate;
    }

    void Investigate()
    {
        agent.speed = patrolSpeed;

        Vector3 targetPosition =
            hasHeardNoise
                ? investigationPosition
                : lastKnownPlayerPosition;

        agent.SetDestination(targetPosition);

        if (agent.remainingDistance < 0.4f)
        {
            hasHeardNoise = false;
            searchTimer = 0f;

            state = BehaviorState.Search;
        }
    }

    void Search()
    {
        agent.speed = patrolSpeed;

        searchTimer += Time.deltaTime;

        if (searchTimer >= searchDuration)
        {
            state = BehaviorState.Patrol;

            if (patrolPoints.Length > 0)
            {
                agent.SetDestination(
                    patrolPoints[currentPatrolPoint].position
                );
            }
        }
    }

    public void DisableAI()
    {
        agent.isStopped = true;
        agent.ResetPath();
        agent.enabled = false;
        enabled = false;
    }
}