# Top 50 Agressores de Performance

Data: `2026-07-14`.

Ranking inicial para orientar profiling. Não representa porcentagem exata: repositório não contém captura `.data`, `.raw`, `.snap` ou dotTrace. Logs do Unity Editor confirmam tempos e contagem de objetos; outras posições usam análise estática de frequência, fan-out e volume potencial.

## Legenda

- `M`: medido em log atual.
- `E`: evidência estática no código ou conteúdo.
- `C`: candidato; captura precisa confirmar custo e posição.
- Recursos: CPU, RAM, GPU, I/O, GC, física, áudio ou rede.

## Baseline disponível

### Captura standalone Unity Profiler — `2026-07-16`

- `600` frames pós-loading no mapa Russia, preset High: CPU p50 `6,73 ms`, p95 `8,55 ms`, p99 `10,46 ms`.
- GPU não foi capturada; esta sessão não permite afirmar GPU-bound nem medir ganho de transferência CPU/GPU.
- Estado de render: `2.354` draw calls, `528` batches, `243` SetPass, `254 mil` triângulos e `144 B/frame` de GC.
- Maiores custos controláveis: `RenderDeferred.GBuffer` `1,236 ms`, `ExecuteRenderQueueJob` `1,129 ms`, `UpdateRendererBoundingVolumes` `0,626 ms`, `CalculateLODJob` `0,591 ms`, `Shadows.RenderJobDir` `0,551 ms` e pós-processamento `0,452 ms`.
- `Profiler.FlushMemoryCounters` adicionou `1,37 ms` de overhead. Pico de `45 ms` veio de `SetPlayerFocus/FindObjectsOfType` ao trocar foco da janela, não do gameplay normal.
- Mão/viewmodel não apareceu durante captura. Nenhuma conclusão nem alteração foi feita em câmera, FOV, viewmodel ou render path.
- Ação segura aplicada: pausar `LODGroup` de objetos fora da visibilidade regional, convergir filas regionais em dois passos pequenos por frame e remover sombras somente do último LOD exclusivo.
- HLOD, mesh combine em runtime e instancing genérico permanecem pendentes: exigem Frame Debugger, conteúdo homogêneo e comparação visual antes de mudar produção.

### Captura standalone Unity Profiler — `2026-07-17`

- Cenário: `20–30 s` parado em local pesado e caminhada breve. Mapa, preset e rota exata não foram registrados; não comparar distribuição com a baseline anterior.
- Frames selecionados: CPU `10,305–11,370 ms`; GPU `3,586–5,390 ms`. CPU continua limitando frame; GPU tem margem. Para `200 FPS`, CPU precisa ficar próximo de `5 ms` por frame no mesmo cenário.
- CPU: `PostLateUpdate.FinishFrameRendering` `5,67 ms`, `Update.ScriptRunBehaviourUpdate` `2,20 ms` e `PostLateUpdate.PlayerFireEndFrame` `1,65 ms`. `PlayerFireEndFrame` é estágio interno da Unity, não prova custo do personagem jogador.
- GPU: `Camera.Render` `3,993 ms`; `Render.OpaqueGeometry` `2,665 ms`; `RenderDeferred.GBuffer` `1,266 ms`; `RenderDeferred.Lighting` `0,794 ms`; sombras `0,427 ms`; reflexos `0,445 ms`; pós-processamento `0,606 ms`.
- Render: `352` SetPass, `2,4k` draw calls, `626` batches, `246,3k` triângulos, `360,7k` vértices e `773` shadow casters. Triângulos não são agressor principal nesta cena; submissão, materiais/passes, scripts e sombras são candidatos melhores.
- Instancing está ativo: `13` batches instanced e `4,4k` draw calls agrupados. Static batching continua dominante (`282` batches); não desligar batching globalmente.
- Memória gráfica observada: `225` RenderTextures / `399,8 MB` e `11.718` buffers / `1,33 GB`. Estes números exigem Memory Profiler antes de qualquer corte: representam recursos gráficos/driver, não RAM gerenciada diretamente.

### Deep Profile atribuído por ferramenta — `2026-07-17`

Captura `Unturned_2026-07-17_17-42-39.data` (`1,6 GB`, `352` frames, Editor Play Mode com Deep Profile) processada por `Window > Unturned > Analyze Profiler Capture`; CSV completo ao lado da captura. Números absolutos são inflados pelo Editor/Deep Profile; contagens de chamadas são exatas. Principais atribuições de `ScriptRunBehaviourUpdate` e GC:

- `FoliageCoord.Equals`: `~9.900` chamadas/frame para `~1.200` `Dictionary.FindEntry`/frame — `~8` comparações por lookup provaram colisão de hash (`x ^ y`). Cluster somava `~3 ms`/frame em deep profile incluindo `GenericEqualityComparer.Equals` (`3,9 M` chamadas na captura). **Corrigido: hash espacial em cinco structs de coordenada.**
- `Animation.CrossFade`: `~99` chamadas nativas/frame — zombies reaplicavam clip de move/idle todo update. **Corrigido: cache de loop com invalidação nos one-shots.**
- `PhysicsTool.GetMaterialName`: `~522` chamadas/frame com veículos — cada roda amostrava splatmap (`getSplatmapHighestWeightLayerIndex` `~114`/frame) e resolvia NetId por string a cada passo. **Corrigido: cache por collider/ponto com limiar de `1 m`.**
- `VolumeManager.GetRegionalAndDynamicVolumes` + variantes: `~400` consultas/frame (`~0,8 ms` self em deep profile), maioria vinda de `WaterUtility.isPointUnderwater` (`~278`/frame, rodas/buoyancy). Aberto: exigiria cache por região ou frequência reduzida; medir após fix das rodas.
- GC por frame no Editor: `FoliageStorageV2.DeserializeTileOnMainThread → List.AddWithResize` `~1,4 KB`/frame (pool já preserva capacity; churn vem de listas novas em streaming — reavaliar com Memory Profiler), `GetComponentNullErrorMessage` `~460 B`/frame (custo só de Editor), `Zombie.findTargetWhileStuck` `~168 B`/frame, `Provider.Update` self `~363 B`/frame sem alocação óbvia no corpo — reatribuir em captura standalone.
- `Wheel.UpdateGrounded` `~295`/frame, `InteractableVehicle.FixedUpdate` `~87`/frame, `Zombie.OnUpdate` `96`/frame e `Buoyancy.FixedUpdate` `~11`/frame seguem como maiores consumidores de script após os fixes; nova captura decide próximo alvo.

#### Próximas ações, sem alteração de código

1. Fazer Deep Profile por `5–10 s` somente no mesmo ponto e expandir `Update.ScriptRunBehaviourUpdate`; decidir scripts por `Self ms` e `GC Alloc`.
2. No Frame Debugger, identificar grupos que somam os `352` SetPass: material, shader pass, mesh, shadow caster e razão de não usar instancing/static batching.
3. Capturar Memory Profiler após carregamento e após `10 min`; comparar RenderTextures, buffers, meshes e texturas por referência.
4. Repetir rota idêntica três vezes em Release e Development, registrar p50/p95/p99. Aceitar mudança apenas se CPU frame time e SetPass reduzirem sem perda visual High/Ultra.

Prioridade atual: atribuir `ScriptRunBehaviourUpdate` e `FinishFrameRendering`; depois reduzir SetPass/sombras por grupo real. Não migrar para Forward/URP, não desabilitar sombras globais e não reescrever meshes com esta captura.

| Métrica | Resultado | Limite |
| --- | ---: | --- |
| Primeiro carregamento de assets | `76,9569 s` | Unity Editor |
| Carregamentos quentes | `11,8790–12,1740 s` | Mesma sessão |
| Diferença cold/warm | `~6,4x` | Inclui JIT e caches do processo/filesystem |
| Abertura de `core.masterbundle` | `5,95–6,13 s` | Quase constante em cold/warm |
| Objetos carregados no menu | `~67.958–68.000` | Log Unity |
| Pico no loading de mapa | `1.043.752` objetos | Antes do cleanup |
| Após cleanup do mapa | `86.958` objetos | Acima do menu |

### Captura standalone `Builds/perf.csv`

- `262` janelas: startup `12`, menu `6`, jogo `244`.
- Gameplay após `60 s`: FPS mediano `103,2`, frame médio `9,663 ms`, p95 `11,220 ms`, p99 `12,417 ms`.
- Main thread mediana `9,567 ms`; GPU mediana `3,433 ms`. CPU domina cenário capturado.
- Loading: frame máximo `11.583,790 ms`, main thread `11.678,792 ms`, alocação máxima `1.577.748,555 KiB`.
- Gameplay aquecido: frame máximo `470,161 ms`, main thread `513,293 ms`, GPU `4,559 ms`, alocação `1,496 KiB` no mesmo pico.
- Interpretação: loading e hitches de gameplay são problemas separados. CSV confirma magnitude, não método responsável; CPU Timeline precisa atribuir custo.

### Contaminação externa da baseline

- `C:\Program Files (x86)\Steam\steamapps\common\Unturned\Bundles\ORIGINAL_ASSETS` contém `80.156` arquivos/`1,38 GB`. Scanner tratou export Unity como bundle/mod e registrou `441.919` erros de parse no `Player.log`, que chegou a `706 MB`.
- Esta pasta aumenta boot, I/O, alocações e ruído do Profiler. Mover para fora de `Unturned/Bundles`, `Maps` e `Sandbox`; depois apagar logs antigos e repetir cold start. Não comparar números capturados com pasta presente contra números sem ela.

### Captura Unity Editor — render

- Frame observado: CPU `12,85 ms`, GPU `5,09 ms`; existe margem de GPU e gargalo de CPU.
- Screenshot veio do módulo `GPU Usage`: `Render.OpaqueGeometry` usa `2,049 ms` de GPU/`397` draws; `RenderDeferred.GBuffer`, `BatchRenderer.Flush` e `Batch.DrawStatic` são subdivisões desse custo GPU.
- Interpretação corrigida: frame está CPU-bound, mas screenshot não identifica causa CPU. Capturar `CPU Usage > Timeline` no standalone e abrir Main Thread, Render Thread e Workers antes de atribuir custo a culling/submissão.

## Top 5 mudanças de maior potencial

Ranking de iniciativas amplas, não de métodos isolados. Itens `1–2` têm medição atual; itens `3–5` são candidatos e podem mudar de ordem após captura standalone.

| # | Mudança | Implementação proposta | Ganho esperado | Evidência e trava |
| ---: | --- | --- | --- | --- |
| 1 | Catálogo de assets em duas fases | Carregar índice, GUID, tipo e configuração necessária primeiro; abrir prefabs, materiais, áudio e outros payloads somente no primeiro uso. Adicionar manifesto persistente apenas após definir invalidação por versão e arquivo alterado. | Boot, RAM e I/O | `M/E`: cold start de `76,96 s`, cerca de `68 mil` objetos no menu e inventário inicial de 67 loads eager em 25 tipos. Fatias aplicadas a `ItemAsset`, `VehicleAsset`, projéteis de armas/magazines e outros tipos; restante continua aberto. |
| 2 | Streaming regional do mundo | Manter manifesto leve por chunk; instanciar regiões próximas e direção de movimento com orçamento por frame; descarregar anéis distantes sem `GC.Collect` forçado. | Loading, RAM, GC e picos de frame time | `M`: pico de `1.043.752` objetos durante loading. Proxies `Skybox` são lazy; limpeza de seis sistemas examina anel anterior em vez das 4.096 regiões. Objetos físicos ainda aguardam Memory Profiler. |
| 3 | Pipeline único de visibilidade e LOD | Reutilizar regiões para frustum/occlusion culling, LOD e distância por categoria; aplicar instancing, impóstores e orçamento separado para folhagem, terreno, água, luzes e sombras. | GPU, VRAM e CPU de render | `M/E/C`: CPU `12,85 ms` contra GPU `5,09 ms` confirma limite CPU, sem método atribuído; `World_Chunk_Radius` já limita far clip, objetos, árvores, estradas e IA distante. Captura standalone ainda precisa medir raios `8/4/2`. |
| 4 | Simulação e pathfinding por relevância | Atualizar entidades próximas em alta frequência, médias em frequência reduzida e distantes somente por evento; limitar repaths por tick e usar rota hierárquica/cache por versão quando medição justificar. | CPU, física e tick do servidor | `E/C`: zombies/animais distantes já pausam e budgets dedicados agora são configuráveis em `50/25` por padrão. Repath separado continua aberto até CPU Timeline medir custo e latência. |
| 5 | Replicação de rede por interesse e estado sujo | Usar mesmas regiões para relevância; enviar somente deltas alterados, agrupar mensagens pequenas e impor budgets/backpressure por conexão e sistema. | CPU do servidor, banda e escalabilidade | `E/C`: limpeza de flags usa anel anterior e snapshot cacheia permissão por destinatário. Deltas, budgets e backpressure continuam abertos até Network Profiler e load test. |

Ordem segura: atacar `1`, explicar pico de `2`, depois medir e reordenar `3–5`. Reescrita completa sem baseline não entra no plano.

### Pesquisa Unity: próximos testes, não defaults

- Projeto foi migrado para Unity 6.3 LTS `6000.3.19f1`; `2022.3.62f3` permanece baseline comparativa. Compilação externa está sem erros, mas smoke test, builds Unity e novo perfil ainda precisam confirmar migração; upgrade isolado não será creditado como otimização.
- CefSharp não existe como dependência encontrada no runtime. Atualização de CEF do Editor não entra em benchmark do Player.
- Remover Win32 reduz manutenção e permite padronizar distribuição 64-bit, mas não aumenta FPS Win64. DX12 deve entrar antes de DX11 com fallback e A/B; tornar DX12 mínimo sem telemetria de hardware é perda de compatibilidade sem ganho comprovado.
- Built-in RP prioriza static batching sobre GPU instancing. Variantes essenciais e materiais Standard dinâmicos agora permitem instancing sem remover static batching. Próximo experimento: confirmar `Draw Mesh (instanced)` no Frame Debugger e só excluir do static combine grupos numerosos com mesmo mesh/material se medição vencer. Não desligar batching global. [Unity 6 — draw calls](https://docs.unity3d.com/6000.0/Documentation/Manual/optimizing-draw-calls-choose-method.html)
- Static batching pode aumentar memória por cópias de geometria transformada. Medir RAM, batches, SetPass, Main/Render Thread e GPU no mesmo frame antes/depois. [Unity 2022.3 — static batching](https://docs.unity3d.com/2022.3/Documentation/Manual/static-batching.html)
- GPU Resident Drawer e GPU Occlusion não existem no Built-in RP. Atualizar engine mantendo pipeline não libera esses ganhos; exigem protótipo separado em URP/Forward+ e conversão de shaders/água/terreno. [Comparação oficial de pipelines](https://docs.unity3d.com/Manual/render-pipelines-feature-comparison.html)
- Baked Occlusion não é solução direta para mapas instanciados em runtime. Só testar por células estáticas preparadas e após CPU Timeline mostrar culling relevante. [Unity 2022.3 — Occlusion Culling](https://docs.unity3d.com/2022.3/Documentation/Manual/OcclusionCulling.html)
- `vercidium-patreon/meshing` é greedy meshing para mundo voxel. Unturned usa Unity Terrain por heightmap e modelos autorais; copiar algoritmo não reduz estes meshes. Só se aplica a futura reescrita voxel, não à otimização básica atual. [Projeto de referência](https://github.com/vercidium-patreon/meshing)
- Auditoria de importação continua válida para assets-fonte: `Read/Write` duplica textura/mesh em CPU e GPU, e dados de vértice não usados podem ser removidos. Não alterar master bundles às cegas; listar top objetos do Memory Profiler e testar por família. [Guia oficial Unity](https://unity.com/how-to/project-configuration-and-assets)
- Physics broadphase, Forward/Deferred e Dynamic Batching são testes A/B, não alterações automáticas. Só manter opção que reduzir p95/p99 sem regressão visual ou de colisão.
- Terreno já usa tiles e `World_Chunk_Radius`. Próximo experimento mínimo para água: reutilizar regiões de `128 m`, ativar somente tiles no raio/frustum e atualizar reflexão planar somente para superfície relevante. Pré-culling sob terreno precisa preservar câmera submersa, cavernas e holes.
- Corte fora do limite de dados pode poupar tiles extras; corte na borda jogável pode remover conteúdo válido fora dela. Implementar somente como opção client-side, com fechamento visual e override por mapa; servidor continua tratando simulação/rede separadamente.

## Sugestões de lógica de jogo

1. Unificar relevância espacial: mesma classificação `próximo/médio/distante/inativo` deve alimentar render, IA, física, áudio e rede. Evita cinco sistemas decidindo distâncias diferentes.
2. Separar simulação de apresentação: servidor mantém regras críticas em tick fixo; animação, partículas, áudio e UI interpolam e podem atualizar em frequência menor.
3. Dormir por estado: entidade parada, sem alvo, fora de interesse e sem timer crítico sai de polling; eventos de dano, proximidade, spawn ou mudança de estado reativam.
4. Orçar pathfinding: fila por ameaça/distância, teto de repaths por tick, cooldown com histerese e cancelamento de destino obsoleto. Cache de rota só entra com versão de navmesh para invalidação correta.
5. Replicar estado sujo: componente marca campos alterados; snapshot envia delta apenas a clientes relevantes. Evento crítico continua confiável e imediato; movimento usa canal não confiável com snapshot periódico completo.
6. Usar índices regionais em consultas: ataques, sentries, spawns, itens e efeitos consultam células próximas, não listas globais. Índice existente deve ser reutilizado antes de criar estrutura nova.
7. Aplicar budgets por frame/tick: streaming, instanciação, destruição, uploads e limpeza param ao atingir tempo definido e continuam depois. Prioridade usa distância e ameaça.
8. Medir custo por entidade e jogador: contadores baratos de ativos, processados, repaths, bytes e filas permitem adaptar frequência sem adivinhar.

## Alterações aplicadas após baseline

- `EffectAsset`: prefab e splatters agora usam `Bundle.loadDeferred`; 176 assets oficiais deixam de carregar visuais no boot.
- `MythicAsset`: quatro prefabs de 81 assets agora carregam no primeiro acesso.
- Cleanup intermediário do mapa medido em `256,43 ms` para recuperar `76,6 KB`; código atual preserva este e o cleanup final até Memory Profiler provar remoção segura.
- Água: `LevelLighting` consulta volumes uma vez por frame para estado submerso e superfície; antes fazia duas travessias.
- Terreno: variantes sem neve dos seis passes removem quatro amostras de máscaras por fragmento; variante de neve permanece igual.
- `ItemAsset`: prefab principal, animações e três texturas base de skin agora usam carregamento sob demanda em master bundles.
- `SkinAsset`: materiais Primary, Secondary, Attachment e Tertiary agora carregam no primeiro uso; meshes de override permanecem eager por compatibilidade.
- `LightingManager`: condições ligadas a horário são notificadas quando o segundo do ciclo muda; blend visual continua por frame.
- `Zombie.OnUpdate`: compartilha uma leitura de relógio/delta por callback; `Animal.tick` compartilha uma leitura de relógio por tick.
- `LightLOD`: estados estáveis próximos/distantes verificam distância a cada oito frames; faixa de fade permanece por frame.
- `LevelObject`: proxy `Skybox` passa a ser criado no primeiro acesso ou ativação regional; editor e batching preservam materialização necessária.
- Foliage: clutter usa um tile a menos que distância geral por preset; opção desligada deixa de copiar matrizes descartadas e nenhum foliage passa de `128 m` do jogador.
- Sombras: draw distance reduzido também reduz alcance de sombras; Lighting Low/Medium deixam clutter distante fora do shadow pass após `32/64 m`. High/Ultra preservam `128 m` e distância máxima original.
- Reflexos: atualização automática de reflection probes fica desligada somente em Lighting Off/Low; presets visuais superiores permanecem iguais.
- Armas/magazines: prefabs de projétil em master bundles carregam no primeiro acesso.
- Modelos: LOD bias base reduzido de `[2,5]` para `[0,75,2]`; players e zombies client pausam animação legacy quando nenhum renderer está visível.
- Loading/GC: leitura comum de GUID em `River` reutiliza buffer e elimina array temporário por identificador de objeto/material.
- Streaming: filas de itens, barricadas e estruturas preservam prioridade próxima e budgets, mas agora removem cauda em O(1), sem deslocar toda fila pendente.
- Editor: modo opt-in limita far clip a `768 m`, reduzindo renderers distantes enviados ao culling e GBuffer; build não contém mudança.
- Chunks: configuração server-side limita janela visual e pausa animais/zombies fora do interesse de qualquer jogador; itens e estruturas preservam regiões nativas.
- Terreno: heightmaps de tiles fora do raio deixam de renderizar; scan ocorre por região e mantém collider/dados carregados.
- Loading de terreno: tiles serializados pulam uploads temporários de heightmap/splatmap; defaults continuam disponíveis para arquivos ausentes.
- Fog: opção gráfica local controla fog atmosférico/barreira; início da barreira fica nos últimos 20% do raio.
- Simulação: respawn normal de animais/zombies ignora áreas sem jogadores; Horde e beacon permanecem ativos.
- IA: budgets de tick no dedicado deixam de ser constantes; `Zombies.Tick_Budget_Per_Frame=50` e `Animals.Tick_Budget_Per_Frame=25` preservam comportamento anterior e podem ser reduzidos após profiling.
- Água: fog testa volumes candidatos do índice espacial, não lista global por câmera/render.
- Hashes de coordenadas: `FoliageCoord`, `LandscapeCoord`, `HeightmapCoord`, `SplatmapCoord` e `RegionCoord` substituem `x ^ y` por hash sem colisões estruturadas; dicionários regionais/foliage/splatmap deixam de comparar `~8` chaves por lookup.
- Zombies: `CrossFade` de move/idle só executa em troca real de clip; one-shots invalidam cache. `~99` chamadas nativas por frame viram no máximo uma por transição.
- Veículos: material do solo por roda é reutilizado até contato mover `1 m` ou trocar collider, eliminando amostragem de splatmap e lookup por string por passo de física; cache cobre também o caminho visual do cliente (`UpdateModel`) e replicação sem motorista lê `linearVelocity` uma vez.
- Volumes: `GetFirstOverlappingVolume` itera fontes diretamente sem lista temporária por consulta; água/fog/oxygen herdam.
- Simulação: `Zombie.OnUpdate` e `Animal.Update` leem posição/yaw do transform uma vez por update; conversão quaternion→euler cai para no máximo uma por entidade.
- UI: stat tracker reformata somente na mudança de kills/tipo; efeito mythic sincroniza uma vez por frame em `LateUpdate`.
- Memória: alerta do loading veio de memória paginada do sistema em `94%`; Unity ocupava `6,25 GB` e cleanup não reduziu total. 5.911 texturas/`1,88 GB` e 6.906 meshes/`1,43 GB` permanecem referenciadas.
- Render atual: Deferred GBuffer/Lighting, `833` shadow casters, 316 RenderTextures/`257,6 MB` e 9.675 buffers/`0,71 GB` precedem otimização dos `177,3k` triângulos.
- Resultado antes/depois ainda precisa de cold boot e loading do mesmo mapa; itens ficam parcialmente abertos até nova captura.

## Ranking atual

Ordem considera impacto potencial, abrangência e evidência. Linha `M` confirma fato observado, não atribuição integral ao método indicado.

| # | Agressor | Recurso | Evidência | Alvo | Próxima ação mínima |
| ---: | --- | --- | --- | --- | --- |
| 1 | Catálogo completo no cold start | CPU/RAM/I/O | `M/E`: `76,96 s` frio, `~12 s` quente | [`Assets.LoadAssetsFromWorkerThread`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Marcar busca, parse, criação Unity e link; lazy-load por tipo. |
| 2 | Pico superior a um milhão de objetos no mapa | CPU/RAM/GC | `M`: `1.043.752` objetos | [`Level`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/Level.cs), [`LevelObjects`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelObjects.cs) | Comparar snapshots antes, no pico e após cleanup; agrupar por tipo/tamanho. |
| 3 | `core.masterbundle` custa cerca de seis segundos | I/O/CPU | `M`: `5,95–6,13 s` | [`Assets`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Separar disco, descompressão, espera e criação de objetos em build standalone. |
| 4 | Cleanup força `UnloadUnusedAssets` e coleta completa | CPU/GC | `M`: intermediário custou `256,43 ms` por `76,6 KB`; código atual mantém dois cleanups por segurança | [`Assets.CleanupMemory`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs), [`Level`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/Level.cs) | Repetir mapa; comparar RAM/pico com ambos e sem intermediário antes de remover. |
| 5 | 67 loads eager em 25 tipos de asset | CPU/RAM/I/O | `E`: inventário estático | [`Bundles`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles) | Priorizar por instâncias/tamanho; adiar conteúdo ausente do menu. |
| 6 | Varredura recursiva de diretórios e Workshop | I/O/CPU | `E`: filesystem inteiro pelo worker | [`AssetsWorker.SearcherThreadMain`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/AssetsWorker.cs) | Contar diretórios, arquivos, bytes e tempo por raiz. |
| 7 | Leitura, parsing e hash de cada `.dat`/`.asset` | CPU/I/O | `E`: custo por arquivo no catálogo | [`AssetsWorker.AddFoundAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/AssetsWorker.cs), [`Assets.TryLoadFile`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Medir hash, parse e construtor separadamente. |
| 8 | Busca de bundles/assets de todos os mapas | I/O/CPU/RAM | `E`: locais adicionados ao catálogo | [`Assets.AddMapSearchLocations`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Avaliar indexação do mapa necessário com regressão de mods/servidor. |
| 9 | Enumeração e validação de todos os mapas | I/O/CPU | `E`: scan em `Level.levels` | [`Level.getLevels`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/Level.cs) | Cache por sessão com invalidação explícita. |
| 10 | Resolução global de spawn tables | CPU/RAM | `E`: passe `linkSpawns` | [`Assets.linkSpawns`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs) | Medir nós/referências; relink incremental para tabelas alteradas. |
| 11 | Payloads derivados de `ItemAsset` ainda podem ser eager | RAM/CPU/GPU | `E`: prefab `Item`, animações, três texturas base e projéteis de armas/magazines agora lazy; derivados adicionais permanecem | [`ItemAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/ItemAsset.cs), [`ItemGunAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/ItemGunAsset.cs), [`ItemMagazineAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/ItemMagazineAsset.cs) | Medir cold boot, primeiro uso e hitch do primeiro projétil; converter próximo derivado somente se dominar captura. |
| 12 | `ResourceAsset` carrega/processa vários prefabs | RAM/CPU/GPU | `E`: 69 recursos; maior contagem eager | [`ResourceAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/ResourceAsset.cs) | Medir prefab/model/skybox; preservar `Auto_Skybox` lazy. |
| 13 | Catálogo/pool de efeitos | RAM/GPU/CPU | `E`: 176 assets agora lazy no boot; preload do mapa permanece | [`EffectAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/EffectAsset.cs), [`EffectManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/EffectManager.cs) | Medir boot e mapa; revisar somente preloads responsáveis por hitch/memória. |
| 14 | Mythics carregam múltiplos prefabs | RAM/GPU/CPU | `E`: quatro prefabs de 81 assets agora lazy | [`MythicAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/MythicAsset.cs) | Medir menu/cold boot e primeiro uso; manter lazy se não houver hitch. |
| 15 | Skins, materiais e texturas cosméticas duplicáveis | RAM/VRAM/CPU | `E/C`: materiais agora lazy em master bundles; override meshes e instâncias de materiais permanecem | [`SkinAsset`](../Assets/Runtime/Assembly-CSharp/Unturned/Bundles/SkinAsset.cs) | Medir boot/RAM e primeiro uso; agrupar materiais instanciados por referência. |
| 16 | Descoberta/reflection de assemblies de módulos | I/O/CPU/RAM | `E`: load no boot | [`ModuleHook.DiscoverAssemblies`](../Assets/Runtime/Assembly-CSharp/Framework/Modules/ModuleHook.cs) | Cronometrar por DLL; pular módulo desativado com ordem preservada. |
| 17 | Hash de arquivos de recursos | I/O/CPU | `E`: leitura integral sequencial | [`ResourceHash.ThreadInitialize`](../Assets/Runtime/Assembly-CSharp/Unturned/Files/ResourceHash.cs) | Registrar bytes/tempo; cache só com invalidação segura. |
| 18 | Regiões, objetos e culling do mundo | CPU/RAM | `E/C`: visibilidade incremental existente; proxies `Skybox` agora materializados por região; filas próximas usam budgets e remoção O(1) | [`LevelObject`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelObject.cs), [`LevelObjects.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelObjects.cs) | Medir backlog, idade, tempo por fila e hitch de primeiro acesso; só então avaliar streaming do modelo físico. |
| 19 | Folhagem em grande volume | GPU/CPU/VRAM | `E/C`: clutter limitado a `1/2/3/4` tiles; instancing/frustum preservados; sombras de clutter limitadas a `32/64/128/128 m` em Lighting Low/Medium/High/Ultra | [`FoliageSystem`](../Assets/Runtime/Assembly-CSharp/Framework/Foliage/FoliageSystem.cs), [`FoliageStorageV2`](../Assets/Runtime/Assembly-CSharp/Framework/Foliage/FoliageStorageV2.cs), [`Shaders`](../Assets/Game/Sources/Shaders/Framework/Foliage) | Capturar floresta antes/depois: batches, shadow casters, CPU/GPU e popping ao cruzar `32/64 m`. |
| 20 | Terreno/chão de mundo amplo | GPU/CPU/VRAM | `E/C`: quatro amostras inúteis removidas sem neve; tiles serializados fazem um upload inicial em vez de default + final | [`LandscapeTile`](../Assets/Runtime/Assembly-CSharp/Framework/Landscapes/LandscapeTile.cs), [`Shaders`](../Assets/Game/Sources/Shaders/Landscapes) | Medir `SetHeightsDelayLOD`, `SetAlphamaps`, GPU e loading no mesmo mapa. |
| 21 | Iluminação, sombras, fog e reflexos | GPU/CPU | `E/C`: sombra agora segue draw/far clip; probes automáticos desligados em Off/Low; High/Ultra preservados | [`LevelLighting`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelLighting.cs), [`LightingManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/LightingManager.cs) | Comparar shadow casters e `RenderDeferred.Lighting` por preset; revisar cascatas somente após captura. |
| 22 | Água transparente e ordenação dinâmica | GPU/CPU | `E/C`: consulta CPU duplicada removida; somente Ultra adiciona uma onda normal procedural e reflexão ambiente nativa, sem câmera planar/RT; transparência e sort permanecem | [`LevelLighting`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LevelLighting.cs), [`DynamicWaterTransparentSort`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/DynamicWaterTransparentSort.cs), [`Water shader`](../Assets/Game/Sources/Shaders/Water_Fallback) | Medir GPU, overdraw e sort em High contra Ultra acima/abaixo d'água. |
| 23 | Clima, chuva, nuvens e relâmpagos | GPU/CPU/RAM | `E/C`: efeitos periódicos não alocam iteradores por atributo; partículas, transparência e iluminação permanecem | [`CustomWeatherComponent`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/CustomWeatherComponent.cs), [`Rain`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/Rain.cs) | Capturar tempestade; medir `GC.Alloc`, partículas, transparência e iluminação. |
| 24 | Decals acumulados | GPU/CPU/RAM | `E/C`: sistema/shaders próprios | [`DecalSystem`](../Assets/Runtime/Assembly-CSharp/Unturned/Decals/DecalSystem.cs), [`Shaders`](../Assets/Game/Sources/Shaders/Decals) | Contar visíveis/ativos; aplicar lifetime/distância se confirmado. |
| 25 | Pós-processamento em resolução de tela | GPU/VRAM | `E/C`: fog, blur, vignette; Sun Shafts usa depth, dois RTs temporários em um quarto/metade da resolução e `1–3` passes | [`UnturnedPostProcess`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/UnturnedPostProcess.cs), [`CustomPostProcess`](../Assets/Runtime/Assembly-CSharp/CustomPostProcess) | Comparar GPU e visual por preset/resolução; conferir oclusão de árvores alpha-cutout. |
| 26 | Escopo renderiza cena adicional | GPU/VRAM/CPU | `E`: câmera e `RenderTexture` | [`PlayerLook`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerLook.cs), [`SrScope`](../Assets/Runtime/Assembly-CSharp/CustomPostProcess/SrScope.cs) | Medir resolução, culling, sombras e single/dual render. |
| 27 | Partículas/mythics atualizam por instância | GPU/CPU/RAM | `E/C`: `Update`/`LateUpdate` | [`MythicalEffectController`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/MythicalEffectController.cs) | Capturar combate denso; pausar invisíveis se confirmado. |
| 28 | Lógica por zumbi | CPU/física/GC | `E/C`: animação client invisível usa culling nativo; normalizações sem efeito foram removidas de flanker e rotação especial; IA restante continua no manager/`Zombie.OnUpdate` | [`ZombieManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/ZombieManager.cs), [`Zombie`](../Assets/Runtime/Assembly-CSharp/Unturned/Zombies/Zombie.cs), [`Zombie_Client`](../Assets/Resources/Characters/Zombie_Client.prefab) | Medir horda visível e fora da câmera; separar percepção, movimento, ataque e rede. |
| 29 | Navegação/repath de zumbis | CPU/física | `C`: implementação ASPFP não está incluída no SDK; build aberto usa fallback sem pathfinding | [`UnturnedPathfinding`](../Assets/Runtime/Assembly-CSharp/Unturned/Pathfinding/UnturnedPathfinding.cs) | Obter/implementar busca real; então medir e aplicar scheduler global com teto de buscas e tempo por tick. |
| 30 | Loop global de veículos | CPU/rede | `E/C`: manager por veículo | [`VehicleManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/VehicleManager.cs) | Separar replicação, decay e relevância. |
| 31 | Física de veículos, rodas, suspensão e trens | CPU/física | `E/C`: `FixedUpdate` extenso | [`InteractableVehicle`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/InteractableVehicle.cs), [`Wheel`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/Wheel.cs) | Medir custo por veículo/roda ativa e distante. |
| 32 | Áudio de motor atualiza por veículo/frame | CPU/áudio | `E/C`: três controladores com `Update` | [`DefaultEngineSoundController`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/DefaultEngineSoundController.cs), [`RealisticEngineSoundController`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/RealisticEngineSoundController.cs) | Medir frota; reduzir frequência sem mudança de parâmetros. |
| 33 | Lógica por animal | CPU/física | `E/C`: `Animal.Update` compartilha relógio entre timers; manager, IA e path permanecem | [`AnimalManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/AnimalManager.cs), [`Animal`](../Assets/Runtime/Assembly-CSharp/Unturned/Animals/Animal.cs) | Separar IA, path, animação e rede. |
| 34 | Barricadas: estado, regiões e rede | CPU/RAM/rede | `E/C`: manager grande | [`BarricadeManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/BarricadeManager.cs) | Capturar base densa; medir regiões/estados sujos. |
| 35 | Estruturas: estado, regiões e rede | CPU/RAM/rede | `E/C`: manager grande | [`StructureManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/StructureManager.cs) | Capturar construção densa; medir por região/visibilidade. |
| 36 | Recursos do mapa e respawn | CPU/RAM | `E/C`: manager e spawnpoints | [`ResourceManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/ResourceManager.cs) | Medir floresta; escalonar respawn/visibilidade. |
| 37 | Itens soltos: regiões, despawn e rede | CPU/RAM/rede | `E/C`: manager contínuo | [`ItemManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/ItemManager.cs) | Medir total, relevante e processado/frame. |
| 38 | Objetos interativos e polling de estado | CPU/física/rede | `E/C`: manager/componentes | [`ObjectManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/ObjectManager.cs) | Localizar tipos dominantes; usar estado sujo/evento existente. |
| 39 | Loop global de jogadores | CPU/rede | `E/C`: manager por conexão | [`PlayerManager.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/PlayerManager.cs) | Medir serialização, relevância e custo/jogador. |
| 40 | Transporte e callbacks de rede | CPU/rede/GC | `E/C`: `Update`/`FixedUpdate` | [`Provider`](../Assets/Runtime/Assembly-CSharp/Unturned/Provider/Provider.cs) | Medir bytes, mensagens, callbacks e alloc por pacote. |
| 41 | Movimento/controller do jogador | CPU/física | `E/C`: update contínuo | [`PlayerMovement.Update`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerMovement.cs) | Separar controller, raycasts, stance e replicação. |
| 42 | Câmera/look/animação do modelo | CPU/GPU | `E/C`: remote player pausa avaliação legacy fora de visibilidade; `HumanAnimator.LateUpdate` compartilha delta; scripts restantes permanecem | [`PlayerLook`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerLook.cs), [`PlayerAnimator`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerAnimator.cs), [`HumanAnimator`](../Assets/Runtime/Assembly-CSharp/Unturned/Characters/HumanAnimator.cs) | Medir multidão visível/oculta; revisar scripts somente se dominarem captura. |
| 43 | Input em `Update` e `FixedUpdate` | CPU/GC | `E/C`: dois loops contínuos | [`PlayerInput`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerInput.cs) | Medir idle/combate; remover trabalho duplicado entre ticks. |
| 44 | Equipamento, armas e melee | CPU/física/GC | `E/C`: validação server-side de alcance balístico agora evita raiz por resultado; loops extensos permanecem | [`PlayerEquipment`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerEquipment.cs), [`UseableGun`](../Assets/Runtime/Assembly-CSharp/Unturned/Useable/UseableGun.cs), [`UseableMelee`](../Assets/Runtime/Assembly-CSharp/Unturned/Useable/UseableMelee.cs) | Separar balística, animação, áudio, raycasts e rede. |
| 45 | Interação/sentry fazem consultas físicas | CPU/física | `E/C`: cone evita raiz/normalização; distância e `cos` são calculados por disparo; timers compartilham relógio por update; raycasts permanecem | [`PlayerInteract`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerInteract.cs), [`InteractableSentry`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/InteractableSentry.cs) | Contar consultas/hits e medir sentries multi-pellet; revisar mask e frequência somente com captura. |
| 46 | UI do jogador atualiza continuamente | CPU/GC/GPU | `E/C`: `PlayerUI.Update` amplo | [`PlayerUI`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerUI.cs) | Medir rebuilds, strings e alloc/frame; atualizar por evento. |
| 47 | Preview de personagem/roupas no menu | CPU/GPU/RAM | `E/C`: updates e modelos | [`Characters`](../Assets/Runtime/Assembly-CSharp/Unturned/Menu/Characters.cs), [`MenuSurvivorsClothing`](../Assets/Runtime/Assembly-CSharp/Unturned/Menu/MenuSurvivorsClothing.cs) | Capturar menu idle; suspender quando invisível. |
| 48 | Buoyancy e projéteis em física fixa | CPU/física | `E/C`: tempo de onda, modo client/server e damping são compartilhados por passo; consulta de água e força por voxel permanecem | [`Buoyancy`](../Assets/Runtime/Assembly-CSharp/Unturned/Interactable/Buoyancy.cs), [`Throwables`](../Assets/Runtime/Assembly-CSharp/Unturned/Throwables) | Medir quantidade, voxels, consultas e lifetime por tipo. |
| 49 | Voz e áudio espacial por jogador | CPU/áudio/rede/GC | `E/C`: `PlayerVoice.Update` | [`PlayerVoice`](../Assets/Runtime/Assembly-CSharp/Unturned/Player/PlayerVoice.cs) | Medir encode/decode, buffers, alloc e fontes audíveis. |
| 50 | Distância, LOD e luzes distribuídas | CPU/GPU | `E/C`: base LOD `[0,75,2]`; luz estável próxima/distante distribuída; assets sem `LODGroup` não ganham geometria reduzida | [`GraphicsSettings`](../Assets/Runtime/Assembly-CSharp/Unturned/Settings/GraphicsSettings.cs), [`LODGroupManager`](../Assets/Runtime/Assembly-CSharp/Unturned/Managers/LODGroupManager.cs), [`LightLOD`](../Assets/Runtime/Assembly-CSharp/Unturned/Level/LightLOD.cs) | Medir triângulos/SetPass antes-depois; criar LOD offline somente nos modelos dominantes. |

## Interpretação

Itens `1–17`: prioridade imediata por afetarem boot/loading e possuírem evidência direta ou caminho estático claro. Itens `18–50`: captura de gameplay decide posição real; otimização sem medição pode alterar sistema barato.

Melhorias já concluídas não entram como agressores separados: busy-spin do `AssetsWorker`, cópia integral para hash/master bundles, geração antecipada de 27 `Auto_Skybox`, loads eager de efeitos/mythics/skin materials no boot, uploads padrão de tiles serializados, segunda travessia dos volumes de água e amostras de máscaras do terreno sem neve foram removidos. Cleanups de mapa continuam presentes até medição de memória autorizar mudança. Sistemas restantes exigem benchmark após cold restart e capturas de gameplay.

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
