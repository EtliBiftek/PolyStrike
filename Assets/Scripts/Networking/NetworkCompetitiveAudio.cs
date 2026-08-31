using System.Collections.Generic;
using PolyStrike.Gameplay;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkCompetitiveAudio : MonoBehaviour
    {
        private sealed class PlayerAudioState
        {
            public uint ShotSequence;
            public uint DetonateSequence;
            public float StepTravel;
            public float3 LastPosition;
            public bool HasPosition;
        }

        private readonly Dictionary<Entity, PlayerAudioState> playerAudio = new Dictionary<Entity, PlayerAudioState>();
        private readonly List<Entity> stale = new List<Entity>();

        private AudioSource uiSource;
        private AudioClip rifleShot;
        private AudioClip pistolShot;
        private AudioClip footstep;
        private AudioClip heExplosion;
        private AudioClip flashPop;
        private AudioClip smokePop;
        private AudioClip fireIgnite;
        private AudioClip bombTick;
        private AudioClip roundStart;
        private AudioClip roundEnd;
        private NetworkMatchPhase previousPhase;
        private bool hasPreviousPhase;
        private float nextBombTickAt;

        private void Awake()
        {
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
            uiSource.volume = 0.65f;

            rifleShot = CreateNoiseBurst("Rifle shot", 0.15f, 0.95f, 118f, 0.62f);
            pistolShot = CreateNoiseBurst("Pistol shot", 0.12f, 0.78f, 172f, 0.48f);
            footstep = CreateNoiseBurst("Footstep", 0.075f, 0.32f, 82f, 0.72f);
            heExplosion = CreateNoiseBurst("HE explosion", 0.58f, 1f, 54f, 0.92f);
            flashPop = CreateNoiseBurst("Flash pop", 0.18f, 0.68f, 410f, 0.28f);
            smokePop = CreateNoiseBurst("Smoke pop", 0.22f, 0.38f, 126f, 0.52f);
            fireIgnite = CreateNoiseBurst("Fire ignite", 0.30f, 0.52f, 92f, 0.74f);
            bombTick = CreateTone("Bomb tick", 980f, 0.055f, 0.42f);
            roundStart = CreateTone("Round start", 760f, 0.10f, 0.32f);
            roundEnd = CreateTone("Round end", 420f, 0.22f, 0.34f);
        }

        private void Update()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
            {
                playerAudio.Clear();
                hasPreviousPhase = false;
                return;
            }

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkPlayerState>(),
                ComponentType.ReadOnly<NetworkCombatPresentationState>(),
                ComponentType.ReadOnly<NetworkUtilityPresentationState>());
            var entities = query.ToEntityArray(Allocator.Temp);

            stale.Clear();
            foreach (var pair in playerAudio)
                stale.Add(pair.Key);

            NetworkMatchSnapshot localMatch = default;
            var hasLocalMatch = false;

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                stale.Remove(entity);
                var player = entityManager.GetComponentData<NetworkPlayerState>(entity);
                var combat = entityManager.GetComponentData<NetworkCombatPresentationState>(entity);
                var utility = entityManager.GetComponentData<NetworkUtilityPresentationState>(entity);

                if (!playerAudio.TryGetValue(entity, out var audioState))
                {
                    audioState = new PlayerAudioState
                    {
                        ShotSequence = combat.ShotSequence,
                        DetonateSequence = utility.DetonateSequence,
                        LastPosition = player.Position,
                        HasPosition = true
                    };
                    playerAudio.Add(entity, audioState);
                }
                else
                {
                    TickGunshot(audioState, in combat);
                    TickGrenade(audioState, in utility);
                    TickFootsteps(audioState, in player);
                }

                if (entityManager.HasComponent<GhostOwnerIsLocal>(entity) &&
                    entityManager.HasComponent<NetworkMatchSnapshot>(entity))
                {
                    localMatch = entityManager.GetComponentData<NetworkMatchSnapshot>(entity);
                    hasLocalMatch = true;
                }
            }

            for (var i = 0; i < stale.Count; i++)
                playerAudio.Remove(stale[i]);

            entities.Dispose();
            query.Dispose();

            if (hasLocalMatch)
                TickMatchAudio(in localMatch);
        }

        private void TickGunshot(PlayerAudioState audioState, in NetworkCombatPresentationState combat)
        {
            if (combat.ShotSequence == audioState.ShotSequence)
                return;

            audioState.ShotSequence = combat.ShotSequence;
            var clip = combat.Weapon == 1 ? rifleShot : pistolShot;
            PlaySpatial(clip, combat.Position, combat.Weapon == 1 ? 0.88f : 0.72f);
        }

        private void TickGrenade(PlayerAudioState audioState, in NetworkUtilityPresentationState utility)
        {
            if (utility.DetonateSequence == audioState.DetonateSequence)
                return;

            audioState.DetonateSequence = utility.DetonateSequence;
            var type = (GrenadeType)utility.DetonateType;
            var clip = type switch
            {
                GrenadeType.HighExplosive => heExplosion,
                GrenadeType.Flashbang => flashPop,
                GrenadeType.Smoke => smokePop,
                GrenadeType.Molotov => fireIgnite,
                _ => smokePop
            };
            PlaySpatial(clip, utility.DetonatePosition, type == GrenadeType.HighExplosive ? 1f : 0.72f);
        }

        private void TickFootsteps(PlayerAudioState audioState, in NetworkPlayerState player)
        {
            var alive = (player.Flags & NetworkPlayerFlags.Alive) != 0;
            var grounded = (player.Flags & NetworkPlayerFlags.Grounded) != 0;
            if (!alive || !grounded)
            {
                audioState.StepTravel = 0f;
                audioState.LastPosition = player.Position;
                audioState.HasPosition = true;
                return;
            }

            if (!audioState.HasPosition)
            {
                audioState.LastPosition = player.Position;
                audioState.HasPosition = true;
                return;
            }

            var delta = player.Position - audioState.LastPosition;
            audioState.LastPosition = player.Position;
            var planarDistance = math.length(delta.xz);
            if (planarDistance > 1.5f)
            {
                audioState.StepTravel = 0f;
                return;
            }

            var speed = math.length(player.Velocity.xz);
            if (speed < 1.1f)
            {
                audioState.StepTravel = 0f;
                return;
            }

            audioState.StepTravel += planarDistance;
            var stride = math.lerp(1.55f, 1.05f, math.saturate(speed / 5.5f));
            if (audioState.StepTravel < stride)
                return;

            audioState.StepTravel %= stride;
            var volume = math.lerp(0.20f, 0.48f, math.saturate((speed - 1.1f) / 4.4f));
            PlaySpatial(footstep, player.Position, volume);
        }

        private void TickMatchAudio(in NetworkMatchSnapshot match)
        {
            if (!hasPreviousPhase)
            {
                previousPhase = match.Phase;
                hasPreviousPhase = true;
            }
            else if (match.Phase != previousPhase)
            {
                if (match.Phase == NetworkMatchPhase.Live)
                    uiSource.PlayOneShot(roundStart);
                else if (match.Phase == NetworkMatchPhase.RoundEnd)
                    uiSource.PlayOneShot(roundEnd);

                previousPhase = match.Phase;
            }

            if (match.BombPlanted == 0 || match.BombTimeRemaining <= 0f)
            {
                nextBombTickAt = 0f;
                return;
            }

            if (nextBombTickAt <= 0f)
                nextBombTickAt = Time.unscaledTime;
            if (Time.unscaledTime < nextBombTickAt)
                return;

            PlaySpatial(bombTick, match.BombPosition, 0.68f);
            var urgency = 1f - Mathf.Clamp01(match.BombTimeRemaining / NetworkMatchRules.BombTimer);
            var interval = Mathf.Lerp(0.72f, 0.16f, urgency * urgency);
            nextBombTickAt = Time.unscaledTime + interval;
        }

        private static void PlaySpatial(AudioClip clip, float3 position, float volume)
        {
            if (clip == null)
                return;
            AudioSource.PlayClipAtPoint(clip, new Vector3(position.x, position.y, position.z), volume);
        }

        private static AudioClip CreateNoiseBurst(string name, float duration, float gain, float toneHz, float noiseMix)
        {
            const int sampleRate = 48000;
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            var samples = new float[sampleCount];
            var random = new System.Random(name.GetHashCode());
            var phase = 0f;
            var step = Mathf.PI * 2f * toneHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var normalized = i / (float)sampleCount;
                var envelope = Mathf.Exp(-normalized * 7.5f) * (1f - normalized);
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                var tone = Mathf.Sin(phase);
                phase += step;
                samples[i] = (noise * noiseMix + tone * (1f - noiseMix)) * envelope * gain;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, float gain)
        {
            const int sampleRate = 48000;
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            var samples = new float[sampleCount];
            var phaseStep = Mathf.PI * 2f * frequency / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var normalized = i / (float)sampleCount;
                var envelope = Mathf.Sin(Mathf.PI * normalized) * Mathf.Exp(-normalized * 2.4f);
                samples[i] = Mathf.Sin(phaseStep * i) * envelope * gain;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
