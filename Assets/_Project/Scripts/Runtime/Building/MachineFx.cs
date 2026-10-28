using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Building
{
    // Faz a máquina parecer funcionar: ventoinhas, anéis e roldanas giram, pistões sobem e descem, a broca gira e
    // desce, o brilho pulsa e saem fumaça, poeira, névoa ou faíscas. Tudo some aos poucos quando falta energia.
    //
    // Convenções de nome nos modelos (tools/gen_models.py), com eixos em coordenadas do Unity:
    //   Spin_<X|Y|Z>_nome   gira no eixo (Fan = rápido, Ring = lento e alternado, Sheave = médio)
    //   Bob_Y_nome          sobe e desce (pistão)
    //   Drill_Y_nome        gira e desce (haste da perfuratriz)
    //   Fx_Smoke|Dust|Mist|Spark   ponto onde nascem as partículas
    //   materiais glow_*    brilho que pulsa com a máquina ligada
    [RequireComponent(typeof(Machine))]
    public class MachineFx : MonoBehaviour
    {
        class Spinner
        {
            public Transform t;
            public Vector3 axis;
            public float speed;
        }

        class Bobber
        {
            public Transform t;
            public Vector3 basePos;
            public float amplitude, rate, phase;
            public bool spin;
        }

        class Glow
        {
            public Material material;
            public Color baseEmissive;
        }

        readonly List<Spinner> spinners = new();
        readonly List<Bobber> bobbers = new();
        readonly List<Glow> glows = new();
        readonly List<ParticleSystem> particles = new();
        readonly List<float> particleRates = new();
        Machine machine;
        float power;
        float time;
        static Texture2D soft;
        static Material fxMaterial;

        void Awake()
        {
            machine = GetComponent<Machine>();
            time = Random.value * 10f;
            int ring = 0;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("Spin_") || n.StartsWith("Bob_") || n.StartsWith("Drill_"))
                    foreach (var c in t.GetComponents<Collider>()) Destroy(c);
                if (n.StartsWith("Spin_") && n.Length > 6)
                {
                    float speed = n.Contains("Fan") ? 620f : n.Contains("Ring") ? 70f : n.Contains("Head") ? 40f :
                        n.Contains("Sheave") ? 170f : 200f;
                    if (n.Contains("Ring") && ++ring % 2 == 0) speed = -speed * 1.4f;
                    spinners.Add(new Spinner { t = t, axis = AxisOf(n[5]), speed = speed });
                }
                else if (n.StartsWith("Bob_Y_"))
                    bobbers.Add(new Bobber { t = t, basePos = t.localPosition, amplitude = 0.32f, rate = 2.1f, phase = Random.value * 6f });
                else if (n.StartsWith("Drill_Y_"))
                    bobbers.Add(new Bobber { t = t, basePos = t.localPosition, amplitude = 0.22f, rate = 1.4f, spin = true });
                else if (n.StartsWith("Fx_"))
                {
                    t.GetComponent<Renderer>()?.gameObject.SetActive(false);
                    BuildParticles(t, n.Substring(3));
                }
            }
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.materials;
                bool any = false;
                foreach (var m in mats)
                {
                    if (!m.name.StartsWith("glow_") || !m.HasProperty("_EmissiveColor")) continue;
                    glows.Add(new Glow { material = m, baseEmissive = m.GetColor("_EmissiveColor") });
                    any = true;
                }
                if (any) r.materials = mats;
            }
        }

        static Vector3 AxisOf(char c) => c == 'X' ? Vector3.right : c == 'Z' ? Vector3.forward : Vector3.up;

        void Update()
        {
            bool running = machine != null && machine.IsRunning;
            power = Mathf.MoveTowards(power, running ? 1f : 0f, Time.deltaTime * (running ? 0.6f : 0.9f));
            time += Time.deltaTime;

            foreach (var s in spinners)
                if (s.t != null) s.t.Rotate(s.axis, s.speed * power * Time.deltaTime, Space.Self);

            foreach (var b in bobbers)
            {
                if (b.t == null) continue;
                float wave = Mathf.Sin((time + b.phase) * b.rate);
                if (b.spin)
                {
                    b.t.localPosition = b.basePos + Vector3.up * ((wave - 1f) * 0.5f * b.amplitude * power);
                    b.t.Rotate(Vector3.up, 320f * power * Time.deltaTime, Space.Self);
                }
                else b.t.localPosition = b.basePos + Vector3.up * (wave * b.amplitude * power);
            }

            // Brilho: apagado sem energia, pulsando levemente em funcionamento.
            float pulse = Mathf.Lerp(0.04f, 0.85f + 0.15f * Mathf.Sin(time * 3.1f), power);
            foreach (var g in glows)
                if (g.material != null) g.material.SetColor("_EmissiveColor", g.baseEmissive * pulse);

            for (int i = 0; i < particles.Count; i++)
            {
                var em = particles[i].emission;
                em.rateOverTime = particleRates[i] * power;
            }
        }

        // ---------------------------------------------------------------- partículas
        void BuildParticles(Transform anchor, string kind)
        {
            var go = new GameObject("Particles_" + kind);
            go.transform.SetParent(anchor, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            var em = ps.emission;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            float rate;
            Color tint;
            Gradient fade = new();

            switch (kind)
            {
                case "Smoke": // chaminés dos aquecedores: sobe, se alarga e esmaece
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                    main.gravityModifier = -0.04f;
                    tint = new Color(0.42f, 0.4f, 0.38f);
                    shape.radius = 0.1f;
                    rate = 9f;
                    fade.SetKeys(new[] { new GradientColorKey(tint, 0f), new GradientColorKey(new Color(0.65f, 0.65f, 0.65f), 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.15f), new GradientAlphaKey(0f, 1f) });
                    size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 2.6f)));
                    break;
                case "Dust": // base da perfuratriz: nuvem baixa que se espalha
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                    main.gravityModifier = -0.01f;
                    tint = new Color(0.55f, 0.42f, 0.3f);
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.5f;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    rate = 14f;
                    fade.SetKeys(new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0f, 1f) });
                    size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 2f)));
                    break;
                case "Mist": // filtros: névoa pálida subindo
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                    tint = new Color(0.7f, 0.95f, 0.85f);
                    shape.radius = 0.15f;
                    rate = 6f;
                    fade.SetKeys(new[] { new GradientColorKey(tint, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.28f, 0.25f), new GradientAlphaKey(0f, 1f) });
                    size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 2.2f)));
                    break;
                default: // Spark: faíscas rápidas e brilhantes
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                    main.gravityModifier = 0.5f;
                    tint = new Color(1f, 0.75f, 0.35f);
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 40f;
                    shape.radius = 0.15f;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    rate = 10f;
                    fade.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                    size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0.2f)));
                    break;
            }
            col.color = new ParticleSystem.MinMaxGradient(fade);
            em.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = FxMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Add(ps);
            particleRates.Add(rate);
            ps.Play();
        }

        public static Material FxMaterial()
        {
            if (fxMaterial != null) return fxMaterial;
            fxMaterial = Resources.Load<Material>("Fx/FxSoft");
            if (fxMaterial == null) fxMaterial = new Material(Shader.Find("HDRP/Unlit"));
            else fxMaterial = new Material(fxMaterial);
            if (soft == null)
            {
                // Disco suave: opacidade cai do centro para a borda.
                soft = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    soft.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                soft.Apply();
            }
            fxMaterial.SetTexture("_MainTex", soft);
            return fxMaterial;
        }
    }
}
