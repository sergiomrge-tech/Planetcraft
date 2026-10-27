using System.Linq;
using Planetcraft.Building;
using Planetcraft.Core;
using Planetcraft.Power;

namespace Planetcraft.UI
{
    // Próximo objetivo para o painel do HUD, derivado do estado do jogo (nada é salvo): abrigo, energia, máquinas,
    // ruínas, estaleiro, módulos e decolagem. Guia o começo sem tutorial forçado.
    public static class Objectives
    {
        const int ShipModules = 5;

        static bool Has(string id) => PlacedObject.All.Any(p => p != null && p.Definition != null && p.Definition.id == id);

        static bool HasAny(params string[] ids) => ids.Any(Has);

        public static string Current()
        {
            var tm = TerraformManager.Instance;
            double ti = tm != null ? tm.Ti : 0;

            if (ShipProgress.Launched) return Loc.T("A nave-mãe voltou à Estação Horizonte. Explore a Terra viva.");
            if (!Has("base_module")) return Loc.T("Construa um Módulo de Base: ele recarrega seu oxigênio.");
            if (PowerGrid.Generation <= 0f) return Loc.T("Construa um Gerador Eólico para ter energia.");
            if (!HasAny("heater_t1", "heater_t2", "heater_t3", "heater_t4") || !HasAny("drill_t1", "drill_t2", "drill_t3") ||
                !HasAny("decon_t1", "decon_t2", "decon_t3"))
                return Loc.T("Construa um Aquecedor, uma Perfuratriz e um Filtro de Radiação para subir o Ti.");
            if (!PowerGrid.IsPowered) return Loc.T("Falta energia: construa mais geradores ou painéis solares.");
            if (!StoryLogs.All.Any()) return Loc.T("Procure as ruínas: os faróis marcam onde estão.");

            var yard = UnityEngine.Object.FindAnyObjectByType<Shipyard>();
            if (yard == null)
            {
                if (ti >= 2_500_000) return Loc.T("Construa o Estaleiro para montar a nave-mãe.");
            }
            else
            {
                if (ShipProgress.Count < ShipModules)
                    return $"{Loc.T("Instale os módulos da nave-mãe no estaleiro")} ({ShipProgress.Count}/{ShipModules}).";
                var launch = yard.LaunchStage;
                if (launch != null && ti < launch.requiredTi)
                    return $"{Loc.T("Leve a Terra ao estágio")} {Loc.T(launch.displayName)} ({TiFormat.Format(launch.requiredTi)}).";
                return Loc.T("Tudo pronto: decole a nave-mãe no estaleiro.");
            }

            var next = tm != null ? tm.NextStage : null;
            return next != null
                ? $"{Loc.T("Aumente o Ti até o estágio")} {Loc.T(next.displayName)} ({TiFormat.Format(next.requiredTi)})."
                : Loc.T("Construa o Estaleiro para montar a nave-mãe.");
        }
    }
}
