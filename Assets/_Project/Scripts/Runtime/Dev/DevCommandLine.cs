using System;
using System.Collections;
using System.Globalization;
using Planetcraft.Core;
using Planetcraft.Player;
using Planetcraft.World;
using UnityEngine;

namespace Planetcraft.Dev
{
    // Argumentos de linha de comando para testes e capturas automáticas:
    //   -pc-ti <valor>          começa com esse Ti (dividido igualmente entre os 4 parâmetros)
    //   -pc-rain                força chuva
    //   -pc-pitch <graus>       inclina a câmera (negativo = olhar para cima)
    //   -pc-screenshot <png>    salva um screenshot depois de -pc-wait segundos (padrão 12) e fecha
    // Roda depois dos outros Start para que os eventos de estágio já tenham ouvintes.
    [DefaultExecutionOrder(100)]
    public class DevCommandLine : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;
        [SerializeField] WeatherController weather;

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            var tm = TerraformManager.Instance;

            if (tm != null && TryNumber(args, "-pc-ti", out double ti))
                for (int i = 0; i < TerraformParameters.Count; i++) tm.Add((TerraformParameter)i, ti / TerraformParameters.Count);
            if (weather != null && HasFlag(args, "-pc-rain")) weather.ForceRain();
            if (player != null && TryNumber(args, "-pc-pitch", out double pitch)) player.SetPitch((float)pitch);

            string shot = Value(args, "-pc-screenshot");
            if (shot == null) yield break;
            if (player != null)
            {
                player.LookEnabled = false;
                if (TryNumber(args, "-pc-yaw", out double yaw)) player.Teleport(player.transform.position, (float)yaw);
            }
            if (!TryNumber(args, "-pc-wait", out double wait)) wait = 12;
            yield return new WaitForSecondsRealtime((float)wait);
            if (player != null)
            {
                var cam = player.GetComponentInChildren<Camera>();
                Debug.Log($"[Dev] player={player.transform.position} camEuler={(cam != null ? cam.transform.eulerAngles : Vector3.zero)} ti={(tm != null ? tm.Ti : 0)}");
            }
            ScreenCapture.CaptureScreenshot(shot);
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        static string Value(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static bool HasFlag(string[] args, string name) => Array.IndexOf(args, name) >= 0;

        static bool TryNumber(string[] args, string name, out double value)
        {
            value = 0;
            string s = Value(args, name);
            return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
