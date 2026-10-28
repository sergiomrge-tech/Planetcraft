using System;
using System.Linq;
using Planetcraft.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Planetcraft.UI
{
    // Painel de opções compartilhado pela tela de título e pelo menu de pausa: geral (som, câmera, vídeo, idioma)
    // e a tela de controles com remapeamento das teclas.
    public static class OptionsUi
    {
        public static VisualElement Build(Action onBack)
        {
            var panel = HudStyle.Panel();
            panel.style.width = 560;
            HudStyle.Pad(panel, 18, 22);
            var content = new VisualElement();
            panel.Add(content);
            ShowGeneral(content, onBack);
            return panel;
        }

        static void Header(VisualElement content, string title)
        {
            content.Add(HudStyle.Tag("Sistema"));
            var t = HudStyle.MakeLabel(title, 30, HudStyle.Ink, bold: true);
            t.style.marginBottom = 12;
            content.Add(t);
        }

        static void ShowGeneral(VisualElement content, Action onBack)
        {
            content.Clear();
            Header(content, "OPÇÕES");

            var volume = new Slider(Loc.T("Volume"), 0f, 1f) { value = Settings.MasterVolume, showInputField = false };
            volume.RegisterValueChangedCallback(e => Settings.MasterVolume = e.newValue);
            content.Add(Style(volume));

            var music = new Slider(Loc.T("Volume da música"), 0f, 1f) { value = Settings.MusicVolume, showInputField = false };
            music.RegisterValueChangedCallback(e => Settings.MusicVolume = e.newValue);
            content.Add(Style(music));

            var effects = new Slider(Loc.T("Volume dos efeitos"), 0f, 1f) { value = Settings.EffectsVolume, showInputField = false };
            effects.RegisterValueChangedCallback(e => Settings.EffectsVolume = e.newValue);
            content.Add(Style(effects));

            var sensitivity = new Slider(Loc.T("Sensibilidade do mouse"), 0.03f, 0.4f) { value = Settings.MouseSensitivity };
            sensitivity.RegisterValueChangedCallback(e => Settings.MouseSensitivity = e.newValue);
            content.Add(Style(sensitivity));

            var fov = new SliderInt(Loc.T("Campo de visão"), 55, 100) { value = Mathf.RoundToInt(Settings.FieldOfView), showInputField = true };
            fov.RegisterValueChangedCallback(e => Settings.FieldOfView = e.newValue);
            content.Add(Style(fov));

            var invert = new Toggle(Loc.T("Inverter eixo Y")) { value = Settings.InvertY };
            invert.RegisterValueChangedCallback(e => Settings.InvertY = e.newValue);
            content.Add(Style(invert));

            var names = QualitySettings.names.ToList();
            var quality = new DropdownField(Loc.T("Qualidade gráfica"), names, Mathf.Clamp(Settings.QualityLevel, 0, names.Count - 1));
            quality.RegisterValueChangedCallback(_ => Settings.QualityLevel = quality.index);
            content.Add(Style(quality));

            var fullscreen = new Toggle(Loc.T("Tela cheia")) { value = Settings.Fullscreen };
            fullscreen.RegisterValueChangedCallback(e => Settings.Fullscreen = e.newValue);
            content.Add(Style(fullscreen));

            // Resoluções distintas (maior taxa de atualização de cada uma), da maior para a menor.
            var resolutions = Screen.resolutions.GroupBy(r => (r.width, r.height))
                .Select(g => g.OrderByDescending(r => r.refreshRateRatio.value).First())
                .OrderByDescending(r => r.width * r.height).ToList();
            if (resolutions.Count > 0)
            {
                var labels = resolutions.Select(r => $"{r.width} x {r.height}").ToList();
                int current = Mathf.Max(0, resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height));
                var resolution = new DropdownField(Loc.T("Resolução"), labels, current);
                resolution.RegisterValueChangedCallback(_ => Settings.SetResolution(resolutions[resolution.index]));
                content.Add(Style(resolution));
            }

            var threatNames = new[] { "Nenhuma", "Baixa", "Normal", "Alta" }.Select(Loc.T).ToList();
            var threat = new DropdownField(Loc.T("Ameaças"), threatNames, Settings.Threat);
            threat.RegisterValueChangedCallback(_ => Settings.Threat = threat.index);
            content.Add(Style(threat));

            var vsync = new Toggle(Loc.T("Sincronização vertical")) { value = Settings.VSync };
            vsync.RegisterValueChangedCallback(e => Settings.VSync = e.newValue);
            content.Add(Style(vsync));

            var languages = new[] { "Português", "English" }.ToList();
            var language = new DropdownField(Loc.T("Idioma"), languages, Settings.Language == "en" ? 1 : 0);
            var languageNote = HudStyle.MakeLabel("O idioma muda ao voltar ao menu principal.", 13, HudStyle.Muted);
            languageNote.style.display = DisplayStyle.None;
            language.RegisterValueChangedCallback(_ =>
            {
                Settings.Language = language.index == 1 ? "en" : "pt";
                languageNote.style.display = DisplayStyle.Flex;
            });
            content.Add(Style(language));
            content.Add(languageNote);

            var controls = HudStyle.BigButton("Controles", () => ShowControls(content, onBack));
            controls.style.marginTop = 14;
            controls.style.marginLeft = 0;
            content.Add(controls);
            var back = HudStyle.BigButton("Voltar", onBack);
            back.style.marginLeft = 0;
            content.Add(back);
        }

        // Lista das ações remapeáveis com a tecla atual; clicar espera a nova tecla. Mostra também o botão do controle.
        static void ShowControls(VisualElement content, Action onBack)
        {
            content.Clear();
            Header(content, "CONTROLES");
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.maxHeight = 430;
            content.Add(list);
            foreach (var (label, action) in GameInput.Rebindable)
            {
                var row = HudStyle.Row();
                row.style.alignItems = Align.Center;
                row.style.height = 32;
                var name = HudStyle.MakeLabel(label, 16, HudStyle.Ink);
                name.style.flexGrow = 1;
                row.Add(name);
                var pad = HudStyle.MakeLabel(GameInput.Display(action, gamepad: true), 13, HudStyle.Muted, mono: true);
                pad.style.width = 110;
                pad.style.unityTextAlign = TextAnchor.MiddleRight;
                pad.style.marginRight = 10;
                row.Add(pad);
                Button key = null;
                key = HudStyle.MakeButton(GameInput.Display(action), () =>
                {
                    key.text = HudStyle.Caps(Loc.T("Pressione uma tecla (Esc cancela)"));
                    GameInput.StartRebind(action, () => key.text = GameInput.Display(action));
                });
                key.style.width = 150;
                row.Add(key);
                list.Add(row);
            }
            var reset = HudStyle.BigButton("Restaurar padrão", () =>
            {
                GameInput.ResetBindings();
                ShowControls(content, onBack);
            });
            reset.style.marginTop = 12;
            reset.style.marginLeft = 0;
            content.Add(reset);
            var back = HudStyle.BigButton("Voltar", () => ShowGeneral(content, onBack));
            back.style.marginLeft = 0;
            content.Add(back);
        }

        // O tema padrão usa texto escuro nos rótulos; aqui o fundo é escuro.
        static VisualElement Style<T>(BaseField<T> field)
        {
            field.style.marginTop = field.style.marginBottom = 6;
            field.labelElement.style.color = HudStyle.Ink;
            field.labelElement.style.fontSize = 16;
            field.labelElement.style.minWidth = 200;
            return field;
        }
    }
}
