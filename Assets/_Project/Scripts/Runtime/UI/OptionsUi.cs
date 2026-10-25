using System;
using System.Linq;
using Planetcraft.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Planetcraft.UI
{
    // Painel de opções compartilhado pela tela de título e pelo menu de pausa.
    public static class OptionsUi
    {
        public static VisualElement Build(Action onBack)
        {
            var panel = HudStyle.Panel();
            panel.style.width = 540;
            HudStyle.Pad(panel, 18, 22);
            panel.Add(HudStyle.Tag("Sistema"));
            var title = HudStyle.MakeLabel("OPÇÕES", 30, HudStyle.Ink, bold: true);
            title.style.marginBottom = 12;
            panel.Add(title);

            var volume = new Slider("Volume", 0f, 1f) { value = Settings.MasterVolume, showInputField = false };
            volume.RegisterValueChangedCallback(e => Settings.MasterVolume = e.newValue);
            panel.Add(Style(volume));

            var sensitivity = new Slider("Sensibilidade do mouse", 0.03f, 0.4f) { value = Settings.MouseSensitivity };
            sensitivity.RegisterValueChangedCallback(e => Settings.MouseSensitivity = e.newValue);
            panel.Add(Style(sensitivity));

            var fov = new SliderInt("Campo de visão", 55, 100) { value = Mathf.RoundToInt(Settings.FieldOfView), showInputField = true };
            fov.RegisterValueChangedCallback(e => Settings.FieldOfView = e.newValue);
            panel.Add(Style(fov));

            var invert = new Toggle("Inverter eixo Y") { value = Settings.InvertY };
            invert.RegisterValueChangedCallback(e => Settings.InvertY = e.newValue);
            panel.Add(Style(invert));

            var names = QualitySettings.names.ToList();
            var quality = new DropdownField("Qualidade gráfica", names, Mathf.Clamp(Settings.QualityLevel, 0, names.Count - 1));
            quality.RegisterValueChangedCallback(_ => Settings.QualityLevel = quality.index);
            panel.Add(Style(quality));

            var fullscreen = new Toggle("Tela cheia") { value = Settings.Fullscreen };
            fullscreen.RegisterValueChangedCallback(e => Settings.Fullscreen = e.newValue);
            panel.Add(Style(fullscreen));

            var back = HudStyle.BigButton("Voltar", onBack);
            back.style.marginTop = 16;
            panel.Add(back);
            return panel;
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
