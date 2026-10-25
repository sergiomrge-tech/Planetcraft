using System;
using System.Collections;
using System.Globalization;
using Planetcraft.Core;
using Planetcraft.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace Planetcraft.UI
{
    // Epílogo: a nave-mãe chega da Terra, alinha com o eixo da Estação Horizonte e acopla; depois o painel final.
    // Argumentos de dev: -pc-epilogue <segundos a pular> (vindo do título), -pc-wait/-pc-screenshot para capturas.
    [RequireComponent(typeof(UIDocument))]
    public class EpilogueDirector : MonoBehaviour
    {
        [SerializeField] Transform station;
        [SerializeField] Transform ship;
        [SerializeField] Transform cameraRig;
        [Tooltip("Ponta do porto de acoplagem, em coordenadas locais da estação.")]
        [SerializeField] Vector3 portLocal = new(0f, 0f, 58f);
        [Tooltip("Distância do pivô da nave até a proa (a proa encosta no porto).")]
        [SerializeField] float shipNose = 23f;
        [SerializeField] Vector3 approachStart = new(420f, 160f, 1300f);
        [SerializeField] float approachSeconds = 18f;
        [SerializeField] float stationSpinDegreesPerSecond = 1.5f;

        // Preenchido pelo estaleiro ao decolar; vazio quando a cena é aberta direto (dev).
        public static string Stats = "";

        VisualElement panel;
        float elapsed;
        bool docked;

        IEnumerator Start()
        {
            Time.timeScale = 1f;
            UIState.MenuOpen = true;
            BuildUi(GetComponent<UIDocument>().rootVisualElement);
            Update();
            cameraRig.rotation = Quaternion.LookRotation(Vector3.Lerp(ship.position, station.position, 0.85f) - cameraRig.position);

            string[] args = Environment.GetCommandLineArgs();
            if (TryNumber(args, "-pc-epilogue", out float skip)) elapsed = skip;

            int shot = Array.IndexOf(args, "-pc-screenshot");
            if (shot < 0 || shot + 1 >= args.Length) yield break;
            if (!TryNumber(args, "-pc-wait", out float wait)) wait = 8f;
            yield return new WaitForSecondsRealtime(wait);
            ScreenCapture.CaptureScreenshot(args[shot + 1]);
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            station.Rotate(0f, 0f, stationSpinDegreesPerSecond * Time.deltaTime, Space.Self);

            // O porto fica no eixo de rotação: não se move com o giro do anel.
            var port = station.TransformPoint(portLocal);
            var axis = station.forward;
            var dockPos = port + axis * shipNose;
            var lineUp = port + axis * 160f;
            var start = station.position + approachStart;

            // Curva de Bézier: vem de longe, entra no eixo e desacelera até encostar.
            float t = Mathf.Clamp01(elapsed / approachSeconds);
            float k = 1f - Mathf.Pow(1f - t, 2.4f);
            var a = Vector3.Lerp(start, lineUp, k);
            var b = Vector3.Lerp(lineUp, dockPos, k);
            var pos = Vector3.Lerp(a, b, k);
            var ahead = (b - a).sqrMagnitude > 0.01f ? (b - a).normalized : -axis;
            ship.position = pos;
            ship.rotation = Quaternion.Slerp(Quaternion.LookRotation(ahead, Vector3.up),
                Quaternion.LookRotation(-axis, station.up), Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
            // A nave gira junto com a estação depois de acoplada.
            if (t >= 1f) ship.rotation = Quaternion.LookRotation(-axis, station.up);

            // Câmera fixa ao lado do eixo, acompanhando a nave com suavidade.
            // Enquadra nave e estação juntas: o foco começa perto da estação e acompanha a nave conforme ela chega.
            var focus = Vector3.Lerp(Vector3.Lerp(pos, station.position, 0.85f), Vector3.Lerp(pos, port, 0.35f), k);
            var look = Quaternion.LookRotation(focus - cameraRig.position, Vector3.up);
            cameraRig.rotation = Quaternion.Slerp(cameraRig.rotation, look, 1f - Mathf.Exp(-2f * Time.deltaTime));

            if (!docked && t >= 1f)
            {
                docked = true;
                Sfx.Build();
                StartCoroutine(ShowPanel());
            }
        }

        IEnumerator ShowPanel()
        {
            yield return new WaitForSeconds(2.5f);
            panel.style.display = DisplayStyle.Flex;
            for (float f = 0f; f < 1f; f += Time.deltaTime / 1.5f)
            {
                panel.style.opacity = f;
                yield return null;
            }
            panel.style.opacity = 1f;
        }

        void BuildUi(VisualElement root)
        {
            root.style.flexGrow = 1;
            root.AddToClassList(HudStyle.RootClass);
            panel = new VisualElement { pickingMode = PickingMode.Position };
            panel.style.position = Position.Absolute;
            panel.style.left = panel.style.right = panel.style.top = panel.style.bottom = 0;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.FlexEnd;
            panel.style.paddingBottom = 70;

            var sheet = HudStyle.Panel();
            sheet.style.width = 720;
            sheet.style.alignItems = Align.Center;
            sheet.Add(HudStyle.MakeLabel("MISSÃO CUMPRIDA", 14, new Color(0.45f, 0.95f, 0.75f), bold: true));
            var title = HudStyle.MakeLabel("Dever cumprido", 40, Color.white, bold: true);
            title.style.marginTop = 4;
            sheet.Add(title);
            var text = HudStyle.MakeLabel(
                "A nave-mãe acopla na Estação Horizonte levando a notícia\n" +
                "que a humanidade esperou por um milhão de anos: a Terra respira de novo.", 16,
                new Color(0.88f, 0.9f, 0.92f));
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 10;
            text.style.marginBottom = 14;
            sheet.Add(text);
            if (!string.IsNullOrEmpty(Stats))
            {
                var stats = HudStyle.MakeLabel(Stats, 15, HudStyle.Muted);
                stats.style.unityTextAlign = TextAnchor.MiddleCenter;
                stats.style.marginBottom = 16;
                sheet.Add(stats);
            }
            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.Add(HudStyle.BigButton("Continuar explorando", () =>
            {
                if (!GameSession.ContinueGame()) GameSession.ToTitle();
            }));
            buttons.Add(HudStyle.BigButton("Menu principal", GameSession.ToTitle));
            sheet.Add(buttons);
            panel.Add(sheet);
            panel.style.display = DisplayStyle.None;
            root.Add(panel);
        }

        static bool TryNumber(string[] args, string name, out float value)
        {
            value = 0f;
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length &&
                   float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform stationRoot, Transform shipRoot, Transform rig)
        {
            station = stationRoot;
            ship = shipRoot;
            cameraRig = rig;
        }
#endif
    }
}
