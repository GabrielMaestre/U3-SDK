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

## Investigação de cold start

Medição em Unity Editor `2022.3.62f3`, mesma sessão e mesmo mapa de startup:

- primeiro load: `76,96 s`;
- loads seguintes: `11,88 s` e `12,15 s`;
- primeiro load foi aproximadamente `6,4x` mais lento;
- `core.masterbundle` permaneceu entre `5,95 s` e `6,13 s` em todas execuções.

Conclusão: diferença não está na abertura do `core.masterbundle`. Cache quente vem principalmente do processo Unity, JIT e cache de filesystem. Não existe cache persistente de definições de assets no runtime atual.

Cache persistente não foi adicionado porque invalidação incorreta quebraria mods, Workshop e reload de assets; cache de SHA-1 também reduziria garantia de integridade. Próxima opção segura: converter loads eager para lazy-load por tipo, com benchmark e regressão individual. Foram localizados 67 pontos de load eager em 25 tipos de asset.

## Profiling recomendado

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
- [TODO.md](TODO.md): backlog priorizado de melhorias.
