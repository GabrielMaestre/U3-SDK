# U3 SDK — Plano de Modernização

## Objetivo

Evoluir código aberto de Unturned com foco mensurável em desempenho, estabilidade, segurança, compatibilidade e capacidade de customização por servidores.

Metas principais:

- reduzir tempo de boot e carregamento de mapas;
- reduzir uso de CPU, memória, GPU, disco e rede;
- eliminar travamentos, picos de frame time, vazamentos e alocações recorrentes;
- recriar pathfinding e navegação de NPCs;
- tornar distância de renderização adaptativa e previsível;
- permitir modelos e hitboxes de jogadores definidos pelo servidor com validação autoritativa;
- criar anticheat em camadas, orientado por evidências;
- melhorar renderização sem quebrar identidade visual ou hardware antigo;
- corrigir bugs com testes de regressão;
- preservar compatibilidade com saves, mapas, mods, plugins, Workshop e servidores existentes.

## Estado atual

- Engine: Unity `2022.3.62f3`.
- Runtime principal: C# em `Assets/Runtime/Assembly-CSharp`.
- Sistemas separados: transporte, pacotes, host bans, UI, utilitários e serialização.
- Dependências úteis já presentes: Burst, Collections, Memory Profiler e Unity Test Framework.
- Aproximadamente 1.955 arquivos C# em `Assets` no início deste plano.

## Melhorias implementadas

### 2026-07-14 — Sombras e foliage por qualidade

- Distância de sombras agora acompanha slider de draw distance entre `50–100%` do limite do preset e nunca ultrapassa `farClipPlane`/raio visível do mundo. Draw distance máximo mantém distância original.
- Clutter instanciado preserva sombras até `32 m` em Lighting Low e `64 m` em Medium. High/Ultra mantêm sombras em toda região de foliage (`128 m`), sem redução visual.
- Batches com e sem sombra reutilizam configurações pré-calculadas por asset; nenhuma criação de material, mesh ou configuração ocorre por instância/frame.
- Reflection probes automáticos foram desligados somente em Lighting Off/Low. Medium/High/Ultra permanecem iguais.
- LOD bias global, cascatas High/Ultra, static batching e render mode do usuário não foram alterados: mudança ampla sem assets LOD ou captura A/B poderia causar popping, mais draws ou regressão visual.
- Validação: `Assembly-CSharp.csproj` compila com 0 erros e 14 warnings preexistentes; `Assembly-CSharp-Editor.csproj` compila com 0 erros. Teste de limite quadrático de sombras de clutter foi adicionado; Test Runner e comparação standalone ainda necessários.

### 2026-07-13 — Loading de master bundles

- Substituído carregamento integral em memória por `AssetBundle.LoadFromFileAsync`.
- Mantido SHA-1 calculado durante leitura para preservar validação de integridade.
- Adicionada leitura sequencial com buffer de 64 KiB.
- Resultado esperado: menor pico de RAM, menos cópias e menor custo de abertura dos bundles.
- Validação: `Assembly-CSharp.csproj` compila com 0 erros; benchmark dentro do jogo ainda necessário.

### 2026-07-13 — Worker de assets, recursos e módulos

- Substituída espera ativa do leitor de assets por `SemaphoreSlim.WaitAsync`.
- Leitores ociosos deixam de consumir CPU e threads são liberadas enquanto busca de diretórios continua.
- Hash de recursos usa leitura sequencial direta, sem `MemoryStream` nem cópia integral temporária.
- Descoberta de módulos enumera DLLs sob demanda e remove log duplicado por assembly.
- Mantidos conteúdo carregado, SHA-1, ordem funcional e suporte a cancelamento no shutdown.
- Validação: invariant check no worker e `Assembly-CSharp.csproj` compilando com 0 erros; benchmark dentro do jogo ainda necessário.

### 2026-07-13 — Auto skybox sob demanda

- Localizados 27 `ResourceAsset` com `Auto_Skybox` executando instanciação, análise de meshes e renderização de ícone antes do menu.
- Geração movida para primeiro acesso durante loading do mapa.
- Resultado esperado: menos CPU, objetos temporários e trabalho gráfico no cold start.
- Compatibilidade preservada: primeiro getter gera escala e material antes de instanciar skybox.
- Validação: callers revisados e `Assembly-CSharp.csproj` compilando com 0 erros; novo cold start ainda precisa ser medido após reiniciar Unity.

### 2026-07-13 — Efeitos, mythics e cleanup do mapa

- `EffectAsset` adia prefab principal e splatters até preload ou primeiro uso; 176 definições oficiais deixam de abrir visual no boot.
- `MythicAsset` adia quatro prefabs visuais por asset; 81 definições oficiais passam a carregar somente quando exibidas ou validadas.
- Reutilizado `Bundle.loadDeferred`; bundles legados, `-NoDeferAssets` e validação mantêm comportamento eager.
- Removido primeiro `UnloadUnusedAssets` + `GC.Collect` do loading de mapa: log mediu `256,43 ms` para somente `76,6 KB`; cleanup final de `7,9 MB` permanece.
- Preservados preload/pool de efeitos e validação. Nenhum cache ou dependência nova.
- Validação: 176 efeitos, 81 mythics e 8.163 `.dat` inventariados; `Assembly-CSharp.csproj` compila com 0 erros e 14 warnings preexistentes. Cold boot e mapa precisam ser repetidos.

### 2026-07-13 — Água e terreno

- `LevelLighting` obtém estado submerso e superfície próxima com uma única consulta aos volumes de água por frame; antes percorria os mesmos volumes duas vezes.
- Consulta é ignorada quando efeitos submersos estão desativados. APIs públicas de água e comportamento visual permanecem compatíveis.
- Seis passes de terreno deixam de amostrar quatro máscaras quando variante `IS_SNOWING` não está ativa; neve continua usando mesmas máscaras e resultado visual.
- Mudança da água foi inspirada pela causa descrita na [issue #5514](https://github.com/SmartlyDressedGames/U3-SDK/issues/5514), com helper interno próprio e sem copiar gerenciador proposto.
- Validação: `Assembly-CSharp.csproj` compila com 0 erros e 14 warnings preexistentes; estrutura condicional dos seis shaders está balanceada. Capturas CPU Timeline e GPU ainda precisam medir ganho real.

### 2026-07-13 — Top 2: payloads de itens e streaming regional

- `ItemAsset` mantém metadados, blueprints e configuração no catálogo, mas adia prefab `Item`, objeto `Animations` e três texturas base de skin até primeiro acesso.
- Reutilizado `Bundle.loadDeferred`: bundles legados, `-NoDeferAssets`, `-ValidateAssets` e `-AlwaysLoadItemPrefab` preservam carregamento eager quando solicitado.
- `LevelObject` adia criação do proxy visual `Skybox` até acesso público ou ativação da região de skybox; atualização incremental existente distribui materialização entre frames.
- Editor e level batching continuam materializando proxies necessários. Modelo principal, colisão, nav, triggers, estado, saves e rede não foram alterados.
- Esta é primeira fatia segura das duas iniciativas. Catálogo totalmente persistente e unload de objetos físicos por chunk continuam abertos porque exigem invalidação, restauração de estado e prova com Memory Profiler.
- Validação: `Assembly-CSharp.csproj` compila com 0 erros e 14 warnings preexistentes. Cold boot, loading do mesmo mapa e primeiro acesso precisam ser medidos.

### 2026-07-13 — Top 5: segunda rodada segura

- Assets: três texturas base de skin de `VehicleAsset` usam o mesmo carregamento sob demanda dos itens; catálogo deixa de abrir payloads de veículos sem skin em uso.
- Streaming: saída de região em objetos, itens, recursos, barricadas e estruturas examina somente anel anteriormente carregado. Antes, cada manager percorria todas as `4.096` regiões por jogador; agora custo normal acompanha raio do sistema.
- Visibilidade/LOD: luzes já fora de alcance consultam distância uma vez a cada oito frames, distribuídas por instância; luzes próximas e transição continuam atualizadas todo frame.
- Simulação/IA: cada `Zombie.tick` lê `Time.time` uma vez e reutiliza valor em timers de alvo, ataque, stuck e habilidades.
- Rede: replicação de jogadores calcula permissão global de visibilidade uma vez por destinatário, não uma vez por par de jogadores.
- Compatibilidade: nenhuma distância, frequência de rede, regra de IA, física, save ou protocolo foi alterado.
- Validação: `Assembly-CSharp.csproj` e `Assembly-CSharp-Editor.csproj` compilam com 0 erros; dois testes cobrem bounds regionais normais e centro inválido. Unity Profiler ainda precisa medir ganho em transições, cidade iluminada, horda e servidor cheio.

### 2026-07-13 — Foliage e próxima fatia de loading

- Clutter, como grama e pedras decorativas marcadas com `Is_Clutter`, recebe distância própria: `1/2/3/4` tiles nos presets Low/Medium/High/Ultra. Cada tile mede 32 metros.
- Distância geral de foliage permanece `2/3/4/5` tiles. Árvores, marcos e assets não marcados como clutter preservam alcance anterior.
- Culling usa distância dinâmica do preset durante desenho; trocar qualidade não exige recarregar tile ou mapa.
- Com opção de clutter desligada, `FoliageStorageV2` deixa de copiar matrizes descartadas para listas runtime. Blob ainda é lido no worker para preservar formato, integridade e ordem.
- Prefabs `Projectile` de armas e magazines usam `Bundle.loadDeferred`; master bundles abrem payload somente no primeiro uso. Bundles legados, `-NoDeferAssets` e validação continuam eager.
- Validação: `Assembly-CSharp.csproj` compila com 0 erros e 14 warnings preexistentes; `git diff --check` sem erros. Ganho precisa ser medido em floresta densa, boot frio e loading do mesmo mapa.

### 2026-07-13 — Render de modelos e animação invisível

- Faixa base de `QualitySettings.lodBias` mudou de `[2,5]` para `[0,75,2]`, usando mesmo slider de draw distance. LODs leves entram antes em árvores, casas, personagens, itens e demais prefabs com `LODGroup`.
- Distâncias de rede, colisão, hitbox e simulação não mudaram. Cinematic Mode e override positivo do usuário continuam preservados.
- Players remotos em cliente puro e `Zombie_Client` usam `AnimationCullingType.BasedOnRenderers`. Unity deixa de avaliar animação legacy quando nenhum renderer estiver visível.
- NPC client já usava mesmo culling. Player local, servidor, listen server e dedicated server mantêm `AlwaysAnimate` para não afetar viewmodel, hitboxes ou lógica autoritativa.
- Árvores e casas já usam regiões, layers, `LevelBatching`, `LODGroup` e proxies skybox quando configurados. Itens dropados já usam layer `ITEM` com distância curta. Novo manager duplicaria trabalho existente.
- Sugestão para mapas: todo modelo grande/caro deve possuir `LODGroup` com meshes reais de menor custo; sem LOD criado no asset, ajuste de bias não consegue reduzir triângulos.

### 2026-07-13 — Matemática em loops quentes

- Scan de sentry testa cone de visão com produto escalar e distância ao quadrado antes de calcular raiz quadrada e normalizar direção. Alvos fora do cone deixam de pagar `sqrt`.
- Mesma diferença vetorial e distância ao quadrado são reutilizadas nos scans de players, zombies, animais e veículos.
- Sentry calcula distância do alvo, normalização de alcance e `cos` de spread uma vez por disparo, não uma vez por pellet. Chance e sequência de `Random` permanecem iguais.
- Flankers removem normalização antes de produto escalar usado somente para testar sinal. Zombies em habilidade especial passam vetor horizontal direto para `Quaternion.LookRotation`, que depende da direção, não do comprimento.
- Validação server-side de alcance balístico compara distâncias ao quadrado, removendo raiz por resultado processado sem alterar limite aceito.
- Fórmulas rápidas e cálculos configuráveis, como expoentes de física dos veículos, não foram alterados sem captura. Aproximações e lookup tables não foram adicionados.
- Validação: `Assembly-CSharp.csproj` compila com 0 erros e 14 warnings preexistentes; captura de horda e sentries com armas multi-pellet ainda precisa medir ganho.

### 2026-07-13 — Stress e observabilidade local

- Captura opt-in `-PerformanceMetrics` grava CSV por um período limitado, sem telemetria remota ou impacto em builds executados sem flag.
- Janela de um segundo registra frame time médio/p50/p95/p99/máximo, main/render thread, CPU/GPU frame time, GC, memória, draw calls, batches, SetPass e triângulos.
- `Window > Unturned > Editor Settings > Misc > Performance Metrics` permite smoke test no Editor; comparação válida permanece em Development Build standalone.
- Cenários de baseline, floresta, cidade, horda, veículos, multiplayer e soak estão definidos em `PERFORMANCE_TESTING.md`.
- Validação: `Assembly-CSharp.csproj` e `Assembly-CSharp-Editor.csproj` compilam com 0 erros e 14 warnings preexistentes; primeira baseline standalone ainda precisa ser capturada.

### 2026-07-13 — Culling e geometria opaca no Unity Editor

- Captura no Play Mode: CPU `12,85 ms`, GPU `5,09 ms`; `Render.OpaqueGeometry` consumiu `40,2%` da CPU com `397` draw calls.
- Dentro do passe opaco, `BatchRenderer.Flush` consumiu `26,6%` e `Batch.DrawStatic` `21,7%`, com somente `63` draws estáticos. Perfil indica gargalo de culling/submissão na CPU, não saturação da GPU.
- Static Batching, Dynamic Batching e Graphics Jobs para Windows já estão ativos. Trocar static batching por instancing genérico ou reescrever culling na GPU não foi aplicado: static batching tem prioridade e mudança ampla pode aumentar draws, memória ou incompatibilidade.
- Novo `Editor Performance Mode`, disponível em `Window > Unturned > Editor Settings > Playing in Unity`, limita far clip a `768 m` somente sob `UNITY_EDITOR`. Menos renderers distantes chegam ao culling e GBuffer; Player build permanece idêntico. Cinematic Mode preserva distância original.
- Ganho deve ser medido no mesmo ponto do mapa. Modo reduz fidelidade de distância e serve para iteração rápida, não baseline final.

### 2026-07-13 — Build Tool e IMGUI

- `Build Test` já concluía com sucesso, mas iniciava `BuildPipeline` dentro de `OnGUI`. Reload do Editor invalidava pilha IMGUI e gerava `EndLayoutGroup: BeginLayoutGroup must be called first` após build.
- Três ações de build agora usam `EditorApplication.delayCall`; execução começa depois do evento de layout. Processo e saída do build não mudaram.
- `com.unity.postprocessing` foi alinhado de `3.4.0` para `3.3.0`, versão compatível com Unity `2022.3`. Versão `3.4.0` adicionou diretivas WebGPU que Unity `2022.3.62f3` não reconhece e gerava cinco warnings em `Uber`/`FinalPass`.

### 2026-07-13 — Chunks de mundo e interesse do servidor

- Mundo já usa grade `64×64` com regiões de `128 m`. Objetos, árvores e estradas já alternavam visibilidade regional; itens usam raio `1`, estruturas/barricadas/recursos raio `2`, e zombies possuem regiões de navegação.
- Nova configuração server-side `Gameplay.World_Chunk_Radius` controla raio ativo, é replicada ao cliente e aparece na configuração avançada do singleplayer. Intervalo `1–32`; padrão `8` equivale a aproximadamente `1024 m`.
- Replicação adiciona campo ao protocolo de configuração; cliente e servidor precisam usar mesmo build desta fork.
- Fora do Cinematic Mode, far clip, objetos, árvores, proxies e estradas respeitam limite. Reduz renderers entregues ao culling/GBuffer; opções gráficas locais ainda podem escolher distância menor.
- No servidor, `Animal.Update`, tick de animais e tick de zombies retornam cedo quando entidade não está dentro do raio regional de nenhum jogador. Relógio do tick continua atualizado para evitar salto de simulação ao reativar.
- Itens e estruturas preservam streaming existente. Respawn regional, descarregamento físico e máscara compartilhada de regiões ficam abertos até nova captura.

### 2026-07-13 — Fog, terreno, LOD, simulação e água

- Nova opção local `World Chunk Fog` em configurações gráficas, padrão `true`. Cada usuário escolhe; servidor não replica fog. `false` desliga fog atmosférico e fog da barreira acima da água; fog submerso permanece ativo.
- Fog da barreira começa nos últimos 20% do raio renderizado, mais próximo do limite. `World_Chunk_Radius` continua definindo posição da barreira visual.
- Cliente desliga `Terrain.drawHeightmap` em tiles de terreno fora do raio, com margem de uma região para reduzir pop-in. Verificação ocorre ao cruzar região de `128 m`, não todo frame. Collider e `TerrainData` permanecem carregados; Cinematic Mode e captura de satélite preservam terreno completo.
- LOD bias global agora varia de `[0,75,2]`. Mudança antecipa LODs distantes somente em assets que já possuem `LODGroup`; meshes sem LOD permanecem iguais.
- Auditoria do conteúdo YAML aberto encontrou `LODGroup` em 9 prefabs de personagens/projéteis. Modelos de mapas e master bundles precisam ser auditados no Unity/Frame Debugger porque não aparecem como prefabs YAML editáveis neste SDK.
- Servidor deixa de tentar respawn normal de animais e zombies fora da área de simulação de qualquer jogador. Horde e beacon preservam regras próprias. Ticks de entidades distantes já retornavam cedo.
- Fog de água consulta índice espacial existente para testar somente volumes candidatos perto da câmera, em vez de percorrer todos os volumes a cada render.
- Rede de itens, objetos, recursos, barricadas e estruturas já usa carregamento regional de raio `1–2`. Terreno vem dos arquivos locais do mapa e não é reenviado pelo servidor por frame; novo protocolo de streaming seria duplicação sem evidência.
- Ticks de zombie: orçamento e chamada ficam em `ZombieManager.Update`; decisões, alvo e movimento ficam em `Zombie.tick`; ruído do jogador entra por `AlertTool.alert` e `Zombie.alert`. Implementação ASPFP não está neste SDK; build aberto usa `NonPathfindingZombieMovementComponent` para steering direto.
- Validação: `Assembly-CSharp.csproj` compila com 0 erros e 14 warnings preexistentes. Teste standalone ainda precisa comparar raios `8/4/2`, fog ligado/desligado, travessia rápida de tiles, água e servidor com jogadores separados.

### 2026-07-13 — Memória, Deferred e visualização de chunks

- `Editor.log` confirmou alerta de memória do sistema: memória paginada chegou a `33,7/35,7 GB` (`94%`). Unity usava `6,25 GB` e descartou frames do Profiler para aliviar pressão. Loading continuou porque não ocorreu falha fatal de alocação.
- Cleanup final encontrou 287 assets não usados, mas memória permaneceu em `6,25 GB`. Forçar `Resources.UnloadUnusedAssets` ou `GC.Collect` novamente não resolve assets ainda referenciados.
- Snapshot: 5.911 texturas usam `1,88 GB` e 6.906 meshes usam `1,43 GB`, enquanto frame usa somente 123 texturas/`9,3 MB` e renderiza `177,3k` triângulos. Gargalo principal é residência/retenção, não quantidade visível de triângulos.
- Repositório contém somente 90 imagens-fonte em `Assets`; maioria das texturas está em core/map/Workshop bundles. Memory Profiler deve fornecer top 20 por tamanho, nome e referência antes de alterar compressão ou importação.
- `RenderDeferred.GBuffer` e `RenderDeferred.Lighting` confirmam custo do caminho Deferred. Primeiro teste seguro: comparar mesmo frame em `Render Mode = Forward`, depois reduzir `Lighting Quality`/sombras. `833` shadow casters, 316 RenderTextures/`257,6 MB` e 9.675 buffers/`0,71 GB` têm prioridade sobre reduzir os `177,3k` triângulos.
- Static batching reduz cerca de 2.100 draws para 130 batches, mas gera meshes combinadas e custou `2.080 ms` no loading. Não desativar globalmente sem A/B de frame time e memória usando `-UseLevelBatching false`.
- Admin pode executar `/drawchunks` localmente. Grade verde mostra área ativa, anel vermelho mostra primeira área inativa e amarelo marca chunk atual. Comando não envia dados ao servidor e não funciona para jogador sem admin.

### 2026-07-13 — Update, LateUpdate, FixedUpdate e GC

- `Animal.Update`, `InteractableSentry.Update`, `HumanAnimator.LateUpdate` e `FlickeringLight.Update` leem relógio Unity uma vez por callback e reutilizam valor. Timers e interpolações preservam mesmo instante do frame.
- `Buoyancy.FixedUpdate` calcula modo de ondas, tempo e damping uma vez por passo de física, não por voxel.
- Efeitos de status de clima percorrem `Provider.clients` diretamente. Iteradores `yield` aninhados deixam de alocar por atributo aplicado; filtro `WeatherMask` e ordem de stamina/health/food/water/virus permanecem.
- LOD adicional não foi gerado em runtime: meshes reduzidas precisam ser produzidas nos assets e medidas. Bias global, `LODGroup`, LightLOD distribuído e culling de animação já cobrem caminho seguro.
- Frequência de `FixedUpdate`, física, IA, raycasts e polling não mudou sem captura standalone. Pooling, jobs e novos managers não foram adicionados.
- Validação: `Assembly-CSharp.csproj` compila com 0 erros e 14 warnings preexistentes. Unity Profiler e Memory Profiler ainda precisam confirmar CPU por callback e `GC.Alloc`.

### 2026-07-13 — GC de loading e filas regionais

- “Aumentar alocação GC” foi interpretado como reduzir alocações e pressão sobre GC. Aumentar heap ou coleta não corrige causa e pode ampliar pausas.
- `River.readGUID` reutiliza buffer existente no caso normal de 16 bytes. Loading de objetos de mapa deixa de criar um `byte[16]` para cada GUID; fallback de dados inesperados preserva comportamento anterior.
- Filas ordenadas de itens, barricadas e estruturas continuam priorizando instâncias próximas e respeitando budgets existentes de `1 ms` e `2 ms`.
- Ordem interna foi invertida para consumir cauda da `List<T>` em O(1). Processamento anterior removia começo da lista e deslocava toda fila restante por frame.
- Streaming amplo não foi reescrito: `RegionIncrementalVisibilityTracker`, atualização incremental de objetos e budgets dos managers já fornecem base correta. Próximo passo é medir backlog, tempo e cancelamentos antes de unificar filas.
- Teste de `River` cobre round-trip e zero bytes gerenciados em 100 leituras comuns de GUID. Validação runtime: `Assembly-CSharp.csproj` compila com 0 erros e 14 warnings preexistentes.

### 2026-07-13 — Comandos administrativos, foliage e budget de IA

- Novos comandos pessoais aceitam `/` ou `@`: `fly` alterna voo, `god` alterna imunidade a dano e `speed <1-10>` define multiplicador de movimento. Somente admin/owner conectado pode executar; console sem jogador não é alvo válido.
- Voo usa caminho de movimento já existente e replica estado do servidor ao cliente. God mode permanece autoritativo no servidor. `/speed 1` restaura velocidade normal.
- Limite visual server-side existente continua sendo `Gameplay.World_Chunk_Radius`: fora do Cinematic Mode, far clip, objetos, árvores, proxies, estradas e terreno distante respeitam raio. Servidor não “renderiza”; ele limita simulação e relevância de entidades.
- Foliage client-side agora tem teto radial exato de uma região (`128 m`/quatro tiles de `32 m`) em todos presets e também no escopo. Qualidade menor ainda reduz densidade e distância abaixo desse teto.
- `Zombies.Tick_Budget_Per_Frame` e `Animals.Tick_Budget_Per_Frame` substituem budgets fixos do servidor dedicado. Padrões preservados: `50` e `25`; runtime limita máximo a `1000` e interpreta `0`/campo ausente como padrão antigo. Valor menor suaviza pico de CPU, mas aumenta latência de reação quando há muitas entidades ativas.
- Plantações não recebem budget: crescimento já usa timestamp e uma coroutine client que acorda no horário final, sem polling server-side contínuo.
- Validação: runtime e Editor compilam com 0 erros; permanecem 14 warnings preexistentes.

## Baseline standalone de `Builds/perf.csv`

Captura contém `262` janelas: `12` de startup, `6` de menu e `244` de jogo. Após `60 s` de aquecimento, `224` janelas de gameplay mostram:

- FPS mediano `103,2`; frame médio mediano `9,663 ms`;
- p95 mediano `11,220 ms`; p99 mediano `12,417 ms`;
- main thread média mediana `9,567 ms` contra GPU mediana `3,433 ms`: cenário principalmente limitado por CPU;
- draw calls medianos `3.674`, batches `1.048`, SetPass `450` e `384.436` triângulos;
- GC médio mediano baixo, `0,224 KiB/frame`, mas loading atingiu `1.577.748,555 KiB` em um frame;
- loading teve frame de `11.583,790 ms` e main thread de `11.678,792 ms`;
- gameplay aquecido ainda teve hitches de `470,161 ms`, com GPU de `4,559 ms` e quase nenhum GC nesse pico.

Conclusão: captura confirma duas classes distintas. Loading possui trabalho e alocação síncronos extremos; gameplay possui hitches de main thread não explicados por GPU nem GC. CSV não contém call stacks, portanto CPU Timeline deve identificar método antes da próxima mudança ampla.

## Investigação de cold start

Medição em Unity Editor `2022.3.62f3`, mesma sessão e mesmo mapa de startup:

- primeiro load: `76,96 s`;
- loads seguintes: `11,88 s` e `12,15 s`;
- primeiro load foi aproximadamente `6,4x` mais lento;
- `core.masterbundle` permaneceu entre `5,95 s` e `6,13 s` em todas execuções.

Conclusão: diferença não está na abertura do `core.masterbundle`. Cache quente vem principalmente do processo Unity, JIT e cache de filesystem. Não existe cache persistente de definições de assets no runtime atual.

Cache persistente não foi adicionado porque invalidação incorreta quebraria mods, Workshop e reload de assets; cache de SHA-1 também reduziria garantia de integridade. Próxima opção segura: converter loads eager para lazy-load por tipo, com benchmark e regressão individual. Foram localizados 67 pontos de load eager em 25 tipos de asset.

## Profiling recomendado

Cenários, comando de captura CSV e procedimento estão em [PERFORMANCE_TESTING.md](PERFORMANCE_TESTING.md).

Ordem de uso:

1. Unity Profiler em Development Build standalone: CPU Timeline, GPU Usage, Rendering, Memory e File Access.
2. Memory Profiler `1.1.11`, já instalado: snapshots antes/depois de mapa e comparação de objetos retidos.
3. Profile Analyzer `1.2.2`, ainda não instalado: comparação de múltiplos frames e capturas antes/depois.
4. JetBrains dotTrace Timeline: complementar para managed CPU, threads, GC e file I/O.

New Relic não é prioridade para cliente Unity: serve melhor para telemetria/APM de serviços e servidores. GPU, draw calls, objetos Unity e boot local exigem profiler nativo da Unity.

Sem captura standalone, ranking exato de CPU/GPU durante gameplay permanece desconhecido. Hipóteses de código não devem ser tratadas como gargalos confirmados.

## Princípios

1. Medir antes de otimizar. Capturar baseline repetível antes de cada mudança.
2. Corrigir causa compartilhada. Evitar guards duplicados em callers.
3. Preservar comportamento. Mudança incompatível exige migração, versão de protocolo ou feature flag.
4. Servidor manda. Movimento, dano, inventário, hitbox e economia precisam de validação autoritativa.
5. Orçamento explícito. Cada sistema recebe limite de CPU, memória, GPU, rede e I/O.
6. Trabalho pequeno. Uma hipótese, uma mudança, uma comparação e um rollback por PR.
7. Reutilizar Unity e dependências instaladas. Nova dependência só com ganho comprovado.
8. Não confiar em FPS médio. Usar percentis de frame time, picos, GC, memória máxima e tempo total.
9. Sem regressão silenciosa. Otimização precisa de teste, benchmark ou captura reproduzível.
10. Compatibilidade primeiro. Cliente modificado nunca pode impor regra insegura ao servidor.
11. Documentação acompanha código. Toda mudança atualiza este histórico e progresso em `TODO.md`.

## Método

Fluxo obrigatório por iniciativa:

1. Descrever cenário e impacto.
2. Criar reprodução ou benchmark.
3. Registrar baseline em hardware e mapa definidos.
4. Localizar hot path e causa raiz com profiler.
5. Aplicar menor mudança correta.
6. Comparar resultado com mesma configuração.
7. Validar gameplay, rede, saves, mods e servidor dedicado.
8. Documentar risco, rollback e resultado.

Métricas mínimas:

- boot frio e quente até menu jogável;
- entrada em servidor até controle do personagem;
- frame time de CPU e GPU em p50, p95 e p99;
- FPS mínimo sustentado, nunca isolado;
- alocações por frame e pausas de GC;
- memória residente, memória gerenciada e VRAM;
- bytes, pacotes e mensagens por segundo por jogador;
- tempo de tick e atraso do servidor em p95 e p99;
- tempo de cálculo, nós visitados e falhas de pathfinding;
- draw calls, SetPass calls, triângulos, overdraw e objetos visíveis.

## Ordem de execução

### Fase 0 — Segurança para mudar

Baseline, cenários reproduzíveis, testes de fumaça, telemetria local e inventário de compatibilidade.

### Fase 1 — Ganhos de baixo risco

Boot, carregamento, alocações frequentes, loops quentes, logs excessivos, pooling comprovado, consultas físicas e trabalho invisível.

### Fase 2 — Renderização e mundo

Culling, LOD, distância adaptativa, vegetação, sombras, partículas, materiais, iluminação e streaming.

### Fase 3 — Simulação e navegação

Tick budgets, animais, zumbis, veículos, pathfinding hierárquico, atualização incremental e jobs apenas onde medição justificar.

### Fase 4 — Rede e anticheat

Protocolos versionados, relevância espacial, snapshots, rate limits, validação autoritativa, evidências e ferramentas de revisão.

### Fase 5 — Customização segura

Modelos, rigs, cosméticos e hitboxes de servidor com limites, cache, fallback e validação no servidor.

## Definição de pronto

Item termina somente quando:

- reprodução ou benchmark existe;
- resultado antes/depois foi registrado;
- ganho supera ruído de medição ou bug não reproduz mais;
- testes relevantes passam;
- cliente e servidor dedicado foram verificados quando aplicável;
- compatibilidade e segurança foram avaliadas;
- rollback é conhecido;
- `TODO.md` foi atualizado.

## Documentos

- [SKILLS.md](SKILLS.md): competências e responsabilidades necessárias.
- [TOPLAG.md](TOPLAG.md): ranking dos 50 principais candidatos de CPU, memória, GPU e loading.
- [PERFORMANCE_TESTING.md](PERFORMANCE_TESTING.md): stress, métricas, Profiler, traces e comparação reproduzível.
- [TODO.md](TODO.md): backlog priorizado de melhorias.
