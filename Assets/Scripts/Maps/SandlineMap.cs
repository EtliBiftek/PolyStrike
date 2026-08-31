using PolyStrike.Gameplay;
using PolyStrike.Match;
using Unity.AI.Navigation;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace PolyStrike.Maps
{
    public static class SandlineMap
    {
        public static readonly Vector3[] TSpawns = ConvertSpawns(SandlineLayout.TSpawns);
        public static readonly Vector3[] CTSpawns = ConvertSpawns(SandlineLayout.CTSpawns);

        public static Vector3 ASiteCenter => ToVector3(SandlineLayout.ASiteCenter);
        public static Vector3 BSiteCenter => ToVector3(SandlineLayout.BSiteCenter);
        public static Vector3 MidControl => ToVector3(SandlineLayout.MidControl);
        public static Vector3 LongControl => ToVector3(SandlineLayout.LongControl);
        public static Vector3 ShortControl => ToVector3(SandlineLayout.ShortControl);
        public static Vector3 TunnelControl => ToVector3(SandlineLayout.TunnelControl);
        public static Vector3 MidDoors => ToVector3(SandlineLayout.MidDoors);
        public static Vector3 CtMid => ToVector3(SandlineLayout.CtMid);
        public static Vector3 ALongEntry => ToVector3(SandlineLayout.ALongEntry);
        public static Vector3 AShortEntry => ToVector3(SandlineLayout.AShortEntry);
        public static Vector3 BTunnelEntry => ToVector3(SandlineLayout.BTunnelEntry);
        public static Vector3 BMidEntry => ToVector3(SandlineLayout.BMidEntry);

        private static readonly Color Sand = new Color(0.64f, 0.53f, 0.37f);
        private static readonly Color SandDark = new Color(0.39f, 0.32f, 0.24f);
        private static readonly Color Stone = new Color(0.50f, 0.46f, 0.39f);
        private static readonly Color Wood = new Color(0.36f, 0.23f, 0.12f);

        public static GameObject Build()
        {
            var root = new GameObject("Sandline");
            CreateFloor(root.transform);
            CreateSolidGeometry(root.transform);
            CreateSites(root.transform);
            CreateVisualLandmarks(root.transform);

            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            return root;
        }

        public static Vector3 GetAttackGoal(bool attackA, int slot)
        {
            if (attackA)
            {
                return slot switch
                {
                    0 => LongControl,
                    1 => ALongEntry,
                    2 => ShortControl,
                    3 => MidControl,
                    _ => AShortEntry
                };
            }

            return slot switch
            {
                0 => TunnelControl,
                1 => BTunnelEntry,
                2 => MidControl,
                3 => BMidEntry,
                _ => TunnelControl + new Vector3(2.4f, 0f, 3.4f)
            };
        }

        public static Vector3 GetDefendGoal(int slot)
        {
            return slot switch
            {
                0 => ASiteCenter + new Vector3(2.5f, 0f, 1.7f),
                1 => AShortEntry + new Vector3(1.2f, 0f, 2.0f),
                2 => CtMid,
                3 => BMidEntry + new Vector3(-1.3f, 0f, 2.0f),
                _ => BSiteCenter + new Vector3(-2.3f, 0f, 1.5f)
            };
        }

        public static Vector3 GetPostPlantGoal(bool siteA, int slot)
        {
            if (siteA)
            {
                return slot switch
                {
                    0 => ALongEntry + new Vector3(1.8f, 0f, -1.6f),
                    1 => ASiteCenter + new Vector3(3.2f, 0f, -1.6f),
                    2 => AShortEntry + new Vector3(-1.4f, 0f, -0.5f),
                    3 => CtMid + new Vector3(5.8f, 0f, -1.8f),
                    _ => ASiteCenter + new Vector3(-2.8f, 0f, 2.4f)
                };
            }

            return slot switch
            {
                0 => BTunnelEntry + new Vector3(-1.6f, 0f, -1.4f),
                1 => BSiteCenter + new Vector3(-3.0f, 0f, -1.4f),
                2 => BMidEntry + new Vector3(1.2f, 0f, -0.8f),
                3 => CtMid + new Vector3(-5.6f, 0f, -1.5f),
                _ => BSiteCenter + new Vector3(2.6f, 0f, 2.3f)
            };
        }

        private static void CreateFloor(Transform root)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Sandline Ground";
            floor.transform.SetParent(root, false);
            floor.transform.localScale = new Vector3(7.4f, 1f, 7.4f);
            ConfigureSurface(floor, SurfaceMaterial.Concrete, Sand);
        }

        private static void CreateSolidGeometry(Transform root)
        {
            for (var i = 0; i < SandlineLayout.SolidBlocks.Length; i++)
            {
                var block = SandlineLayout.SolidBlocks[i];
                var surface = block.Surface == SandlineSurface.Wood ? SurfaceMaterial.Wood : SurfaceMaterial.Concrete;
                var color = block.Surface == SandlineSurface.Wood
                    ? Wood
                    : PickStoneColor(block.Center, block.Size);

                CreateBlock(
                    root,
                    $"Sandline Geometry {i + 1}",
                    ToVector3(block.Center),
                    ToVector3(block.Size),
                    color,
                    surface);
            }
        }

        private static void CreateSites(Transform root)
        {
            CreateSite(root, "A", ASiteCenter, new Vector3(
                SandlineLayout.ASiteHalfExtents.x * 2f,
                0.12f,
                SandlineLayout.ASiteHalfExtents.y * 2f), new Color(0.55f, 0.30f, 0.08f));

            CreateSite(root, "B", BSiteCenter, new Vector3(
                SandlineLayout.BSiteHalfExtents.x * 2f,
                0.12f,
                SandlineLayout.BSiteHalfExtents.y * 2f), new Color(0.16f, 0.32f, 0.52f));
        }

        private static void CreateVisualLandmarks(Transform root)
        {
            CreateNonSolidMarker(root, new Vector3(26.0f, 3.25f, 17.5f), new Vector3(0.18f, 3.5f, 4.8f), new Color(0.46f, 0.20f, 0.08f));
            CreateNonSolidMarker(root, new Vector3(-26.0f, 3.0f, 17.5f), new Vector3(0.18f, 3.1f, 4.8f), new Color(0.10f, 0.24f, 0.38f));
            CreateNonSolidMarker(root, new Vector3(0f, 3.2f, 15.0f), new Vector3(4.8f, 0.12f, 0.20f), new Color(0.62f, 0.55f, 0.43f));
        }

        private static void CreateNonSolidMarker(Transform root, Vector3 position, Vector3 scale, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Sandline Landmark";
            marker.transform.SetParent(root, false);
            marker.transform.position = position;
            marker.transform.localScale = scale;
            var collider = marker.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
            SetColor(marker.GetComponent<Renderer>(), color);
        }

        private static Color PickStoneColor(float3 center, float3 size)
        {
            if (size.y <= 1.5f)
                return center.x < 0f ? Stone * 0.92f : Stone;
            return math.abs(center.x) > 20f ? SandDark : Stone;
        }

        private static void CreateSite(Transform root, string id, Vector3 position, Vector3 scale, Color color)
        {
            var site = GameObject.CreatePrimitive(PrimitiveType.Cube);
            site.name = $"Sandline Site {id}";
            site.transform.SetParent(root, false);
            site.transform.position = position;
            site.transform.localScale = scale;
            SetColor(site.GetComponent<Renderer>(), color);
            site.AddComponent<BombSite>().Configure(id);
        }

        private static void CreateBlock(
            Transform root,
            string name,
            Vector3 position,
            Vector3 scale,
            Color color,
            SurfaceMaterial surfaceMaterial)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(root, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            ConfigureSurface(block, surfaceMaterial, color);
        }

        private static void ConfigureSurface(GameObject gameObject, SurfaceMaterial material, Color color)
        {
            gameObject.AddComponent<PenetrableSurface>().Configure(material);
            SetColor(gameObject.GetComponent<Renderer>(), color);
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            var material = renderer.material;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
        }

        private static Vector3[] ConvertSpawns(float3[] source)
        {
            var result = new Vector3[source.Length];
            for (var i = 0; i < source.Length; i++)
                result[i] = ToVector3(source[i]);
            return result;
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
