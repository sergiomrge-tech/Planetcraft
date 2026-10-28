using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Core
{
    // Textos da interface em português (o texto-fonte é a chave) e inglês. Textos sem tradução aparecem como estão.
    // Os dados do jogo (itens, estágios, registros) ainda são só em português.
    public static class Loc
    {
        // -pc-lang <pt|en>: força o idioma só nesta execução (capturas de teste), sem gravar nas opções.
        static readonly string Forced = ReadForced();

        public static bool English => (Forced ?? Settings.Language) == "en";

        static string ReadForced()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-pc-lang");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public static string T(string pt) => Translate(pt, English);

        public static string Translate(string pt, bool english)
        {
            if (string.IsNullOrEmpty(pt) || !english) return pt;
            return En.TryGetValue(pt, out var en) ? en : pt;
        }

        public static bool HasEnglish(string pt) => !string.IsNullOrEmpty(pt) && En.ContainsKey(pt);

        public static string SystemDefault() =>
            Application.systemLanguage == SystemLanguage.Portuguese ? "pt" : "en";

        static readonly Dictionary<string, string> En = WithData(new()
        {
            // HUD
            ["Índice de terraformação"] = "Terraformation index",
            ["Estágio"] = "Stage",
            ["Próximo"] = "Next",
            ["Todos os estágios alcançados"] = "All stages reached",
            ["SEM ENERGIA"] = "NO POWER",
            ["Energia"] = "Power",
            ["Suporte de vida"] = "Life support",
            ["Objetivo"] = "Objective",
            ["A nave-mãe voltou à Estação Horizonte. Explore a Terra viva."] = "The mothership is back at Horizon Station. Explore the living Earth.",
            ["Construa um Módulo de Base: ele recarrega seu oxigênio."] = "Build a Base Module: it refills your oxygen.",
            ["Construa um Gerador Eólico para ter energia."] = "Build a Wind Turbine for power.",
            ["Construa um Aquecedor, uma Perfuratriz e um Filtro de Radiação para subir o Ti."] = "Build a Heater, a Drill and a Radiation Filter to raise the Ti.",
            ["Falta energia: construa mais geradores ou painéis solares."] = "Not enough power: build more generators or solar panels.",
            ["Procure as ruínas: os faróis marcam onde estão."] = "Search the ruins: the beacons mark where they are.",
            ["Construa o Estaleiro para montar a nave-mãe."] = "Build the Shipyard to assemble the mothership.",
            ["Instale os módulos da nave-mãe no estaleiro"] = "Install the mothership modules at the shipyard",
            ["Leve a Terra ao estágio"] = "Bring Earth to the stage",
            ["Tudo pronto: decole a nave-mãe no estaleiro."] = "All set: launch the mothership from the shipyard.",
            ["Aumente o Ti até o estágio"] = "Raise the Ti to the stage",
            ["Oxigênio"] = "Oxygen",
            ["Calor"] = "Heat",
            ["Pressão"] = "Pressure",
            ["Biomassa"] = "Biomass",
            ["Descontaminação"] = "Decontamination",
            ["menu"] = "menu",
            ["interagir"] = "interact",
            ["desmontar"] = "dismantle",
            ["salvar"] = "save",
            ["carregar"] = "load",
            ["acelerar"] = "speed up",
            ["mover"] = "move",
            ["pausa"] = "pause",
            ["Você desmaiou e acordou no ponto inicial."] = "You passed out and woke up at the start point.",
            // Menu
            ["Inventário"] = "Inventory",
            ["Fabricar"] = "Craft",
            ["Construir"] = "Build",
            ["Baú"] = "Storage",
            ["Registros"] = "Logs",
            ["Nave-mãe · Estaleiro"] = "Mothership · Shipyard",
            ["Guardar →"] = "Store →",
            ["Usar"] = "Use",
            ["← Pegar"] = "← Take",
            ["Ler"] = "Read",
            ["vazio"] = "empty",
            ["grátis"] = "free",
            ["Instalar"] = "Install",
            ["DECOLAR"] = "LAUNCH",
            ["instalado"] = "installed",
            ["Módulos instalados"] = "Modules installed",
            ["de"] = "of",
            ["requer"] = "requires",
            ["requer microchip das ruínas"] = "requires a microchip from the ruins",
            ["Nenhum registro encontrado.\nProcure ruínas: os faróis marcam onde estão."] =
                "No logs found.\nSearch the ruins: the beacons mark where they are.",
            ["Registro recuperado"] = "Recovered log",
            ["Fechar  [E / Esc]"] = "Close  [E / Esc]",
            // Pausa e opções
            ["Sistema"] = "System",
            ["PAUSADO"] = "PAUSED",
            ["Continuar"] = "Continue",
            ["Salvar jogo"] = "Save game",
            ["Opções"] = "Options",
            ["OPÇÕES"] = "OPTIONS",
            ["Menu principal"] = "Main menu",
            ["Volume"] = "Volume",
            ["Sensibilidade do mouse"] = "Mouse sensitivity",
            ["Campo de visão"] = "Field of view",
            ["Inverter eixo Y"] = "Invert Y axis",
            ["Qualidade gráfica"] = "Graphics quality",
            ["Tela cheia"] = "Fullscreen",
            ["Idioma"] = "Language",
            ["Resolução"] = "Resolution",
            ["Sincronização vertical"] = "Vertical sync",
            ["Jogo salvo"] = "Game saved",
            ["Salvamento automático"] = "Autosaved",
            ["Nenhum save encontrado"] = "No save found",
            ["Save incompatível"] = "Incompatible save",
            ["Voltar"] = "Back",
            ["Controles"] = "Controls",
            ["CONTROLES"] = "CONTROLS",
            ["Pressione uma tecla (Esc cancela)"] = "Press a key (Esc cancels)",
            ["Restaurar padrão"] = "Reset to default",
            ["Pular"] = "Jump",
            ["Correr"] = "Sprint",
            ["Interagir"] = "Interact",
            ["Desmontar"] = "Dismantle",
            ["Menu"] = "Menu",
            ["Pausa"] = "Pause",
            ["Construir / confirmar"] = "Build / confirm",
            ["Cancelar"] = "Cancel",
            ["Girar construção"] = "Rotate building",
            ["Salvar"] = "Save",
            ["Carregar"] = "Load",
            ["O idioma muda ao voltar ao menu principal."] = "The language changes when you return to the main menu.",
            // Título e epílogo
            ["Estação Horizonte  ·  Missão de retorno"] = "Horizon Station  ·  Return mission",
            ["Um milhão de anos depois, a humanidade volta para casa."] = "A million years later, humanity comes home.",
            ["Novo jogo"] = "New game",
            ["Sair"] = "Quit",
            ["Créditos"] = "Credits",
            ["CRÉDITOS"] = "CREDITS",
            ["MISSÃO CUMPRIDA"] = "MISSION ACCOMPLISHED",
            ["Missão cumprida"] = "Mission accomplished",
            ["Pausado"] = "Paused",
            ["DEVER CUMPRIDO"] = "DUTY FULFILLED",
            ["Dever cumprido"] = "Duty fulfilled",
            ["A nave-mãe acopla na Estação Horizonte levando a notícia\nque a humanidade esperou por um milhão de anos: a Terra respira de novo."] =
                "The mothership docks at Horizon Station carrying the news\nhumanity waited a million years for: Earth breathes again.",
            ["Continuar explorando"] = "Keep exploring",
        });

        static Dictionary<string, string> WithData(Dictionary<string, string> en)
        {
            LocData.Fill(en);
            return en;
        }
    }
}
