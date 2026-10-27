using System.Collections.Generic;

namespace Planetcraft.Core
{
    // Inglês dos dados do jogo (tabelas do PrototypeBootstrap: itens, construções, módulos da nave, estágios,
    // registros) e dos prompts de interação. A chave é o texto em português exatamente como está na tabela.
    static class LocData
    {
        public static void Fill(Dictionary<string, string> en)
        {
            // Itens
            en["Ferro"] = "Iron";
            en["Silício"] = "Silicon";
            en["Titânio"] = "Titanium";
            en["Gelo"] = "Ice";
            en["Magnésio"] = "Magnesium";
            en["Liga Metálica"] = "Metal Alloy";
            en["Cápsula de O2"] = "O2 Capsule";
            en["Garrafa d'Água"] = "Water Bottle";
            en["Barra Nutritiva"] = "Nutrient Bar";
            en["Urânio"] = "Uranium";
            en["Barra de Combustível"] = "Fuel Rod";

            // Construções
            en["Módulo de Base"] = "Base Module";
            en["Abrigo pressurizado: recarrega oxigênio."] = "Pressurized shelter: refills oxygen.";
            en["Guarda até 30 pilhas de itens. [E] para abrir."] = "Holds up to 30 item stacks. [E] to open.";
            en["Gerador Eólico"] = "Wind Turbine";
            en["Aquecedor"] = "Heater";
            en["Aquecedor II"] = "Heater II";
            en["Aquecedor III"] = "Heater III";
            en["Aquecedor IV"] = "Heater IV";
            en["Perfuratriz"] = "Drill";
            en["Perfuratriz II"] = "Drill II";
            en["Perfuratriz III"] = "Drill III";
            en["Filtro de Radiação"] = "Radiation Filter";
            en["Filtro de Radiação II"] = "Radiation Filter II";
            en["Filtro de Radiação III"] = "Radiation Filter III";
            en["Vegetubo"] = "Vegetube";
            en["Vegetubo II"] = "Vegetube II";
            en["Painel Solar"] = "Solar Panel";
            en["Fazenda de Algas"] = "Algae Farm";
            en["Estaleiro"] = "Shipyard";
            en["Reator Nuclear"] = "Nuclear Reactor";
            en["+1.2 kW"] = "+1.2 kW";
            en["+6.5 kW"] = "+6.5 kW";
            en["Calor +1.2/s. Consome 1 kW."] = "Heat +1.2/s. Uses 1 kW.";
            en["Pressão +1/s. Consome 1 kW."] = "Pressure +1/s. Uses 1 kW.";
            en["Descontaminação +1/s. Consome 1 kW."] = "Decontamination +1/s. Uses 1 kW.";
            en["Oxigênio +0.8/s. Consome 0.5 kW."] = "Oxygen +0.8/s. Uses 0.5 kW.";
            en["Calor +12/s. Consome 4 kW."] = "Heat +12/s. Uses 4 kW.";
            en["Pressão +10/s. Consome 4 kW."] = "Pressure +10/s. Uses 4 kW.";
            en["Biomassa +25/s, Oxigênio +5/s. Consome 2 kW."] = "Biomass +25/s, Oxygen +5/s. Uses 2 kW.";
            en["Plataforma para montar a nave-mãe que leva a notícia de volta à Estação Horizonte. Só um por mundo."] =
                "Platform to assemble the mothership that carries the news back to Horizon Station. One per world.";
            en["Descontaminação +15/s. Consome 4 kW."] = "Decontamination +15/s. Uses 4 kW.";
            en["Calor +400/s. Consome 15 kW."] = "Heat +400/s. Uses 15 kW.";
            en["Oxigênio +60/s. Consome 6 kW."] = "Oxygen +60/s. Uses 6 kW.";
            en["+80 kW. Precisa de barras de combustível para construir."] = "+80 kW. Needs fuel rods to build.";
            en["Calor +6000/s. Consome 40 kW."] = "Heat +6000/s. Uses 40 kW.";
            en["Pressão +3000/s. Consome 35 kW."] = "Pressure +3000/s. Uses 35 kW.";
            en["Descontaminação +3500/s. Consome 30 kW."] = "Decontamination +3500/s. Uses 30 kW.";

            // Módulos da nave-mãe
            en["Casco"] = "Hull";
            en["Casco facetado em flecha e blindagem."] = "Faceted arrowhead hull and armor.";
            en["Motores"] = "Engines";
            en["Propulsores da popa e naceles sob as asas."] = "Stern thrusters and under-wing nacelles.";
            en["Superestrutura"] = "Superstructure";
            en["Espinha dorsal com conveses, hangares e alojamentos."] = "Dorsal spine with decks, hangars and quarters.";
            en["Ponte de comando"] = "Command bridge";
            en["Cabine de comando e mastro de sensores."] = "Command cabin and sensor mast.";
            en["Reator de salto"] = "Jump reactor";
            en["Anel ventral de salto para alcançar a órbita da estação."] = "Ventral jump ring to reach the station's orbit.";

            // Estágios
            en["Terra Morta"] = "Dead Earth";
            en["A Última Guerra queimou o ar e contaminou o solo."] = "The Last War burned the air and poisoned the soil.";
            en["Névoa"] = "Haze";
            en["Os primeiros gases engrossam o ar."] = "The first gases thicken the air.";
            en["Céu Azul"] = "Blue Sky";
            en["A luz começa a se espalhar no céu."] = "Light begins to scatter across the sky.";
            en["Nuvens"] = "Clouds";
            en["Vapor condensa em nuvens."] = "Vapor condenses into clouds.";
            en["Chuva"] = "Rain";
            en["A primeira chuva cai."] = "The first rain falls.";
            en["Água Líquida"] = "Liquid Water";
            en["A água fica líquida na superfície."] = "Water stays liquid on the surface.";
            en["Lagos"] = "Lakes";
            en["A bacia começa a encher."] = "The basin begins to fill.";
            en["Musgo"] = "Moss";
            en["Musgo cobre as rochas úmidas."] = "Moss covers the damp rocks.";
            en["Flora"] = "Flora";
            en["Plantas se espalham pelo vale."] = "Plants spread through the valley.";
            en["Florestas"] = "Forests";
            en["Árvores crescem sozinhas."] = "Trees grow on their own.";
            en["Fauna"] = "Fauna";
            en["Os primeiros animais caminham pelo vale."] = "The first animals walk the valley.";
            en["Renascimento"] = "Rebirth";
            en["A Terra respira sozinha. A nave-mãe pode voltar à Estação Horizonte."] =
                "Earth breathes on its own. The mothership can return to Horizon Station.";

            // Registros
            en["Bunker Lisboa-7 — Diário de Ilsa Moreau, agrônoma"] = "Bunker Lisbon-7 — Journal of Ilsa Moreau, agronomist";
            en["Ano 2189. As sirenes pararam ontem. Lá fora o céu está cor de ferrugem e nada que eu planto passa da " +
               "primeira folha.\n\nGravei neste núcleo de dados o projeto dos vegetubos de alta eficiência. Se um dia " +
               "alguém voltar a pisar aqui fora, plante. Plante muito."] =
                "Year 2189. The sirens stopped yesterday. Outside the sky is the color of rust and nothing I plant grows " +
                "past its first leaf.\n\nI stored the design for high-efficiency vegetubes in this data core. If someone ever " +
                "walks out here again: plant. Plant a lot.";
            en["Relatório técnico — Reator KR-4"] = "Technical report — KR-4 Reactor";
            en["O reator compacto KR-4 roda com barras de urânio refinado misturado com liga metálica.\n\nOs veios " +
               "verdes que brilham perto das crateras são urânio exposto pelas ogivas da Última Guerra. Use traje ao " +
               "coletar. O projeto completo do reator está no núcleo deste abrigo."] =
                "The compact KR-4 reactor runs on refined uranium rods mixed with metal alloy.\n\nThe glowing green veins " +
                "near the craters are uranium exposed by the warheads of the Last War. Wear a suit when collecting. The " +
                "full reactor design is in this shelter's core.";
            en["Última ordem — Conselho da Estação Horizonte"] = "Final order — Horizon Station Council";
            en["Ano zero. Partimos hoje com os últimos quarenta mil. Os esterilizadores atmosféricos que lançamos uns " +
               "contra os outros não podem mais ser desligados: vão queimar o ar e o solo por eras.\n\nA quem um dia " +
               "voltar: quando a Terra respirar sozinha, monte uma nave-mãe no estaleiro e traga a notícia de volta " +
               "para a estação. Vamos esperar o tempo que for preciso."] =
                "Year zero. We leave today with the last forty thousand. The atmospheric sterilizers we launched at each " +
                "other can no longer be shut down: they will burn the air and the soil for ages.\n\nTo whoever returns one " +
                "day: when Earth breathes on its own, build a mothership at the shipyard and bring the news back to the " +
                "station. We will wait as long as it takes.";
            en["IA de defesa — ATLAS"] = "Defense AI — ATLAS";
            en["Registro automático. Esterilizadores atmosféricos: sem energia há 999.412 anos.\n\nDetecto " +
               "atividade de terraformação na superfície, de origem humana. Atualizando projeções: com perfuratrizes " +
               "de alta pressão o índice cresce em ordens de grandeza. Projeto anexado. Esperança: recalculando... " +
               "valor positivo."] =
                "Automatic log. Atmospheric sterilizers: unpowered for 999,412 years.\n\nDetecting terraforming activity " +
                "on the surface, of human origin. Updating projections: with high-pressure drills the index grows by orders " +
                "of magnitude. Design attached. Hope: recalculating... positive value.";
            en["Cofre genético — Dra. Nadia Okoro"] = "Gene vault — Dr. Nadia Okoro";
            en["Selei o cofre genético antes do último apagão. Sementes, insetos, animais: tudo em estase no fundo " +
               "do vale, fundo o bastante para atravessar a radiação.\n\nQuando a Terra respirar sozinha, eles " +
               "acordam. Você vai saber que deu certo quando ouvir algo correndo entre as árvores."] =
                "I sealed the gene vault before the last blackout. Seeds, insects, animals: all in stasis at the bottom of " +
                "the valley, deep enough to outlast the radiation.\n\nWhen Earth breathes on its own, they wake up. You will " +
                "know it worked when you hear something running between the trees.";

            // Prompts, avisos e banners
            en["Coletar"] = "Collect";
            en["Desmontar"] = "Dismantle";
            en["Reler"] = "Reread";
            en["Ler registro"] = "Read log";
            en["Abrir baú"] = "Open storage";
            en["Pegar microchip"] = "Take microchip";
            en["Estaleiro: montar a nave-mãe"] = "Shipyard: assemble the mothership";
            en["Estaleiro vazio — a nave-mãe voltou à estação"] = "Empty shipyard — the mothership returned to the station";
            en["Mire no chão"] = "Aim at the ground";
            en["Requer"] = "Requires";
            en["Só é possível ter um"] = "Only one allowed";
            en["Requer microchip das ruínas"] = "Requires a microchip from the ruins";
            en["Terreno muito inclinado"] = "Ground too steep";
            en["Recursos insuficientes"] = "Not enough resources";
            en["construir"] = "build";
            en["girar"] = "rotate";
            en["cancelar"] = "cancel";
            en["A nave-mãe está em montagem neste estaleiro"] = "The mothership is being assembled at this shipyard";
            en["Esvazie o baú antes de desmontar"] = "Empty the storage before dismantling";
            en["Saia da base para desmontar"] = "Leave the base to dismantle";
            en["Inventário cheio"] = "Inventory full";
            en["instale antes"] = "install first";
            en["recursos insuficientes"] = "not enough resources";
            en["a nave já partiu"] = "the ship has already left";
            en["monte todos os módulos"] = "assemble all modules";
            en["atmosfera instável: requer o estágio"] = "unstable atmosphere: requires the stage";
            en["NOVO ESTÁGIO"] = "NEW STAGE";
            en["NOVO PROJETO"] = "NEW DESIGN";
            en["PROJETO OBTIDO"] = "DESIGN ACQUIRED";
            en["Desbloqueado"] = "Unlocked";
            en["Disponível no menu [Tab]."] = "Available in the [Tab] menu.";
            en["Disponível a partir de"] = "Available from";
        }
    }
}
