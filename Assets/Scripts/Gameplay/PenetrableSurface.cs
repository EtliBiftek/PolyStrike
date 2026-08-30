using UnityEngine;

namespace PolyStrike.Gameplay
{
    public enum SurfaceMaterial
    {
        Concrete,
        Metal,
        Wood,
        Cardboard,
        Plastic,
        Glass,
        Grate
    }

    [DisallowMultipleComponent]
    public sealed class PenetrableSurface : MonoBehaviour
    {
        [SerializeField] private SurfaceMaterial material = SurfaceMaterial.Concrete;

        public SurfaceMaterial Material => material;

        public float PenetrationModifier
        {
            get
            {
                switch (material)
                {
                    case SurfaceMaterial.Metal:
                        return 0.4f;
                    case SurfaceMaterial.Concrete:
                        return 0.5f;
                    case SurfaceMaterial.Wood:
                        return 0.9f;
                    case SurfaceMaterial.Cardboard:
                        return 0.95f;
                    case SurfaceMaterial.Plastic:
                        return 0.75f;
                    case SurfaceMaterial.Glass:
                    case SurfaceMaterial.Grate:
                        return 3f;
                    default:
                        return 1f;
                }
            }
        }

        public float DamageLossModifier => material == SurfaceMaterial.Glass || material == SurfaceMaterial.Grate ? 0.05f : 0.16f;

        public float SameMaterialModifier
        {
            get
            {
                if (material == SurfaceMaterial.Wood || material == SurfaceMaterial.Cardboard)
                    return 3f;

                if (material == SurfaceMaterial.Plastic)
                    return 2f;

                return PenetrationModifier;
            }
        }

        public void Configure(SurfaceMaterial surfaceMaterial)
        {
            material = surfaceMaterial;
        }
    }
}
