using System;
using System.Collections;
using System.Globalization;
using System.Linq;
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

            // -pc-quiet: sem banner/flash de estágio (o salto de Ti dispara vários e clareia as capturas).
            if (HasFlag(args, "-pc-quiet"))
                foreach (var feedback in FindObjectsByType<StageFeedback>(FindObjectsSortMode.None)) feedback.enabled = false;
            if (tm != null && TryNumber(args, "-pc-ti", out double ti))
                for (int i = 0; i < TerraformParameters.Count; i++) tm.Add((TerraformParameter)i, ti / TerraformParameters.Count);
            if (weather != null && HasFlag(args, "-pc-rain")) weather.ForceRain();
            if (player != null && TryNumber(args, "-pc-pitch", out double pitch)) player.SetPitch((float)pitch);
            if (player != null && TryNumber(args, "-pc-yaw", out double startYaw)) player.Teleport(player.transform.position, (float)startYaw);
            // -pc-showcase [-pc-showcase-radius <m>]: uma de cada construção em arco à frente.
            if (HasFlag(args, "-pc-showcase")) Showcase(TryNumber(args, "-pc-showcase-radius", out double radius) ? (float)radius : 16f);
            if (HasFlag(args, "-pc-unlock-all") && database != null)
            {
                foreach (var b in database.buildables) if (b != null) Blueprints.Unlock(b.blueprintId);
                foreach (var r in database.recipes) if (r != null) Blueprints.Unlock(r.blueprintId);
            }
            if (player != null && TryNumber(args, "-pc-wreck", out double wreckIndex)) GoToWreck((int)wreckIndex);
            if (TryNumber(args, "-pc-log", out double logIndex) && database != null && database.logs.Count > 0)
            {
                yield return null;
                var log = database.logs[Mathf.Clamp((int)logIndex, 0, database.logs.Count - 1)];
                StoryLogs.MarkRead(log.id);
                if (GameHud.Instance != null) GameHud.Instance.ShowLog(log);
            }
            // -pc-shipyard <n>: estaleiro 45 m à frente com os n primeiros módulos instalados.
            if (TryNumber(args, "-pc-shipyard", out double modulesToInstall))
            {
                var yard = SpawnShipyard((int)modulesToInstall);
                if (yard != null && HasFlag(args, "-pc-shipyard-menu"))
                {
                    yield return null;
                    if (GameHud.Instance != null) GameHud.Instance.OpenShipyard(yard);
                }
            }
            // -pc-launch <segundos>: decola e adianta a sequência.
            if (TryNumber(args, "-pc-launch", out double skip))
            {
                yield return null;
                var yard = FindFirstObjectByType<Shipyard>();
                if (yard != null)
                {
                    yard.Launch();
                    yard.SkipAhead((float)skip);
                }
            }
            if (HasFlag(args, "-pc-pause"))
            {
                yield return null;
                if (GameHud.Instance != null) GameHud.Instance.SetPaused(true);
            }
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
            if (HasFlag(args, "-pc-diag")) LogVegetation();
            ScreenCapture.CaptureScreenshot(shot);
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        // Diagnóstico: por camada de vegetação, quantos itens estão ativos e o tamanho/altura de uma amostra.
        static void LogVegetation()
        {
            var root = GameObject.Find("Vegetação");
            if (root == null)
            {
                Debug.Log("[Diag] sem objeto Vegetação");
                return;
            }
            var terrain = Terrain.activeTerrain;
            foreach (Transform layer in root.transform)
            {
                int active = 0;
                Transform sample = null;
                foreach (Transform item in layer)
                {
                    if (!item.gameObject.activeInHierarchy) continue;
                    active++;
                    if (sample == null) sample = item;
                }
                string detail = "";
                if (sample != null)
                {
                    var renderers = sample.GetComponentsInChildren<Renderer>();
                    var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(sample.position, Vector3.zero);
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    float ground = terrain != null ? terrain.SampleHeight(sample.position) : 0f;
                    var mats = string.Join(",", renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null)
                        .Select(m => m.shader != null ? m.shader.name : "null").Distinct());
                    detail = $" amostra pos={sample.position} chão={ground:0.0} escala={sample.localScale.x:0.00} " +
                             $"renderers={renderers.Length} ativos={renderers.Count(r => r.enabled)} bounds={b.size} shaders={mats}";
                }
                Debug.Log($"[Diag] {layer.name}: {active}/{layer.childCount} ativos{detail}");
            }
        }

        // Coloca o jogador a ~14 m do destroço n (ordem dos terminais de registro), olhando para ele.
        void GoToWreck(int index)
        {
            var terminals = FindObjectsByType<LogTerminal>(FindObjectsSortMode.None)
                .OrderBy(t => t.transform.parent != null ? t.transform.parent.name : t.name).ToArray();
            var terrain = Terrain.activeTerrain;
            if (terminals.Length == 0 || terrain == null) return;
            var site = terminals[Mathf.Clamp(index, 0, terminals.Length - 1)].transform.parent ?? terminals[0].transform;
            var target = site.position;
            var dir = new Vector3(target.x - 250f, 0f, target.z - 250f).normalized; // aproxima pelo lado do vale
            var pos = target - dir * 14f;
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 0.2f;
            var look = target - pos;
            player.Teleport(pos, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
        }

        Shipyard SpawnShipyard(int modules)
        {
            var def = database != null ? database.buildables.FirstOrDefault(b => b != null && b.id == "shipyard") : null;
            var terrain = Terrain.activeTerrain;
            if (def == null || player == null || terrain == null) return null;
            var dir = player.transform.forward;
            var pos = player.transform.position + dir * 45f;
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            var yard = PlacedObject.Spawn(def, pos, Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up));
            var shipyard = yard.GetComponent<Shipyard>();
            foreach (var m in shipyard.Modules.Take(modules)) ShipProgress.Install(m.id);
            return shipyard;
        }

        // Constrói uma de cada construção num arco à frente do jogador, viradas para ele.
        void Showcase(float radius)
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
                var pos = origin + dir * radius;
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
