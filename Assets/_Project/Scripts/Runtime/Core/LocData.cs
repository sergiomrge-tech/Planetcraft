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
            // Kit de base, utilidades e defesas
            // Kit de base, utilidades e defesas
            en["Fundação"] = "Foundation";
            en["Laje de 4 x 4 m sobre estacas: define o nível do piso da base."] = "A 4 x 4 m slab on piles: sets the base's floor level.";
            en["Piso e Teto"] = "Floor and Ceiling";
            en["Piso de 4 x 4 m. Empilhe para fazer andares e tetos."] = "4 x 4 m floor. Stack it for upper floors and ceilings.";
            en["Parede"] = "Wall";
            en["Parede de painéis de 4 m. Vede as quatro arestas e o teto de uma sala."] = "4 m panel wall. Seal all four edges and the ceiling of a room.";
            en["Parede com Porta"] = "Wall with Door";
            en["Parede com passagem. Mantém a sala selada."] = "Wall with a doorway. Keeps the room sealed.";
            en["Parede com Janela"] = "Wall with Window";
            en["Parede com janela panorâmica."] = "Wall with a panoramic window.";
            en["Câmara de Ar"] = "Airlock";
            en["Escotilha blindada para entrar e sair da base."] = "Armored hatch for entering and leaving the base.";
            en["Parede Reforçada"] = "Reinforced Wall";
            en["Liga metálica: aguenta muito mais dano."] = "Metal alloy: takes far more damage.";
            en["Pilar"] = "Pillar";
            en["Coluna de apoio para os cantos."] = "Support column for corners.";
            en["Escada"] = "Stairs";
            en["Sobe um andar (3 m). [R] gira."] = "Climbs one floor (3 m). [R] rotates.";
            en["Gerador de O2"] = "O2 Generator";
            en["Pressuriza a sala selada em que está. Consome 0.8 kW."] = "Pressurizes the sealed room it is in. Uses 0.8 kW.";
            en["Célula de Energia"] = "Power Cell";
            en["+3 kW."] = "+3 kW.";
            en["Purificador de Água"] = "Water Purifier";
            en["[E] para beber. Consome 0.4 kW."] = "[E] to drink. Uses 0.4 kW.";
            en["Beliche"] = "Bunk";
            en["[E] para descansar e recuperar vida."] = "[E] to rest and recover health.";
            en["Enfermaria"] = "Medbay";
            en["[E] cura todos os ferimentos. Consome 0.6 kW."] = "[E] heals all injuries. Uses 0.6 kW.";
            en["Armário"] = "Locker";
            en["Bancada"] = "Workbench";
            en["Bancada de trabalho: dá vida à base."] = "Work bench: gives the base some life.";
            en["Cozinha"] = "Kitchen";
            en["Cozinha compacta: a base fica mais confortável."] = "Compact kitchen: the base feels more comfortable.";
            en["Torre de Pulso"] = "Pulse Turret";
            en["Atira nos inimigos próximos. Consome 0.8 kW."] = "Shoots nearby enemies. Uses 0.8 kW.";
            en["Torre de Plasma"] = "Plasma Turret";
            en["Dano pesado e longo alcance. Consome 3 kW."] = "Heavy damage, long range. Uses 3 kW.";
            en["Refletor"] = "Floodlight";
            en["Ilumina a área e afasta os lobos. Consome 0.3 kW."] = "Lights the area and scares off wolves. Uses 0.3 kW.";
            en["Cerca Eletrificada"] = "Electric Fence";
            en["Dá choque em quem atravessa. Consome 0.6 kW."] = "Shocks anything that crosses it. Uses 0.6 kW.";

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

            // Novas máquinas
            en["Condensador Atmosférico"] = "Atmospheric Condenser";
            en["Pressão +8/s, Biomassa +4/s. Consome 2.5 kW."] = "Pressure +8/s, Biomass +4/s. Uses 2.5 kW.";
            en["Gerador a Combustível"] = "Fuel Generator";
            en["+8 kW. Barulhento, mas compacto."] = "+8 kW. Loud, but compact.";

            // Registros 6 a 10
            en["Diário da Estação Horizonte — Ano 14"] = "Horizon Station Journal — Year 14";
            en["Ano 14 da estação. Hoje as crianças perguntaram por que o céu do vídeo é azul se a janela mostra marrom.\n\n" +
               "Explicamos que a Terra é um lugar que está dormindo. A mais nova respondeu que então era preciso acordá-la " +
               "com cuidado. Anotei isso no quadro da escola: a primeira lição da nossa geração é paciência."] =
                "Station year 14. Today the children asked why the sky in the video is blue when the window shows brown.\n\n" +
                "We explained that Earth is a place that is asleep. The youngest answered that then it had to be woken " +
                "carefully. I wrote that on the school board: our generation's first lesson is patience.";
            en["Carta de Tomás Rocha, engenheiro de reatores"] = "Letter from Tomás Rocha, reactor engineer";
            en["A quem encontrar este abrigo: o KR-4 não perdoa pressa. Antes de ligar o núcleo, tenha energia estável e " +
               "filtros de radiação rodando — o ar daqui ainda cobra juros.\n\nDeixei o projeto do filtro de três estágios " +
               "na caixa preta da porta. Se a luz do painel estiver verde, o ar já está limpando. Boa sorte, e desculpe " +
               "pelo estado do mundo."] =
                "To whoever finds this shelter: the KR-4 does not forgive haste. Before you start the core, have stable " +
                "power and radiation filters running — the air here still charges interest.\n\nI left the three-stage " +
                "filter design in the black box by the door. If the panel light is green, the air is already clearing. Good " +
                "luck, and sorry about the state of the world.";
            en["Arquivo ATLAS — Cronologia da Última Guerra"] = "ATLAS Archive — Timeline of the Last War";
            en["Dia 1: colapso das redes de alerta. Dia 2: ativação dos esterilizadores atmosféricos em oito frentes. " +
               "Dia 3: evacuação da Terra encerrada, quarenta mil a bordo.\n\nNão houve vencedor. Os esterilizadores foram " +
               "feitos para tornar o território inimigo inabitável; ninguém previu que o inimigo respirava o mesmo ar. " +
               "Registro encerrado por ordem do Conselho."] =
                "Day 1: collapse of the warning networks. Day 2: atmospheric sterilizers activated on eight fronts. " +
                "Day 3: evacuation of Earth ends, forty thousand aboard.\n\nThere was no winner. The sterilizers were built " +
                "to make enemy territory uninhabitable; nobody foresaw that the enemy breathed the same air. Log closed " +
                "by order of the Council.";
            en["Canção de ninar (transcrição danificada)"] = "Lullaby (damaged transcript)";
            en["...durma, Terra, durma, a chuva vai voltar. Teu azul está guardado onde a gente vai buscar. Quando o vento " +
               "for brisa e o mar tiver cheiro de sal, a gente desce de volta pra cuidar do teu quintal...\n\nA última " +
               "estrofe foi cantada por uma criança. A estação a tocou todas as noites durante um milhão de anos."] =
                "...sleep, Earth, sleep, the rain will come again. Your blue is kept where we will fetch it. When the wind " +
                "is a breeze and the sea smells of salt, we will come down to tend your yard...\n\nThe last verse was sung by " +
                "a child. The station played it every night for a million years.";
            en["Mensagem final — Estação Horizonte"] = "Final message — Horizon Station";
            en["Aqui é a Estação Horizonte. Recebemos o sinal de terraformação há onze anos e não ousamos acreditar. Hoje os " +
               "sensores confirmaram: há oxigênio, há água, há vida.\n\nSe alguém aí embaixo ouve isto: terminem o que " +
               "começaram e voltem para casa. A porta de acoplagem está aberta. Esperamos um milhão de anos; podemos " +
               "esperar mais um dia."] =
                "This is Horizon Station. We received the terraforming signal eleven years ago and did not dare believe it. " +
                "Today the sensors confirmed: there is oxygen, there is water, there is life.\n\nIf anyone down there can " +
                "hear this: finish what you started and come home. The docking port is open. We have waited a million " +
                "years; we can wait one more day.";

            // Opções de áudio, dicas e objetivos
            en["Volume da música"] = "Music volume";
            en["Volume dos efeitos"] = "Effects volume";
            en["Salvamento automático"] = "Autosave";
            en["Dica"] = "Hint";
            en["{WASD} andar   ·   mouse olhar   ·   [SHIFT] correr   ·   [ESPAÇO] pular"] =
                "{WASD} move   ·   mouse look   ·   [SHIFT] sprint   ·   [SPACE] jump";
            en["Aperte {E} para coletar o minério. Ferro, silício, gelo e magnésio abrem as primeiras máquinas."] =
                "Press {E} to collect the ore. Iron, silicon, ice and magnesium unlock the first machines.";
            en["Aperte {TAB} para abrir o menu: fabrique itens e construa máquinas com o que você coletou."] =
                "Press {TAB} to open the menu: craft items and build machines with what you collected.";
            en["{R} gira a construção, o clique esquerdo confirma e o direito cancela."] =
                "{R} rotates the building, left click confirms and right click cancels.";
            en["Seu oxigênio está acabando! Entre num Módulo de Base ou use uma Cápsula de O2 pelo menu ({TAB})."] =
                "You are running out of oxygen! Enter a Base Module or use an O2 Capsule from the menu ({TAB}).";
            en["Faltou energia: as máquinas param. Construa um Gerador Eólico ou um Painel Solar."] =
                "Out of power: machines stop. Build a Wind Turbine or a Solar Panel.";
            en["Os pilares azuis no horizonte são ruínas do velho mundo: guardam registros, projetos novos e urânio."] =
                "The blue pillars on the horizon are old-world ruins: they hold logs, new designs and uranium.";
            en["Colete 2 de ferro: aproxime-se de uma rocha escura com nódulos e aperte [E]."] =
                "Collect 2 iron: approach a dark rock with nodules and press [E].";
            en["Construa um Módulo de Base: ele recarrega seu oxigênio. Abra o menu [TAB] e escolha Construir."] =
                "Build a Base Module: it refills your oxygen. Open the [TAB] menu and choose Build.";

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
