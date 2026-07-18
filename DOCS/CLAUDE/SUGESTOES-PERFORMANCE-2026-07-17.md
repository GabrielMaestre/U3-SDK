# Sugestões de Performance — 2026-07-17

Análise validada contra `DOCS/README.md`, `DOCS/TOPLAG.md`, `DOCS/TODO.md` e leitura direta do código. Nenhuma alteração de código foi aplicada. Ordem abaixo segue o próprio método do projeto: destravar medição, atribuir custo, só então otimizar.

## 1. Destravar a medição (P0 abertos — maior retorno imediato)

Estes itens bloqueiam qualquer baseline válida e já estão identificados em `TODO.md`:

1. **Mover `ORIGINAL_ASSETS` para fora das pastas escaneadas.** `C:\Program Files (x86)\Steam\steamapps\common\Unturned\Bundles\ORIGINAL_ASSETS` tem `80.156` arquivos/`1,38 GB`; o scanner gerou `441.919` erros de parse e `Player.log` de `706 MB`. Contamina boot, I/O, RAM e qualquer captura. Custo: mover uma pasta e apagar logs antigos. É a ação de maior retorno por esforço de todo o backlog.
2. **Concluir a migração Unity 6.3:** sair do Safe Mode, resolver `com.unity.toolchain.win-x86_64-linux@1.1.0`, regenerar project files, Console sem erros, smoke test e build standalone. Sem isso, nenhuma captura nova é comparável e nenhuma otimização pode ser creditada.
3. **Recapturar cold start** após reiniciar Unity, comparando contra `76,96 s` — só depois dos itens 1–2.

## 2. Atribuir custo antes de otimizar (ações da captura de 2026-07-17)

A captura de hoje já define os quatro passos, sem mudança de código:

- Deep Profile de `5–10 s` no mesmo ponto; expandir `Update.ScriptRunBehaviourUpdate` (`2,20 ms`) e decidir por `Self ms` e `GC Alloc`. Este é o único caminho para saber quais scripts otimizar — a lista de `Update()` candidatos (`PlayerMovement`, `PlayerInput`, `PlayerUI`, managers) tem 12+ arquivos e escolher sem captura é adivinhação.
- Frame Debugger: agrupar os `352` SetPass por material/shader/mesh e razão de não instanciar/combinar. `PostLateUpdate.FinishFrameRendering` (`5,67 ms`) cai reduzindo submissão, não triângulos (`246,3k` não é agressor).
- Memory Profiler pós-load e pós-`10 min`: `225` RenderTextures/`399,8 MB` e `11.718` buffers/`1,33 GB` precisam de dono antes de qualquer corte.
- Repetir rota idêntica 3× em Release e Development; p50/p95/p99.

## 3. Correção pequena validada no código (opcional, risco zero)

**`PlayerLifeUI.updateStatTracker`** (`Assets/Runtime/Assembly-CSharp/Unturned/UI/Player/PlayerLifeUI.cs:483`): chamada todo frame por `PlayerUI.Update`. Com arma de stat tracker equipada, aloca **duas strings por frame** (`kills.ToString("D7")` + `localization.format`) mesmo quando `kills` não mudou. A baseline mediu `144 B/frame` de GC total em gameplay — este caminho sozinho pode responder por parcela relevante disso com stat tracker equipado.

Correção no padrão já aprovado pelo projeto (cache + reformatar só na mudança): guardar `(type, kills)` do último frame e só reformatar quando o par mudar. O setter `Text` do Glazier uGUI repassa para `UnityEngine.UI.Text`, que já ignora string igual, então o custo atual é alocação/format, não re-layout.

## 4. Verificado como já otimizado (não re-investigar)

Caminhos auditados nesta análise que **não** precisam de trabalho:

- `PlayerLifeUI.hasCompassInInventory`: cacheado por `doesSearchNeedRefresh`; não varre inventário por frame.
- `PlayerLifeUI.updateHotbar`: early-out por cache de busca; refresh completo só quando inventário muda.
- `PlayerUI.updateGroupLabels`: early-out sem specstats/grupo; distância ao quadrado antes de `WorldToViewportPoint`.
- Setters `TextColor`/`Text` do Glazier uGUI: `Graphic.color` e `Text.text` da Unity fazem equality check; escrever valor igual todo frame não suja canvas.
- `FindObjectsOfType` não aparece em nenhum hot path (só comando de debug e menus).
- `updateHintsAndMessages`, `updateVoteDisplay`, `updatePauseTimeScale`: só flags e comparações; sem alocação.

## 5. O que não fazer agora (reafirmando decisões existentes)

- Não migrar URP/Forward+, não desligar batching global, não reescrever meshes — decisões já registradas em `TOPLAG.md` com as travas corretas.
- Não adicionar cache persistente de assets antes de definir invalidação (Workshop/mods/reload).
- Não converter mais eager loads sem medir os já convertidos: 15 fatias lazy foram aplicadas e **nenhuma teve cold boot re-medido**. Medir antes de continuar a série evita acumular risco sem crédito.

## Execução — mesma data, segunda sessão

Usuário confirmou: `ORIGINAL_ASSETS` movida e migração 6.3 pendente somente de builds Mac/Linux/servidor. Autorizou aplicar melhorias com aviso e documentação.

Feito nesta sessão:

1. **Ferramenta de atribuição criada e executada.** `Window > Unturned > Analyze Profiler Capture` (+ `Analyze Newest`, sem diálogo) agrega markers de capturas `.data` por self time/calls/GC. Executada via bridge MCP local na captura Deep Profile de `1,6 GB`; CSV ao lado da captura e resultados em `TOPLAG.md`.
2. **Cinco correções aplicadas com evidência da captura:** hash espacial nos cinco structs de coordenada (`~8` `Equals` por lookup antes), cache de `CrossFade` de zombies (`~99` chamadas nativas/frame antes), cache de material do solo por roda (`~522` `GetMaterialName`/frame antes), stat tracker sem strings por frame, remoção do `Update` redundante do `MythicalEffectController`.
3. **Validação:** MSBuild 0 erros em runtime e editor; Unity 6.3 recompilou sem erros de Console via refresh remoto.

Aberto para próxima sessão: nova captura na mesma rota para creditar os fixes; consultas de volume (`~400`/frame, maioria água/rodas) e GC de streaming de foliage ficam pendentes de medição pós-fix; som de motor por veículo/frame segue aguardando captura de frota (TOPLAG #32).

## Passe de matemática — terceira sessão, mesma data

Auditoria pedida: validar cálculos/funções matemáticas dos hot paths recentes e aplicar até ganhos pequenos.

Aplicado (todos compilados, 0 erros):

1. `Wheel.UpdateModel` (cliente): terceiro site de `GetMaterialName` que escapou do cache — roteado; helper agora recebe `WheelHit` e cobre os três chamadores.
2. `VolumeManagerBase.GetFirstOverlappingVolume`: eliminada construção de lista temporária por consulta (`~391`/frame); iteração direta preserva ordem e filtros (estáticos null/enabled, dinâmicos sem filtro, fallback `allVolumes`).
3. `Zombie.OnUpdate` + `Animal.Update`/`updateStates`: `transform.position` 4→1 leituras e `eulerAngles` (quaternion→euler com trig) ≤2→1 por entidade/update.
4. `InteractableVehicle`: `linearVelocity` 2→1 na replicação sem motorista.

Auditado, sem mudança (não re-investigar):

- `LevelVolume.IsPositionInsideVolume`: box usa um `InverseTransformPoint`, sphere usa `sqrMagnitude` — correto e enxuto.
- Compass/Glazier uGUI: setters de posição/cor usam dirty-flag com equality check — escritas redundantes já são grátis.
- `Buoyancy.FixedUpdate`: consulta água por voxel; colapsar pra 1 consulta/corpo muda física em borda d'água (encalhe) — só com A/B.
- `CalculateWheelSpeed`, falloff de explosão: magnitude real necessária.
- Revalidação dos fixes da sessão anterior: hashes nunca serializados, `SplatmapCoord` é `int`, zumbi nunca chama `animator.Stop()`/`SetActive` — cache de `CrossFade` seguro inclusive em `tellAlive`.

Unity foi fechada durante a sessão; Editor recompila na próxima abertura (MSBuild validou 0 erros).

## Resumo executivo

| Prioridade | Ação | Esforço | Retorno |
| --- | --- | --- | --- |
| 1 | Mover `ORIGINAL_ASSETS` + limpar logs | Minutos | Baseline válida; boot/I/O reais |
| 2 | Concluir migração 6.3 + smoke test | Horas | Desbloqueia todo o resto |
| 3 | Deep Profile + Frame Debugger + Memory Profiler | Horas | Converte candidatos `C` em medidos `M` |
| 4 | Fix stat tracker (strings/frame) | Minutos | GC/frame menor; risco zero |
| 5 | Re-medir cold boot das 15 fatias lazy | Horas | Credita ou reverte trabalho já feito |
