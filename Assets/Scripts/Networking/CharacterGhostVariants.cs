using System.Collections.Generic;
using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    public partial class CharacterGhostVariantSystem : DefaultVariantSystemBase
    {
        protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
        {
            defaultVariants.Add(typeof(KinematicCharacterBody), Rule.ForAll(typeof(PolyStrikeKinematicCharacterBodyVariant)));
            defaultVariants.Add(typeof(CharacterInterpolation), Rule.ForAll(typeof(PolyStrikeCharacterInterpolationVariant)));
            defaultVariants.Add(typeof(TrackedTransform), Rule.ForAll(typeof(PolyStrikeTrackedTransformVariant)));
        }
    }

    [GhostComponentVariation(typeof(KinematicCharacterBody))]
    [GhostComponent]
    public struct PolyStrikeKinematicCharacterBodyVariant
    {
        [GhostField(Quantization = 1000)] public float3 RelativeVelocity;
        [GhostField] public bool IsGrounded;
        [GhostField] public Entity ParentEntity;
        [GhostField(Quantization = 1000)] public float3 ParentLocalAnchorPoint;
        [GhostField(Quantization = 1000)] public float3 ParentVelocity;
    }

    [GhostComponentVariation(typeof(CharacterInterpolation))]
    [GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
    public struct PolyStrikeCharacterInterpolationVariant
    {
    }

    [GhostComponentVariation(typeof(TrackedTransform))]
    [GhostComponent]
    public struct PolyStrikeTrackedTransformVariant
    {
        [GhostField] public RigidTransform CurrentFixedRateTransform;
    }
}
