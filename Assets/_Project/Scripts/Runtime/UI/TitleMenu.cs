using System;
using System.Collections;
using Planetcraft.Core;
using Planetcraft.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace Planetcraft.UI
{
    // Tela de título: Novo jogo, Continuar, Opções, Sair.
    // Argumentos -pc-* de jogo (capturas automáticas) pulam direto para a cena de jogo.
    [RequireComponent(typeof(UIDocument))]
    public class TitleMenu : MonoBehaviour
    {
        static readonly Color Accent = new(0.55f, 0.85f, 1f);

        VisualElement buttons, options;

        IEnumerator Start()
        {
            Time.timeScale = 1f;
            UIState.MenuOpen = true;

            string[] args = Environment.GetCommandLineArgs();
            if (Array.Exists(args, a => a.StartsWith("-pc-") && a != "-pc-screenshot" && a != "-pc-wait" && a != "-pc-title-options"))
            {
                GameSession.NewGame();
                yield break;
            }

            BuildUi(GetComponent<UIDocument>().rootVisualElement);
            if (Array.IndexOf(args, "-pc-title-options") >= 0) ShowOptions(true);

            // Captura automática da própria tela de título.
            int shotIndex = Array.IndexOf(args, "-pc-screenshot");
            if (shotIndex >= 0 && shotIndex + 1 < args.Length)
            {
                yield return new WaitForSecondsRealtime(6f);
                ScreenCapture.CaptureScreenshot(args[shotIndex + 1]);
                yield return new WaitForSecondsRealtime(1f);
                Application.Quit();
            }
        }

        void BuildUi(VisualElement root)
        {
            root.style.flexGrow = 1;
            var layout = new VisualElement();
            layout.style.position = Position.Absolute;
            layout.style.left = 120;
            layout.style.top = 0;
            layout.style.bottom = 0;
            layout.style.justifyContent = Justify.Center;
            root.Add(layout);

            var title = new Label("PLANETCRAFT");
            title.style.fontSize = 84;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 6;
            layout.Add(title);
            var subtitle = new Label("Torne um planeta morto habitável.");
            subtitle.style.fontSize = 22;
            subtitle.style.color = Accent;
            subtitle.style.marginBottom = 40;
            layout.Add(subtitle);

            buttons = new VisualElement();
            buttons.style.width = 320;
            layout.Add(buttons);
            buttons.Add(MenuButton("Novo jogo", GameSession.NewGame));
            var cont = MenuButton("Continuar", () => GameSession.ContinueGame());
            cont.SetEnabled(SaveSystem.HasSaveFile);
            buttons.Add(cont);
            buttons.Add(MenuButton("Opções", () => ShowOptions(true)));
            buttons.Add(MenuButton("Sair", Application.Quit));

            options = new VisualElement();
            options.style.display = DisplayStyle.None;
            layout.Add(options);

            var hint = new Label("WASD mover · E interagir · Tab menu · X desmontar · Esc pausa");
            hint.style.position = Position.Absolute;
            hint.style.left = 120;
            hint.style.bottom = 36;
            hint.style.fontSize = 14;
            hint.style.color = new Color(1f, 1f, 1f, 0.55f);
            root.Add(hint);
        }

        void ShowOptions(bool show)
        {
            buttons.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            options.Clear();
            if (show) options.Add(OptionsUi.Build(() => ShowOptions(false)));
            options.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static Button MenuButton(string text, Action onClick)
        {
            var b = new Button(() => { Sfx.Click(); onClick(); }) { text = text };
            b.style.height = 48;
            b.style.fontSize = 20;
            b.style.marginTop = b.style.marginBottom = 6;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.paddingLeft = 18;
            return b;
        }
    }
}
