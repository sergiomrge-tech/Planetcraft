using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Planetcraft.Intro
{
    // Interface do comunicador de pulso: desenhada com UI Toolkit numa RenderTexture e projetada num quadro flutuante
    // sobre o relógio (holograma). Mostra quem fala, a mensagem (digitada) e o objetivo atual.
    [RequireComponent(typeof(UIDocument))]
    public class HoloComm : MonoBehaviour
    {
        static readonly Color Cyan = new(0.45f, 0.95f, 1f);
        static readonly Color CyanDim = new(0.25f, 0.6f, 0.75f);
        static readonly Color Amber = new(1f, 0.78f, 0.35f);

        Label speaker, message, objective, clock;
        VisualElement root, scan;
        Coroutine typing;
        float flicker;

        public bool Typing { get; private set; }

        void OnEnable()
        {
            if (root != null) return;
            root = GetComponent<UIDocument>().rootVisualElement;
            Build();
        }

        void Build()
        {
            root.style.flexGrow = 1;
            root.style.paddingLeft = root.style.paddingRight = 26;
            root.style.paddingTop = 20;
            root.style.paddingBottom = 18;
            // Moldura do holograma: contorno fino ciano e fundo translúcido.
            root.style.backgroundColor = new Color(0.04f, 0.18f, 0.24f, 0.55f);
            SetBorder(root, 2f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f));
            var font = Resources.Load<Font>("Fonts/Rajdhani-SemiBold");

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            var tag = Make("CENTRAL HORIZONTE · COM", 22, Cyan, font);
            clock = Make("", 22, CyanDim, font);
            header.Add(tag);
            header.Add(clock);
            root.Add(header);
            var rule = new VisualElement { style = { height = 2, marginTop = 8, marginBottom = 12, backgroundColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f) } };
            root.Add(rule);

            speaker = Make("", 26, Amber, font);
            root.Add(speaker);
            message = Make("", 28, new Color(0.88f, 1f, 1f), font);
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.flexGrow = 1;
            message.style.marginTop = 6;
            root.Add(message);

            var bottom = new VisualElement { style = { borderTopWidth = 2, borderTopColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f), paddingTop = 10 } };
            bottom.Add(Make("OBJETIVO", 18, CyanDim, font));
            objective = Make("", 26, Cyan, font);
            objective.style.whiteSpace = WhiteSpace.Normal;
            bottom.Add(objective);
            root.Add(bottom);

            // Linhas de varredura que descem devagar: a "cara" de holograma.
            scan = new VisualElement { pickingMode = PickingMode.Ignore };
            scan.style.position = Position.Absolute;
            scan.style.left = scan.style.right = 0;
            scan.style.height = 6;
            scan.style.backgroundColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.18f);
            root.Add(scan);
        }

        static Label Make(string text, int size, Color color, Font font)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            if (font != null) l.style.unityFont = font;
            l.style.unityFontDefinition = font != null ? new StyleFontDefinition(font) : StyleKeyword.Null;
            return l;
        }

        static void SetBorder(VisualElement v, float w, Color c)
        {
            v.style.borderTopWidth = v.style.borderBottomWidth = v.style.borderLeftWidth = v.style.borderRightWidth = w;
            v.style.borderTopColor = v.style.borderBottomColor = v.style.borderLeftColor = v.style.borderRightColor = c;
        }

        float debugAt = 3f;

        void Update()
        {
            if (debugAt > 0f && (debugAt -= Time.unscaledDeltaTime) <= 0f)
            {
                var settings = GetComponent<UIDocument>().panelSettings;
                Debug.Log($"[Holo] layout={root.layout} painel={(root.panel != null)} rt={(settings != null && settings.targetTexture != null ? settings.targetTexture.name : "nenhuma")}");
            }
            // Varredura e leve tremida de brilho.
            float t = Time.unscaledTime;
            scan.style.top = new Length(Mathf.Repeat(t * 0.18f, 1f) * 100f, LengthUnit.Percent);
            flicker = 0.92f + 0.08f * Mathf.Sin(t * 37f) * Mathf.Sin(t * 5.3f);
            root.style.opacity = flicker;
            var span = System.TimeSpan.FromSeconds(Mathf.Floor(t));
            clock.text = $"{span.Minutes:00}:{span.Seconds:00}";
        }

        public void SetObjective(string text) => objective.text = text ?? "";

        public void Say(string who, string text, float charsPerSecond = 38f)
        {
            if (typing != null) StopCoroutine(typing);
            speaker.text = who;
            typing = StartCoroutine(Type(text, charsPerSecond));
        }

        IEnumerator Type(string text, float cps)
        {
            Typing = true;
            message.text = "";
            float shown = 0f;
            while (shown < text.Length)
            {
                shown += cps * Time.unscaledDeltaTime;
                message.text = text.Substring(0, Mathf.Min(text.Length, Mathf.FloorToInt(shown)));
                yield return null;
            }
            message.text = text;
            Typing = false;
        }

        // Tempo que uma fala leva para ser lida: digitação mais uma pausa proporcional ao tamanho.
        public static float ReadTime(string text, float cps = 38f) => text.Length / cps + 1.4f + text.Length * 0.022f;
    }
}
