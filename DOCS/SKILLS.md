# Skills Necessárias

Perfis abaixo representam competências, não cargos nem novas camadas de arquitetura. Uma pessoa ou agente pode assumir várias skills.

## Performance e profiling

- Unity Profiler, Profile Analyzer, Memory Profiler, Frame Debugger e ferramentas nativas de plataforma.
- Captura de CPU, GPU, memória, GC, I/O, carregamento e servidor dedicado.
- Benchmarks reproduzíveis, comparação estatística e detecção de regressões.
- Identificação de hot paths, contenção, cache misses, boxing, LINQ quente e alocações temporárias.

Saída: baseline, hipótese, captura, mudança mínima e relatório antes/depois.

## Runtime C# e Unity

- Ciclo de vida de `MonoBehaviour`, coroutines, jobs, Burst e Collections.
- Física, animação, assets, cenas, Addressables/AssetBundles existentes e serialização.
- Redução de trabalho em `Update`, consultas repetidas e acesso desnecessário a componentes.
- Compatibilidade com Unity 6.3 LTS `6000.3.19f1` e baseline anterior `2022.3.62f3`, incluindo diferenças de API, serialização, packages e backends.

Saída: mudança de runtime pequena, verificável e sem regressão funcional.

## Renderização e shaders

- Frame Debugger, RenderDoc, shaders, batching, instancing, LOD, occlusion e culling.
- Overdraw, sombras, transparência, partículas, terreno, água, vegetação e pós-processamento.
- Orçamento por preset e adaptação baseada em frame time com limites estáveis.
- Compatibilidade entre GPUs, APIs gráficas e mapas de Workshop.

Saída: menor custo de GPU/CPU por frame com imagem comparável e fallback.

## Carregamento, assets e memória

- Grafo de dependências, importação, bundles, compressão, cache e streaming assíncrono.
- Ciclo de vida de texturas, meshes, áudio, materiais e objetos Unity.
- Vazamentos, duplicação, retenção, fragmentação e picos de memória.
- Boot de cliente, servidor dedicado, mapa e Workshop.

Saída: loading trace, uso máximo de memória e plano de descarregamento seguro.

## Pathfinding e simulação

- A*, navegação hierárquica, navmesh, grids, custos, obstáculos dinâmicos e avoidance.
- Atualização incremental, cache de rotas, orçamento por tick e degradação controlada.
- Jobs/Burst para lotes medidos; determinismo onde servidor exigir.
- Comportamento de zumbis, animais, jogadores e veículos em mapas oficiais e customizados.

Saída: paths válidos, orçamento previsível e testes para becos, portas, alturas e mudanças dinâmicas.

## Rede e servidor

- Transporte, serialização, snapshots, relevância espacial, compressão e controle de congestionamento.
- Tick rate, latência, jitter, perda, reconexão e abuso de mensagens.
- Versionamento de protocolo e compatibilidade entre cliente, servidor e plugins.
- Profiling com contagens reais de jogadores e entidades.

Saída: menor custo por jogador sem perda de correção ou autoridade.

## Segurança e anticheat

- Threat modeling, validação autoritativa, rate limiting e hardening de parsers.
- Detecção de movimento, mira, dano, inventário, economia, RPC e manipulação temporal.
- Evidências auditáveis, níveis de confiança, falsos positivos, recursos e privacidade.
- Segredos fora do cliente; detecção cliente tratada como sinal, nunca verdade final.

Saída: regra testável no servidor, evidência explicável e resposta proporcional.

## Modelos, rigs e hitboxes

- Importação e validação de mesh, rig, animação, bounds, materiais e conteúdo remoto.
- Representação de hitbox por cápsulas/caixas limitadas e versão assinada pelo servidor.
- Separação entre aparência visual e colisão competitiva.
- Cache, fallback, limites de tamanho/complexidade e proteção contra assets maliciosos.

Saída: customização previsível, servidor autoritativo e clientes incompatíveis rejeitados com mensagem clara.

## Qualidade, testes e compatibilidade

- Testes unitários para lógica pura, integração para sistemas e cenas de benchmark para performance.
- Saves antigos, mapas, Workshop, plugins, servidor dedicado e diferentes configurações gráficas.
- Reprodução de bugs, testes de regressão, fuzzing de formatos e soak tests.
- CI com limites estáveis; benchmark ruidoso gera relatório antes de bloquear build.

Saída: uma verificação pequena que falha se regressão voltar.

## Observabilidade e diagnóstico

- Logs estruturados, contadores, traces e dumps acionáveis.
- Instrumentação desligável e barata em produção.
- Identificadores de sessão/evento sem coletar dados pessoais desnecessários.
- Relatórios de crash com versão, mapa, mods e contexto suficiente.

Saída: diagnóstico rápido sem causar novo gargalo.

## Documentação e triagem

- Issues reproduzíveis, prioridade por impacto e risco, ADR curto para decisão incompatível.
- Changelog, guia de migração e documentação de protocolo/configuração.
- Separação entre fato medido, hipótese e ideia futura.

Saída: próximo colaborador consegue reproduzir, medir e continuar trabalho.
