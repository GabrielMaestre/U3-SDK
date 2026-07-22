# Backlog de Melhorias

Legenda: `P0` bloqueia trabalho seguro ou corrige risco crítico; `P1` alto impacto; `P2` ganho incremental; `P3` experimento futuro.

Checklist não autoriza implementação cega. Cada item exige profiling, reprodução ou ameaça demonstrada.

Progresso usa `N/X`: `N` melhorias concluídas; `X` permanece aberto porque performance, loading e estabilidade exigem trabalho contínuo.

## P0 — Baseline e segurança

- [x] Migrar arquivos de Unity `2022.3.62f3` para Unity 6.3 LTS `6000.3.19f1` e revisar conversões automáticas de física/luzes.
- [x] Substituir APIs Editor removidas/obsoletas: AssetBundle legado, scripting defines, rotação UI Toolkit e busca de objetos.
- [ ] Sair do Safe Mode, aguardar Package Manager resolver `com.unity.toolchain.win-x86_64-linux@1.1.0`, regenerar project files e confirmar Console sem erros.
- [ ] Após migração, reimportar em cópia/branch, compilar runtime e Editor, executar smoke test e gerar novas baselines Development/Release. Não comparar diretamente Editor/versões diferentes.
- [ ] Validar packages, Built-in RP, shaders, Post Processing, água, terrain, master bundles, Workshop, mods, servidor dedicado, saves e protocolo em Unity 6.3.
- [x] Definir matriz de hardware, resolução, presets, mapas, rotas e entidades em `PERFORMANCE_TESTING.md`; preencher valores reais por máquina em cada captura.
- [x] Criar ranking inicial dos 50 agressores e separar medição de candidatos estáticos em `TOPLAG.md`.
- [x] Medir boot frio/quente até menu no Unity Editor: `76,96 s` frio contra `11,88–12,15 s` quente.
- [ ] Medir loading de mapa, entrada em servidor e primeiro frame controlável.
- [x] Definir cenários reproduzíveis de baseline, cidade, floresta, horda, veículos, multiplayer e soak; execução e baselines continuam constantes por build.
- [x] Executar ações do Build Tool após evento IMGUI, removendo falso erro `EndLayoutGroup` ao concluir `Build Test`.
- [x] Persistir `UnitsPerEM` de fontes TMP migradas e remover warnings repetidos de serialização.
- [x] Remover AI Inference/Sentis sem consumidores, shaders de build desnecessários e dependências órfãs.
- [x] Alinhar Post Processing para `3.3.0`, removendo diretivas WebGPU incompatíveis com Unity `2022.3.62f3`.
- [x] Diagnosticar alerta de memória no loading: pressão paginada do sistema em `94%`; Profiler descartou frames, Unity permaneceu em `6,25 GB`.
- [x] Identificar scan acidental de `Bundles/ORIGINAL_ASSETS`: `80.156` arquivos, `441.919` erros de parse e log de `706 MB` contaminam boot, RAM e profiling.
- [ ] Mover `ORIGINAL_ASSETS` para fora das pastas pesquisadas pelo jogo antes de medir nova baseline.
- [ ] Registrar CPU/GPU frame time p50/p95/p99, GC, RAM, VRAM, I/O, rede e tick do servidor.
- [ ] Separar métricas de Editor, Development Build, Release e servidor dedicado.
- [ ] Criar smoke test de boot, criação de mundo, conexão, spawn, inventário, combate, veículo e shutdown.
- [ ] Mapear formatos de save, protocolo, bundles, mapas, mods e APIs públicas que exigem compatibilidade.
- [ ] Capturar crashes, hangs, erros e warnings atuais antes de alterações.
- [ ] Definir limites de regressão e ruído aceitável por métrica.
- [ ] Manter resultados de benchmark fora do repositório quando forem binários ou grandes; versionar só resumo textual.

## P1 — Boot e loading — 15/X

- [x] Carregar master bundles direto do disco com `LoadFromFileAsync`, sem copiar arquivo inteiro para memória; manter SHA-1 de integridade.
- [x] Suspender leitores de assets ociosos com `SemaphoreSlim.WaitAsync`, eliminando busy-spin durante varredura de diretórios.
- [x] Calcular hash de recursos por stream sequencial, sem duplicar arquivos em `MemoryStream`.
- [x] Enumerar DLLs de módulos sob demanda e remover log duplicado durante descoberta.
- [x] Adiar geração `Auto_Skybox` de 27 recursos até primeiro uso no loading do mapa.
- [x] Adiar prefabs de 176 `EffectAsset` até preload ou primeiro uso, preservando bundles legados e validação eager.
- [x] Adiar quatro prefabs visuais de 81 `MythicAsset` até primeiro uso.
- [x] Medir cleanup intermediário do mapa: `256,43 ms` por `76,6 KB`; manter ambos os cleanups até snapshots comprovarem remoção segura.
- [x] Adiar prefab `Item`, animações e três texturas base de skin de `ItemAsset` até primeiro uso, preservando modos eager existentes.
- [x] Adiar três texturas base de skin de `VehicleAsset` até primeiro uso, reutilizando helper lazy compartilhado.
- [x] Adiar materiais Primary, Secondary, Attachment e Tertiary de `SkinAsset` até primeiro uso em master bundles; preservar modos eager e API pública.
- [x] Adiar prefabs `Projectile` de armas e magazines até primeiro uso em master bundles, preservando modos eager existentes.
- [x] Evitar uploads padrão de heightmap/splatmap antes de ler tiles serializados; manter defaults e criação de tiles novos.
- [x] Não copiar matrizes de foliage clutter para listas runtime quando opção de clutter estiver desligada.
- [x] Ler GUIDs comuns de arquivos `River` no buffer compartilhado, removendo um `byte[16]` por GUID durante loading de mapas.
- [ ] Repetir cold start após reiniciar Unity e comparar contra baseline de `76,96 s`.
- [ ] Traçar ordem e duração de inicializadores, cenas, subsistemas, bundles, Workshop e conexão Steam.
- [ ] Remover inicialização duplicada, bloqueante ou não usada antes do menu.
- [ ] Adiar sistemas até primeiro uso quando não afetarem correção.
- [ ] Avaliar lazy-load dos 67 pontos eager em 25 tipos de asset, um tipo por vez, com teste de regressão.
- [ ] Paralelizar somente I/O e trabalho independente comprovadamente seguro.
- [ ] Evitar varreduras repetidas de diretórios, mods, assets e configurações.
- [ ] Cachear metadados de assets/mods somente após definir invalidação segura para core, Workshop, mapas, Sandbox e reload.
- [ ] Agrupar leituras pequenas e reduzir seeks durante boot e loading.
- [ ] Mover descompressão e parsing pesado para etapas assíncronas sem tocar Unity API fora da main thread.
- [ ] Pré-aquecer apenas shaders, pools e assets que causarem hitch medido.
- [ ] Exibir progresso real por bytes/tarefas, sem barra artificial.
- [ ] Permitir cancelamento e recuperação limpa de loading falho.
- [ ] Descarregar cenas, bundles e temporários após transição.
- [ ] Reduzir domínio/editor iteration time sem afetar build do jogo.
- [ ] Otimizar boot e memória do servidor dedicado separadamente do cliente.

## P1 — CPU e frame time — 31/X

- [x] Cobrir o terceiro site de material do solo (`Wheel.UpdateModel`, cliente) com o cache por collider/ponto; helper único recebe `WheelHit`.
- [x] Iterar volumes regionais/dinâmicos diretamente em `GetFirstOverlappingVolume`, sem lista temporária combinada por consulta (`~391`/frame).
- [x] Ler `transform.position` e `eulerAngles` uma vez por update em `Zombie.OnUpdate` e `Animal.Update`/`updateStates`, nos caminhos server e client.
- [x] Ler `linearVelocity` uma vez na replicação de veículo sem motorista.
- [x] Substituir hash `x ^ y` por hash espacial sem colisões estruturadas em `FoliageCoord`, `LandscapeCoord`, `HeightmapCoord`, `SplatmapCoord` e `RegionCoord`; Deep Profile mediu `~8` `Equals` por lookup de dicionário de foliage.
- [x] Cachear clip de move/idle de zombies e chamar `Animation.CrossFade` somente em troca real; one-shots (`Play`) invalidam o cache. Antes: `~99` chamadas nativas por frame.
- [x] Reutilizar material do solo por roda até contato mover `1 m` ou trocar collider, removendo amostragem de splatmap e resolução de nome/NetId por passo de física. Antes: `~522` `GetMaterialName` por frame.
- [x] Remover `Update` redundante de `MythicalEffectController`; `LateUpdate` idêntico já sobrescrevia o resultado no mesmo frame.

- [x] Consolidar estado submerso e superfície próxima em uma consulta aos volumes de água por frame; ignorar consulta quando efeitos submersos estão desativados.
- [x] Consultar distância de `LightLOD` estável próximo ou distante a cada oito frames, mantendo fade da transição por frame.
- [x] Ler `Time.time` uma vez por `Zombie.tick` e reutilizar valor nos timers da mesma simulação.
- [x] Ler `Time.time` e `Time.deltaTime` uma vez por `Zombie.OnUpdate`, eliminando até `24+4` acessos repetidos por zombie/callback.
- [x] Ler `Time.timeAsDouble` uma vez por `Animal.tick` e reutilizar valor em alvo, ataque e wander.
- [x] Notificar condições de horário somente quando o segundo do ciclo muda, preservando blend de iluminação por frame e eventos explícitos de data.
- [x] Limitar limpeza de regiões ao anel anteriormente carregado em seis sistemas, evitando varreduras globais de `64×64`.
- [x] Usar culling nativo de animação nos prefabs client de players e zombies quando nenhum renderer estiver visível.
- [x] Rejeitar candidatos fora do cone da sentry com matemática ao quadrado antes de calcular raiz e normalização.
- [x] Calcular distância e multiplicador trigonométrico da sentry uma vez por disparo, não por pellet.
- [x] Remover normalizações sem efeito em testes de sinal e rotação horizontal de zombies.
- [x] Validar alcance balístico por distância ao quadrado, sem raiz por resultado de tiro no servidor.
- [x] Compartilhar relógio por callback em animal, sentry, animação humana e luz oscilante.
- [x] Compartilhar tempo de onda, modo client/server e damping entre voxels de cada `Buoyancy.FixedUpdate`.
- [x] Remover iteradores de jogadores dos efeitos periódicos de clima, mantendo filtro e ordem dos atributos.
- [x] Consumir filas ordenadas de itens, barricadas e estruturas pela cauda, evitando `RemoveRange(0, N)` e deslocamento da fila restante a cada frame.
- [x] Adicionar modo opt-in exclusivo do Unity Editor que limita far clip a `768 m`, reduzindo culling e submissão de renderers distantes sem alterar Player build.
- [x] Pausar `Update`/tick server-side de animais e tick de zombies fora do raio regional de todos os jogadores, preservando relógio do tick.
- [x] Ignorar respawn normal de animais e zombies fora da área de simulação de todos os jogadores; preservar Horde e beacon.
- [x] Consultar candidatos de fog de água pelo índice espacial existente, evitando varredura de todos os volumes por câmera/render.
- [x] Tornar budgets de tick de zombies e animais configuráveis no servidor dedicado, preservando padrões `50/25`, fallback para campo ausente e teto `1000`.
- [x] Suspender `LODGroup` junto dos renderers de `LevelObject` fora da visibilidade regional/culling, preservando raiz, scripts e colliders importantes no singleplayer e servidor host.
- [x] Executar dois passos pequenos de visibilidade regional por frame para objetos e árvores, reduzindo tempo de convergência sem atualizar região inteira de uma vez.
- [ ] Inventariar `Update`, `LateUpdate`, `FixedUpdate`, coroutines e callbacks mais caros.
- [ ] Remover polling substituível por eventos existentes.
- [ ] Distribuir trabalho não urgente entre frames com orçamento explícito.
- [ ] Reduzir buscas repetidas de componentes, objetos, assets e serviços em hot paths.
- [ ] Eliminar LINQ, boxing, closures, concatenação e coleções temporárias em loops quentes.
- [ ] Reutilizar buffers onde lifetime e limite forem claros.
- [ ] Revisar consultas físicas, layers, raios, overlaps e frequência.
- [ ] Limitar atualizações de entidades distantes, invisíveis ou inativas.
- [ ] Separar frequência de simulação, animação, UI e efeitos visuais.
- [ ] Reduzir contenção, locks globais e sincronização main-thread/worker.
- [ ] Aplicar Burst/jobs somente em lotes grandes, independentes e medidos.
- [ ] Revisar reflection e geração dinâmica executadas durante gameplay.
- [ ] Detectar long frames e atribuir custo por sistema.
- [ ] Medir `TickZombies`, `TickZombiesInRegionsWithPlayers` e `AnimalManager.Tick` com budgets `50/25`, `20/10` e `10/5`; definir presets somente após comparar latência de IA.

## P1 — Memória e GC — 5/X

- [x] Reformatar texto do stat tracker somente quando tipo ou kills mudam, removendo duas alocações de string por frame com arma rastreada equipada.
- [x] Remover alocações dos iteradores `yield` nos efeitos periódicos de clima.
- [x] Remover array temporário de 16 bytes por GUID no caminho comum de `River.readGUID`, com teste de alocação.
- [x] Manter materiais e texturas dependentes de skins não utilizadas fora da memória até primeiro acesso.
- [x] Após `StaticBatchingUtility.Combine`, liberar cópia CPU das meshes duplicadas para atlas com `Mesh.UploadMeshData(true)`; UVs e batching já foram concluídos, enquanto meshes de origem continuam intactas.

- [ ] Capturar snapshots em boot, loading, gameplay prolongado, troca de mapa e disconnect.
- [ ] Localizar objetos gerenciados, nativos e assets retidos após descarregamento.
- [ ] Corrigir inscrições de eventos, delegates, coroutines e referências estáticas vazando lifetime.
- [ ] Eliminar alocações por frame em UI, rede, inventário, combate e IA.
- [ ] Reduzir duplicação de meshes, materiais, texturas, strings e buffers.
- [ ] Implementar Asset Residency Streaming por mapa/servidor em PR isolada; detalhes, compatibilidade e medições em `ASSET_RESIDENCY_STREAMING.md`.
- [ ] Revisar caches sem limite; definir teto e descarte baseado em uso real.
- [ ] Pool somente objetos com churn medido; limitar tamanho e limpar estado no retorno.
- [ ] Usar formatos e compressão adequados para textura, áudio e mesh por plataforma.
- [ ] Ativar Texture Streaming somente após selecionar texturas de mundo elegíveis e comparar Ultra: fontes atuais não marcam `streamingMipmaps`, portanto ativação global isolada não reduz memória.
- [ ] Descarregar assets e bundles sem destruir recursos ainda referenciados.
- [ ] Medir fragmentação, large object heap e picos de desserialização.
- [ ] Fazer soak test com troca repetida de mapa, conexão e respawn.

## P1 — GPU e renderização — 18/X

- [x] Remover quatro amostras de máscaras dos seis passes de terreno quando variante de neve não está ativa, preservando resultado com `IS_SNOWING`.
- [x] Limitar clutter a `1/2/3/4` tiles por preset sem reduzir distância de foliage não decorativo.
- [x] Reduzir faixa base de LOD bias de `[2,5]` para `[0,75,2]`, fazendo modelos leves entrarem antes pelo slider existente.
- [x] Corrigir classificação da captura: CPU `12,85 ms` contra GPU `5,09 ms` confirma CPU-bound; `Render.OpaqueGeometry`, `BatchRenderer.Flush` e `Batch.DrawStatic` vieram do painel GPU e não identificam método CPU.
- [x] Limitar far clip e visibilidade regional por `Gameplay.World_Chunk_Radius`, reutilizando chunks de `128 m` existentes.
- [x] Desligar desenho do heightmap em tiles de terreno distantes, mantendo collider/dados e margem de preload de uma região.
- [x] Antecipar transições de `LODGroup` com faixa global `[0,75,2]`, preservando override do usuário e Cinematic Mode.
- [x] Tornar fog da barreira configurável localmente e aproximar início para últimos 20% do raio visual.
- [x] Limitar foliage client-side a uma região radial de `128 m`, inclusive presets Ultra e render do escopo.
- [x] Limitar sombras de clutter a `32/64/128/128 m` em Lighting Low/Medium/High/Ultra, preservando High/Ultra.
- [x] Escalar distância de sombras pelo slider de draw distance e limitar ao `farClipPlane`; draw distance máximo mantém visual original.
- [x] Tornar cálculo de distância de sombras idempotente; reaplicar configuração não reduz alcance cumulativamente.
- [x] Desligar atualização automática de reflection probes somente em Lighting Off/Low; preservar Medium/High/Ultra.
- [x] Desligar sombras somente de renderers exclusivos do último LOD de objetos e árvores; preservar renderers reutilizados por LOD próximo, geometria e recepção de sombras.
- [x] Limitar resolução máxima do Unity Terrain a LOD `1` somente para tiles inteiramente no anel externo de 25% da distância visual; preservar tiles próximos e Cinematic Mode.
- [x] Restaurar Sun Shafts no SDK aberto com PPv2, depth occlusion, presets escaláveis e shader preservado no Player build.
- [x] Diferenciar água Ultra no fallback com reflexão ambiente nativa e uma onda normal procedural leve; preservar caminho barato nas demais qualidades.
- [x] Refinar água Ultra com tint azul preservando metade da cor do mapa e alpha `0,68`; manter Low/Medium/High intactos.
- [ ] Comparar visual e GPU de Sun Shafts Off/Medium/High/Ultra em floresta e cidade no standalone.
- [ ] Capturar frames representativos por preset, resolução e GPU-alvo.
- [ ] Medir draw calls, SetPass, triângulos, overdraw, bandwidth, sombras e pós-processamento.
- [x] Preservar variantes essenciais e habilitar GPU instancing seletivo em materiais Standard dinâmicos; manter static batching como prioridade.
- [ ] Medir no Frame Debugger árvores, pedras, itens e estruturas repetidas. Instanciar somente grupos com mesmo mesh, material, lightmap, sombras e estado de renderer; foliage já usa `DrawMeshInstanced` e mapas já podem usar `LevelBatching`.
- [ ] Corrigir bounds excessivos que impedem frustum e occlusion culling.
- [ ] Criar LODs para personagens, veículos, objetos, vegetação e efeitos caros.
- [ ] Revisar transições de LOD para evitar popping e custo simultâneo excessivo.
- [ ] Reduzir objetos transparentes, partículas e decals fora de relevância visual.
- [ ] Orçar luzes dinâmicas, sombras, cascatas, resolução e distância por preset.
- [ ] Atualizar probes, reflexos e render textures somente quando necessário.
- [ ] Reduzir variantes de shader e tempo de compilação/carregamento.
- [x] Remover OpenGLCore/Vulkan do build Windows; manter D3D11 padrão e DX12 opt-in por `-force-d3d12` durante baseline Unity 6.3.
- [ ] Pré-aquecer variantes responsáveis por hitch; não pré-aquecer catálogo inteiro.
- [ ] Revisar terreno, água, folhagem, céu, nuvens, aurora e pós-processamento.
- [ ] Evitar materiais instanciados acidentalmente e uploads repetidos de propriedades.
- [ ] Validar APIs gráficas e GPUs suportadas após cada mudança de shader.

## P1 — Distância de renderização e streaming — 12/X

- [x] Materializar proxies `Skybox` de `LevelObject` sob demanda pela região existente; preservar editor e level batching.
- [x] Desativar objetos, itens, recursos, barricadas e estruturas somente dentro dos bounds da região anterior; centro inicial inválido produz bounds vazios.
- [x] Separar distância de clutter da distância geral de foliage e aplicar cap dinâmico por preset.
- [x] Antecipar transições de `LODGroup` globalmente sem alterar distâncias de rede, física ou streaming regional.
- [x] Preservar prioridade próxima e budgets regionais de itens/estruturas enquanto remoção O(1) reduz custo de filas grandes.
- [x] Replicar raio de chunks do servidor ao cliente e aplicar mesmo limite visual/simulação no singleplayer e multiplayer.
- [x] Mover fog para configuração gráfica local e preservar fog submerso quando opção estiver desligada.
- [x] Atualizar visibilidade dos tiles de terreno somente ao cruzar região ou mudar raio, evitando scan por frame.
- [x] Adicionar `/drawchunks` local para admin visualizar área ativa, primeira faixa inativa e chunk atual.
- [x] Aplicar limite exato de uma região ao foliage e manter árvores/objetos/terreno sob `Gameplay.World_Chunk_Radius`.
- [x] Duplicar passo time-sliced de objetos e árvores ao cruzar chunk, convergindo ativação/desativação em metade dos frames sem sincronizar região inteira.
- [x] Reavaliar LOD do terreno somente ao cruzar região ou mudar raio; manter detalhe original nos 75% próximos e reduzir somente tile inteiramente distante.
- [ ] Separar distância por categoria: terreno, estruturas, itens, jogadores, veículos, IA, vegetação, sombras e efeitos.
- [ ] Definir limites mínimo/máximo por preset e opção manual.
- [ ] Adaptar distância por orçamento de frame time com histerese e cooldown para evitar oscilação.
- [ ] Usar custo e importância visual, não distância única para tudo.
- [ ] Integrar frustum, occlusion, LOD e relevância de rede sem decisões contraditórias.
- [ ] Manter proxies/impóstores baratos para marcos distantes quando necessário.
- [ ] Priorizar streaming na direção e velocidade da câmera/jogador.
- [ ] Cancelar solicitações de streaming obsoletas.
- [ ] Limitar uploads de CPU para GPU por frame.
- [ ] Impedir pop-in crítico de jogadores, ameaças, tiros e veículos.
- [ ] Definir fallback estável quando CPU, GPU, memória ou I/O saturarem.
- [ ] Testar teleporte, voo rápido, veículos, escopos, mapas grandes e hardware lento.
- [ ] Medir tamanho, idade e tempo das filas existentes; depois adicionar cancelamento por geração de região, histerese entre anéis e prioridade por direção de movimento.
- [ ] Separar I/O de ativação: worker lê/decodifica dados puros; main thread cria objetos Unity sob orçamento. Nunca acessar Unity API no worker.
- [ ] Manter teleporte/carga crítica síncronos até provar que colisão, stance, nav e primeiro frame permanecem corretos.

## P1 — Pathfinding e IA — 2/X

- [x] Compartilhar relógio por tick de zumbi, reduzindo chamadas Unity nativas sem mudar timers ou decisões.
- [x] Expor budgets server-side por frame para ticks caros de zombies e animais; plantações permanecem orientadas por timestamp.

- [ ] Medir chamadas, duração, nós visitados, falhas, repaths e agentes simultâneos.
- [ ] Catalogar agentes e necessidades: zumbi, animal, NPC e casos especiais.
- [ ] Definir navegação autoritativa do servidor e dados necessários no cliente.
- [ ] Escolher representação por mapa após benchmark: navmesh, grid, grafo hierárquico ou híbrido.
- [ ] Dividir mundo em regiões/chunks com conectividade de alto nível.
- [ ] Calcular rota hierárquica longa e refinar somente trechos próximos.
- [ ] Reutilizar rotas comuns com invalidação por versão do grafo.
- [ ] Atualizar obstáculos dinâmicos incrementalmente, sem rebuild global.
- [ ] Aplicar orçamento por tick e fila por prioridade/distância/ameaça.
- [ ] Cancelar buscas de agentes mortos, removidos ou com destino obsoleto.
- [ ] Limitar repath por agente e usar histerese para destinos móveis.
- [ ] Separar pathfinding global de steering/avoidance local.
- [ ] Detectar agente preso e recuperar sem loop infinito ou teleportes abusivos.
- [ ] Tratar portas, escadas, água, terreno, desníveis, barricadas e veículos.
- [ ] Executar lotes independentes com jobs/Burst se benchmark justificar.
- [ ] Garantir limites de memória e tempo para mapas malformados/customizados.
- [ ] Criar testes de caminho válido, inalcançável, dinâmico, estreito e longa distância.
- [ ] Comparar qualidade e custo contra implementação atual antes de substituir.
- [ ] Entregar atrás de flag até paridade funcional e migração de mapas.
- [ ] Implementar scheduler compartilhado de repath quando implementação ASPFP estiver disponível: teto de buscas e milissegundos por tick, prioridade por ameaça/distância, cooldown, histerese e cancelamento. SDK atual contém fallback vazio, sem busca real para orçar.

## P1 — Rede e servidor dedicado — 3/X

- [x] Limitar limpeza de flags carregadas ao anel anterior por jogador em itens, objetos, recursos, barricadas e estruturas.
- [x] Cachear permissão de visibilidade global uma vez por destinatário durante snapshot de jogadores.
- [x] Indexar conexões SteamNetworkingSockets por `HSteamNetConnection` para lookup `O(1)` por pacote, preservando connect e close.

Estado verificado: itens usam raio regional `1`; objetos, recursos, barricadas e estruturas usam raio `2`; terreno é conteúdo local e não gera envio contínuo por tile.

- [ ] Medir bytes, pacotes, mensagens, serialização e CPU por jogador/sistema.
- [ ] Mapear mensagens confiáveis, não confiáveis, ordenadas e redundantes.
- [ ] Aplicar relevância espacial e prioridade por entidade/evento.
- [ ] Reduzir frequência de snapshots com interpolação e extrapolação limitadas.
- [ ] Usar delta/compressão somente quando CPU total também melhorar.
- [ ] Coalescer mensagens pequenas sem aumentar latência crítica.
- [ ] Reutilizar buffers com limites e ownership claros.
- [ ] Validar tamanho, frequência, estado e permissão de toda entrada remota.
- [ ] Aplicar rate limits por conexão, mensagem e custo.
- [ ] Impedir filas sem limite e backpressure ausente.
- [ ] Otimizar replicação de inventário, construção, veículos, zombies e objetos.
- [ ] Corrigir tempestades de spawn/despawn e ressincronização.
- [ ] Versionar protocolo para mudanças incompatíveis.
- [ ] Testar latência, jitter, perda, reordenação, reconnect e ataques de flood.
- [ ] Fazer load test com bots e conteúdo representativo.
- [ ] Monitorar tick p95/p99, fila de jobs e memória por jogador.

## P1 — Anticheat e hardening

- [ ] Criar threat model por ativo: movimento, combate, inventário, economia, construção, veículos e rede.
- [ ] Tornar servidor autoritativo para posição aceita, dano, cadência, munição, cooldown, alcance e linha de visão.
- [ ] Validar transições de estado, sequência de RPCs e ownership.
- [ ] Rejeitar NaN, infinito, overflow, índices inválidos, payloads grandes e dados truncados.
- [ ] Limitar velocidade, aceleração, teleportes e voo considerando ping, veículo e estados legítimos.
- [ ] Validar tiros por histórico temporal limitado e lag compensation com teto.
- [ ] Verificar hitbox usada no servidor, nunca confiar em geometria enviada pelo cliente.
- [ ] Proteger duplicação de itens, race conditions, rollback e replay de transações.
- [ ] Rate-limit chat, comandos, interação, inventário, spawn e mensagens caras.
- [ ] Gerar evidências com regra, valores, contexto, versão e relógio do servidor.
- [ ] Usar score/níveis de confiança antes de punição automática não reversível.
- [ ] Criar modo observação, alerta, restrição, kick e ban separados.
- [ ] Criar revisão e recurso para reduzir dano de falso positivo.
- [ ] Assinar/configurar políticas do servidor sem embutir segredo útil no cliente.
- [ ] Detectar adulteração cliente apenas como sinal complementar.
- [ ] Fuzzar parsers, pacotes, saves, assets e configurações remotas.
- [ ] Testar abuso de CPU/memória por clientes e mods maliciosos.
- [ ] Registrar somente dados necessários; definir retenção e acesso.
- [ ] Não divulgar regras detalhadas que facilitem evasão em builds públicos.

## P2 — Modelos e hitboxes customizados por servidor

- [ ] Especificar formato versionado para modelo, rig, animações, materiais e hitboxes.
- [ ] Definir allowlist de formatos e rejeitar scripts/conteúdo executável.
- [ ] Limitar tamanho de download, textura, mesh, bones, materiais e animações.
- [ ] Validar hashes, versão, origem e cache local.
- [ ] Oferecer modelo padrão quando download, validação ou compatibilidade falhar.
- [ ] Separar hitbox competitiva de mesh visual.
- [ ] Representar hitboxes com primitivas limitadas e nomes semânticos.
- [ ] Impor volumes, offsets, quantidade, escala e relações anatômicas permitidas.
- [ ] Calcular colisão e dano no servidor com configuração assinada/versionada.
- [ ] Replicar identificador da hitbox, não aceitar resultado final do cliente.
- [ ] Definir negociação de capacidade e mensagem clara para cliente incompatível.
- [ ] Evitar vantagem por modelo invisível, pequeno, transparente ou animação divergente.
- [ ] Validar câmera, altura, postura, cobertura e veículos com modelos customizados.
- [ ] Testar latência, replay, espectador, gravação e killcam se existirem.
- [ ] Documentar API para servidor sem expor superfície arbitrária de execução.

## P2 — Gameplay e simulação

- [x] Fazer GOD remover/bloquear fratura, adicionar HEAL, limitar SPEED a `1–50` e adicionar NOCLIP replicado para atravessar cenário/construções, todos restritos a admin/owner.
- [x] Criar inventário administrativo em `F3`, com busca/paginação, catálogo automático base+mods e entrega autoritativa compatível com permissão RocketMod `give`.
- [ ] Medir frequência/custo de animais, zombies, jogadores, veículos, barricadas e estruturas.
- [ ] Aplicar níveis de simulação por distância e relevância sem alterar resultados críticos.
- [ ] Reduzir física de objetos dormindo e entidades fora de interesse.
- [ ] Prototipar três níveis somente após captura: próximo com física completa; médio com frequência reduzida para lógica não crítica; distante dormindo/event-driven. Servidor mantém colisões e resultados autoritativos.
- [ ] Produzir colliders simples offline para assets dominantes e validar contatos, veículos, projéteis e exploits. Não trocar collider crítico em runtime apenas por distância.
- [ ] Revisar frequência de raycasts de armas, interação e percepção de IA.
- [ ] Corrigir diferenças entre `Update` e `FixedUpdate` dependentes de frame rate.
- [ ] Revisar determinismo e drift em timers, cooldowns e status.
- [ ] Evitar loops globais sobre todas as entidades quando índice espacial já resolver.
- [ ] Otimizar spawn/despawn em massa e limpeza de mundo.
- [ ] Validar veículos sob alta velocidade, colisões e rede ruim.
- [ ] Criar testes de regressão para exploits e bugs corrigidos.

## P2 — UI, áudio e input

- [x] Reatribuir atlas TMP aos materiais de fonte uGUI no runtime após migração Unity 6, corrigindo quads brancos/coloridos no lugar de texto.
- [x] Virtualizar catálogo do inventário administrativo com pool fixo de 14 linhas/ícones e 5 abas; carregar ícones somente para página visível usando cache compartilhado do `ItemTool`, filtrar por `EItemType`, omitir cosméticos `isPro` e paginar por botão/scroll.
- [ ] Medir rebuilds de canvas/layout, bindings, texto e listas grandes.
- [ ] Virtualizar listas de servidores, inventário, Workshop e logs quando necessário.
- [ ] Atualizar UI por evento ou frequência reduzida em vez de todo frame.
- [ ] Evitar strings, rich text e formatação repetidos em hot paths.
- [ ] Pool de elementos somente onde churn for medido.
- [ ] Reduzir vozes, mixers, filtros e áudio 3D fora de alcance.
- [ ] Carregar/streamar áudio conforme duração e uso.
- [ ] Preservar acessibilidade, remapeamento e navegação por teclado/controle.
- [ ] Verificar input duplicado, polling caro e foco de janela.

## P2 — Assets, mods e Workshop

- [ ] Indexar assets/mods uma vez por versão e invalidar entradas alteradas.
- [ ] Detectar dependências ausentes, ciclos, duplicatas e versões incompatíveis cedo.
- [ ] Limitar recursos consumidos por bundle, mapa e mod não confiável.
- [ ] Validar paths para impedir traversal e escrita fora de diretórios permitidos.
- [ ] Isolar falha de mod sem derrubar boot ou servidor quando possível.
- [ ] Exibir diagnóstico com mod, asset e dependência responsáveis.
- [ ] Evitar recarregar assets imutáveis entre sessões/mapas quando seguro.
- [ ] Definir orçamento de memória por conteúdo customizado.
- [ ] Preservar APIs públicas ou fornecer guia de migração.
- [ ] Criar matriz de compatibilidade para mods populares e mapas grandes.

## P2 — Bugs e estabilidade

- [x] Remover bloco `Update` de debug comentado em `PlayerInput`; comentário parcial deixou `#endif` órfão e bloqueou compilação.
- [ ] Classificar issues por reprodução, severidade, frequência e área afetada.
- [ ] Exigir passos, logs, versão, mapa, mods e configuração relevantes.
- [ ] Corrigir causa raiz e adicionar menor teste de regressão útil.
- [ ] Tratar nulls e estados inválidos na fronteira onde surgem, não espalhar guards.
- [ ] Revisar exceções engolidas e loops de retry sem limite.
- [ ] Garantir saves atômicos, backup e recuperação após interrupção.
- [ ] Validar migração e corrupção parcial de saves/configurações.
- [ ] Corrigir shutdown, disconnect e troca de mapa sem tarefas pendentes.
- [ ] Corrigir `NullReferenceException` de `PlayerEquipment.OnDestroy`/Crosshair quando UI já foi destruída; reproduzir antes de alterar lifecycle.
- [ ] Fazer soak tests de cliente e servidor por longos períodos.
- [ ] Agrupar crashes por assinatura e atacar maior frequência primeiro.

## P2 — Ferramentas, testes e CI — 3/X

- [ ] Automatizar build limpo de cliente e servidor suportados.
- [ ] Executar testes existentes de `SDG.NetPak` e `UnturnedDat`.
- [ ] Adicionar testes unitários somente para lógica pura crítica ou regressão real.
- [ ] Criar integração mínima para boot, load, conexão e save.
- [x] Adicionar captura standalone opt-in `-PerformanceMetrics` com CSV e duração limitada; automação de rota continua aberta quando houver replay determinístico.
- [x] Criar `Analyze Profiler Capture`: agrega markers de `.data` por self time/calls/GC em CSV ranqueado, com variante sem diálogo executável via MCP.
- [x] Adicionar MCP local editor-only como fallback ao bridge oficial, com conexão loopback autenticada e 11 ferramentas básicas validadas.
- [ ] Comparar performance em hardware fixo; não bloquear CI compartilhada por ruído aleatório.
- [ ] Detectar assets duplicados, referências quebradas e variantes excessivas.
- [ ] Verificar compatibilidade de protocolo e formato de save.
- [ ] Gerar relatório curto de regressões por build.
- [ ] Manter símbolos e dumps úteis para builds de diagnóstico.

## P2 — Observabilidade — 2/X

- [ ] Padronizar categorias, severidade e contexto de logs.
- [ ] Remover spam e logs caros de hot paths em builds normais.
- [x] Adicionar contadores opt-in de frame, CPU, GPU, GC, memória e render; tick, entidades, filas e rede continuam abertos por subsistema.
- [x] Permitir captura temporária geral com `-PerformanceMetrics` e `-PerformanceMetricsSeconds=N`, sem recompilar.
- [ ] Correlacionar sessão, conexão e evento sem dados pessoais desnecessários.
- [ ] Gerar relatório de crash com versão, plataforma, mapa e mods.
- [ ] Proteger logs contra injeção, dados sensíveis e crescimento sem limite.

## P3 — Experimentos futuros

- [ ] Usar Frame Debugger para escolher um único mesh/material repetido e comparar `LevelBatching` contra GPU instancing antes de alterar categoria inteira.
- [ ] Auditar `Read/Write Enabled`, canais de vértice, Vertex Compression e Optimize Mesh quando fontes dos core assets estiverem disponíveis.
- [ ] Considerar terrain por `SV_VertexID`/indirect rendering somente se GPU capture provar limite de vértices ou submissão e LOD nativo falhar.
- [ ] Testar DX12 como primeira API mantendo DX11 fallback; comparar CPU/Render Thread, GPU, p95/p99, shader stutter e crashes em várias GPUs antes de cogitar DX12 mínimo.
- [ ] Remover build Win32 somente após confirmar ausência de usuários/servidores dependentes; não contabilizar como ganho de FPS Win64.
- [ ] Comparar Forward e Deferred no mesmo cenário porque GBuffer/Deferred Lighting apareceram caros; rejeitar regressão visual ou aumento de passes por luz.
- [ ] Prototipar culling de água por regiões existentes, frustum e distância; limitar reflexão a água visível e preservar visão submersa/cavernas.
- [ ] Adicionar opção client-side para ocultar terreno/foliage/água fora dos limites de dados, com fog/skirt e opt-in por mapa para borda jogável.
- [ ] Avaliar streaming de mundo mais granular após gargalos atuais estarem medidos.
- [ ] Avaliar Entities/ECS apenas para subsistema isolado com protótipo superior ao código atual.
- [ ] Avaliar compute shaders para workload paralelo comprovadamente limitado por GPU.
- [ ] Avaliar geração offline de proxies, HLODs e dados hierárquicos de navegação.
- [ ] Avaliar URP/Forward+ em protótipo separado somente após estabilizar Unity 6.3 com Built-in RP; medir GPU Resident Drawer antes de converter projeto.
- [ ] Avaliar replay autoritativo para investigação de anticheat e bugs.
- [ ] Avaliar escalonamento dinâmico de tick por relevância e carga.
- [ ] Avaliar servidor headless mais enxuto após separar dependências gráficas reais.

## Fora de escopo até existir evidência

- [ ] Reescrever sistema inteiro sem benchmark e critérios de paridade.
- [ ] Trocar arquitetura, render pipeline ou engine por expectativa teórica.
- [ ] Adicionar dependência para substituir poucas linhas estáveis.
- [ ] Criar cache, pool, job ou thread sem gargalo medido e política de limite.
- [ ] Punir jogador automaticamente por uma heurística isolada.
- [ ] Permitir hitbox arbitrária controlada pelo cliente.
- [ ] Quebrar saves, mods ou protocolo sem versão, migração e rollback.
