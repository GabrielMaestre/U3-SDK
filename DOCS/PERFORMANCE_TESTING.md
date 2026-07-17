# Testes de performance e stress

## Objetivo

Comparar builds no mesmo hardware, mapa, rota e preset. Captura local mede frame time, CPU, GPU, GC, memória e render sem enviar telemetria.

## Validar migração para Unity 6.3

Engine atual: Unity 6.3 LTS `6000.3.19f1`. Baseline anterior: Unity `2022.3.62f3`.

1. Preserve branch/cópia e baseline Release 2022.3 antes de abrir projeto na Unity 6.3.
2. Após reimport, revise Console e diff de `ProjectSettings`, `Packages`, cenas, prefabs, materiais e arquivos `.meta`; não aceite atualização serializada em massa sem causa conhecida.
3. Mantenha Built-in RP no primeiro build. Não misture upgrade de engine, URP e otimizações de código na mesma comparação.
4. Execute smoke test: boot, menu, singleplayer, servidor, água acima/abaixo, terrain, iluminação, inventário, veículos, Workshop/mods, save/load e shutdown.
5. Gere Development Build Win64 para CPU/GPU/Memory Profiler e Release Win64 para FPS. Registre versão Unity no nome/relatório de cada captura.
6. Compare DX11 padrão e DX12 opt-in separadamente: CPU Main/Render Thread, GPU, p50/p95/p99, RAM/VRAM, shader stutter e crashes.
7. Rejeite migração se houver regressão funcional, visual, de bundles/mods ou p95/p99 sem benefício que justifique correção.

Antes do smoke test, saia do Safe Mode com `Retry`, aguarde Package Manager concluir, confirme toolchain Linux `1.1.0`, use `Assets > Open C# Project` para regenerar `.csproj` e limpe Console. Não edite `.csproj` gerado manualmente.

Não medir CefSharp/CEF interno do Editor como custo do Player: nenhuma dependência CefSharp foi encontrada no runtime do repositório. Remoção de Win32 é simplificação de distribuição, não teste de FPS Win64.

## Captura rápida

0. Mova `C:\Program Files (x86)\Steam\steamapps\common\Unturned\Bundles\ORIGINAL_ASSETS` para fora de `Unturned/Bundles`, `Maps` e `Sandbox`. Esta exportação de `80.156` arquivos é interpretada como conteúdo do jogo e invalida boot/RAM/profiling.
1. Para Profiler, gere `Build Test` ou Development Build Windows 64-bit. Para FPS final, gere Windows 64-bit Release (`BuildOptions.None`); Development/Debugging adicionam overhead.
   `Building Test player successful!` confirma sucesso. Erro IMGUI `EndLayoutGroup` posterior era do Build Tool e foi corrigido agendando build fora de `OnGUI`.
2. Desative VSync e mantenha resolução, preset gráfico, mapa e mods idênticos.
3. Execute:

```powershell
.\Builds\Test\Unturned.exe -PerformanceMetrics -PerformanceMetricsSeconds=300 -FrameRateLimit=0
```

Para testar DX12, acrescente `-force-d3d12`. Sem argumento, Player usa DX11. API gráfica é escolhida antes do código gerenciado iniciar; prefira argumento nativo da Unity em vez de variável de ambiente interna.

4. Aguarde 60 segundos de aquecimento antes de iniciar rota.
5. Repita rota por pelo menos 180 segundos e execute três capturas por build.
6. CSV fica em `Application.persistentDataPath\PerformanceCaptures`; caminho completo aparece no log como `Performance metrics capture started`.

No Editor, habilite `Window > Unturned > Editor Settings > Misc > Performance Metrics`. Resultado válido deve vir do executável standalone, porque Editor adiciona CPU, GC e render próprios.

Não compare FPS de `Build Test` contra Release. Use Development para localizar custo; use Release para aceitar/rejeitar ganho final.

## Baseline pós-load de `2026-07-16`

- Russia, High, `600` frames: CPU p50 `6,73 ms`, p95 `8,55 ms`, p99 `10,46 ms`.
- Draw calls `2.354`, batches `528`, SetPass `243`, triângulos `254 mil`, GC `144 B/frame`.
- GPU ficou sem amostras. Nova comparação precisa ativar módulo GPU Usage e confirmar suporte da API/driver no standalone.
- Desconsidere `Profiler.FlushMemoryCounters` (`1,37 ms`) ao estimar Release e separe picos de `SetPlayerFocus` causados por Alt+Tab.
- Mão/viewmodel não estava visível. Não use captura para decidir mudança de câmera, FOV, near clip ou forward/deferred da viewmodel.

## Rebuild rápido e baixo pico de RAM

1. Após mudar package, asset, cena ou layout serializado de `MonoBehaviour`, execute `Build Test` completo uma vez.
2. Para alterações somente em código, reutilize saída anterior com `Build Test (Scripts Only)`. Unity 6 também reaproveita conteúdo automaticamente em builds normais quando nada mudou.
3. Preserve `Library/ShaderCache`, `Library/Bee` e `Library/Artifacts`. Use Clean Build somente para release final ou suspeita de cache inválido; apagar `Library` força import e shaders completos.
4. Em pouca RAM, feche Play Mode, Profiler, Memory Profiler e abas pesadas antes do build. Último recurso: reinicie Editor com `-diag-debug-shader-compiler`; Unity usa um compilador de shader, reduz pico de RAM e aumenta bastante o tempo.

Referências: [scripts-only/incremental](https://docs.unity3d.com/Manual/build-scripts-only.html), [clean build e caches](https://docs.unity3d.com/6000.0/Documentation/Manual/build-clean-build.html), [compilação de shaders](https://docs.unity3d.com/Manual/shader-compilation.html).

## Play Mode com menos lag

1. Fora do Play Mode, abra `Window > Unturned > Editor Settings > Playing in Unity`.
2. Ative `Editor Performance Mode`; deixe `Cinematic` desligado.
3. Entre novamente no Play Mode. Se opções de Enter Play Mode estiverem sem Domain Reload, reinicie Unity após alterar toggle.
4. Maximize aba `Game`, feche ou oculte `Scene`, desligue `Gizmos` na aba Game e deixe somente módulos necessários do Profiler gravando.
5. Modo limita câmera a `768 m` somente no Unity Editor. Visual distante pode desaparecer; física, rede, regras e Player build não mudam.
6. Para medir efeito, capture mesmo local com modo desligado/ligado e compare `Camera.Render`, `Culling`, `Render.OpaqueGeometry`, `BatchRenderer.Flush`, draw calls e batches.

Não use modo como baseline visual ou comparação com build. Desative toggle antes de validar pop-in, LOD e distância final.

## Testar chunks de mundo

- Singleplayer: abra configuração avançada e altere `Gameplay > World Chunk Radius`.
- Servidor: em `Servers/<id>/Config.txt`, defina `World_Chunk_Radius` dentro de `Gameplay`. Reinicie servidor; raio é replicado ao cliente ao conectar.
- Cada unidade equivale a `128 m`. Padrão `8` ≈ `1024 m`; teste inicial recomendado `4` ≈ `512 m`. Valores são limitados a `1–32`.
- Compare `8`, `4` e `2` no mesmo ponto/rota. Registre `Camera.Render`, `Culling`, `Render.OpaqueGeometry`, draws, entidades ativas e tick do servidor.
- Em configurações gráficas de cada usuário, compare `World Chunk Fog` ligado/desligado; confirme fog da barreira nos últimos 20% do raio e fog submerso ativo nos dois casos.
- No Frame Debugger, confirme ausência de draws de `Terrain` distante. Cruze fronteiras de `128 m` e valide margem sem buracos; collider deve continuar funcionando.
- Valide teleporte, escopo, veículos rápidos, captura de satélite, fronteira de região, dois jogadores distantes, zombie perseguindo e animal cruzando limite. Cinematic Mode ignora limite visual, mas servidor ainda limita simulação.
- No servidor, deixe áreas sem jogadores e monitore `ZombieManager.Update`, `AnimalManager.Update` e contagem de respawns. Horde e beacon devem continuar funcionando.
- Perto de vários volumes de água, compare CPU de `SkyFogRenderer.FindRelevantWaterVolumes`; aparência acima/abaixo da água deve permanecer igual.
- Como admin, execute `/drawchunks`: verde deve cobrir área ativa, vermelho primeira faixa inativa e amarelo chunk atual. Execute novamente para desligar.
- Confirme que grass/pedras decorativas somem após `128 m`, inclusive em Foliage Ultra e ao usar escopo. Árvores e objetos seguem `World_Chunk_Radius`, não teto de foliage.

## Testar sombras e foliage por qualidade

1. Use mesma floresta, horário, clima, resolução e posição em Development Build standalone.
2. Compare Lighting Low, Medium, High e Ultra com draw distance em `100%`. High/Ultra devem manter visual anterior; registre `Shadow Casters`, batches, CPU e GPU.
3. Em Low, atravesse limite aproximado de `32 m`; em Medium, `64 m`. Somente sombras de clutter distante devem desaparecer. Árvores, estruturas, personagens e geometria continuam visíveis.
4. Repita High/Ultra com draw distance em `100%` e `50%`. Em `100%`, alcance de sombras permanece original; abaixo disso acompanha redução escolhida pelo usuário e nunca passa do far clip/chunk.
5. Teste scope camera, teleporte e fronteira de tile. Rejeite mudança se houver popping próximo, tile sem sombra junto da câmera ou aumento de batches maior que economia no shadow pass.
6. Alterne outra opção gráfica várias vezes e reaplique High/Ultra. `QualitySettings.shadowDistance` deve permanecer igual para mesmo preset/draw distance, sem redução cumulativa.

## Testar LOD e convergência regional

1. Use Development Build standalone, mesma posição e preset High. Capture antes/depois sem Deep Profiling.
2. Cruze limites de região a pé, veículo rápido e teleporte. Objetos e árvores devem ativar/desativar sem buracos, pico integral ou mudança de distância configurada.
3. Valide colisão, interação, barricadas, estruturas e scripts fora/dentro da área: somente renderers e `LODGroup` visual ficam pausados; gameplay deve permanecer igual.
4. Compare `CalculateLODJob`, `UpdateRendererBoundingVolumes`, `Shadows.RenderJobDir`, shadow casters e p95/p99.
5. Inspecione objetos próximos: geometria, recepção de sombras e sombras dos LODs próximos devem permanecer iguais. Somente renderer exclusivo do último LOD deixa de projetar sombra.
6. Teste asset cujo mesmo renderer é reutilizado em múltiplos LODs; sombra deve permanecer ativa. Rejeite mudança se aparecer popping próximo, objeto ausente ou collider alterado.
7. Para terreno, mantenha câmera e High/Ultra: tile dentro dos 75% próximos deve usar `heightmapMaximumLOD = 0`; somente tile inteiramente no anel externo pode usar `1`.
8. Valide seams, holes/cavernas, scope, veículo, teleporte, planar reflection, satellite capture e Cinematic Mode. Rejeite se terreno próximo mudar ou colisão divergir do visual.

## Testar lazy skins e loading do terreno

1. Compare cold boot e RAM no menu antes/depois sem abrir inventário cosmético. Registre materiais/texturas carregados e tempo de catálogo.
2. Teste skin padrão, pattern, arma com cinco attachments e veículo. Primeiro uso pode carregar conteúdo uma vez; acessos seguintes não podem gerar novo hitch ou material ausente.
3. Execute com `-ValidateAssets` e confirme mesmos erros de Primary/Secondary e Attachment/Tertiary de antes. Bundles legados devem continuar eager.
4. Carregue mesmo mapa três vezes. No CPU Timeline, cada tile serializado deve executar somente upload final de `SetHeightsDelayLOD`/`SetAlphamaps`; terreno, collider, materiais e bordas devem ficar idênticos.
5. Em cópia de teste do mapa, remova um heightmap/splatmap e confirme fallback padrão. Não modifique mapa original.

## Testar hot paths de CPU desta rodada

1. Em Development Build, capture 300–600 frames na mesma cidade à noite e na mesma horda com animais. Deep Profiling desligado.
2. Em `CPU Usage > Timeline/Hierarchy`, compare `LightingManager.updateLighting`, callbacks de condições em `PlayerQuests/LevelObject`, `LightLOD.Update`, `Zombie.OnUpdate` e `Animal.tick`.
3. Valide comando de dia/noite/hora, mudança natural do ciclo, objetos condicionados por data, luzes ao atravessar faixa LOD, ataque/regen/especiais de zombies e ataque/wander de animais.
4. Rejeite se condição de horário atrasar mais de um segundo, fade de luz ficar irregular ou IA mudar. Depois repita CSV em Release para medir FPS/p95/p99.

## Testar comandos e budget de IA

- Admin/owner: teste `/fly` duas vezes; `/god` com dano e queda; `/heal` após dano, sangramento e fratura; `/speed 50`, finalizando com `/speed 1`. Variantes com `@` devem produzir mesmo resultado.
- `/speed 0`, `/speed 51` e texto inválido devem mostrar uso `1-50` sem alterar multiplicador.
- Jogador sem admin: quatro comandos devem responder `Admin or owner permission required` e não alterar estado.
- Em `Config.txt`, teste `Zombies.Tick_Budget_Per_Frame` e `Animals.Tick_Budget_Per_Frame` primeiro com padrões `50/25`, depois `20/10` e `10/5`. Reinicie servidor entre configurações.
- Capture CPU Timeline e latência de aquisição de alvo/ataque com mesma contagem de entidades. Não reduza padrão se reação ficar visivelmente atrasada.
- Valor `0` usa padrão antigo (`50/25`); valores acima de `1000` são limitados. Plantações não entram no teste: crescimento é calculado por timestamp, sem tick server contínuo.

## Evitar alerta de memória durante profiling

- Log observado: memória paginada `33,7/35,7 GB` (`94%`); Unity `6,25 GB`. Aumente pagefile do Windows ou use tamanho gerenciado pelo sistema em unidade com espaço livre.
- Feche navegador, IDE e outros processos grandes. Não capture CPU Profiler e Memory Profiler simultaneamente durante loading.
- Desative `Deep Profile`, limite módulos e grave somente janela necessária; pare gravação logo após hitch.
- No Memory Profiler, ordene `Texture2D` e `Mesh` por `Size`, exporte top 20 com nome, tamanho e `Referenced By`. Otimize somente grupos dominantes.
- Compare Deferred/Forward no mesmo ponto. Registre `RenderDeferred.GBuffer`, `RenderDeferred.Lighting`, SetPass, shadow casters, RenderTextures, buffers e GPU frame time.

## Métricas CSV

- frame time médio, p50, p95, p99 e máximo;
- tempo de main thread e render thread;
- CPU/GPU frame time quando plataforma suporta `FrameTimingManager`;
- `GC Allocated In Frame`, heap GC e memória total do processo;
- draw calls, batches, SetPass calls e triângulos;
- cena e timestamp para localizar transições e loading.

Valor `0` indica contador indisponível na plataforma/build. Captura grava uma linha por segundo, encerra após 300 segundos por padrão e não coleta dados pessoais.

## Cenários reproduzíveis

Use mesmo save e rota gravada em vídeo como referência. Não misture cenários na mesma comparação.

1. `BASELINE`: menu parado por 180 s. Mede custo ocioso e vazamentos básicos.
2. `FLORESTA`: caminhar/correr por região com máxima densidade de grama, pedras e árvores por 300 s.
3. `CIDADE`: dirigir rota fixa por cidade densa, entrando em duas estruturas, por 300 s.
4. `HORDA`: mesma arena, número fixo de zombies, arma e efeitos por 300 s.
5. `VEÍCULOS`: mesma rota, veículo e velocidade aproximada por 300 s.
6. `MULTIPLAYER`: servidor local com quantidade fixa de clientes/bots, itens e estruturas por 900 s.
7. `SOAK`: cliente e servidor dedicados por 2 h; comparar memória no começo, 1 h e final.

Registre em cada resultado: commit, Unity, CPU, GPU, RAM, SO, resolução, preset, mapa, mods, número de entidades/jogadores e temperatura aproximada do hardware.

## Profiler e traces

1. Unity Profiler: Development Build, Autoconnect Profiler, Deep Profiling desligado. Capture CPU Timeline, GPU, Rendering, Memory e File Access durante trecho lento.
2. Memory Profiler: snapshot após aquecimento e após cenário; use `Compare Snapshots` para objetos retidos.
3. Frame Debugger: um frame de cidade, floresta e horda para localizar passes, materiais e draws repetidos.
4. dotTrace Timeline: somente quando Unity Profiler apontar custo managed sem causa clara ou file I/O/thread contention.

Experimento futuro de instancing: no Frame Debugger, selecione árvores/pedras repetidas e confirme mesh, material, shader e motivo do batch. Só testar exclusão seletiva do static batching quando existir grupo grande compatível; aceitar somente se Main/Render Thread, SetPass e RAM melhorarem juntos.

Não adicione APM remoto agora. CSV + ferramentas Unity cobrem diagnóstico local sem serviço, conta, custo ou impacto permanente no jogador.

## Capturar Unity Profiler CPU Timeline

### Preparar build

1. Abra `File > Build Settings`.
2. Selecione `PC, Mac & Linux Standalone`, Windows e arquitetura `x86_64`.
3. Marque `Development Build` e `Autoconnect Profiler`.
4. Deixe `Deep Profiling Support` e `Script Debugging` desligados na primeira captura. Deep Profiling altera custo e pode esconder comportamento real.
5. Gere build novo. Development Build é necessário para conexão do Profiler; Editor Play Mode não serve como baseline final.

### Gravar cenário

1. Abra `Window > Analysis > Profiler` antes de executar jogo.
2. Mantenha módulos `CPU Usage`, `Memory`, `Rendering`, `GPU Usage`, `Physics` e `File Access` disponíveis.
3. Execute build. Confirme player standalone no seletor de alvo do Profiler; `Autoconnect` deve selecioná-lo desde startup.
4. Ative gravação e limpe frames antigos antes de reprodução, se startup não for alvo.
5. Reproduza um cenário por captura:
   - boot até menu;
   - entrada no mesmo mapa até controle do personagem;
   - rota de streaming por cidade/floresta;
   - hitch aquecido observado após `60 s`.
6. Quando travamento ocorrer, pare gravação imediatamente. Preserve frames anteriores ao pico.

### Ler Timeline

1. Selecione frame alto no gráfico `CPU Usage`.
2. No painel inferior, escolha `Timeline`.
3. Expanda `Main Thread > PlayerLoop`. Procure bloco mais largo; anote `Total`, `Self` e cadeia pai.
4. Compare threads `Job.Worker`, loading e render. `WaitFor...` indica espera; custo causador pode estar em outra thread.
5. Troque para `Hierarchy`, ordene por `Total ms`; depois por `Self ms` e `GC Alloc`.
6. Durante loading, procure `Level`, `LevelObjects`, `AssetBundle`, `Resources.UnloadUnusedAssets`, `GC.Collect`, serialização e File Access.
7. Durante streaming, procure samples `PendingInstantiations`, `ItemManager.Update`, barricadas, estruturas e ativação de GameObjects.
8. Não conclua causa por nome pai genérico como `PlayerLoop`. Desça até primeiro método controlável que concentre tempo próprio ou filhos.

### Salvar evidência

1. Salve captura pelo botão `Save` do Profiler em arquivo `.data`.
2. Registre frame, cenário, mapa, posição, build/commit, hardware e preset.
3. Anexe screenshot do Timeline aberto no pico e exporte tabela curta: método, `Total ms`, `Self ms`, chamadas e `GC Alloc`.
4. Repita três vezes em build anterior e novo. Compare mediana e p95/p99; não compare Editor contra standalone.

Referências oficiais Unity 2022.3: [profiling de Player/Development Build](https://docs.unity3d.com/2022.3/Documentation/Manual/profiler-profiling-applications.html) e [CPU Usage/Timeline](https://docs.unity3d.com/2022.3/Documentation/Manual/ProfilerCPU.html).

Durante migração, mantenha documentação 2022.3 para reproduzir baseline antiga e use documentação correspondente a Unity `6000.3` para nova captura.

## Critério de comparação

- Compare mediana de três execuções.
- Ganho precisa superar variação entre execuções.
- Priorize p95/p99 e máximo; FPS médio esconde stutter.
- Rejeite mudança que reduz média mas aumenta GC, loading ou p99.
- Anexe somente resumo textual ao repositório; mantenha `.data`, snapshots e CSV grandes fora dele.
