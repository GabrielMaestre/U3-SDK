# LDM / Rocket — Contexto de Integração

Atualizado: 2026-07-22.

Documento-base para trabalho isolado em LDM (Legally Distinct Missile), fork mantida de Rocket para Unturned. Escopo: plugins, compatibilidade, desempenho de servidor e relação com alterações de rede desta fork.

## Estado atual

- LDM/Rocket não está incluída como código-fonte neste repositório. É módulo externo carregado pelo servidor.
- O jogo procura módulo `Rocket.Unturned` durante startup do dedicado.
- A integração existente preserva hooks legados, incluindo `RocketLegacyOnDeath`, publicidade de plugins e permissões de comandos.
- Código recomenda LDM/fork mantida por compatibilidade e correções de exceções de multithread e exploits de teleporte.
- Antes de alterar integração, confirmar versão instalada em `Modules/Rocket.Unturned` e testar com build dedicado desta fork.

## Compatibilidade obrigatória

1. Cliente e servidor devem usar mesma fork. `ReplicateConfig` recebeu `World_Chunk_Radius` no início da mensagem. Cliente vanilla ou build antiga é incompatível.
2. Plugin Rocket/LDM roda somente no servidor. Nunca distribuir assembly de plugin ao cliente.
3. Plugins devem consumir APIs públicas e hooks existentes. Não injetar, reordenar ou escrever diretamente mensagens `NetMessages`/`ReplicateConfig`.
4. Não modificar saves, GUIDs de assets ou formatos de inventário sem migração e rollback.
5. O código compara versão mínima `4.9.3.1`, mas mensagem exibida cita `4.9.3.3+`. Confirmar versão do módulo distribuído antes de corrigir esse desencontro.

## Alterações de rede já aplicadas

| Alteração | Estado | Efeito |
| --- | --- | --- |
| Limpeza de flags carregadas | Concluída | Itens, objetos, recursos, barricadas e estruturas limpam somente anel regional anterior por jogador. |
| Cache de visibilidade global | Concluída | Permissão é calculada uma vez por destinatário durante snapshot de jogadores. |
| Relevância espacial | Concluída parcialmente | Itens usam raio `1`; objetos, recursos, barricadas e estruturas usam raio `2`. Zombies e animais usam interesse regional. |
| `World_Chunk_Radius` | Concluída | Limita janela visual e simulação distante; alterou protocolo `ReplicateConfig`. |
| Lookup de conexão SteamNetworkingSockets | Concluída | `HSteamNetConnection -> TransportConnection` usa `Dictionary`; cada pacote evita busca linear em conexões. |

Transporte padrão é `SteamNetworkingSockets`. Não trocar para `SystemSockets` sem benchmark: implementação alternativa percorre conexões e mantém fila de buffers por update.

## Objetivos de escalabilidade

- Reduzir CPU por jogador e por entidade ativa.
- Manter RAM estável após troca de mapa, reconnect e picos de jogadores.
- Reduzir atraso de processamento e jitter do servidor.
- Aumentar quantidade de jogadores sem alterar autoridade, saves ou comportamento próximo.
- Preservar compatibilidade com LDM, plugins, Workshop e mapas existentes.
- Manter apresentação visual separada da simulação autoritativa.

Ping físico depende de distância, rota e provedor. Código não reduz RTT geográfico. Servidor pode reduzir tempo em fila, tick atrasado, GC e processamento de mensagens; isso melhora ping percebido e estabilidade.

## Renderização e servidor dedicado

Servidor dedicado deve executar build `StandaloneBuildSubtarget.Server`, com `DEDICATED_SERVER`/`UNITY_SERVER`, áudio desabilitado e sem apresentação gráfica. Código atual já evita água visual, foliage, UI e partes do terreno; `Terrain.drawHeightmap` fica desligado no dedicado.

- Não transferir trabalho do servidor para GPU. Host dedicado pode não possuir GPU útil.
- Render distance do cliente não define autoridade. Servidor usa interesse/simulação configurados e mantém eventos críticos.
- Singleplayer e servidor hospedado renderizam porque também são cliente. Otimização visual deve permanecer atrás de `!Dedicator.IsDedicatedServer`.
- Mesh, textura, material, shader, áudio e prefab visual não devem carregar no dedicado quando lógica usa somente ID, collider simples ou metadado.
- Collider, navegação, spawn, dano, linha de visão e bounds continuam necessários mesmo sem renderer.

## Prioridades recomendadas

| Prioridade | Mudança | CPU | RAM | Latência | Compatibilidade |
| --- | --- | --- | --- | --- | --- |
| P0 | Métricas baratas por sistema/plugin | Neutro | Neutro | Diagnóstico | Sem mudança de protocolo |
| P0 | Eliminar I/O bloqueante e logs excessivos de plugins | Alto potencial | Baixo | Alto potencial | Transparente |
| P1 | Budget de recebimento/processamento por frame | Alto sob flood | Neutro | Evita travadas | Testar carga e reconnect |
| P1 | Relevância espacial compartilhada | Alto | Médio | Menos fila | Preservar eventos críticos |
| P1 | Ticks por distância/estado | Alto | Neutro | Tick mais estável | Sem reduzir lógica próxima |
| P1 | Asset Residency para dedicado | Médio no boot | Alto | Menos GC/hitch | Testar mapas e Workshop |
| P2 | Snapshot por estado sujo/delta | Alto | Baixo | Menos banda | Exige protocolo versionado |
| P2 | Fila de repath com prioridade | Alto em IA | Baixo | Tick mais estável | Validar comportamento |
| P3 | Jobs/Burst ou novo transporte | Incerto | Incerto | Incerto | Somente com profile comprovando necessidade |

### 1. Observabilidade antes de limites

Adicionar contadores baratos, agregados por segundo, sem log por pacote:

- mensagens e bytes recebidos/enviados por tipo e confiabilidade;
- tempo gasto em `Provider.Update`, `listenServer`, deserialize, handler e envio;
- tamanho e idade de filas;
- mensagens rejeitadas por rate limit/validação;
- jogadores, zombies, animais, veículos e regiões processados por tick;
- repaths solicitados, concluídos, cancelados e aguardando;
- tempo e GC alloc por plugin/hook LDM;
- RAM após boot, mapa, Workshop, reconnect e troca de mapa.

Registrar p50/p95/p99. Média esconde travadas. Não escrever cada amostra em disco; acumular em memória e emitir resumo periódico ou sob comando admin.

### 2. Recebimento com budget e proteção contra flood

`Provider.listenServer()` consome mensagens enquanto transporte retorna dados. Sob flood, loop pode ocupar frame inteiro e atrasar simulação.

Proposta segura:

- budget configurável por frame em quantidade, bytes e/ou tempo;
- limite alto por padrão para não alterar servidor normal;
- mensagens restantes permanecem na fila nativa para próximo frame;
- rate limit por conexão e por categoria após cabeçalho ser validado;
- desconectar somente abuso sustentado, nunca pico curto legítimo;
- métricas de fila/idade antes de ativar punição.

Risco: budget baixo aumenta latência e pode acumular backlog. Aplicar somente com load test, teto de fila e política clara de descarte.

### 3. Relevância espacial única

Reutilizar regiões existentes para decidir rede, IA, física e apresentação. Evita percorrer listas globais e evita sistemas com raios contraditórios.

- Próximo: simulação e snapshots completos.
- Médio: frequência reduzida e deltas essenciais.
- Distante sem jogador: entidade dorme; timers persistem por timestamp.
- Evento crítico: dano, morte, explosão, inventário e construção continuam imediatos e confiáveis.
- Entrada em região: snapshot completo inicial; depois somente estado sujo.
- Saída de região: limpar visibilidade sem destruir estado autoritativo.

Não usar posição enviada pelo cliente como única fonte de interesse. Servidor usa posição aceita/autoritativa.

### 4. Ticks, IA e pathfinding

Estado atual já oferece `Zombies.Tick_Budget_Per_Frame` e `Animals.Tick_Budget_Per_Frame`, padrões `50/25`, além de pausa fora do alcance de simulação.

Próximos passos:

- manter lógica próxima na frequência atual;
- escalonar entidades médias em grupos por frame;
- remover polling de entidade parada, sem alvo e sem jogador próximo;
- reativar por evento de proximidade, dano, spawn, alvo ou timer;
- limitar repaths simultâneos por quantidade e milissegundos;
- priorizar ameaça próxima e cancelar destino obsoleto;
- preservar horde, beacon, bosses e eventos de mapa como exceções explícitas.

Não reduzir `Application.targetFrameRate` do dedicado abaixo de `50` por padrão. Isso economiza CPU, mas aumenta tempo mínimo de resposta. Aumentar acima de `50` também não é ganho gratuito: eleva CPU e deve ocorrer somente com folga comprovada.

### 5. Física e entidades distantes

- Dormir rigidbodies estáveis e evitar acordar por atualização visual.
- Usar colliders simples no dedicado quando forma detalhada não altera gameplay.
- Não executar animação visual, partículas, áudio ou LOD no dedicado.
- Veículos ocupados/próximos mantêm frequência completa; abandonados e estáveis podem atualizar menos.
- Plantações, combustível, geradores e timers longos usam timestamps/eventos, não polling por frame.
- Consultas de dano, sentry e interação usam índices regionais já existentes.

Não desabilitar collider, linha de visão ou física crítica apenas porque renderer estaria invisível ao cliente.

### 6. Replicação e banda

- Movimento, zombie/animal snapshot e estado descartável usam `Unreliable` quando recuperação por snapshot seguinte é segura.
- Inventário, construção, morte, comandos e transações permanecem `Reliable`.
- Não enviar valor que não mudou.
- Snapshot completo periódico corrige perda e entrada tardia; deltas cobrem intervalo.
- Coalescer mensagens pequenas somente se reduzir CPU/banda sem atrasar evento crítico.
- Não comprimir pacote pequeno. Compressão custa CPU e pode piorar latência.
- Reutilizar buffers existentes; pooling novo só entra quando GC alloc por pacote aparecer no profile.
- Evitar broadcast global quando região ou destinatário específico resolve.

Mudança de layout de mensagem exige versão/negociação de protocolo. Cliente antigo deve ser rejeitado antes de interpretar payload incompatível.

### 7. RAM e carregamento dedicado

- Carregar catálogo leve de GUID, ID, tipo e config; adiar payload visual.
- Carregar somente mapa ativo, Workshop solicitado e bundles configurados pelo servidor.
- Não importar assets de mapas/mods não usados na sessão.
- Liberar referências da sessão anterior antes de `Resources.UnloadUnusedAssets`.
- Não usar `AssetBundle.Unload(true)` enquanto objetos vivos referenciam bundle.
- Limitar caches por quantidade/memória e limpar no unload de plugin/mapa.
- Cancelar requests e remover event handlers quando plugin descarrega.
- Evitar cache duplicado de item/player em cada plugin; preferir ID/GUID e referência fraca quando aplicável.

Asset Residency precisa permanecer em PR/teste isolado até validar mapa vanilla, Workshop, server bundles, reconnect e troca de mapa.

### 8. LDM e plugins

Maior risco de plugin é bloquear main thread. Framework não pode compensar handler síncrono lento.

- Medir cada callback e identificar plugin/método acima do orçamento.
- Banco, HTTP e arquivos trabalham fora do thread Unity; retorno ao thread principal somente para API do jogo.
- Limitar concorrência externa. `Task.Run` sem teto troca travada por excesso de threads/RAM.
- Agrupar saves e usar debounce; não salvar arquivo/DB por evento repetido.
- Cachear permission checks com invalidação correta.
- Desinscrever eventos, cancelar timers e liberar caches em unload/reload.
- Proteger exceções por plugin para uma falha não interromper tick global.
- Comando admin pesado usa cooldown, paginação e resposta limitada.
- Plugins não devem manter cópia permanente de todos `Player`, `ItemAsset`, prefab ou textura.

Compatibilidade vem antes de otimização: hooks públicos existentes permanecem; melhorias internas não mudam assinatura nem ordem sem teste com plugins reais.

## Configuração inicial recomendada para teste

- Transporte: `SteamNetworkingSockets` padrão.
- Update dedicado: `50` FPS/ticks de frame atual.
- Zombies: budget padrão `50`.
- Animals: budget padrão `25`.
- `World_Chunk_Radius`: comparar valores usados em produção; não reduzir como “otimização” sem validar gameplay.
- Build: Dedicated Release, sem render e áudio.
- Plugins: baseline sem plugins, depois LDM vazio, depois plugins um por vez e conjunto completo.

Configuração não substitui profile. Valor ideal depende de mapa, plugins, jogadores e quantidade de entidades.

## Cenário de capacidade

Executar mesma rota e seed com `0`, `8`, `24`, `48` e meta futura de jogadores. Para cada nível:

1. Jogadores juntos em cidade pesada.
2. Jogadores distribuídos por regiões.
3. Zombies/animais ativos, veículos, itens e construções.
4. Chat/comandos/plugins sob uso normal.
5. Pico controlado de connect/reconnect e spawn.
6. Teste de 30 minutos; depois soak de 2 horas para RAM.

Aceitar mudança somente quando:

- tick p95/p99 melhora ou permanece dentro do alvo;
- RAM estabiliza e retorna próximo do baseline após unload;
- GC alloc e pausas não aumentam;
- fila de rede não cresce continuamente;
- nenhum evento crítico atrasa ou desaparece;
- save/restart/reconnect preservam estado;
- cliente e servidor desta fork permanecem compatíveis;
- LDM e plugins de referência continuam funcionando.

## Roadmap mínimo

1. Instrumentar custo por mensagem, sistema e plugin.
2. Corrigir plugin/I/O/log comprovadamente lento.
3. Implementar budget de recebimento com padrão alto e telemetria.
4. Expandir relevância espacial e estado sujo sem mudar protocolo primeiro.
5. Reduzir assets gráficos residentes no dedicado.
6. Versionar protocolo antes de deltas ou novos snapshots.
7. Avaliar Jobs/Burst somente se um loop puro e isolado continuar dominante.

Sharding, ECS completo, troca de transporte e reescrita de Rocket ficam fora até servidor real provar que etapas anteriores não atingem meta.

## Checklist de smoke test

1. Build dedicado Release em `Builds/Windows64_Headless/Unturned.exe`.
2. Iniciar com `+LANServer/LDMTest` e carregar mapa vanilla.
3. Instalar LDM e plugins de teste; confirmar carregamento sem erro no console.
4. Conectar dois clientes desta fork, usar comandos admin e confirmar negação para não-admin.
5. Testar chat, morte, inventário, item, veículo, estrutura, teleport permitido e reconnect.
6. Afastar jogadores para validar chunks, zombies e animais; retornar à região.
7. Reiniciar servidor; confirmar save, plugins, permissões e ausência de duplicação.
8. Executar 30 minutos e registrar CPU p95/p99, memória, GC, bytes/pacotes, disconnects e erros.

## Pendências

- Instrumentar bytes, mensagens, serialização e CPU por jogador/sistema.
- Criar budgets/backpressure por conexão somente após load test.
- Mapear hooks LDM ativos e custo por plugin.
- Revisar divergência de versão mínima LDM exibida no startup.
- Testar Workshop, Rocket/LDM e saves com servidor dedicado Release.

## Arquivos de referência

- `Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs`: detecção e requisito de Rocket/LDM.
- `Assets/Runtime/Assembly-CSharp/Framework/Modules/ModuleHook.cs`: carregamento de módulos no dedicado.
- `Assets/Runtime/Assembly-CSharp/Unturned/Provider/Provider.cs`: recebimento de rede e conexão.
- `Assets/Runtime/Assembly-CSharp/NetTransport_SteamNetworkingSockets/ServerTransport_SteamNetworkingSockets.cs`: transporte padrão e índice de conexões.
- `DOCS/SERVER_SIDE.md`: riscos e smoke test geral do servidor.
- `DOCS/TODO.md`: backlog de rede e servidor dedicado.
