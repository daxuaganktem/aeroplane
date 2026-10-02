using System;
using System.Collections;
using UnityEngine;

namespace MotoBrawler.Bike
{
    /// <summary>
    /// Detects head-on wall impacts above the crash speed (from RidingAssists), freezes the
    /// bike, then respawns it on the road after a delay. While riding it remembers recent
    /// "safe" positions (both wheels on the road, upright, moving) and respawns at the oldest
    /// one, i.e. a couple of seconds back down the road, facing the way you were riding.
    ///
    /// Lives on the bike root (next to the Rigidbody) so it receives OnCollisionEnter.
    /// Later, combat can call Crash() directly (e.g. knocked off by a kick).
    /// </summary>
    [RequireComponent(typeof(BikeController))]
    public class BikeCrashHandler : MonoBehaviour
    {
        [Header("Crash detection")]
        [Tooltip("Layers that can crash the bike (walls, barriers, props). Ground layers are ignored " +
                 "by the angle test anyway.")]
        [SerializeField] private LayerMask crashMask = ~0;
        [Tooltip("How head-on the hit must be: 1 = dead straight, 0.6 ≈ within 53° of straight on.")]
        [SerializeField, Range(0f, 1f)] private float headOnThreshold = 0.6f;
        [Tooltip("Surfaces whose normal points up more than this are floors, not walls.")]
        [SerializeField, Range(0f, 1f)] private float maxWallNormalY = 0.6f;
        [Tooltip("Below this height the bike counts as fallen off the world.")]
        [SerializeField] private float killHeight = -30f;

        [Header("Respawn")]
        [SerializeField] private float respawnDelay = 2f;
        [Tooltip("No crashes for this long after respawning.")]
        [SerializeField] private float invulnerableTime = 1.5f;
        [Tooltip("Fallback respawn point (e.g. the start line). Uses the start pose if empty.")]
        [SerializeField] private Transform spawnPoint;
        [Tooltip("Surfaces that count as 'on the road' for respawn points.")]
        [SerializeField] private LayerMask respawnSurfaceMask = ~0;
        [Tooltip("How far back in time the respawn point is (s).")]
        [SerializeField, Range(0.5f, 6f)] private float respawnLookBack = 2.5f;
        [SerializeField] private float sampleInterval = 0.25f;
        [SerializeField] private float respawnHeightOffset = 0.3f;

        public event Action Crashed;
        public event Action Respawned;

        public bool IsInvulnerable => Time.time < invulnerableUntil;

        private BikeController bike;
        private Pose startPose;
        private Pose[] history;
        private int historyCount;
        private int historyHead;     // index of the next write
        private float sampleTimer;
        private float invulnerableUntil;
        private Coroutine respawnRoutine;

        private void Awake()
        {
            bike = GetComponent<BikeController>();
            startPose = new Pose(transform.position, transform.rotation);
            int size = Mathf.Max(2, Mathf.CeilToInt(respawnLookBack / sampleInterval) + 1);
            history = new Pose[size];
        }

        private void FixedUpdate()
        {
            if (bike.IsCrashed) return;

            if (transform.position.y < killHeight)
            {
                Crash();
                return;
            }

            sampleTimer += Time.fixedDeltaTime;
            if (sampleTimer >= sampleInterval)
            {
                sampleTimer = 0f;
                TryRecordSafePose();
            }
        }

        private void TryRecordSafePose()
        {
            var front = bike.FrontContact;
            var rear = bike.RearContact;
            if (!front.grounded || !rear.grounded) return;
            if (!IsInMask(front.collider, respawnSurfaceMask) || !IsInMask(rear.collider, respawnSurfaceMask)) return;
            if (bike.ForwardSpeed < 3f) return;
            if (Vector3.Dot(transform.up, Vector3.up) < 0.8f) return;

            // Store a level pose facing the direction of travel.
            Vector3 heading = Vector3.ProjectOnPlane(bike.Body.linearVelocity, Vector3.up);
            if (heading.sqrMagnitude < 0.01f) heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Quaternion rot = Quaternion.LookRotation(heading.normalized, Vector3.up);

            history[historyHead] = new Pose(transform.position, rot);
            historyHead = (historyHead + 1) % history.Length;
            historyCount = Mathf.Min(historyCount + 1, history.Length);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (bike.IsCrashed || IsInvulnerable) return;
            if (!IsInMask(collision.collider, crashMask)) return;

            Vector3 velocity = bike.VelocityBeforeStep;
            float speed = velocity.magnitude;
            if (speed < 0.5f) return;

            // Use the contact normal that faces against our motion (sign-safe).
            Vector3 normal = collision.GetContact(0).normal;
            if (Vector3.Dot(normal, velocity) > 0f) normal = -normal;

            // Floors / ramps / landings are not walls.
            if (Mathf.Abs(normal.y) > maxWallNormalY) return;

            // Speed straight into the wall, and how head-on the bike was pointing.
            float impactSpeed = -Vector3.Dot(velocity, normal);
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 flatIntoWall = Vector3.ProjectOnPlane(-normal, Vector3.up).normalized;
            float headOn = Vector3.Dot(flatForward, flatIntoWall);

            float crashSpeed = bike.Assists ? bike.Assists.CrashSpeed : 50f / 3.6f;
            if (headOn >= headOnThreshold && impactSpeed >= crashSpeed)
                Crash();
        }

        /// <summary>Crash now (also usable by combat / scripted events).</summary>
        public void Crash()
        {
            if (bike.IsCrashed) return;
            bike.SetCrashed(true);
            Crashed?.Invoke();
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            Respawn();
            respawnRoutine = null;
        }

        /// <summary>Put the bike back on the road immediately.</summary>
        public void Respawn()
        {
            Pose pose = PickRespawnPose();
            bike.SetCrashed(false);
            bike.Teleport(pose.position + Vector3.up * respawnHeightOffset, pose.rotation);

            // Start a fresh history from here so a second crash doesn't jump further back.
            historyCount = 0;
            historyHead = 0;
            sampleTimer = 0f;
            history[historyHead] = pose;
            historyHead = 1;
            historyCount = 1;

            invulnerableUntil = Time.time + invulnerableTime;
            Respawned?.Invoke();
        }

        private Pose PickRespawnPose()
        {
            if (historyCount > 0)
            {
                // Oldest stored sample = furthest back along the road.
                int oldest = (historyHead - historyCount + history.Length) % history.Length;
                return history[oldest];
            }
            return spawnPoint ? new Pose(spawnPoint.position, spawnPoint.rotation) : startPose;
        }

        private static bool IsInMask(Collider col, LayerMask mask)
        {
            return col && (mask.value & (1 << col.gameObject.layer)) != 0;
        }
    }
}
