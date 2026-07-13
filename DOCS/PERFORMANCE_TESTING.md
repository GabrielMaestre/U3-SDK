# Testes de performance e stress

## Objetivo

Comparar builds no mesmo hardware, mapa, rota e preset. Captura local mede frame time, CPU, GPU, GC, memória e render sem enviar telemetria.

## Captura rápida

1. Gere `Build Test` ou Development Build Windows 64-bit.
2. Desative VSync e mantenha resolução, preset gráfico, mapa e mods idênticos.
3. Execute:

```powershell
.\Builds\Test\Unturned.exe -PerformanceMetrics -PerformanceMetricsSeconds=300 -FrameRateLimit=0
```

4. Aguarde 60 segundos de aquecimento antes de iniciar rota.
5. Repita rota por pelo menos 180 segundos e execute três capturas por build.
6. CSV fica em `Application.persistentDataPath\PerformanceCaptures`; caminho completo aparece no log como `Performance metrics capture started`.

No Editor, habilite `Window > Unturned > Editor Settings > Misc > Performance Metrics`. Resultado válido deve vir do executável standalone, porque Editor adiciona CPU, GC e render próprios.

## Play Mode com menos lag

1. Fora do Play Mode, abra `Window > Unturned > Editor Settings > Playing in Unity`.
2. Ative `Editor Performance Mode`; deixe `Cinematic` desligado.
3. Entre novamente no Play Mode. Se opções de Enter Play Mode estiverem sem Domain Reload, reinicie Unity após alterar toggle.
4. Maximize aba `Game`, feche ou oculte `Scene`, desligue `Gizmos` na aba Game e deixe somente módulos necessários do Profiler gravando.
5. Modo limita câmera a `768 m` somente no Unity Editor. Visual distante pode desaparecer; física, rede, regras e Player build não mudam.
6. Para medir efeito, capture mesmo local com modo desligado/ligado e compare `Camera.Render`, `Culling`, `Render.OpaqueGeometry`, `BatchRenderer.Flush`, draw calls e batches.

Não use modo como baseline visual ou comparação com build. Desative toggle antes de validar pop-in, LOD e distância final.

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

## Critério de comparação

- Compare mediana de três execuções.
- Ganho precisa superar variação entre execuções.
- Priorize p95/p99 e máximo; FPS médio esconde stutter.
- Rejeite mudança que reduz média mas aumenta GC, loading ou p99.
- Anexe somente resumo textual ao repositório; mantenha `.data`, snapshots e CSV grandes fora dele.
