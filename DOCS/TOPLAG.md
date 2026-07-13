# Top 50 Agressores de Performance

Data: `2026-07-13`.

Ranking inicial para orientar profiling. Não representa porcentagem exata: repositório não contém captura `.data`, `.raw`, `.snap` ou dotTrace. Logs do Unity Editor confirmam tempos e contagem de objetos; outras posições usam análise estática de frequência, fan-out e volume potencial.

## Legenda

- `M`: medido em log atual.
- `E`: evidência estática no código ou conteúdo.
- `C`: candidato; captura precisa confirmar custo e posição.
- Recursos: CPU, RAM, GPU, I/O, GC, física, áudio ou rede.

## Baseline disponível

| Métrica | Resultado | Limite |
| --- | ---: | --- |
| Primeiro carregamento de assets | `76,9569 s` | Unity Editor |
| Carregamentos quentes | `11,8790–12,1740 s` | Mesma sessão |
| Diferença cold/warm | `~6,4x` | Inclui JIT e caches do processo/filesystem |
| Abertura de `core.masterbundle` | `5,95–6,13 s` | Quase constante em cold/warm |
| Objetos carregados no menu | `~67.958–68.000` | Log Unity |
| Pico no loading de mapa | `1.043.752` objetos | Antes do cleanup |
| Após cleanup do mapa | `86.958` objetos | Acima do menu |

## Top 5 mudanças de maior potencial

Ranking de iniciativas amplas, não de métodos isolados. Itens `1–2` têm medição atual; itens `3–5` são candidatos e podem mudar de ordem após captura standalone.

| # | Mudança | Implementação proposta | Ganho esperado | Evidência e trava |
| ---: | --- | --- | --- | --- |
| 1 | Catálogo de assets em duas fases | Carregar índice, GUID, tipo e configuração necessária primeiro; abrir prefabs, materiais, áudio e outros payloads somente no primeiro uso. Adicionar manifesto persistente apenas após definir invalidação por versão e arquivo alterado. | Boot, RAM e I/O | `M/E`: cold start de `76,96 s`, cerca de `68 mil` objetos no menu e 67 loads eager em 25 tipos. Primeira fatia aplicada a `ItemAsset`; restante continua aberto. |
| 2 | Streaming regional do mundo | Manter manifesto leve por chunk; instanciar regiões próximas e direção de movimento com orçamento por frame; descarregar anéis distantes sem `GC.Collect` forçado. | Loading, RAM, GC e picos de frame time | `M`: pico de `1.043.752` objetos durante loading. Primeira fatia materializa proxies `Skybox` por região; objetos físicos aguardam Memory Profiler e restauração segura de estado. |
| 3 | Pipeline único de visibilidade e LOD | Reutilizar regiões para frustum/occlusion culling, LOD e distância por categoria; aplicar instancing, impóstores e orçamento separado para folhagem, terreno, água, luzes e sombras. | GPU, VRAM e CPU de render | `E/C`: terreno, folhagem, transparência e luzes cobrem grandes áreas. GPU Profiler e Frame Debugger precisam confirmar draw calls, overdraw e shadows antes. |
| 4 | Simulação e pathfinding por relevância | Atualizar entidades próximas em alta frequência, médias em frequência reduzida e distantes somente por evento; limitar repaths por tick e usar rota hierárquica/cache por versão quando medição justificar. | CPU, física e tick do servidor | `E/C`: zombies, animais, veículos e managers possuem loops contínuos. CPU Timeline precisa separar IA, física, animação, rede e pathfinding. |
| 5 | Replicação de rede por interesse e estado sujo | Usar mesmas regiões para relevância; enviar somente deltas alterados, agrupar mensagens pequenas e impor budgets/backpressure por conexão e sistema. | CPU do servidor, banda e escalabilidade | `E/C`: vários managers replicam estado globalmente. Network Profiler e load test precisam medir bytes, mensagens e CPU por jogador. |

Ordem segura: atacar `1`, explicar pico de `2`, depois medir e reordenar `3–5`. Reescrita completa sem baseline não entra no plano.

## Alterações aplicadas após baseline

- `EffectAsset`: prefab e splatters agora usam `Bundle.loadDeferred`; 176 assets oficiais deixam de carregar visuais no boot.
- `MythicAsset`: quatro prefabs de 81 assets agora carregam no primeiro acesso.
- Primeiro cleanup do mapa removido: `256,43 ms` para recuperar somente `76,6 KB`. Cleanup final, que recuperou `7,9 MB`, permanece.
- Água: `LevelLighting` consulta volumes uma vez por frame para estado submerso e superfície; antes fazia duas travessias.
- Terreno: variantes sem neve dos seis passes removem quatro amostras de máscaras por fragmento; variante de neve permanece igual.
- `ItemAsset`: prefab principal, animações e três texturas base de skin agora usam carregamento sob demanda em master bundles.
- `LevelObject`: proxy `Skybox` passa a ser criado no primeiro acesso ou ativação regional; editor e batching preservam materialização necessária.
- Resultado antes/depois ainda precisa de cold boot e loading do mesmo mapa; itens ficam parcialmente abertos até nova captura.

## Ranking atual

Ordem considera impacto potencial, abrangência e evidência. Linha `M` confirma fato observado, não atribuição integral ao método indicado.

| # | Agressor | Recurso | Evidência | Alvo | Próxima ação mínima |
| ---: | --- | --- | --- | --- | --- |
| 1 | Catálogo completo no cold start | CPU/RAM/I/O | `M/E`: `76,96 s` frio, `~12 s` quente | [`Assets.LoadAssetsFromWorkerThread`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Marcar busca, parse, criação Unity e link; lazy-load por tipo. |
| 2 | Pico superior a um milhão de objetos no mapa | CPU/RAM/GC | `M`: `1.043.752` objetos | [`Level`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/Level.cs), [`LevelObjects`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelObjects.cs) | Comparar snapshots antes, no pico e após cleanup; agrupar por tipo/tamanho. |
| 3 | `core.masterbundle` custa cerca de seis segundos | I/O/CPU | `M`: `5,95–6,13 s` | [`Assets`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Separar disco, descompressão, espera e criação de objetos em build standalone. |
| 4 | Cleanup força `UnloadUnusedAssets` e coleta completa | CPU/GC | `M`: primeiro custou `256,43 ms` por `76,6 KB` e foi removido; cleanup final permanece | [`Assets.CleanupMemory`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs), [`Level`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/Level.cs) | Repetir mapa; medir cleanup final e memória recuperada. |
| 5 | 67 loads eager em 25 tipos de asset | CPU/RAM/I/O | `E`: inventário estático | [`Bundles`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles) | Priorizar por instâncias/tamanho; adiar conteúdo ausente do menu. |
| 6 | Varredura recursiva de diretórios e Workshop | I/O/CPU | `E`: filesystem inteiro pelo worker | [`AssetsWorker.SearcherThreadMain`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/AssetsWorker.cs) | Contar diretórios, arquivos, bytes e tempo por raiz. |
| 7 | Leitura, parsing e hash de cada `.dat`/`.asset` | CPU/I/O | `E`: custo por arquivo no catálogo | [`AssetsWorker.AddFoundAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/AssetsWorker.cs), [`Assets.TryLoadFile`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Medir hash, parse e construtor separadamente. |
| 8 | Busca de bundles/assets de todos os mapas | I/O/CPU/RAM | `E`: locais adicionados ao catálogo | [`Assets.AddMapSearchLocations`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Avaliar indexação do mapa necessário com regressão de mods/servidor. |
| 9 | Enumeração e validação de todos os mapas | I/O/CPU | `E`: scan em `Level.levels` | [`Level.getLevels`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/Level.cs) | Cache por sessão com invalidação explícita. |
| 10 | Resolução global de spawn tables | CPU/RAM | `E`: passe `linkSpawns` | [`Assets.linkSpawns`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Medir nós/referências; relink incremental para tabelas alteradas. |
| 11 | Payloads derivados de `ItemAsset` ainda podem ser eager | RAM/CPU/GPU | `E`: prefab `Item`, animações e três texturas base agora lazy; derivados adicionais permanecem | [`ItemAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/ItemAsset.cs) | Medir cold boot e primeiro uso; converter próximo derivado somente se dominar captura. |
| 12 | `ResourceAsset` carrega/processa vários prefabs | RAM/CPU/GPU | `E`: 69 recursos; maior contagem eager | [`ResourceAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/ResourceAsset.cs) | Medir prefab/model/skybox; preservar `Auto_Skybox` lazy. |
| 13 | Catálogo/pool de efeitos | RAM/GPU/CPU | `E`: 176 assets agora lazy no boot; preload do mapa permanece | [`EffectAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/EffectAsset.cs), [`EffectManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/EffectManager.cs) | Medir boot e mapa; revisar somente preloads responsáveis por hitch/memória. |
| 14 | Mythics carregam múltiplos prefabs | RAM/GPU/CPU | `E`: quatro prefabs de 81 assets agora lazy | [`MythicAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/MythicAsset.cs) | Medir menu/cold boot e primeiro uso; manter lazy se não houver hitch. |
| 15 | Skins, materiais e texturas cosméticas duplicáveis | RAM/VRAM/CPU | `E/C`: eager load e materiais | [`SkinAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/SkinAsset.cs) | Agrupar Texture/Mesh/Material por conteúdo e referência. |
| 16 | Descoberta/reflection de assemblies de módulos | I/O/CPU/RAM | `E`: load no boot | [`ModuleHook.DiscoverAssemblies`](../Assets/Runtime/Assembly-CSharp/Framework/Modules/ModuleHook.cs) | Cronometrar por DLL; pular módulo desativado com ordem preservada. |
| 17 | Hash de arquivos de recursos | I/O/CPU | `E`: leitura integral sequencial | [`ResourceHash.ThreadInitialize`](../Assets/Runtime/Assembly-CSharp/Unturned/Files/ResourceHash.cs) | Registrar bytes/tempo; cache só com invalidação segura. |
| 18 | Regiões, objetos e culling do mundo | CPU/RAM | `E/C`: visibilidade incremental existente; proxies `Skybox` agora materializados por região | [`LevelObject`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelObject.cs), [`LevelObjects.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelObjects.cs) | Medir objetos evitados e hitch de primeiro acesso; só então avaliar streaming do modelo físico. |
| 19 | Folhagem em grande volume | GPU/CPU/VRAM | `E/C`: tiles, instâncias e shaders | [`FoliageSystem`](../Assets/Runtime/Assembly-CSharp/Framework/Foliage/FoliageSystem.cs), [`Shaders`](../Assets/Game/Sources/Shaders/Framework/Foliage) | Capturar floresta: batches, triângulos, overdraw e sombras. |
| 20 | Terreno/chão de mundo amplo | GPU/CPU/VRAM | `E/C`: quatro amostras inúteis removidas sem neve; custo amplo permanece | [`LevelGround`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelGround.cs), [`Shaders`](../Assets/Game/Sources/Shaders/Landscapes) | Medir GPU antes/depois; separar terreno, detalhes, basemap e uploads por preset. |
| 21 | Iluminação, sombras, fog e reflexos | GPU/CPU | `E/C`: updates e render textures | [`LevelLighting`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelLighting.cs), [`LightingManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/LightingManager.cs) | Medir cada feature GPU; reduzir frequência/distância após captura. |
| 22 | Água transparente e ordenação dinâmica | GPU/CPU | `E/C`: consulta CPU duplicada removida; transparência/reflection/sort permanecem | [`LevelLighting`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelLighting.cs), [`DynamicWaterTransparentSort`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/DynamicWaterTransparentSort.cs), [`Water shader`](../Assets/Game/Sources/Shaders/Water_Fallback) | Medir CPU da consulta, overdraw e sort acima/abaixo d'água. |
| 23 | Clima, chuva, nuvens e relâmpagos | GPU/CPU/RAM | `E/C`: updates e efeitos | [`CustomWeatherComponent`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/CustomWeatherComponent.cs), [`Rain`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/Rain.cs) | Capturar tempestade; separar partículas, transparência e iluminação. |
| 24 | Decals acumulados | GPU/CPU/RAM | `E/C`: sistema/shaders próprios | [`DecalSystem`](../Assets/Runtime/Assembly-CSharp/Unturned/Decals/DecalSystem.cs), [`Shaders`](../Assets/Game/Sources/Shaders/Decals) | Contar visíveis/ativos; aplicar lifetime/distância se confirmado. |
| 25 | Pós-processamento em resolução de tela | GPU/VRAM | `E/C`: fog, blur, vignette | [`UnturnedPostProcess`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/UnturnedPostProcess.cs), [`CustomPostProcess`](../Assets/Runtime/Assembly-CSharp/CustomPostProcess) | Comparar GPU ligado/desligado por resolução. |
| 26 | Escopo renderiza cena adicional | GPU/VRAM/CPU | `E`: câmera e `RenderTexture` | [`PlayerLook`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerLook.cs), [`SrScope`](../Assets/Runtime/Assembly-CSharp/CustomPostProcess/SrScope.cs) | Medir resolução, culling, sombras e single/dual render. |
| 27 | Partículas/mythics atualizam por instância | GPU/CPU/RAM | `E/C`: `Update`/`LateUpdate` | [`MythicalEffectController`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/MythicalEffectController.cs) | Capturar combate denso; pausar invisíveis se confirmado. |
| 28 | Lógica por zumbi | CPU/física/GC | `E/C`: manager e `Zombie.OnUpdate` | [`ZombieManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/ZombieManager.cs), [`Zombie`](../Assets/Runtime/Assembly-CSharp/Unturned/Zombies/Zombie.cs) | Separar percepção, movimento, ataque, animação e rede. |
| 29 | Navegação/repath de zumbis | CPU/física | `E/C`: regiões e consultas de mundo | [`ZombieRegion`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/ZombieRegion.cs), [`Zombie`](../Assets/Runtime/Assembly-CSharp/Unturned/Zombies/Zombie.cs) | Medir agentes, chamadas, duração, nós e falhas por tick. |
| 30 | Loop global de veículos | CPU/rede | `E/C`: manager por veículo | [`VehicleManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/VehicleManager.cs) | Separar replicação, decay e relevância. |
| 31 | Física de veículos, rodas, suspensão e trens | CPU/física | `E/C`: `FixedUpdate` extenso | [`InteractableVehicle`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/InteractableVehicle.cs), [`Wheel`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/Wheel.cs) | Medir custo por veículo/roda ativa e distante. |
| 32 | Áudio de motor atualiza por veículo/frame | CPU/áudio | `E/C`: três controladores com `Update` | [`DefaultEngineSoundController`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/DefaultEngineSoundController.cs), [`RealisticEngineSoundController`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/RealisticEngineSoundController.cs) | Medir frota; reduzir frequência sem mudança de parâmetros. |
| 33 | Lógica por animal | CPU/física | `E/C`: manager e `Animal.Update` | [`AnimalManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/AnimalManager.cs), [`Animal`](../Assets/Runtime/Assembly-CSharp/Unturned/Animals/Animal.cs) | Separar IA, path, animação e rede. |
| 34 | Barricadas: estado, regiões e rede | CPU/RAM/rede | `E/C`: manager grande | [`BarricadeManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/BarricadeManager.cs) | Capturar base densa; medir regiões/estados sujos. |
| 35 | Estruturas: estado, regiões e rede | CPU/RAM/rede | `E/C`: manager grande | [`StructureManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/StructureManager.cs) | Capturar construção densa; medir por região/visibilidade. |
| 36 | Recursos do mapa e respawn | CPU/RAM | `E/C`: manager e spawnpoints | [`ResourceManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/ResourceManager.cs) | Medir floresta; escalonar respawn/visibilidade. |
| 37 | Itens soltos: regiões, despawn e rede | CPU/RAM/rede | `E/C`: manager contínuo | [`ItemManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/ItemManager.cs) | Medir total, relevante e processado/frame. |
| 38 | Objetos interativos e polling de estado | CPU/física/rede | `E/C`: manager/componentes | [`ObjectManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/ObjectManager.cs) | Localizar tipos dominantes; usar estado sujo/evento existente. |
| 39 | Loop global de jogadores | CPU/rede | `E/C`: manager por conexão | [`PlayerManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/PlayerManager.cs) | Medir serialização, relevância e custo/jogador. |
| 40 | Transporte e callbacks de rede | CPU/rede/GC | `E/C`: `Update`/`FixedUpdate` | [`Provider`](../Assets/Runtime/Assembly-CSharp/Unturned/Provider/Provider.cs) | Medir bytes, mensagens, callbacks e alloc por pacote. |
| 41 | Movimento/controller do jogador | CPU/física | `E/C`: update contínuo | [`PlayerMovement.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerMovement.cs) | Separar controller, raycasts, stance e replicação. |
| 42 | Câmera/look/animação do modelo | CPU/GPU | `E/C`: update/late update | [`PlayerLook`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerLook.cs), [`PlayerAnimator`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerAnimator.cs) | Medir Animator, IK, câmera e renderers em multidão. |
| 43 | Input em `Update` e `FixedUpdate` | CPU/GC | `E/C`: dois loops contínuos | [`PlayerInput`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerInput.cs) | Medir idle/combate; remover trabalho duplicado entre ticks. |
| 44 | Equipamento, armas e melee | CPU/física/GC | `E/C`: loops extensos | [`PlayerEquipment`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerEquipment.cs), [`UseableGun`](../Assets/Runtime/Assembly-CSharp/Unturned/Useable/UseableGun.cs), [`UseableMelee`](../Assets/Runtime/Assembly-CSharp/Unturned/Useable/UseableMelee.cs) | Separar balística, animação, áudio, raycasts e rede. |
| 45 | Interação/sentry fazem consultas físicas | CPU/física | `E/C`: raycasts/overlaps frequentes | [`PlayerInteract`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerInteract.cs), [`InteractableSentry`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/InteractableSentry.cs) | Contar consultas/hits; revisar mask, distância e frequência. |
| 46 | UI do jogador atualiza continuamente | CPU/GC/GPU | `E/C`: `PlayerUI.Update` amplo | [`PlayerUI`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerUI.cs) | Medir rebuilds, strings e alloc/frame; atualizar por evento. |
| 47 | Preview de personagem/roupas no menu | CPU/GPU/RAM | `E/C`: updates e modelos | [`Characters`](../Assets/Runtime/Assembly-CSharp/Unturned/Menu/Characters.cs), [`MenuSurvivorsClothing`](../Assets/Runtime/Assembly-CSharp/Unturned/Menu/MenuSurvivorsClothing.cs) | Capturar menu idle; suspender quando invisível. |
| 48 | Buoyancy e projéteis em física fixa | CPU/física | `E/C`: componente por objeto | [`Buoyancy`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/Buoyancy.cs), [`Throwables`](../Assets/Runtime/Assembly-CSharp/Unturned/Throwables) | Medir quantidade, consultas e lifetime por tipo. |
| 49 | Voz e áudio espacial por jogador | CPU/áudio/rede/GC | `E/C`: `PlayerVoice.Update` | [`PlayerVoice`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerVoice.cs) | Medir encode/decode, buffers, alloc e fontes audíveis. |
| 50 | Distância, LOD e luzes distribuídas | CPU/GPU | `E/C`: manager/componentes por objeto | [`LODGroupManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/LODGroupManager.cs), [`LightLOD`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LightLOD.cs) | Medir antes; definir budgets por categoria para render distance. |

## Interpretação

Itens `1–17`: prioridade imediata por afetarem boot/loading e possuírem evidência direta ou caminho estático claro. Itens `18–50`: captura de gameplay decide posição real; otimização sem medição pode alterar sistema barato.

Melhorias já concluídas não entram como agressores separados: busy-spin do `AssetsWorker`, cópia integral para hash/master bundles, geração antecipada de 27 `Auto_Skybox`, loads eager de efeitos/mythics no boot, cleanup intermediário inútil, segunda travessia dos volumes de água e amostras de máscaras do terreno sem neve foram removidos. Sistemas restantes exigem benchmark após cold restart e capturas de gameplay.

## Captura necessária

Nenhuma ferramenta nova necessária na primeira rodada. Unity Profiler e Memory Profiler já cobrem CPU, GPU, RAM, GC, I/O e objetos Unity.

1. Gerar Development Build standalone com Autoconnect Profiler e `Deep Profiling` desligado.
2. Capturar três boots frios e três quentes até menu controlável.
3. Capturar 300–600 frames em menu idle, cidade, floresta, horda, combate e frota de veículos.
4. Salvar CPU Timeline, GPU Usage, Rendering, Memory, Physics, Audio, Network e File Access.
5. Tirar snapshots no menu, pico do mapa, gameplay e após disconnect.
6. Comparar p50/p95/p99, main/render thread, GC alloc, RAM, VRAM, draw calls e física.
7. Instalar Profile Analyzer somente após existirem capturas repetíveis para comparação agregada.
8. Usar dotTrace Timeline somente se managed CPU, locks, GC ou I/O continuarem ambíguos.

Candidato `C` vira medido `M` quando captura reproduzível registra custo próprio/children time, cenário, hardware e variação de pelo menos três execuções.

## Ordem proposta

1. Instrumentar fases dos itens `1–10` sem mudar comportamento.
2. Explicar pico de objetos do item `2` antes de criar cache persistente.
3. Converter eager loads `11–15` individualmente; medir boot, mapa e primeiro uso.
4. Capturar gameplay; reordenar itens `18–50` por custo medido.
5. Otimizar um agressor por alteração; registrar antes/depois em `README.md` e progresso em `TODO.md`.
