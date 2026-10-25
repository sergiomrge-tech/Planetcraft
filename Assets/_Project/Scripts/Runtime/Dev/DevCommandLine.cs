using System;
using System.Collections;
using System.Globalization;
using Planetcraft.Building;
using Planetcraft.Core;
using Planetcraft.Data;
using Planetcraft.UI;
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
        [SerializeField] GameDatabase database;

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            var tm = TerraformManager.Instance;
            if (player != null && Value(args, "-pc-screenshot") != null) player.LookEnabled = false;

            if (tm != null && TryNumber(args, "-pc-ti", out double ti))
                for (int i = 0; i < TerraformParameters.Count; i++) tm.Add((TerraformParameter)i, ti / TerraformParameters.Count);
            if (weather != null && HasFlag(args, "-pc-rain")) weather.ForceRain();
            if (player != null && TryNumber(args, "-pc-pitch", out double pitch)) player.SetPitch((float)pitch);
            if (player != null && TryNumber(args, "-pc-yaw", out double startYaw)) player.Teleport(player.transform.position, (float)startYaw);
            if (HasFlag(args, "-pc-showcase")) Showcase();
            if (HasFlag(args, "-pc-menu"))
            {
                yield return null; // deixa o HUD montar a UI
                if (GameHud.Instance != null) GameHud.Instance.ToggleMenu(true);
            }

            string shot = Value(args, "-pc-screenshot");
            if (shot == null) yield break;
            if (player != null) player.LookEnabled = false;
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

        // Constrói uma de cada construção num arco à frente do jogador, viradas para ele.
        void Showcase()
        {
            var terrain = Terrain.activeTerrain;
            if (database == null || player == null || terrain == null) return;
            var origin = player.transform.position;
            var forward = player.transform.forward;
            int n = database.buildables.Count;
            for (int i = 0; i < n; i++)
            {
                var def = database.buildables[i];
                if (def == null || def.prefab == null) continue;
                float angle = Mathf.Lerp(-55f, 55f, n == 1 ? 0.5f : i / (float)(n - 1));
                var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                var pos = origin + dir * 16f;
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
                PlacedObject.Spawn(def, pos, Quaternion.LookRotation(-dir, Vector3.up));
            }
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
