using System.Collections;
using PolyStrike.Audio;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField] private bool firstPersonTracers = true;

        private static Material tracerMaterial;
        private static Material impactMaterial;
        private static Material bloodMaterial;

        private AudioSource weaponSource;
        private AudioSource feedbackSource;
        private Transform muzzle;
        private Light muzzleLight;
        private Coroutine duckRoutine;
        private Coroutine flashRoutine;

        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position;

        private void Awake()
        {
            weaponSource = gameObject.AddComponent<AudioSource>();
            weaponSource.playOnAwake = false;
            weaponSource.spatialBlend = 0f;
            weaponSource.volume = 0.92f;
            weaponSource.dopplerLevel = 0f;

            feedbackSource = gameObject.AddComponent<AudioSource>();
            feedbackSource.playOnAwake = false;
            feedbackSource.spatialBlend = 0f;
            feedbackSource.volume = 0.88f;
            feedbackSource.dopplerLevel = 0f;
        }

        public void SetMuzzle(Transform muzzleTransform)
        {
            muzzle = muzzleTransform;

            if (muzzleLight != null)
                return;

            var lightObject = new GameObject("Namlu Işığı");
            lightObject.transform.SetParent(muzzle, false);
            muzzleLight = lightObject.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = 2.4f;
            muzzleLight.intensity = 0f;
            muzzleLight.color = new Color(1f, 0.62f, 0.24f);
            muzzleLight.shadows = LightShadows.None;
        }

        public void SetFirstPersonTracers(bool visible)
        {
            firstPersonTracers = visible;
        }

        public void PlayWeaponShot(int style)
        {
            weaponSource.pitch = style == 0 ? 0.98f : 1.02f;
            weaponSource.PlayOneShot(ProceduralSfxBank.WeaponShot(style), 1f);
        }

        public void PlayReloadStart(int style)
        {
            weaponSource.pitch = 1f;
            weaponSource.PlayOneShot(ProceduralSfxBank.ReloadStart(style), 0.62f);
        }

        public void PlayReloadInsert(int style)
        {
            weaponSource.pitch = 1f;
            weaponSource.PlayOneShot(ProceduralSfxBank.ReloadInsert(style), 0.72f);
        }

        public void PlayDeploy(int style)
        {
            weaponSource.pitch = 1f;
            weaponSource.PlayOneShot(ProceduralSfxBank.Deploy(style), 0.58f);
        }

        public void PlayMuzzleFlash()
        {
            if (muzzleLight == null)
                return;

            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = StartCoroutine(MuzzleFlashRoutine());
        }

        public void PlayTracer(Vector3 end)
        {
            if (!firstPersonTracers)
                return;

            var start = MuzzlePosition;
            var tracer = new GameObject("Mermi İzi");
            var line = tracer.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = 0.006f;
            line.endWidth = 0.0015f;
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.material = GetTracerMaterial();
            line.startColor = new Color(1f, 0.82f, 0.34f, 0.90f);
            line.endColor = new Color(1f, 0.55f, 0.16f, 0.06f);

            Object.Destroy(tracer, 0.045f);
        }

        public void PlaySurfaceImpact(RaycastHit hit, SurfaceMaterial material)
        {
            PlayWorldSound(ProceduralSfxBank.SurfaceImpact(material), hit.point, 0.52f, 1.6f, 28f);
            CreateImpactMark(hit.point, hit.normal, material);
            CreateImpactParticles(hit.point, hit.normal, material);
        }

        public void PlayPlayerImpact(Vector3 point, Vector3 normal, HitGroup hitGroup, int healthDamage)
        {
            if (healthDamage <= 0)
                return;

            var headshot = hitGroup == HitGroup.Head;
            feedbackSource.pitch = headshot ? 1.04f : 1f;
            feedbackSource.PlayOneShot(ProceduralSfxBank.FleshImpact(headshot), headshot ? 0.94f : 0.76f);
            CreateBloodParticles(point, normal, headshot ? 14 : 8);

            if (duckRoutine != null)
                StopCoroutine(duckRoutine);

            duckRoutine = StartCoroutine(DuckWeaponForHit());
        }

        private IEnumerator MuzzleFlashRoutine()
        {
            muzzleLight.intensity = Random.Range(4.2f, 5.4f);
            yield return new WaitForSeconds(0.018f);

            var start = muzzleLight.intensity;
            var elapsed = 0f;
            while (elapsed < 0.025f)
            {
                elapsed += Time.deltaTime;
                muzzleLight.intensity = Mathf.Lerp(start, 0f, elapsed / 0.025f);
                yield return null;
            }

            muzzleLight.intensity = 0f;
            flashRoutine = null;
        }

        private IEnumerator DuckWeaponForHit()
        {
            weaponSource.volume = 0.34f;
            yield return new WaitForSeconds(0.045f);

            var elapsed = 0f;
            const float restoreTime = 0.055f;
            while (elapsed < restoreTime)
            {
                elapsed += Time.deltaTime;
                weaponSource.volume = Mathf.Lerp(0.34f, 0.92f, elapsed / restoreTime);
                yield return null;
            }

            weaponSource.volume = 0.92f;
            duckRoutine = null;
        }

        private static void CreateImpactMark(Vector3 point, Vector3 normal, SurfaceMaterial material)
        {
            if (material == SurfaceMaterial.Glass || material == SurfaceMaterial.Grate)
                return;

            var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mark.name = "Mermi İzi Lekesi";
            mark.transform.position = point + normal * 0.002f;
            mark.transform.rotation = Quaternion.LookRotation(normal);
            mark.transform.localScale = Vector3.one * Random.Range(0.026f, 0.042f);

            var collider = mark.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            mark.GetComponent<Renderer>().material = GetImpactMaterial();
            Object.Destroy(mark, 18f);
        }

        private static void CreateImpactParticles(Vector3 point, Vector3 normal, SurfaceMaterial material)
        {
            var objectName = material == SurfaceMaterial.Metal || material == SurfaceMaterial.Grate ? "Kıvılcım" : "Yüzey Parçası";
            var particleObject = new GameObject(objectName);
            particleObject.transform.position = point + normal * 0.01f;

            var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
            main.gravityModifier = 0.65f;
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.005f;
            particleObject.transform.rotation = Quaternion.LookRotation(normal);

            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetTracerMaterial();

            particles.Emit(material == SurfaceMaterial.Metal || material == SurfaceMaterial.Grate ? 8 : 5);
            particles.Play();
            Object.Destroy(particleObject, 0.45f);
        }

        private static void CreateBloodParticles(Vector3 point, Vector3 normal, int count)
        {
            var particleObject = new GameObject("Kan Parçacığı");
            particleObject.transform.position = point + normal * 0.01f;
            particleObject.transform.rotation = Quaternion.LookRotation(normal);

            var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.14f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.043f);
            main.gravityModifier = 0.72f;
            main.maxParticles = 18;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.012f;

            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetBloodMaterial();

            particles.Emit(count);
            particles.Play();
            Object.Destroy(particleObject, 0.55f);
        }

        private static void PlayWorldSound(AudioClip clip, Vector3 position, float volume, float minDistance, float maxDistance)
        {
            var soundObject = new GameObject("Dünya Sesi");
            soundObject.transform.position = position;

            var source = soundObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.Play();

            Object.Destroy(soundObject, clip.length + 0.1f);
        }

        private static Material GetTracerMaterial()
        {
            if (tracerMaterial == null)
                tracerMaterial = CreateMaterial(new Color(1f, 0.70f, 0.20f, 1f));

            return tracerMaterial;
        }

        private static Material GetImpactMaterial()
        {
            if (impactMaterial == null)
                impactMaterial = CreateMaterial(new Color(0.055f, 0.05f, 0.045f, 1f));

            return impactMaterial;
        }

        private static Material GetBloodMaterial()
        {
            if (bloodMaterial == null)
                bloodMaterial = CreateMaterial(new Color(0.42f, 0.015f, 0.012f, 1f));

            return bloodMaterial;
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;

            return material;
        }
    }
}
