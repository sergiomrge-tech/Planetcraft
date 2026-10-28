using System;
using System.Collections;
using Planetcraft.Core;
using Planetcraft.Player;
using Planetcraft.Save;
using Planetcraft.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Planetcraft.Intro
{
    // Abertura do jogo: o jogador acorda na cabine da Estação Horizonte, o comunicador de pulso (holograma) dá as
    // instruções, ele veste o traje, atravessa o corredor até o hangar, embarca na nave e desce para a Terra.
    public class IntroDirector : MonoBehaviour
    {
        [Header("Cena")]
        [SerializeField] FirstPersonController player;
        [SerializeField] Interactor interactor;
        [SerializeField] Transform cameraPivot;
        [SerializeField] HoloComm comm;
        [SerializeField] Transform wrist;
        [SerializeField] Transform holo;
        [SerializeField] Transform bedPoint;
        [SerializeField] Transform standPoint;
        [SerializeField] float doorZ = 7.2f;
        [SerializeField] float hangarZ = 28.5f;
        [SerializeField] IntroInteract locker;
        [SerializeField] SlidingDoor cabinDoor;
        [SerializeField] SlidingDoor hangarDoor;
        [SerializeField] IntroInteract hatch;
        [SerializeField] GameObject[] beacons;
        [SerializeField] Transform lander;
        [SerializeField] Transform cockpitPoint;
        [SerializeField] Transform earthTarget;
        [SerializeField] GameObject[] engineFx;

        VisualElement fade, entry;
        Label prompt, skipHint, caption;
        bool skipping, paused;
        float speedBeforePause = 1f;
        VisualElement pausePanel;
        float shake;
        bool boarded;
        AudioSource hum, engine, music;

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        void Awake()
        {
            // Mostra no layout do jogo: sem cursor, sem pausa.
            Time.timeScale = float.TryParse(Arg("-pc-intro-speed"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var speed) ? speed : 1f;
            UIState.MenuOpen = false;
        }

        IEnumerator Start()
        {
            BuildOverlay();
            SetupAudio();
            // O quadro do holograma é um Quad sem cor de vértice: o shader multiplica por ela, então ela precisa ser branca.
            var holoFilter = holo.GetComponent<MeshFilter>();
            var holoMesh = Instantiate(holoFilter.sharedMesh);
            holoMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            holoFilter.sharedMesh = holoMesh;

            // Raios do projetor do relógio (lente -> cantos do quadro).
            var beamMat = new Material(Planetcraft.Combat.CombatFxAccess.Material());
            beamMat.SetFloat("_Intensity", 350f);
            beams = new LineRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("RaioHolo" + i);
                go.transform.SetParent(wrist, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.startWidth = 0.004f;
                lr.endWidth = 0.001f;
                lr.startColor = new Color(0.5f, 0.95f, 1f, 0.7f);
                lr.endColor = new Color(0.5f, 0.95f, 1f, 0.1f);
                lr.sharedMaterial = beamMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.enabled = false;
                beams[i] = lr;
            }
            foreach (var b in beacons) if (b != null) b.SetActive(false);
            foreach (var fx in engineFx) if (fx != null) fx.SetActive(false);
            player.enabled = false;
            interactor.enabled = false;
            wrist.localPosition = WristHidden;
            holo.gameObject.SetActive(false);

            string shot = Arg("-pc-intro-shot");
            int jump = int.TryParse(Arg("-pc-intro-step"), out var j) ? j : 0;
            yield return Run(jump, shot);
        }

        static readonly Vector3 WristHidden = new(-0.14f, -0.72f, 0.3f);
        static readonly Vector3 WristShown = new(-0.08f, -0.15f, 0.38f);

        // ------------------------------------------------------------------ roteiro
        IEnumerator Run(int startStep, string shotPath)
        {
            if (shotPath != null) StartCoroutine(ShotAndQuit(shotPath, float.TryParse(Arg("-pc-intro-wait"), out var w) ? w : 6f));

            // 1) Acorda na cápsula, deitado: a visão abre devagar, em duas piscadas.
            if (startStep <= 0)
            {
                cameraPivot.SetPositionAndRotation(bedPoint.position, bedPoint.rotation);
                SetFade(1f);
                yield return Fade(1f, 0.55f, 1.6f);
                yield return Fade(0.55f, 0.9f, 0.7f);
                yield return Fade(0.9f, 0f, 1.6f);
                yield return Say("CENTRAL HORIZONTE", "Colono 7, acorde. Aqui é a Central da Estação Horizonte.");
                yield return Say("CENTRAL HORIZONTE", "Seus sinais vitais estão estáveis. Depois de um milhão de anos em órbita, hoje é o Dia do Retorno.");
                // Levanta da cápsula.
                yield return Rise();
            }
            else
            {
                // Pulo de teste (-pc-intro-step): começa direto no trecho pedido.
                var at = startStep switch
                {
                    1 => standPoint.position,
                    2 => new Vector3(0f, 1.6f, 8.5f),
                    3 => new Vector3(0.2f, 1.6f, 17.5f),
                    _ => new Vector3(0f, 1.6f, 29.5f),
                };
                SetFade(0f);
                ReleasePlayer(at);
                ShowWrist(true);
                if (startStep >= 2) cabinDoor.Unlock(player.transform);
                if (startStep >= 3) hangarDoor.Unlock(player.transform);
            }

            // 2) Instruções de movimento e saída da cabine.
            if (startStep <= 1)
            {
                Objective("Saia da cabine pela porta");
                yield return Say("CENTRAL HORIZONTE", "Seu comunicador de pulso está ativo. Eu vou guiá-lo. Mova-se com [WASD] e olhe com o mouse.");
                Beacon(0);
                cabinDoor.Unlock(player.transform);
                yield return WaitUntil(() => player.transform.position.z > doorZ, () => Warp(doorZ + 1f));
                Beacon(-1);
            }

            // 3) O traje no armário do corredor.
            if (startStep <= 2)
            {
                Objective("Pegue o traje no armário [E]");
                yield return Say("CENTRAL HORIZONTE", "Comportas liberadas. No corredor há um armário com o seu traje: aproxime-se e aperte [E].");
                Beacon(1);
                yield return WaitUntil(() => locker.Used, () => locker.Interact(gameObject));
                Beacon(-1);
                Sfx.Craft();
                yield return Say("CENTRAL HORIZONTE", "Traje ajustado e selado. Oxigênio em cem por cento. Sua pistola de pulso também foi liberada: o botão esquerdo do mouse atira.");
                hangarDoor.Unlock(player.transform);
            }

            // 4) Até o hangar.
            if (startStep <= 3)
            {
                Objective("Siga o corredor até o hangar");
                yield return Say("CENTRAL HORIZONTE", "Agora siga o corredor até o hangar. Pode correr com [Shift] e pular com [Espaço].");
                Beacon(2);
                yield return WaitUntil(() => player.transform.position.z > hangarZ, () => Warp(hangarZ + 1f));
                Beacon(-1);
                yield return Say("CENTRAL HORIZONTE", "Aquela é a sua nave de descida. A Terra lá fora está queimada, Colono, mas nossos sensores mostram uma chance.");
                yield return Say("CENTRAL HORIZONTE", "Construa uma base, recupere o ar, a água e a vida. Quando a Terra respirar de novo, traga a nave-mãe de volta para casa.");
            }

            // 5) Embarque.
            Objective("Embarque na nave [E]");
            Beacon(3);
            yield return WaitUntil(() => hatch.Used, () => hatch.Interact(gameObject));
            Beacon(-1);
            yield return Boarding();
        }

        // ------------------------------------------------------------------ cenas
        IEnumerator Rise()
        {
            ShowWrist(true);
            float t = 0f;
            var from = cameraPivot.position;
            var fromRot = cameraPivot.rotation;
            while (t < 1f)
            {
                t += Time.deltaTime / 3f;
                float k = Mathf.SmoothStep(0f, 1f, t);
                cameraPivot.SetPositionAndRotation(Vector3.Lerp(from, standPoint.position, k), Quaternion.Slerp(fromRot, standPoint.rotation, k));
                yield return null;
            }
            ReleasePlayer();
        }

        void ReleasePlayer() => ReleasePlayer(standPoint.position);

        void ReleasePlayer(Vector3 pos)
        {
            // O jogador assume a câmera na posição indicada.
            var ctrl = player.GetComponent<CharacterController>();
            ctrl.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y - 1.6f, pos.z), Quaternion.Euler(0f, standPoint.eulerAngles.y, 0f));
            ctrl.enabled = true;
            cameraPivot.SetParent(player.transform, false);
            cameraPivot.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraPivot.localRotation = Quaternion.identity;
            player.enabled = true;
            interactor.enabled = true;
        }

        IEnumerator Boarding()
        {
            player.enabled = false;
            interactor.enabled = false;
            prompt.style.display = DisplayStyle.None;
            ShowWrist(false);
            yield return Fade(0f, 1f, 0.8f);

            // Banco do piloto: a câmera passa a ser da nave.
            var ctrl = player.GetComponent<CharacterController>();
            ctrl.enabled = false;
            player.transform.SetParent(lander, false);
            player.transform.position = cockpitPoint.position - Vector3.up * 1.6f;
            player.transform.rotation = lander.rotation;
            cameraPivot.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraPivot.localRotation = Quaternion.identity;
            comm.SetObjective("");
            foreach (var fx in engineFx) if (fx != null) fx.SetActive(true);
            engine.volume = 0f;
            engine.Play();
            boarded = true;
            yield return Fade(1f, 0f, 1.0f);
            yield return Say("CENTRAL HORIZONTE", "Iniciando a descida. Boa sorte, Colono 7. A humanidade conta com você.", false);

            // Decola do pad e sai pela abertura do hangar.
            var start = lander.position;
            float t = 0f;
            var heading = lander.rotation;
            while (t < 9f)
            {
                t += Time.deltaTime;
                engine.volume = Mathf.Clamp01(t / 2.5f) * 0.8f * Settings.EffectsVolume;
                float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 2f)) * 1.6f;
                float forward = Mathf.Pow(Mathf.Clamp01((t - 1.5f) / 7.5f), 2f) * 70f;
                lander.position = start + Vector3.up * lift + heading * Vector3.forward * forward;
                shake = Mathf.Clamp01(t / 4f) * 0.015f;
                yield return null;
            }

            // Fora da estação: vira para a Terra e mergulha.
            var outPos = lander.position;
            var dirToEarth = (earthTarget.position - outPos).normalized;
            var targetRot = Quaternion.LookRotation(dirToEarth, Vector3.up);
            var fromRot = lander.rotation;
            t = 0f;
            while (t < 9f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / 7f);
                lander.rotation = Quaternion.Slerp(fromRot, targetRot, k);
                lander.position += lander.forward * (28f + t * 14f) * Time.deltaTime;
                shake = Mathf.Lerp(0.015f, 0.06f, t / 9f);
                if (t > 5.5f) entry.style.opacity = Mathf.Clamp01((t - 5.5f) / 3.5f) * 0.7f;
                yield return null;
            }
            yield return Say("CENTRAL HORIZONTE", "Entrando na atmosfera. Segure firme.", false);

            // Reentrada: tremor forte, clarão laranja e, por fim, branco.
            t = 0f;
            while (t < 4.5f)
            {
                t += Time.deltaTime;
                lander.position += lander.forward * 120f * Time.deltaTime;
                shake = Mathf.Lerp(0.06f, 0.14f, t / 4.5f);
                entry.style.opacity = Mathf.Lerp(0.7f, 1f, t / 4.5f);
                engine.volume = Mathf.Lerp(0.8f, 1f, t / 4.5f) * Settings.EffectsVolume;
                yield return null;
            }
            fade.style.backgroundColor = new Color(1f, 0.97f, 0.9f);
            yield return Fade(0f, 1f, 1.2f);
            GameSession.StartGame(fromIntro: true);
        }

        // ------------------------------------------------------------------ utilidades de roteiro
        IEnumerator Say(string who, string text, bool wait = true)
        {
            ShowWrist(true);
            Sfx.Click();
            comm.Say(who, Loc.T(text));
            if (wait) yield return new WaitForSeconds(HoloComm.ReadTime(Loc.T(text)));
            else yield return new WaitForSeconds(1.2f);
        }

        void Objective(string text) => comm.SetObjective(Loc.T(text));

        void Beacon(int index)
        {
            for (int i = 0; i < beacons.Length; i++) if (beacons[i] != null) beacons[i].SetActive(i == index);
        }

        // -pc-intro-auto (testes): cumpre os objetivos sozinho para percorrer a abertura inteira.
        static readonly bool Auto = Array.IndexOf(Environment.GetCommandLineArgs(), "-pc-intro-auto") >= 0;

        static IEnumerator WaitUntil(Func<bool> condition, Action assist = null)
        {
            while (!condition())
            {
                if (Auto) assist?.Invoke();
                yield return null;
            }
        }

        void Warp(float z)
        {
            var ctrl = player.GetComponent<CharacterController>();
            ctrl.enabled = false;
            player.transform.position = new Vector3(0f, 0f, z);
            ctrl.enabled = true;
        }

        bool wristShown;
        LineRenderer[] beams;
        // Lente do projetor no modelo do braço (Blender x, z, -y).
        static readonly Vector3 LensLocal = new(0f, 0.15f, -0.13f);
        // Tamanho do quadro do holograma (proporção 4:3 da RenderTexture).
        static readonly Vector3 HoloSize = new(0.26f, 0.195f, 1f);

        void ShowWrist(bool show)
        {
            if (wristShown == show) return;
            wristShown = show;
            StartCoroutine(MoveWrist(show));
        }

        IEnumerator MoveWrist(bool show)
        {
            if (show) holo.gameObject.SetActive(true);
            var from = wrist.localPosition;
            var to = show ? WristShown : WristHidden;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.7f;
                wrist.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                holo.localScale = HoloSize * (show ? Mathf.Lerp(0.2f, 1f, t) : Mathf.Lerp(1f, 0.2f, t));
                yield return null;
            }
            if (!show) holo.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ tela
        void BuildOverlay()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            root.style.flexGrow = 1;
            root.AddToClassList(HudStyle.RootClass);
            root.pickingMode = PickingMode.Ignore;

            entry = HudStyle.FullScreen();
            entry.style.backgroundColor = new Color(1f, 0.45f, 0.1f);
            entry.style.opacity = 0f;
            root.Add(entry);

            fade = HudStyle.FullScreen();
            fade.style.backgroundColor = Color.black;
            fade.style.opacity = 1f;
            root.Add(fade);

            prompt = HudStyle.MakeLabel("", 20, HudStyle.Ink);
            var panel = HudStyle.Panel(true, -0.5f);
            panel.style.position = Position.Absolute;
            panel.style.left = new Length(50f, LengthUnit.Percent);
            panel.style.top = new Length(60f, LengthUnit.Percent);
            panel.style.translate = new Translate(new Length(-50f, LengthUnit.Percent), 0f);
            panel.Add(prompt);
            panel.style.display = DisplayStyle.None;
            promptPanel = panel;
            root.Add(panel);

            skipHint = HudStyle.MakeLabel(HudStyle.KeyCaps(Loc.T("[Esc] pausa · pular abertura")), 15, new Color(0.8f, 0.85f, 0.9f));
            skipHint.style.position = Position.Absolute;
            skipHint.style.right = 24;
            skipHint.style.bottom = 16;
            root.Add(skipHint);
            caption = null;

            // Pausa da abertura: continuar, pular para a Terra ou sair do jogo.
            pausePanel = HudStyle.FullScreen();
            pausePanel.style.backgroundColor = new Color(0.01f, 0.015f, 0.02f, 0.72f);
            pausePanel.style.alignItems = Align.Center;
            pausePanel.style.justifyContent = Justify.Center;
            pausePanel.style.display = DisplayStyle.None;
            var box = HudStyle.Panel();
            box.style.width = 360;
            box.style.alignItems = Align.Stretch;
            HudStyle.Pad(box, 18, 20);
            box.Add(HudStyle.Tag("Sistema"));
            var title = HudStyle.MakeLabel(Loc.T("Pausado"), 34, HudStyle.Ink, bold: true);
            title.style.marginBottom = 12;
            box.Add(title);
            box.Add(HudStyle.BigButton("Continuar", () => SetPaused(false)));
            box.Add(HudStyle.BigButton("Pular abertura", Skip));
            box.Add(HudStyle.BigButton("Sair do jogo", Application.Quit));
            pausePanel.Add(box);
            root.Add(pausePanel);
        }

        VisualElement promptPanel;

        void SetFade(float alpha) => fade.style.opacity = alpha;

        IEnumerator Fade(float from, float to, float seconds)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, seconds);
                fade.style.opacity = Mathf.Lerp(from, to, t);
                yield return null;
            }
            fade.style.opacity = to;
        }

        void SetupAudio()
        {
            hum = gameObject.AddComponent<AudioSource>();
            hum.clip = Resources.Load<AudioClip>("Audio/Ambience/hum_loop");
            hum.loop = true;
            hum.volume = 0.16f * Settings.EffectsVolume;
            if (hum.clip != null) hum.Play();
            music = gameObject.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("Audio/Music/music_dead");
            music.loop = true;
            music.volume = 0.28f * Settings.MusicVolume;
            if (music.clip != null) music.Play();
            engine = gameObject.AddComponent<AudioSource>();
            engine.clip = ProceduralAudio.Rumble(4f);
            engine.loop = true;
            engine.volume = 0f;
        }

        void Update()
        {
            // Mostra a dica de interação da mira (armário, escotilha).
            if (interactor != null && interactor.enabled && interactor.Current != null)
            {
                prompt.text = HudStyle.KeyCaps(interactor.Current.Prompt);
                promptPanel.style.display = DisplayStyle.Flex;
            }
            else if (promptPanel != null) promptPanel.style.display = DisplayStyle.None;

            // O holograma nasce da lente do relógio: o quadro flutua logo acima dela, encara os olhos do jogador e dois
            // raios finos ligam a lente aos cantos de baixo.
            if (holo != null && holo.gameObject.activeSelf)
            {
                var lens = wrist.TransformPoint(LensLocal);
                var up = cameraPivot.up;
                var right = cameraPivot.right;
                float k = holo.localScale.x / HoloSize.x;
                holo.position = lens + up * (0.15f * k) + right * 0.01f;
                holo.rotation = Quaternion.LookRotation(holo.position - cameraPivot.position, up);
                if (beams != null)
                {
                    var half = right * (HoloSize.x * 0.5f * k);
                    var bottom = holo.position - up * (HoloSize.y * 0.5f * k);
                    beams[0].SetPosition(0, lens); beams[0].SetPosition(1, bottom - half);
                    beams[1].SetPosition(0, lens); beams[1].SetPosition(1, bottom + half);
                    foreach (var b in beams) b.enabled = k > 0.3f;
                }
            }
            else if (beams != null) foreach (var b in beams) b.enabled = false;

            // Esc abre/fecha a pausa (continuar, pular abertura, sair do jogo).
            if (!skipping && GameInput.Pause.WasPressedThisFrame()) SetPaused(!paused);

            if (shake > 0.0001f && boarded)
                cameraPivot.localPosition = new Vector3(0f, 1.6f, 0f) + UnityEngine.Random.insideUnitSphere * shake;
        }

        void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            if (paused) speedBeforePause = Time.timeScale;
            Time.timeScale = paused ? 0f : speedBeforePause;
            pausePanel.style.display = paused ? DisplayStyle.Flex : DisplayStyle.None;
            if (paused) pausePanel.Q<Button>()?.Focus();
            UIState.MenuOpen = paused;
            AudioListener.pause = paused;
        }

        void Skip()
        {
            if (skipping) return;
            skipping = true;
            SetPaused(false);
            Time.timeScale = 1f;
            StopAllCoroutines();
            GameSession.StartGame(fromIntro: true);
        }

        IEnumerator ShotAndQuit(string path, float wait)
        {
            yield return new WaitForSecondsRealtime(wait);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }
    }
}
