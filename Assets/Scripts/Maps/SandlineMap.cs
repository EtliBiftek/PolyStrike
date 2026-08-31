using PolyStrike.Gameplay;
using PolyStrike.Match;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace PolyStrike.Maps
{
    /// <summary>
    /// Sandline keeps the classic three-lane desert-map rhythm without copying another map's geometry.
    /// The important part here is timing: long sightline, contested mid, short connector and a tighter tunnel route.
    /// </summary>
    public static class SandlineMap
    {
        public static readonly Vector3[] TSpawns =
        {
            new Vector3(-1.6f, 0.05f, -24.0f),
            new Vector3(-0.8f, 0.05f, -24.0f),
            new Vector3(0.0f, 0.05f, -24.0f),
            new Vector3(0.8f, 0.05f, -24.0f),
            new Vector3(1.6f, 0.05f, -24.0f)
        };

        public static readonly Vector3[] CTSpawns =
        {
            new Vector3(-1.6f, 0.05f, 24.0f),
            new Vector3(-0.8f, 0.05f, 24.0f),
            new Vector3(0.0f, 0.05f, 24.0f),
            new Vector3(0.8f, 0.05f, 24.0f),
            new Vector3(1.6f, 0.05f, 24.0f)
        };

        public static readonly Vector3 ASiteCenter = new Vector3(17.0f, 0.08f, 14.5f);
        public static readonly Vector3 BSiteCenter = new Vector3(-16.5f, 0.08f, 15.0f);
        public static readonly Vector3 MidControl = new Vector3(0.0f, 0.05f, 5.0f);
        public static readonly Vector3 LongControl = new Vector3(16.0f, 0.05f, 0.0f);
        public static readonly Vector3 ShortControl = new Vector3(6.0f, 0.05f, 9.0f);
        public static readonly Vector3 TunnelControl = new Vector3(-16.0f, 0.05f, 2.0f);

        private static readonly Color Sand = new Color(0.58f, 0.48f, 0.34f);
        private static readonly Color SandDark = new Color(0.38f, 0.31f, 0.23f);
        private static readonly Color Stone = new Color(0.47f, 0.43f, 0.37f);
        private static readonly Color Wood = new Color(0.34f, 0.22f, 0.12f);

        public static GameObject Build()
        {
            var root = new GameObject("Sandline");

            CreateFloor(root.transform);
            CreatePerimeter(root.transform);
            CreateLaneGeometry(root.transform);
            CreateSites(root.transform);
            CreateCover(root.transform);

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
                return slot % 3 switch
                {
                    0 => LongControl,
                    1 => ShortControl,
                    _ => MidControl
                };
            }

            return slot % 3 switch
            {
                0 => TunnelControl,
                1 => MidControl,
                _ => new Vector3(-8f, 0.05f, 8f)
            };
        }

        public static Vector3 GetDefendGoal(int slot)
        {
            return slot switch
            {
                0 => ASiteCenter + new Vector3(2.0f, 0f, 1.5f),
                1 => ASiteCenter + new Vector3(-2.2f, 0f, -1.5f),
                2 => MidControl + new Vector3(0f, 0f, 6f),
                3 => BSiteCenter + new Vector3(2.0f, 0f, -1.3f),
                _ => BSiteCenter + new Vector3(-2.0f, 0f, 1.2f)
            };
        }

        private static void CreateFloor(Transform root)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Sandline Ground";
            floor.transform.SetParent(root, false);
            floor.transform.localScale = new Vector3(6f, 1f, 6f);
            ConfigureSurface(floor, SurfaceMaterial.Concrete, Sand);
        }

        private static void CreatePerimeter(Transform root)
        {
            CreateBlock(root, "North Boundary", new Vector3(0f, 1.5f, 29.5f), new Vector3(60f, 3f, 1f), Stone);
            CreateBlock(root, "South Boundary", new Vector3(0f, 1.5f, -29.5f), new Vector3(60f, 3f, 1f), Stone);
            CreateBlock(root, "West Boundary", new Vector3(-29.5f, 1.5f, 0f), new Vector3(1f, 3f, 60f), Stone);
            CreateBlock(root, "East Boundary", new Vector3(29.5f, 1.5f, 0f), new Vector3(1f, 3f, 60f), Stone);
        }

        private static void CreateLaneGeometry(Transform root)
        {
            // East lane: long route with a deliberate short/connector opening near A.
            CreateBlock(root, "East Divider South", new Vector3(8.2f, 1.5f, -8f), new Vector3(1f, 3f, 20f), SandDark);
            CreateBlock(root, "East Divider North", new Vector3(8.2f, 1.5f, 17f), new Vector3(1f, 3f, 12f), SandDark);
            CreateBlock(root, "Long Outer Building", new Vector3(24f, 1.7f, 2f), new Vector3(4f, 3.4f, 32f), SandDark);

            // West lane: a tighter route that forces two turns before B.
            CreateBlock(root, "West Divider South", new Vector3(-8.5f, 1.5f, -7f), new Vector3(1f, 3f, 22f), SandDark);
            CreateBlock(root, "West Divider North", new Vector3(-8.5f, 1.5f, 18f), new Vector3(1f, 3f, 10f), SandDark);
            CreateBlock(root, "Tunnel Outer Building", new Vector3(-24f, 1.7f, 2f), new Vector3(4f, 3.4f, 31f), SandDark);
            CreateBlock(root, "Tunnel Bend", new Vector3(-16f, 1.5f, 7.5f), new Vector3(8f, 3f, 1f), SandDark);

            // Mid has a narrow early duel and then fans out into both sites.
            CreateBlock(root, "Mid Gate Left", new Vector3(-4.8f, 1.5f, 8f), new Vector3(7.4f, 3f, 1f), Stone);
            CreateBlock(root, "Mid Gate Right", new Vector3(4.8f, 1.5f, 8f), new Vector3(7.4f, 3f, 1f), Stone);
            CreateBlock(root, "T Mid Left", new Vector3(-4.5f, 1.5f, -12f), new Vector3(8f, 3f, 1f), SandDark);
            CreateBlock(root, "T Mid Right", new Vector3(4.5f, 1.5f, -12f), new Vector3(8f, 3f, 1f), SandDark);

            // Low-poly arches are represented by two solid pillars; the open center remains playable.
            CreateBlock(root, "Mid Arch Left", new Vector3(-1.8f, 1.5f, 13f), new Vector3(1.4f, 3f, 1.2f), Stone);
            CreateBlock(root, "Mid Arch Right", new Vector3(1.8f, 1.5f, 13f), new Vector3(1.4f, 3f, 1.2f), Stone);
        }

        private static void CreateSites(Transform root)
        {
            CreateSite(root, "A", ASiteCenter, new Vector3(7.5f, 0.12f, 7.0f), new Color(0.52f, 0.28f, 0.08f));
            CreateSite(root, "B", BSiteCenter, new Vector3(7.0f, 0.12f, 7.0f), new Color(0.16f, 0.30f, 0.50f));
        }

        private static void CreateCover(Transform root)
        {
            CreateBlock(root, "A Triple", ASiteCenter + new Vector3(1.7f, 0.65f, 0.4f), new Vector3(1.4f, 1.3f, 1.4f), Wood, SurfaceMaterial.Wood);
            CreateBlock(root, "A Ramp Cover", ASiteCenter + new Vector3(-2.4f, 0.55f, 2.1f), new Vector3(1.2f, 1.1f, 2.0f), Stone);
            CreateBlock(root, "B Double", BSiteCenter + new Vector3(-1.5f, 0.65f, 0.5f), new Vector3(2.2f, 1.3f, 1.2f), Wood, SurfaceMaterial.Wood);
            CreateBlock(root, "B Platform Cover", BSiteCenter + new Vector3(2.0f, 0.55f, -1.8f), new Vector3(1.2f, 1.1f, 2.0f), Stone);
            CreateBlock(root, "Mid Box", new Vector3(2.4f, 0.55f, 2.5f), new Vector3(1.2f, 1.1f, 1.2f), Wood, SurfaceMaterial.Wood);
            CreateBlock(root, "Long Corner", new Vector3(13f, 0.65f, -5f), new Vector3(1.4f, 1.3f, 1.4f), Stone);
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
            SurfaceMaterial surfaceMaterial = SurfaceMaterial.Concrete)
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
    }
}
