using Planetcraft.Building;
using UnityEngine;

namespace Planetcraft.Combat
{
    // Efeitos curtos de combate: rastro de tiro, fagulhas de impacto e explosão. Tudo criado em código e descartado.
    public static class CombatFx
    {
        static Material tracerMaterial;

        // Rastro luminoso entre dois pontos que some em poucos centésimos de segundo.
        public static void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.04f, float life = 0.08f)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = TracerMaterial();
            lr.startColor = lr.endColor = color;
            Object.Destroy(go, life);
        }

        public static void Sparks(Vector3 point, Vector3 normal, Color color, int count = 10)
        {
            var go = new GameObject("Sparks");
            go.transform.position = point;
            go.transform.rotation = Quaternion.LookRotation(normal == Vector3.zero ? Vector3.up : normal);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = color;
            main.gravityModifier = 0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MachineFx.FxMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            Object.Destroy(go, 1.2f);
        }

        // Explosão de drone ou torre: clarão de faíscas e uma nuvem de fumaça.
        public static void Explosion(Vector3 point, float scale = 1f)
        {
            Sparks(point, Vector3.up, new Color(1f, 0.6f, 0.2f), (int)(30 * scale));
            var go = new GameObject("Smoke");
            go.transform.position = point;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 2f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.1f * scale);
            main.startColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(10 * scale)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f * scale;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MachineFx.FxMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            Object.Destroy(go, 3f);
        }

        static Material TracerMaterial()
        {
            if (tracerMaterial != null) return tracerMaterial;
            tracerMaterial = new Material(MachineFx.FxMaterial());
            // Só a cor do vértice importa: textura branca (o disco suave afinaria a linha).
            tracerMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
            return tracerMaterial;
        }

        // Dispara um raio e aplica dano ao primeiro alvo (Health) que não seja aliado. Devolve o ponto final.
        public static Vector3 Hitscan(Vector3 origin, Vector3 direction, float range, float damage, Team shooter, GameObject source,
            out Health hit, int mask = Physics.DefaultRaycastLayers)
        {
            hit = null;
            var ray = new Ray(origin, direction);
            var hits = Physics.RaycastAll(ray, range, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (source != null && h.collider.transform.IsChildOf(source.transform)) continue;
                var target = h.collider.GetComponentInParent<Health>();
                if (target != null && !target.IsDead)
                {
                    bool ally = shooter == Team.Hostile ? target.Team == Team.Hostile : target.Team != Team.Hostile;
                    if (ally) continue;
                    target.Damage(damage, source);
                    hit = target;
                }
                Sparks(h.point, h.normal, shooter == Team.Hostile ? new Color(1f, 0.3f, 0.2f) : new Color(0.5f, 0.9f, 1f), 6);
                return h.point;
            }
            return origin + direction * range;
        }
    }
}
