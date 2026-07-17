# Verificação Server-Side

Data da auditoria: 2026-07-17.

## Estado atual

- Compilação equivalente ao servidor Release, com `DEDICATED_SERVER`, `UNITY_SERVER`, `GAME` e `WITH_GAME_THREAD_ASSERTIONS`: **0 erros**.
- Compilação do runtime e Editor: **0 erros**.
- Nenhuma alteração de formato de save foi encontrada.
- GPU Instancing, shaders, fog, água, LOD e demais mudanças visuais são ignorados ou não executados pelo servidor dedicado.
- Comandos `/fly`, `/noclip`, `/god`, `/heal` e `/speed` validam admin/owner no servidor.
- Inventário administrativo valida permissão `/give`, rate limit e GUID do item no servidor.

## Problemas e riscos

### 1. Cliente e servidor precisam usar a mesma fork

`World_Chunk_Radius` adicionou um byte no começo da mensagem `ReplicateConfig`. Cliente vanilla ou build antiga pode interpretar os campos seguintes incorretamente. Servidor deve aceitar somente cliente construído com a mesma versão desta fork.

Risco: **alto para compatibilidade**, não para compilação do servidor.

### 2. Dedicated Development/Profile não compila

Com `DEDICATED_SERVER` e `DEVELOPMENT_BUILD`, seis chamadas de `CheckStructureRegionCoordIsCorrect` são compiladas, mas definição fica removida por `#if !DEDICATED_SERVER` em `StructureManager`. Dedicated Release não ativa esse caminho e continua compilando com zero erros.

Risco: **médio para profiling do servidor**; build padrão não é afetado.

### 3. Validação runtime do dedicado ainda falta

Compilação não confirma conexão, carregamento do mapa, RocketMod, Workshop, saves ou execução prolongada. Build real deve ser iniciado e testado antes de release.

Risco: **médio até concluir smoke test**.

### 4. IA distante muda de comportamento

Zombies e animais fora de `Gameplay.World_Chunk_Radius` de todos os jogadores pausam simulação e respawn normal. Horde e beacon mantêm exceções de spawn, mas precisam de teste com jogadores se afastando e retornando.

Risco: **baixo**, mudança intencional de performance.

### 5. Budgets baixos aumentam latência da IA

`Zombies.Tick_Budget_Per_Frame` e `Animals.Tick_Budget_Per_Frame` preservam padrões `50/25`. Reduzir demais economiza CPU, mas zombies e animais demoram mais para reagir sob carga.

Risco: **configuração**, não defeito no padrão.

### 6. Carregamento lazy precisa de teste com mods

Assets pesados agora podem carregar no primeiro uso. Isso reduz boot e RAM, mas mapa ou mod incompatível pode causar hitch ou asset ausente somente quando entidade aparece.

Risco: **baixo a médio para mapas/mods não testados**.

## Smoke test obrigatório

1. Gerar `Builds/Windows64_Headless/Unturned.exe`.
2. Iniciar mapa vanilla e conectar dois clientes desta fork em regiões distantes.
3. Testar spawn, morte e retorno de zombies/animais dentro e fora do raio.
4. Testar horde, beacon, itens dropados, barricadas, estruturas e veículos.
5. Executar comandos administrativos e inventário `F3` com admin, não-admin e permissão RocketMod.
6. Salvar, reiniciar servidor e confirmar inventários, estruturas, veículos e mundo.
7. Executar por pelo menos 30 minutos e revisar erros, tick, RAM e desconexões.

## Conclusão

Dedicated Release está compilável. Dedicated Development/Profile possui quebra confirmada nos guardas de compilação de `StructureManager`. Cliente e servidor também devem usar mesma fork por causa do protocolo modificado. Release permanece bloqueado até smoke test do dedicado.
