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
- [TODO.md](TODO.md): backlog priorizado de melhorias.
