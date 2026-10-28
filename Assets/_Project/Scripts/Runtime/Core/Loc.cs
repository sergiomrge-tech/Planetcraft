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
            ["Colono 7, acorde. Aqui é a Central da Estação Horizonte."] = "Colonist 7, wake up. This is Horizon Station Control.",
            ["Seus sinais vitais estão estáveis. Depois de um milhão de anos em órbita, hoje é o Dia do Retorno."] = "Your vitals are stable. After a million years in orbit, today is Return Day.",
            ["Seu comunicador de pulso está ativo. Eu vou guiá-lo. Mova-se com [WASD] e olhe com o mouse."] = "Your wrist communicator is active. I will guide you. Move with [WASD] and look with the mouse.",
            ["Saia da cabine pela porta"] = "Leave the cabin through the door",
            ["Comportas liberadas. No corredor há um armário com o seu traje: aproxime-se e aperte [E]."] = "Bulkheads unlocked. There is a locker with your suit in the corridor: step up and press [E].",
            ["Pegue o traje no armário [E]"] = "Get the suit from the locker [E]",
            ["Pegar o traje"] = "Take the suit",
            ["Traje ajustado e selado. Oxigênio em cem por cento. Sua pistola de pulso também foi liberada: o botão esquerdo do mouse atira."] = "Suit fitted and sealed. Oxygen at one hundred percent. Your pulse pistol is also unlocked: the left mouse button fires.",
            ["Siga o corredor até o hangar"] = "Follow the corridor to the hangar",
            ["Agora siga o corredor até o hangar. Pode correr com [Shift] e pular com [Espaço]."] = "Now follow the corridor to the hangar. You can run with [Shift] and jump with [Space].",
            ["Aquela é a sua nave de descida. A Terra lá fora está queimada, Colono, mas nossos sensores mostram uma chance."] = "That is your descent ship. Earth is scorched out there, Colonist, but our sensors show a chance.",
            ["Construa uma base, recupere o ar, a água e a vida. Quando a Terra respirar de novo, traga a nave-mãe de volta para casa."] = "Build a base, bring back air, water and life. When Earth breathes again, bring the mothership home.",
            ["Embarque na nave [E]"] = "Board the ship [E]",
            ["Embarcar"] = "Board",
            ["Iniciando a descida. Boa sorte, Colono 7. A humanidade conta com você."] = "Starting descent. Good luck, Colonist 7. Humanity is counting on you.",
            ["Entrando na atmosfera. Segure firme."] = "Entering the atmosphere. Hold on tight.",
            ["Segure [Esc] para pular a abertura"] = "Hold [Esc] to skip the intro",
            ["Pouso confirmado. Boa sorte, Colono 7."] = "Touchdown confirmed. Good luck, Colonist 7.",
            ["CENTRAL HORIZONTE"] = "HORIZON CONTROL",

            ["Reparar"] = "Repair",
            ["Uma peça da base foi destruída!"] = "A base piece was destroyed!",
            ["Você apagou e acordou na base"] = "You blacked out and woke up at base",
            ["Já existe uma fundação aqui"] = "There is already a foundation here",
            ["Terreno irregular demais para a fundação"] = "Ground too uneven for a foundation",
            ["Construa uma fundação primeiro"] = "Build a foundation first",
            ["Pisos e tetos vão sobre paredes e outros pisos"] = "Floors and ceilings go on walls and other floors",
            ["Já existe um piso aqui"] = "There is already a floor here",
            ["Precisa de paredes ou de um piso vizinho"] = "Needs walls or a neighboring floor",
            ["Já existe uma parede aqui"] = "There is already a wall here",
            ["Precisa de um piso ao lado"] = "Needs a floor next to it",
            ["Precisa de um piso"] = "Needs a floor",
            ["Coloque sobre um piso da base"] = "Place it on a base floor",
            ["Brecha na base: o ar está escapando!"] = "Base breach: air is escaping!",
            ["Sinal hostil detectado"] = "Hostile signal detected",
            ["drones chegam em"] = "drones arrive in",
            ["Tempestade de poeira! Abrigue-se numa base selada."] = "Dust storm! Take shelter in a sealed base.",
            ["A tempestade de poeira passou"] = "The dust storm has passed",
            ["Sucata recuperada"] = "Scrap recovered",
            ["Ameaças"] = "Threats",
            ["Nenhuma"] = "None",
            ["Baixa"] = "Low",
            ["Normal"] = "Normal",
            ["Alta"] = "High",
            ["L"] = "E",
            ["NL"] = "NE",
            ["N"] = "N",
            ["NO"] = "NW",
            ["O"] = "W",
            ["SO"] = "SW",
            ["S"] = "S",
            ["SL"] = "SE",
            ["Abrigo pressurizado"] = "Pressurized shelter",
            ["Exposto"] = "Exposed",
            ["radiação"] = "radiation",
            ["VIDA"] = "HP",
            ["ARMA"] = "GUN",
            ["Dormir"] = "Sleep",
            ["Usar enfermaria"] = "Use medbay",
            ["Beber água"] = "Drink water",
            ["sem energia"] = "no power",
            ["Você descansou um pouco"] = "You rested a little",
            ["Ferimentos tratados"] = "Injuries treated",
            ["Água purificada"] = "Water purified",
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
