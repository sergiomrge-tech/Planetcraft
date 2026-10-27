# Return to Earth

Jogo de sobrevivência e terraformação em primeira pessoa, inspirado em *The Planet Crafter*. A Terra foi
devastada pela Última Guerra; um milhão de anos depois, a humanidade volta da Estação Horizonte para reconstruí-la.
Unity **6000.6.2f1**, HDRP, Input System, UI Toolkit.

## Como rodar

1. Abra a pasta no Unity Hub (Add → Planetcraft) com a versão 6000.6.2f1.
2. Menu **Planetcraft → Gerar Protótipo** (cria dados, prefabs e as cenas Title, Prototype e Epilogue; pode rodar de novo).
3. Abra `Assets/_Project/Scenes/Title.unity` e dê Play, ou **Planetcraft → Build Windows** (`Builds/Windows/ReturnToEarth.exe`).

### Controles (remapeáveis em Opções → Controles)

| Teclado / mouse | Controle | Ação |
|---|---|---|
| WASD / Shift / Espaço | analógico esquerdo / L3 / A | andar / correr / pular |
| Mouse | analógico direito | olhar |
| E | X | interagir (coletar, abrir, ler) |
| X | Y | desmontar construção (devolve o custo) |
| Tab | Select | menu (inventário, fabricar, construir, registros) |
| Esc | Start | pausa |
| LMB / R ou scroll / RMB | RT / RB / B | construir / girar / cancelar |
| F5 / F9 / F2 | — | salvar / carregar / acelerar a simulação |

## Loop

Colete minérios → construa geradores e máquinas → o **Ti** (oxigênio + calor + pressão + biomassa +
descontaminação) sobe → estágios mudam o céu, enchem a bacia, espalham grama, florestas e fauna →
microchips nas ruínas liberam máquinas avançadas → monte a nave-mãe no estaleiro, decole no Renascimento e
acople na Estação Horizonte.

## Arquitetura

```
Assets/_Project/Scripts/Runtime
  Core/      TerraformManager (tick fixo, Ti, estágios), GameInput (ações e remapeamento), Loc/LocData (PT/EN),
             Settings, Blueprints, StoryLogs, ShipProgress
  World/     reatores do Ti (atmosfera, água, solo, clima), ProgressiveActivator (vegetação), AnimalWander, ruínas
  Power/     PowerGrid
  Items/     ItemDefinition (SO), Inventory, InventoryComponent
  Crafting/  RecipeDefinition (SO), CraftingService
  Building/  BuildableDefinition (SO), BuildPlacer, Deconstructor, Shipyard, PlacedObject, Machine
  Player/    FirstPersonController, Interactor, SurvivalStats, PressurizedZone
  Save/      SaveSystem (JSON versionado, só estado), GameSession (cenas)
  UI/        GameHud, TitleMenu, OptionsUi, EpilogueDirector, StageFeedback, HudStyle (+ UI/Hud.uss)
  Dev/       DevCommandLine (argumentos -pc-*)
Assets/_Project/Scripts/Editor
  PrototypeBootstrap (dados, prefabs e cenas), HdrpSetup, ModelPostprocessor (PBR triplanar),
  PolyHavenAssets, SketchfabAnimals, BuildScript
Assets/_Project/Tests/EditMode   testes de inventário, terraformação, tradução e entrada
```

Princípios:
- **Conteúdo é dado.** Itens, receitas, construções e estágios são ScriptableObjects gerados pelo bootstrap;
  textos e projetos exigidos sempre sincronizam com a tabela do código, valores de balanceamento não.
- **Estado separado da apresentação.** O mundo reage a eventos do `TerraformManager`.
- **Save guarda estado, nunca a cena**, com campo `version` para migrações.

## Assets (todos com licença comercial; ver CREDITS.md)

| Script | O que faz |
|---|---|
| `tools/fetch_polyhaven.py` | baixa texturas e modelos CC0 do Poly Haven e atualiza CREDITS.md |
| `tools/optimize_polyhaven.py` | (bpy) otimiza árvores/arbustos para jogo: LOD0/LOD1, raleio de folhas por orçamento |
| `tools/fetch_surfaces.py` | prepara as superfícies PBR das máquinas (albedo tingível + máscara HDRP) |
| `tools/fetch_nasa.py` | Terra (Blue Marble, domínio público) para o título e o epílogo |
| `tools/gen_models.py` | (bpy) modelos procedurais: máquinas, estaleiro, nave-mãe, estação, ruínas |
| `tools/import_sketchfab.py` | (bpy) prepara animais CC BY baixados do Sketchfab (zip, FBX ou glTF) |

O `bpy` roda num venv isolado (`pip install bpy`), sem Blender instalado.

## Testes

- EditMode: `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
- Visuais: `ReturnToEarth.exe -pc-ti <Ti> -pc-yaw <graus> -pc-pitch <graus> -pc-wait <s> -pc-screenshot <png>` e:
  `-pc-quiet` (sem banners), `-pc-showcase [-pc-showcase-radius <m>]`, `-pc-menu`, `-pc-pause`, `-pc-log <n>`,
  `-pc-wreck <n>`, `-pc-shipyard <módulos>`, `-pc-epilogue <s>`, `-pc-lang <pt|en>`, `-pc-diag` (vegetação no log),
  `-pc-fps` (FPS médio e pior quadro no log, sem vsync).
