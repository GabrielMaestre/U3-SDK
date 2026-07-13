# Backlog de Melhorias

Legenda: `P0` bloqueia trabalho seguro ou corrige risco crítico; `P1` alto impacto; `P2` ganho incremental; `P3` experimento futuro.

Checklist não autoriza implementação cega. Cada item exige profiling, reprodução ou ameaça demonstrada.

Progresso usa `N/X`: `N` melhorias concluídas; `X` permanece aberto porque performance, loading e estabilidade exigem trabalho contínuo.

## P0 — Baseline e segurança

- [ ] Fixar máquinas, SO, resolução, presets, mapas, rotas e número de bots/jogadores para benchmarks.
- [x] Criar ranking inicial dos 50 agressores e separar medição de candidatos estáticos em `TOPLAG.md`.
- [x] Medir boot frio/quente até menu no Unity Editor: `76,96 s` frio contra `11,88–12,15 s` quente.
- [ ] Medir loading de mapa, entrada em servidor e primeiro frame controlável.
- [ ] Criar cenas/cenários de benchmark: cidade densa, floresta, combate, horda, veículos e servidor cheio.
- [ ] Registrar CPU/GPU frame time p50/p95/p99, GC, RAM, VRAM, I/O, rede e tick do servidor.
- [ ] Separar métricas de Editor, Development Build, Release e servidor dedicado.
- [ ] Criar smoke test de boot, criação de mundo, conexão, spawn, inventário, combate, veículo e shutdown.
- [ ] Mapear formatos de save, protocolo, bundles, mapas, mods e APIs públicas que exigem compatibilidade.
- [ ] Capturar crashes, hangs, erros e warnings atuais antes de alterações.
- [ ] Definir limites de regressão e ruído aceitável por métrica.
- [ ] Manter resultados de benchmark fora do repositório quando forem binários ou grandes; versionar só resumo textual.

## P1 — Boot e loading — 9/X

- [x] Carregar master bundles direto do disco com `LoadFromFileAsync`, sem copiar arquivo inteiro para memória; manter SHA-1 de integridade.
- [x] Suspender leitores de assets ociosos com `SemaphoreSlim.WaitAsync`, eliminando busy-spin durante varredura de diretórios.
- [x] Calcular hash de recursos por stream sequencial, sem duplicar arquivos em `MemoryStream`.
- [x] Enumerar DLLs de módulos sob demanda e remover log duplicado durante descoberta.
- [x] Adiar geração `Auto_Skybox` de 27 recursos até primeiro uso no loading do mapa.
- [x] Adiar prefabs de 176 `EffectAsset` até preload ou primeiro uso, preservando bundles legados e validação eager.
- [x] Adiar quatro prefabs visuais de 81 `MythicAsset` até primeiro uso.
- [x] Remover cleanup intermediário do mapa que custou `256,43 ms` e recuperou somente `76,6 KB`; preservar cleanup final.
- [x] Adiar prefab `Item`, animações e três texturas base de skin de `ItemAsset` até primeiro uso, preservando modos eager existentes.
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

## P1 — CPU e frame time — 1/X

- [x] Consolidar estado submerso e superfície próxima em uma consulta aos volumes de água por frame; ignorar consulta quando efeitos submersos estão desativados.
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
- [ ] Definir orçamento de tick para cliente e servidor dedicado.

## P1 — Memória e GC

- [ ] Capturar snapshots em boot, loading, gameplay prolongado, troca de mapa e disconnect.
- [ ] Localizar objetos gerenciados, nativos e assets retidos após descarregamento.
- [ ] Corrigir inscrições de eventos, delegates, coroutines e referências estáticas vazando lifetime.
- [ ] Eliminar alocações por frame em UI, rede, inventário, combate e IA.
- [ ] Reduzir duplicação de meshes, materiais, texturas, strings e buffers.
- [ ] Revisar caches sem limite; definir teto e descarte baseado em uso real.
- [ ] Pool somente objetos com churn medido; limitar tamanho e limpar estado no retorno.
- [ ] Usar formatos e compressão adequados para textura, áudio e mesh por plataforma.
- [ ] Descarregar assets e bundles sem destruir recursos ainda referenciados.
- [ ] Medir fragmentação, large object heap e picos de desserialização.
- [ ] Fazer soak test com troca repetida de mapa, conexão e respawn.

## P1 — GPU e renderização — 1/X

- [x] Remover quatro amostras de máscaras dos seis passes de terreno quando variante de neve não está ativa, preservando resultado com `IS_SNOWING`.
- [ ] Capturar frames representativos por preset, resolução e GPU-alvo.
- [ ] Medir draw calls, SetPass, triângulos, overdraw, bandwidth, sombras e pós-processamento.
- [ ] Agrupar materiais e ativar instancing/batching onde produzir ganho real.
- [ ] Corrigir bounds excessivos que impedem frustum e occlusion culling.
- [ ] Criar LODs para personagens, veículos, objetos, vegetação e efeitos caros.
- [ ] Revisar transições de LOD para evitar popping e custo simultâneo excessivo.
- [ ] Reduzir objetos transparentes, partículas e decals fora de relevância visual.
- [ ] Orçar luzes dinâmicas, sombras, cascatas, resolução e distância por preset.
- [ ] Atualizar probes, reflexos e render textures somente quando necessário.
- [ ] Reduzir variantes de shader e tempo de compilação/carregamento.
- [ ] Pré-aquecer variantes responsáveis por hitch; não pré-aquecer catálogo inteiro.
- [ ] Revisar terreno, água, folhagem, céu, nuvens, aurora e pós-processamento.
- [ ] Evitar materiais instanciados acidentalmente e uploads repetidos de propriedades.
- [ ] Validar APIs gráficas e GPUs suportadas após cada mudança de shader.

## P1 — Distância de renderização e streaming — 1/X

- [x] Materializar proxies `Skybox` de `LevelObject` sob demanda pela região existente; preservar editor e level batching.
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

## P1 — Pathfinding e IA

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

## P1 — Rede e servidor dedicado

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

- [ ] Medir frequência/custo de animais, zombies, jogadores, veículos, barricadas e estruturas.
- [ ] Aplicar níveis de simulação por distância e relevância sem alterar resultados críticos.
- [ ] Reduzir física de objetos dormindo e entidades fora de interesse.
- [ ] Revisar frequência de raycasts de armas, interação e percepção de IA.
- [ ] Corrigir diferenças entre `Update` e `FixedUpdate` dependentes de frame rate.
- [ ] Revisar determinismo e drift em timers, cooldowns e status.
- [ ] Evitar loops globais sobre todas as entidades quando índice espacial já resolver.
- [ ] Otimizar spawn/despawn em massa e limpeza de mundo.
- [ ] Validar veículos sob alta velocidade, colisões e rede ruim.
- [ ] Criar testes de regressão para exploits e bugs corrigidos.

## P2 — UI, áudio e input

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

- [ ] Classificar issues por reprodução, severidade, frequência e área afetada.
- [ ] Exigir passos, logs, versão, mapa, mods e configuração relevantes.
- [ ] Corrigir causa raiz e adicionar menor teste de regressão útil.
- [ ] Tratar nulls e estados inválidos na fronteira onde surgem, não espalhar guards.
- [ ] Revisar exceções engolidas e loops de retry sem limite.
- [ ] Garantir saves atômicos, backup e recuperação após interrupção.
- [ ] Validar migração e corrupção parcial de saves/configurações.
- [ ] Corrigir shutdown, disconnect e troca de mapa sem tarefas pendentes.
- [ ] Fazer soak tests de cliente e servidor por longos períodos.
- [ ] Agrupar crashes por assinatura e atacar maior frequência primeiro.

## P2 — Ferramentas, testes e CI

- [ ] Automatizar build limpo de cliente e servidor suportados.
- [ ] Executar testes existentes de `SDG.NetPak` e `UnturnedDat`.
- [ ] Adicionar testes unitários somente para lógica pura crítica ou regressão real.
- [ ] Criar integração mínima para boot, load, conexão e save.
- [ ] Criar benchmark executável sem interação manual quando possível.
- [ ] Comparar performance em hardware fixo; não bloquear CI compartilhada por ruído aleatório.
- [ ] Detectar assets duplicados, referências quebradas e variantes excessivas.
- [ ] Verificar compatibilidade de protocolo e formato de save.
- [ ] Gerar relatório curto de regressões por build.
- [ ] Manter símbolos e dumps úteis para builds de diagnóstico.

## P2 — Observabilidade

- [ ] Padronizar categorias, severidade e contexto de logs.
- [ ] Remover spam e logs caros de hot paths em builds normais.
- [ ] Adicionar contadores baratos para frame, tick, entidades, filas, memória e rede.
- [ ] Permitir captura temporária por sistema sem recompilar.
- [ ] Correlacionar sessão, conexão e evento sem dados pessoais desnecessários.
- [ ] Gerar relatório de crash com versão, plataforma, mapa e mods.
- [ ] Proteger logs contra injeção, dados sensíveis e crescimento sem limite.

## P3 — Experimentos futuros

- [ ] Avaliar streaming de mundo mais granular após gargalos atuais estarem medidos.
- [ ] Avaliar Entities/ECS apenas para subsistema isolado com protótipo superior ao código atual.
- [ ] Avaliar compute shaders para workload paralelo comprovadamente limitado por GPU.
- [ ] Avaliar geração offline de proxies, HLODs e dados hierárquicos de navegação.
- [ ] Avaliar recompilação/upgrade de Unity somente com matriz completa de compatibilidade.
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
