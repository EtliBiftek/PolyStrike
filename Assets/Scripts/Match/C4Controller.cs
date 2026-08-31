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
        private const float BombMoveSpeed = 250f;

        private static GameObject plantedBomb;

        private MatchParticipant participant;
        private PlayerMovement movement;
        private HitscanWeapon weapon;
        private UtilityController utility;
        private ViewmodelMotion viewmodel;
        private Camera playerCamera;

        private float interactionStartedAt;
        private float interactionDuration;
        private bool planting;
        private bool defusing;
        private bool bombEquipped;

        public static Transform PlantedBombTransform => plantedBomb != null ? plantedBomb.transform : null;
        public bool IsInteracting => planting || defusing;
        public bool IsPlanting => planting;
        public bool IsDefusing => defusing;
        public bool IsBombEquipped => bombEquipped;
        public float InteractionProgress => IsInteracting && interactionDuration > 0f
            ? Mathf.Clamp01((Time.time - interactionStartedAt) / interactionDuration)
            : 0f;

        private void Awake()
        {
            participant = GetComponent<MatchParticipant>();
            movement = GetComponent<PlayerMovement>();
            utility = GetComponent<UtilityController>();
            weapon = GetComponentInChildren<HitscanWeapon>();
            viewmodel = GetComponentInChildren<ViewmodelMotion>(true);
            playerCamera = Camera.main;
            participant.Died += OnParticipantDied;
        }

        private void Start()
        {
            var match = MatchRoundManager.Instance;
            if (match != null)
                match.StateChanged += OnMatchStateChanged;
        }

        private void OnDestroy()
        {
            if (participant != null)
                participant.Died -= OnParticipantDied;

            var match = MatchRoundManager.Instance;
            if (match != null)
                match.StateChanged -= OnMatchStateChanged;
        }

        private void Update()
        {
            var match = MatchRoundManager.Instance;
            if (match == null || participant.Health == null || participant.Health.IsDead)
            {
                CancelInteraction();
                HolsterBomb();
                return;
            }

            if (participant.Team == MatchTeam.Terrorists)
                UpdateTerroristBomb(match);
            else
                UpdateCounterTerroristDefuse(match);
        }

        private void UpdateTerroristBomb(MatchRoundManager match)
        {
            var canHandleBomb = participant.CarriesBomb &&
                                (match.Phase == RoundPhase.FreezeTime || match.Phase == RoundPhase.Live);

            if (GameInput.BombPressed && canHandleBomb)
                EquipBomb();

            if (bombEquipped && (GameInput.Weapon1Pressed || GameInput.Weapon2Pressed || GameInput.UtilityPressed ||
                                 GameInput.HeGrenadePressed || GameInput.FlashbangPressed || GameInput.SmokePressed ||
                                 GameInput.MolotovPressed))
            {
                CancelInteraction();
                HolsterBomb();
                return;
            }

            if (bombEquipped && GameInput.DropPressed && canHandleBomb)
            {
                DropCarriedBomb(true);
                return;
            }

            if (match.Phase != RoundPhase.Live || !participant.CarriesBomb)
            {
                CancelInteraction();
                return;
            }

            var inSite = BombSite.FindAt(transform.position) != null;
            var wantsPlant = inSite && (GameInput.UseHeld || (bombEquipped && GameInput.FireHeld));

            if (!wantsPlant)
            {
                CancelInteraction();
                return;
            }

            if (!bombEquipped)
                EquipBomb();

            UpdatePlant(match);
        }

        private void UpdateCounterTerroristDefuse(MatchRoundManager match)
        {
            if (bombEquipped)
                HolsterBomb();

            if (match.Phase == RoundPhase.PostPlant && GameInput.UseHeld)
                UpdateDefuse(match);
            else
                CancelInteraction();
        }

        private void UpdatePlant(MatchRoundManager match)
        {
            var site = BombSite.FindAt(transform.position);
            if (site == null || !participant.CarriesBomb)
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
            participant.GiveBomb(false);
            FinishInteraction();
            HolsterBomb();
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

        public static bool TryBotPlant(MatchParticipant bot)
        {
            var match = MatchRoundManager.Instance;
            if (bot == null || !bot.IsAlive || bot.Team != MatchTeam.Terrorists || !bot.CarriesBomb ||
                match == null || match.Phase != RoundPhase.Live)
                return false;

            var site = BombSite.FindAt(bot.transform.position);
            if (site == null)
                return false;

            var position = bot.transform.position;
            position.y = Mathf.Max(site.PlantPosition.y + 0.05f, 0.05f);
            SpawnPlantedBomb(position);
            bot.GiveBomb(false);
            match.RegisterBombPlanted(bot);
            return true;
        }

        public static bool TryBotDefuse(MatchParticipant bot)
        {
            var match = MatchRoundManager.Instance;
            if (bot == null || !bot.IsAlive || bot.Team != MatchTeam.CounterTerrorists || plantedBomb == null ||
                match == null || match.Phase != RoundPhase.PostPlant)
                return false;

            if (Vector3.Distance(bot.transform.position, plantedBomb.transform.position) > DefuseUseDistance)
                return false;

            match.RegisterBombDefused(bot);
            ClearPlantedBomb();
            return true;
        }

        public bool DropCarriedBomb(bool throwForward)
        {
            if (!participant.CarriesBomb || plantedBomb != null)
                return false;

            CancelInteraction();
            participant.GiveBomb(false);

            var origin = transform.position + Vector3.up * 0.85f + transform.forward * 0.35f;
            var inherited = movement != null ? SourceUnit.ToSourceUnits(movement.WorldVelocity) : Vector3.zero;
            var toss = inherited;

            if (throwForward)
                toss += transform.forward * 220f + Vector3.up * 80f;
            else
                toss += Vector3.up * 45f;

            DroppedMatchItem.SpawnBomb(origin, SourceUnit.ToMeters(toss));
            HolsterBomb();
            return true;
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

        private void EquipBomb()
        {
            if (bombEquipped || !participant.CarriesBomb)
                return;

            bombEquipped = true;
            ApplyBombInputState();
            viewmodel?.SetBombMode(true);
            viewmodel?.PlayDeploy(0.45f);
        }

        private void ApplyBombInputState()
        {
            if (!bombEquipped)
                return;

            utility?.SetExternalInputBlocked(true);
            weapon?.SetExternalInputBlocked(true);
            movement?.SetExternalMaxSpeed(BombMoveSpeed);
        }

        private void HolsterBomb()
        {
            if (!bombEquipped)
                return;

            bombEquipped = false;
            viewmodel?.SetBombMode(false);
            movement?.ClearExternalMaxSpeed();

            var match = MatchRoundManager.Instance;
            var roundLocked = match != null && (match.Phase == RoundPhase.FreezeTime || match.Phase == RoundPhase.RoundEnd ||
                                                 match.Phase == RoundPhase.HalfTime || match.Phase == RoundPhase.MatchEnd);
            weapon?.SetExternalInputBlocked(roundLocked || (utility != null && utility.IsEquipped));
            utility?.SetExternalInputBlocked(roundLocked);
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
            weapon?.SetExternalInputBlocked(roundLocked || bombEquipped);
            utility?.SetExternalInputBlocked(roundLocked || bombEquipped);
        }

        private void OnMatchStateChanged()
        {
            if (!bombEquipped)
                return;

            var match = MatchRoundManager.Instance;
            if (match == null || (match.Phase != RoundPhase.FreezeTime && match.Phase != RoundPhase.Live))
            {
                HolsterBomb();
                return;
            }

            ApplyBombInputState();
        }

        private void OnParticipantDied(MatchParticipant deadParticipant)
        {
            if (deadParticipant == participant && participant.CarriesBomb)
                DropCarriedBomb(false);
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
