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

        VisualElement buttons, options;

        IEnumerator Start()
        {
            Time.timeScale = 1f;
            UIState.MenuOpen = true;

            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-pc-epilogue") >= 0)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(Scenes.Epilogue);
                yield break;
            }
            if (Array.Exists(args, a => a.StartsWith("-pc-") && a != "-pc-screenshot" && a != "-pc-wait" && a != "-pc-title-options" && a != "-pc-title-credits" && a != "-pc-lang"))
            {
                GameSession.NewGame();
                yield break;
            }

            BuildUi(GetComponent<UIDocument>().rootVisualElement);
            // Foco inicial no primeiro botão: com controle, o direcional navega e A confirma.
            buttons.Q<Button>()?.Focus();
            if (Array.IndexOf(args, "-pc-title-options") >= 0) ShowOptions(true);
            if (Array.IndexOf(args, "-pc-title-credits") >= 0) ShowCredits();

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
            root.AddToClassList(HudStyle.RootClass);
            var layout = new VisualElement();
            layout.style.position = Position.Absolute;
            layout.style.left = 120;
            layout.style.top = 0;
            layout.style.bottom = 0;
            layout.style.justifyContent = Justify.Center;
            root.Add(layout);

            layout.Add(HudStyle.Tag("Estação Horizonte  ·  Missão de retorno", HudStyle.SceneAccent));
            var title = HudStyle.MakeLabel("RETURN TO EARTH", 96, HudStyle.OnScene, bold: true);
            title.style.letterSpacing = HudStyle.Paper ? 2 : 10;
            title.style.marginTop = -6;
            layout.Add(title);
            var subtitle = HudStyle.MakeLabel("Um milhão de anos depois, a humanidade volta para casa.", 22,
                HudStyle.OnSceneSoft);
            subtitle.style.marginBottom = 44;
            subtitle.style.paddingLeft = 12;
            subtitle.style.borderLeftWidth = 2;
            subtitle.style.borderLeftColor = HudStyle.SceneAccent;
            layout.Add(subtitle);

            buttons = HudStyle.Panel(true, -1f);
            buttons.style.width = 340;
            layout.Add(buttons);
            buttons.Add(MenuButton("Novo jogo", GameSession.NewGame));
            var cont = MenuButton("Continuar", () => GameSession.ContinueGame());
            cont.SetEnabled(SaveSystem.HasSaveFile);
            buttons.Add(cont);
            buttons.Add(MenuButton("Opções", () => ShowOptions(true)));
            buttons.Add(MenuButton("Créditos", ShowCredits));
            buttons.Add(MenuButton("Sair", Application.Quit));

            options = new VisualElement();
            options.style.display = DisplayStyle.None;
            layout.Add(options);

            var hint = HudStyle.MakeLabel(HudStyle.KeyCaps(string.Join("   ·   ", $"[WASD] {Loc.T("mover")}",
                    $"{GameInput.Hint(GameInput.Interact)} {Loc.T("interagir")}", $"{GameInput.Hint(GameInput.Menu)} {Loc.T("menu")}",
                    $"{GameInput.Hint(GameInput.Deconstruct)} {Loc.T("desmontar")}", $"{GameInput.Hint(GameInput.Pause)} {Loc.T("pausa")}")),
                14, HudStyle.OnSceneSoft);
            hint.style.position = Position.Absolute;
            hint.style.left = 120;
            hint.style.bottom = 36;
            root.Add(hint);
        }

        // Créditos dos assets de terceiros (CREDITS.md copiado para Resources pelo gerador): exigido pelas licenças CC BY.
        void ShowCredits()
        {
            buttons.style.display = DisplayStyle.None;
            options.Clear();
            var panel = HudStyle.Panel();
            panel.style.width = 760;
            panel.style.maxHeight = 520;
            HudStyle.Pad(panel, 18, 22);
            panel.Add(HudStyle.Tag("Sistema"));
            var title = HudStyle.MakeLabel(HudStyle.Paper ? "Créditos" : "CRÉDITOS", 30, HudStyle.Ink, bold: true);
            title.style.marginBottom = 10;
            panel.Add(title);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            var text = Resources.Load<TextAsset>("credits");
            var body = HudStyle.MakeLabel(text != null ? CreditsToRichText(text.text) : "-", 14, HudStyle.Body);
            body.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(body);
            panel.Add(scroll);
            var back = HudStyle.BigButton("Voltar", () =>
            {
                options.Clear();
                options.style.display = DisplayStyle.None;
                buttons.style.display = DisplayStyle.Flex;
                buttons.Q<Button>()?.Focus();
            });
            back.style.marginTop = 12;
            back.style.marginLeft = 0;
            panel.Add(back);
            options.Add(panel);
            options.style.display = DisplayStyle.Flex;
            back.Focus();
        }

        // Markdown simples do CREDITS.md -> texto rico: títulos em destaque, links viram só o nome.
        static string CreditsToRichText(string md)
        {
            var lines = md.Replace("\r", "").Split('\n');
            var sb = new System.Text.StringBuilder();
            foreach (var raw in lines)
            {
                string line = System.Text.RegularExpressions.Regex.Replace(raw, @"\[([^\]]+)\]\([^)]+\)", "$1").Replace("`", "");
                if (line.StartsWith("# ")) continue;
                if (line.StartsWith("## ")) sb.Append("\n<b><color=#FFB848>").Append(line.Substring(3)).Append("</color></b>\n");
                else sb.Append(line.StartsWith("- ") ? "  ·  " + line.Substring(2) : line).Append('\n');
            }
            return sb.ToString().Trim();
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
            var b = HudStyle.BigButton(text, onClick);
            b.style.height = 50;
            b.style.fontSize = 21;
            b.style.marginLeft = 0;
            return b;
        }
    }
}
