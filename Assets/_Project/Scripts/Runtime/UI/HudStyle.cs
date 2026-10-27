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
        public static readonly Color Ink = new(0.93f, 0.91f, 0.86f);
        public static readonly Color Accent = new(1f, 0.72f, 0.28f);
        public static readonly Color Signal = new(0.45f, 0.9f, 0.8f);
        public static readonly Color Warn = new(1f, 0.36f, 0.24f);
        public static readonly Color Muted = new(0.6f, 0.62f, 0.6f);
        public static readonly Color PanelColor = new(0.02f, 0.03f, 0.04f, 0.5f);
        public static readonly Color Hairline = new(1f, 1f, 1f, 0.09f);

        public const string RootClass = "rte-root";

        // Painel do visor: fundo escuro translúcido, contorno fino e cantoneiras âmbar.
        public static VisualElement Panel(bool brackets = true)
        {
            var v = new VisualElement();
            v.style.backgroundColor = PanelColor;
            Border(v, 1f, Hairline);
            Pad(v, 12, 14);
            if (brackets) Brackets(v, Accent, 9f);
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
            var l = MakeLabel(Loc.T(text).ToUpperInvariant(), 11, color ?? Muted);
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
            b.style.fontSize = 13;
            b.style.minWidth = 86;
            return b;
        }

        public static Button BigButton(string text, System.Action onClick)
        {
            var b = MakeButton(text, onClick);
            b.AddToClassList("rte-button--big");
            b.style.height = 40;
            b.style.fontSize = 17;
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
                        : new Color(1f, 1f, 1f, 0.08f);
                }
            }
        }

        static readonly Regex KeyPattern = new(@"\[([^\]]+)\]");

        // "[E] interagir" -> tecla destacada como um botão do teclado.
        public static string KeyCaps(string text) =>
            string.IsNullOrEmpty(text) ? text : KeyPattern.Replace(text, "<mark=#FFB84833><color=#FFB848> $1 </color></mark>");

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
