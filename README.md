# Return to Earth

Jogo de sobrevivência e terraformação em primeira pessoa, inspirado em *The Planet Crafter*.
Unity **6000.6.2f1**, HDRP, Input System.

## Como rodar

1. Abra a pasta no Unity Hub (Add → Planetcraft) com a versão 6000.6.2f1.
2. Menu **Planetcraft → Gerar Protótipo** (cria dados, prefabs e a cena; pode rodar de novo).
3. Abra `Assets/_Project/Scenes/Prototype.unity` e dê Play.

### Controles

| Tecla | Ação |
|---|---|
| WASD / Shift / Espaço | andar / correr / pular |
| E | coletar recurso / abrir baú |
| X | desmontar construção (devolve o custo) |
| Tab / Esc | abrir / fechar menu (inventário, fabricar, construir, baú) |
| LMB / R ou scroll / RMB | construir / girar / cancelar |
| F5 / F9 | salvar / carregar |
| F2 | acelerar a simulação (x1 → x10 → x100) |

## Loop do protótipo

Colete minérios → construa geradores e máquinas → o **Ti** (oxigênio + calor + pressão + biomassa) sobe →
novos estágios mudam o céu, enchem a bacia central de água e fazem a vegetação aparecer →
novas máquinas desbloqueiam. Fique de olho no O2: o Módulo de Base recarrega.

## Arquitetura

```
Assets/_Project/Scripts/Runtime
  Core/      TerraformManager (tick fixo, Ti, estágios), StageDatabase, TiFormat, UIState
  World/     TerraformReactor (base, progresso em escala log) → SkyReactor, WaterReactor,
             StageActivator (liga vegetação por estágio), ResourceNode, Spinner
  Power/     PowerGrid (rede global sem fios)
  Items/     ItemDefinition (SO), Inventory (slots), InventoryComponent
  Crafting/  RecipeDefinition (SO), CraftingService
  Building/  BuildableDefinition (SO), BuildPlacer (fantasma + validação), PlacedObject, Machine
  Player/    FirstPersonController, Interactor, SurvivalStats, PressurizedZone
  Save/      SaveSystem (JSON versionado, só estado; carregar = recarregar cena + aplicar)
  Data/      GameDatabase (registro de ids)
  UI/        DebugHud (IMGUI provisório)
Assets/_Project/Scripts/Editor
  PrototypeBootstrap (gera conteúdo de exemplo e a cena)
```

Princípios:
- **Conteúdo é dado.** Itens, receitas, construções e estágios são ScriptableObjects em `Assets/_Project/Data`.
  O bootstrap só cria o que ainda não existe, então ajustes de balanceamento não são sobrescritos.
- **Estado separado da apresentação.** O mundo reage a eventos do `TerraformManager`, o que facilita
  adicionar save, simulação offline e, no futuro, multiplayer.
- **Save guarda estado, nunca a cena**, com campo `version` para migrações.

## Modelos 3D

`tools/gen_models.py` gera todos os modelos com o pacote `bpy` (Blender como módulo Python) e exporta FBX para
`Assets/_Project/Models/`. Rode `python tools/gen_models.py Assets/_Project/Models [nomes...] [--preview <pasta>]`
e depois **Planetcraft → Gerar Protótipo**.

## Testes visuais automáticos

`ReturnToEarth.exe -pc-ti <Ti> -pc-yaw <graus> -pc-pitch <graus> [-pc-rain] [-pc-showcase] [-pc-menu] -pc-wait <s> -pc-screenshot <png>`
— `-pc-showcase` constrói uma de cada máquina na frente do jogador; `-pc-menu` abre o menu.

## Próximos passos

- Trocar primitivas por modelos (Blender/Asset Store) e o HUD IMGUI por UI Toolkit
- Água com shader de verdade (Crest/KWS), chuva e nuvens por estágio
- Desconstruir, baús de armazenamento, blueprints por microchip em destroços
- Planilha de balanceamento → importador CSV para os ScriptableObjects
