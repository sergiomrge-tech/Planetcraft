using System.Text.RegularExpressions;
using Planetcraft.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Planetcraft.UI
{
    // Linguagem visual do visor do traje, compartilhada por HUD, título, opções e epílogo.
    // As fontes e os estados de botão ficam em Assets/_Project/UI/Hud.uss (importado pelo tema do PanelSettings).
    public static class HudStyle
    {
        // Tema "caderno de campo": papel pautado, tinta e lápis. `Paper = false` volta ao visor escuro.
        public static bool Paper = true;

        public static Color Ink => Paper ? new Color(0.16f, 0.12f, 0.07f) : new Color(0.93f, 0.91f, 0.86f);
        public static Color Accent => Paper ? new Color(0.72f, 0.3f, 0.1f) : new Color(1f, 0.72f, 0.28f);
        public static Color Signal => Paper ? new Color(0.08f, 0.4f, 0.36f) : new Color(0.45f, 0.9f, 0.8f);
        public static Color Warn => Paper ? new Color(0.75f, 0.15f, 0.1f) : new Color(1f, 0.36f, 0.24f);
        public static Color Muted => Paper ? new Color(0.42f, 0.35f, 0.25f) : new Color(0.6f, 0.62f, 0.6f);
        // Texto corrido (um tom abaixo da tinta) e fundo dos blocos.
        public static Color Body => Paper ? new Color(0.22f, 0.17f, 0.1f) : new Color(0.84f, 0.84f, 0.8f);
        public static Color PanelColor => Paper ? new Color(0.92f, 0.87f, 0.74f, 0.97f) : new Color(0.02f, 0.03f, 0.04f, 0.5f);
        public static Color Hairline => Paper ? new Color(0.45f, 0.36f, 0.2f, 0.45f) : new Color(1f, 1f, 1f, 0.09f);
        public static Color Empty => Paper ? new Color(0.25f, 0.18f, 0.08f, 0.16f) : new Color(1f, 1f, 1f, 0.08f);
        public static Color CardColor => Paper ? new Color(0.55f, 0.42f, 0.22f, 0.14f) : new Color(0f, 0f, 0f, 0.28f);
        // Texto sobre a cena (título, epílogo): sempre claro, fora do papel.
        public static readonly Color OnScene = new(0.95f, 0.93f, 0.88f);
        public static readonly Color OnSceneSoft = new(0.82f, 0.8f, 0.75f);
        public static readonly Color SceneAccent = new(1f, 0.72f, 0.28f);

        public const string RootClass = "rte-root";

        // Painel: nota de papel presa com fita (ou, no visor escuro, caixa com cantoneiras âmbar).
        // `tilt` gira o bloco alguns graus, como papéis colados sem muito cuidado.
        public static VisualElement Panel(bool brackets = true, float tilt = 0f)
        {
            var v = new VisualElement();
            v.style.backgroundColor = PanelColor;
            Border(v, 1f, Hairline);
            Pad(v, 12, 14);
            if (!Paper)
            {
                if (brackets) Brackets(v, Accent, 9f);
                return v;
            }
            // Sombra de papel: borda inferior/direita mais grossa e escura.
            v.style.borderBottomWidth = v.style.borderRightWidth = 2;
            v.style.borderBottomColor = v.style.borderRightColor = new Color(0.3f, 0.22f, 0.1f, 0.55f);
            v.style.borderTopLeftRadius = v.style.borderTopRightRadius = v.style.borderBottomLeftRadius = v.style.borderBottomRightRadius = 2;
            if (Mathf.Abs(tilt) > 0.01f) v.style.rotate = new Rotate(new Angle(tilt, AngleUnit.Degree));
            if (brackets)
            {
                var tape = new VisualElement { pickingMode = PickingMode.Ignore };
                tape.style.position = Position.Absolute;
                tape.style.top = -9;
                tape.style.left = new Length(50, LengthUnit.Percent);
                tape.style.marginLeft = -28;
                tape.style.width = 56;
                tape.style.height = 15;
                tape.style.backgroundColor = new Color(0.78f, 0.7f, 0.5f, 0.82f);
                tape.style.rotate = new Rotate(new Angle(-2.5f, AngleUnit.Degree));
                v.Add(tape);
            }
            return v;
        }

        public static void Brackets(VisualElement v, Color color, float size)
        {
            for (int i = 0; i < 4; i++)
            {
                bool top = i < 2, left = i % 2 == 0;
                var c = new VisualElement { pickingMode = PickingMode.Ignore };
                c.style.position = Position.Absolute;
                c.style.width = c.style.height = size;
                if (top) c.style.top = -1; else c.style.bottom = -1;
                if (left) c.style.left = -1; else c.style.right = -1;
                if (top) { c.style.borderTopWidth = 2; c.style.borderTopColor = color; }
                else { c.style.borderBottomWidth = 2; c.style.borderBottomColor = color; }
                if (left) { c.style.borderLeftWidth = 2; c.style.borderLeftColor = color; }
                else { c.style.borderRightWidth = 2; c.style.borderRightColor = color; }
                v.Add(c);
            }
        }

        // Rótulo pequeno em caixa alta e espaçado ("ÍNDICE DE TERRAFORMAÇÃO").
        public static Label Tag(string text, Color? color = null)
        {
            var l = MakeLabel(Paper ? Loc.T(text) : Loc.T(text).ToUpperInvariant(), Paper ? 14 : 11, color ?? Muted);
            l.AddToClassList("rte-tag");
            if (color != null) l.style.color = color.Value;
            return l;
        }

        public static Label MakeLabel(string text, int size, Color? color = null, bool bold = false, bool mono = false)
        {
            var l = new Label(Loc.T(text)) { pickingMode = PickingMode.Ignore, enableRichText = true };
            l.style.fontSize = size;
            l.style.color = color ?? Ink;
            if (bold) l.AddToClassList("rte-strong");
            if (mono) l.AddToClassList("rte-mono");
            l.style.marginLeft = l.style.marginRight = 0;
            l.style.paddingLeft = l.style.paddingRight = 0;
            return l;
        }

        public static Button MakeButton(string text, System.Action onClick)
        {
            var b = new Button(() => { Sfx.Click(); onClick(); }) { text = Loc.T(text).ToUpperInvariant() };
            b.AddToClassList("rte-button");
            if (Paper) b.text = Loc.T(text);
            b.style.fontSize = Paper ? 16 : 13;
            b.style.minWidth = 86;
            return b;
        }

        public static Button BigButton(string text, System.Action onClick)
        {
            var b = MakeButton(text, onClick);
            b.AddToClassList("rte-button--big");
            b.style.height = 40;
            b.style.fontSize = Paper ? 21 : 17;
            b.style.minWidth = 220;
            return b;
        }

        // Barra segmentada (blocos), mais legível de relance que uma barra lisa.
        public sealed class Segments
        {
            readonly VisualElement[] blocks;

            public VisualElement Root { get; }

            public Segments(int count, float height)
            {
                Root = Row();
                Root.style.flexGrow = 1;
                Root.style.height = height;
                blocks = new VisualElement[count];
                for (int i = 0; i < count; i++)
                {
                    var b = new VisualElement { pickingMode = PickingMode.Ignore };
                    b.style.flexGrow = 1;
                    b.style.marginRight = i < count - 1 ? 2 : 0;
                    blocks[i] = b;
                    Root.Add(b);
                }
            }

            public void Set(float value, Color color)
            {
                value = Mathf.Clamp01(value);
                float lit = value * blocks.Length;
                for (int i = 0; i < blocks.Length; i++)
                {
                    float f = Mathf.Clamp01(lit - i);
                    blocks[i].style.backgroundColor = f > 0f
                        ? new Color(color.r, color.g, color.b, Mathf.Lerp(0.35f, 1f, f))
                        : Empty;
                }
            }
        }

        static readonly Regex KeyPattern = new(@"\[([^\]]+)\]");

        // "[E] interagir" -> tecla destacada como um botão do teclado.
        public static string KeyCaps(string text) =>
            string.IsNullOrEmpty(text) ? text : KeyPattern.Replace(text, Paper
                ? "<mark=#2A1F1226><color=#B84D1A> $1 </color></mark>"
                : "<mark=#FFB84833><color=#FFB848> $1 </color></mark>");

        // Rótulos em caixa alta só no visor escuro; no caderno vale a letra normal.
        public static string Caps(string text) => Paper ? text : text.ToUpperInvariant();

        public static VisualElement FullScreen()
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = v.style.right = v.style.top = v.style.bottom = 0;
            return v;
        }

        public static VisualElement Row()
        {
            var v = new VisualElement();
            v.style.flexDirection = FlexDirection.Row;
            return v;
        }

        public static void Pad(VisualElement v, float vertical, float horizontal)
        {
            v.style.paddingTop = v.style.paddingBottom = vertical;
            v.style.paddingLeft = v.style.paddingRight = horizontal;
        }

        public static void Border(VisualElement v, float width, Color color)
        {
            v.style.borderTopWidth = v.style.borderBottomWidth = v.style.borderLeftWidth = v.style.borderRightWidth = width;
            v.style.borderTopColor = v.style.borderBottomColor = v.style.borderLeftColor = v.style.borderRightColor = color;
        }
    }
}
