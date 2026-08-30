using PolyStrike.Core;
using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Match
{
    [RequireComponent(typeof(MatchParticipant))]
    public sealed class C4Controller : MonoBehaviour
    {
        private const float DefuseUseDistance = 2.0f;
        private const float DefuseLookDot = 0.52f;

        private static GameObject plantedBomb;

        private MatchParticipant participant;
        private PlayerMovement movement;
        private HitscanWeapon weapon;
        private UtilityController utility;
        private Camera playerCamera;

        private float interactionStartedAt;
        private float interactionDuration;
        private bool planting;
        private bool defusing;

        public static Transform PlantedBombTransform => plantedBomb != null ? plantedBomb.transform : null;
        public bool IsInteracting => planting || defusing;
        public bool IsPlanting => planting;
        public bool IsDefusing => defusing;
        public float InteractionProgress => IsInteracting && interactionDuration > 0f
            ? Mathf.Clamp01((Time.time - interactionStartedAt) / interactionDuration)
            : 0f;

        private void Awake()
        {
            participant = GetComponent<MatchParticipant>();
            movement = GetComponent<PlayerMovement>();
            utility = GetComponent<UtilityController>();
            weapon = GetComponentInChildren<HitscanWeapon>();
            playerCamera = Camera.main;
        }

        private void Update()
        {
            var match = MatchRoundManager.Instance;
            if (match == null || participant.Health == null || participant.Health.IsDead)
            {
                CancelInteraction();
                return;
            }

            if (!GameInput.UseHeld)
            {
                CancelInteraction();
                return;
            }

            if (participant.Team == MatchTeam.Terrorists && match.Phase == RoundPhase.Live)
                UpdatePlant(match);
            else if (participant.Team == MatchTeam.CounterTerrorists && match.Phase == RoundPhase.PostPlant)
                UpdateDefuse(match);
            else
                CancelInteraction();
        }

        private void UpdatePlant(MatchRoundManager match)
        {
            if (!participant.CarriesBomb)
            {
                CancelInteraction();
                return;
            }

            var site = BombSite.FindAt(transform.position);
            if (site == null)
            {
                CancelInteraction();
                return;
            }

            if (!planting)
                BeginInteraction(true, MatchRules.PlantTime);

            if (Time.time - interactionStartedAt < interactionDuration)
                return;

            var position = transform.position;
            position.y = Mathf.Max(site.PlantPosition.y + 0.05f, 0.05f);
            SpawnPlantedBomb(position);
            FinishInteraction();
            match.RegisterBombPlanted(participant);
        }

        private void UpdateDefuse(MatchRoundManager match)
        {
            if (plantedBomb == null || !CanUsePlantedBomb())
            {
                CancelInteraction();
                return;
            }

            var duration = participant.HasDefuseKit ? MatchRules.DefuseKitTime : MatchRules.DefuseTime;
            if (!defusing || !Mathf.Approximately(interactionDuration, duration))
                BeginInteraction(false, duration);

            if (Time.time - interactionStartedAt < interactionDuration)
                return;

            FinishInteraction();
            match.RegisterBombDefused(participant);
            ClearPlantedBomb();
        }

        private bool CanUsePlantedBomb()
        {
            if (plantedBomb == null)
                return false;

            var eye = playerCamera != null ? playerCamera.transform.position : transform.position + Vector3.up * 1.6f;
            var toBomb = plantedBomb.transform.position - eye;
            var distance = toBomb.magnitude;
            if (distance > DefuseUseDistance || distance <= 0.01f)
                return false;

            var forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            if (Vector3.Dot(forward, toBomb / distance) < DefuseLookDot)
                return false;

            if (!Physics.Raycast(eye, toBomb / distance, out var hit, distance + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                return true;

            return hit.transform == plantedBomb.transform || hit.transform.IsChildOf(plantedBomb.transform);
        }

        private void BeginInteraction(bool isPlant, float duration)
        {
            planting = isPlant;
            defusing = !isPlant;
            interactionStartedAt = Time.time;
            interactionDuration = duration;

            movement?.SetRoundMovementLocked(true);
            weapon?.SetExternalInputBlocked(true);
            utility?.SetExternalInputBlocked(true);
        }

        private void CancelInteraction()
        {
            if (IsInteracting)
                FinishInteraction();
        }

        private void FinishInteraction()
        {
            planting = false;
            defusing = false;
            interactionStartedAt = 0f;
            interactionDuration = 0f;

            var phase = MatchRoundManager.Instance != null ? MatchRoundManager.Instance.Phase : RoundPhase.Live;
            var roundLocked = phase == RoundPhase.FreezeTime || phase == RoundPhase.RoundEnd || phase == RoundPhase.HalfTime || phase == RoundPhase.MatchEnd;
            movement?.SetRoundMovementLocked(roundLocked);
            weapon?.SetExternalInputBlocked(roundLocked);
            utility?.SetExternalInputBlocked(roundLocked);
        }

        private static void SpawnPlantedBomb(Vector3 position)
        {
            ClearPlantedBomb();

            plantedBomb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plantedBomb.name = "Planted C4";
            plantedBomb.transform.position = position;
            plantedBomb.transform.localScale = new Vector3(0.28f, 0.12f, 0.20f);

            var renderer = plantedBomb.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
                renderer.material = new Material(shader);

            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", new Color(0.22f, 0.18f, 0.12f));
            else
                renderer.material.color = new Color(0.22f, 0.18f, 0.12f);

            plantedBomb.AddComponent<C4Beep>();
        }

        public static void ExplodePlantedBomb()
        {
            if (plantedBomb == null)
                return;

            var position = plantedBomb.transform.position;
            C4ExplosionPresentation.Play(position);
            Object.Destroy(plantedBomb);
            plantedBomb = null;
        }

        public static void ClearPlantedBomb()
        {
            if (plantedBomb != null)
                Object.Destroy(plantedBomb);

            plantedBomb = null;
        }
    }
}
