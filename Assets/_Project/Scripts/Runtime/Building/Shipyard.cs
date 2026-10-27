using System.Collections;
using System.Linq;
using Planetcraft.Core;
using Planetcraft.Items;
using Planetcraft.Player;
using Planetcraft.Save;
using Planetcraft.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Planetcraft.Building
{
    // Estaleiro: monta a nave-mãe módulo a módulo (cada um aparece no modelo) e,
    // com tudo instalado e a atmosfera no estágio-meta, decola — é o final do jogo.
    [RequireComponent(typeof(PlacedObject))]
    public class Shipyard : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform ship;
        [SerializeField] ShipModule[] modules;
        [SerializeField] TerraformStage launchStage;
        [SerializeField] AudioSource rumble;

        bool launching;
        float elapsed;

        public ShipModule[] Modules => modules;
        public TerraformStage LaunchStage => launchStage;
        public bool AllInstalled => modules.All(m => m != null && ShipProgress.IsInstalled(m.id));

        public string Prompt => ShipProgress.Launched ? Loc.T("Estaleiro vazio — a nave-mãe voltou à estação") :
            launching ? "" : $"[E] {Loc.T("Estaleiro: montar a nave-mãe")}";

        void Start()
        {
            ShipProgress.Changed += Refresh;
            Refresh();
        }

        void OnDestroy() => ShipProgress.Changed -= Refresh;

        public void Interact(GameObject interactor)
        {
            if (!ShipProgress.Launched && !launching && GameHud.Instance != null) GameHud.Instance.OpenShipyard(this);
        }

        // Mostra só as partes já instaladas; depois da decolagem a nave some do estaleiro.
        public void Refresh()
        {
            if (ship == null || launching) return;
            ship.gameObject.SetActive(!ShipProgress.Launched && ShipProgress.Count > 0);
            foreach (var m in modules)
            {
                if (m == null) continue;
                var part = ship.Find(m.objectName);
                if (part != null) part.gameObject.SetActive(ShipProgress.IsInstalled(m.id));
            }
        }

        public string BlockReason(ShipModule m, Inventory inv, double ti)
        {
            if (ShipProgress.IsInstalled(m.id)) return Loc.T("instalado");
            int index = System.Array.IndexOf(modules, m);
            if (index > 0 && !ShipProgress.IsInstalled(modules[index - 1].id)) return $"{Loc.T("instale antes")}: {Loc.T(modules[index - 1].displayName)}";
            if (ti < m.requiredTi) return $"{Loc.T("requer")} {TiFormat.Format(m.requiredTi)}";
            if (!inv.Has(m.cost)) return Loc.T("recursos insuficientes");
            return null;
        }

        public bool TryInstall(ShipModule m, Inventory inv, double ti)
        {
            if (BlockReason(m, inv, ti) != null || !inv.TryRemoveAll(m.cost)) return false;
            ShipProgress.Install(m.id);
            Sfx.Build();
            return true;
        }

        public string LaunchBlockReason(double ti)
        {
            if (ShipProgress.Launched) return Loc.T("a nave já partiu");
            if (!AllInstalled) return Loc.T("monte todos os módulos");
            if (launchStage != null && ti < launchStage.requiredTi)
                return $"{Loc.T("atmosfera instável: requer o estágio")} {Loc.T(launchStage.displayName)} ({TiFormat.Format(launchStage.requiredTi)})";
            return null;
        }

        public void Launch()
        {
            if (launching || LaunchBlockReason(TerraformManager.Instance != null ? TerraformManager.Instance.Ti : 0) != null) return;
            StartCoroutine(LaunchSequence());
        }

        // Dev/capturas: pula para perto do fim da decolagem.
        public void SkipAhead(float seconds) => elapsed += seconds;

        IEnumerator LaunchSequence()
        {
            launching = true;
            var start = ship.position;
            var forward = ship.forward;
            if (rumble != null)
            {
                rumble.clip = ProceduralAudio.Rumble();
                rumble.loop = true;
                rumble.volume = 0.9f;
                rumble.Play();
            }

            // 1) sobe devagar saindo dos berços; 2) acelera para o céu inclinando a proa.
            const float lift = 7f, climb = 14f;
            elapsed = 0f;
            while (elapsed < lift + climb)
            {
                elapsed += Time.deltaTime;
                if (elapsed < lift)
                {
                    float k = elapsed / lift;
                    ship.position = start + Vector3.up * (Mathf.SmoothStep(0f, 1f, k) * 30f);
                }
                else
                {
                    float k = (elapsed - lift) / climb;
                    float distance = k * k * 2500f;
                    ship.position = start + Vector3.up * (30f + distance * 0.55f) + forward * (distance * 0.8f);
                    ship.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-Mathf.Lerp(0f, 25f, k), 0f, 0f);
                    if (rumble != null) rumble.volume = Mathf.Lerp(0.9f, 0f, k);
                }
                yield return null;
            }

            if (rumble != null) rumble.Stop();
            ship.gameObject.SetActive(false);
            ShipProgress.Launched = true;
            launching = false;
            // Salva com a nave já partida (o "Continuar explorando" do epílogo volta para este mundo) e segue
            // para a cena da chegada à Estação Horizonte.
            EpilogueDirector.Stats =
                $"Tempo de jogo: {PlayClock.Formatted}\n" +
                $"Índice de terraformação: {TiFormat.Format(TerraformManager.Instance.Ti)}\n" +
                $"Construções na Terra: {PlacedObject.All.Count}\n" +
                $"Registros encontrados: {StoryLogs.All.Count()} de 5";
            var save = FindAnyObjectByType<SaveSystem>();
            if (save != null) save.Save();
            SceneManager.LoadScene(Scenes.Epilogue);
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform shipTransform, ShipModule[] shipModules, TerraformStage stage, AudioSource audio)
        {
            ship = shipTransform;
            modules = shipModules;
            launchStage = stage;
            rumble = audio;
        }
#endif
    }
}
