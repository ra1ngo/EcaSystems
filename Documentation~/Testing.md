# Тестирование EcaSystems

Тесты используют Unity Test Framework + NUnit. Production Runtime не содержит test-only кода. Карта всех 22 сценариев бывшего `EcaSystemsSmokeTest` находится в [TestMigration.md](TestMigration.md); дополнительные проверки покрывают валидацию Rule, Pending, расход Limit при ошибке и внутреннюю защиту Bind из старого .NET harness.

## PR #17 follow-up: Time/Eca structure и PlayerLoop separation — 2026-09-18

Commands перенесены в Eca/Commands, TimeWaitArgs — в standalone Time/Core (namespace EcaSystems.Time); GUID всех перемещённых файлов сохранены. Internal EcaTimeEvent и local internal Event/Command key → string mappings заменяют вложенную declaration и public raw ID constants. Строковые runtime IDs не изменились. Добавлены 3 Time/Eca tests: полное и уникальное соответствие event keys, command keys (включая отклонение неизвестного key), принадлежность TimeWaitArgs standalone assembly. Прежние tests проверяют concrete EcaTimeEvent, canonical caching, Commands delegation, lifecycle snapshots, Connect/Disconnect и Wait.

TimeSystemPlayerLoop владеет Unity hook/initialization и передаёт одну пару clocks в TimeTicker. PlayerLoop tests адаптированы к этой границе без ослабления assertions: idempotent installation, сохранение чужих entries, общие ticks независимых instances, nested ticks и 0 managed bytes на 100 warmed updates при прежнем/меньшем количестве активных Systems. TimeTicker сохраняет cached subscriber snapshot, clocks-before-callbacks и failure isolation. TimerTickProcessor, ScaleMode/lifecycle architecture и Core/Core1/Core2 не менялись.

Unity **6000.5.6f1**, фактически выполненные batchmode EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Focused Time/Core assembly | 53 | 53 | 0 | 0 | 0 |
| Focused Time/Eca assembly | 16 | 16 | 0 | 0 | 0 |
| Focused TimePlayerLoopTests | 4 | 4 | 0 | 0 | 0 |
| Полный regression suite | 376 | 376 | 0 | 0 | 0 |

Полный suite: Core 37, Core1 77, Core2 193, standalone Time 53, Time/Eca 16. Все четыре запуска завершились с exit code 0. После focused Time/Eca run mapping assertions усилены с EquivalentTo до EqualTo для проверки соответствия порядку ключей; финальный полный suite выполнил усиленные assertions. В логах этих запусков нет compiler errors/warnings. XML/log находятся в игнорируемой .validation~ с префиксами pr17-structure-core, pr17-structure-eca, pr17-structure-playerloop, pr17-structure-full. PlayMode, IL2CPP и удалённый CI не запускались. ScaleMode/SOLID cleanup остаётся отдельной будущей итерацией.

## PR #17 follow-up: canonical registries и Time/Eca cleanup — 2026-09-18

Четыре simple registries используют live read-only items, public Resolve и exact-instance CheckRegistered. Generic Event Register<E> удалён; typed metadata проверяется потребителем. EcaSystem хранит local Event/Command registries, Connector передаёт exact references и отклоняет foreign replacements до Detach mutation. EventEmitter production/API, RuleRegistry/ExecutionGroupRegistry и standalone Time/Core не менялись.

Добавлены 24 Core2 cases: 16 для четырёх registries (live view, identity, lookup, duplicate, Ordinal, unregister/re-registration, null/invalid IDs), 7 для System/Connector (local/global identity, unrelated registrations, foreign export/namespace replacement, Attach/Detach rollback, обязательные constructor dependencies), 1 для canonical-only Fire. Прежние Event tests адаптированы к non-generic registration и identity. Rule metadata validation проверяется на canonical зарегистрированной declaration, не на чужом instance.

Time/Eca прежние 8 cases переведены на local registries/concrete Commands; добавлены 5 cases для однократного resolve/cache, missing events, несовместимых interface/metadata и null constructor dependencies. EcaTimeEventState сохраняет immutable/reentrant snapshot semantics; metadata GUID сохранён при rename. TimeEcaEvents/TimeEcaCommandIds удалены, команды проверяются по concrete types. Всего добавлено **29 cases**, standalone Time tests сохранены без изменений.

Unity **6000.5.6f1**, фактически выполненные EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Focused Core2 registry/System/EventEmitter | 62 | 62 | 0 | 0 | 0 |
| Focused Time/Eca | 13 | 13 | 0 | 0 | 0 |
| Полный regression suite | 373 | 373 | 0 | 0 | 0 |

Полный suite: Core 37, Core1 77, Core2 193, standalone Time 53, Time/Eca 13. После финального review сохранена прежняя System ID validation в Connector.Detach через Resolve; повторный полный run pr17-cleanup-final также дал 373/373 passed, 0 failed/skipped/inconclusive. Все четыре запуска завершились с exit code 0. Standalone Time отдельно не запускался, поскольку его code/tests не менялись; все 53 cases выполнены полным suite. Первоначальная компиляция повторила прежний CS0108 в EcaScopeTests.Fire(int), новых compiler warnings нет.

XML/log находятся в игнорируемой .validation~ с префиксами pr17-registry-focused, pr17-time-eca, pr17-cleanup-full, pr17-cleanup-final. PlayMode/IL2CPP и удалённый CI не запускались. Политика rollback по-прежнему требует стабильной configuration и exception-safe registry operations; tests инъецируют отказ до mutation, не утверждают универсальную атомарность произвольного custom registry.
## TimeSystem refactor + Time/Eca — 2026-09-18

Актуальная архитектура заменяет исторические TimerRunner/snapshot детали ниже: TimeTicker, один TimerRegistry, TimerController/TimerTickProcessor с due-only двухфазным batch и отдельные WaitRegistry/WaitTimer/WaitTickProcessor. Предыдущие 46 standalone cases адаптированы к system-level lifecycle events и frame-visible Timer data, без потери lifecycle/error/reentrancy coverage. Добавлены 7 архитектурных cases: subscription/unsubscription по Timer/Wait work, ticker clocks и изоляция failures, Timer error без starvation Wait/другой System, новые Timer/Wait из callbacks, обновление всех frame values до callbacks, Pause/Destroy по текущим clocks и failing Wait continuation без starvation.

Добавлена отдельная assembly EcaSystems.Time.Eca.Editor.Tests в Tests/Editor/Systems/Time/Eca: references EcaSystems.Time, EcaSystems.Time.Eca, EcaSystems.Core2 и TestAssemblies. Её 8 cases проверяют шесть typed Event exports по identity, семь Commands, все lifecycle forwards, immutable reentrant completion snapshot, Connect/Disconnect/reconnect, Attach-before-Connect и Disconnect-before-Detach, Wait Task для двух scale modes, validation/typed args. Используется test-only IEcaEventEmitter double с реальным EcaBaseEventRegistry; production Core2 composition/internal emitter binding не добавлялись. Standalone test assembly не зависит от adapter/Core2.

Unity **6000.5.6f1**, фактически завершённые batchmode EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Focused standalone Time, final | 53 | 53 | 0 | 0 | 0 |
| Focused Time/Eca | 8 | 8 | 0 | 0 | 0 |
| Полный regression suite | 344 | 344 | 0 | 0 | 0 |

Все три финальных запуска — exit code 0. Полный suite: Core 37, Core1 77, Core2 169, Time 53, Time/Eca 8. До них первый запуск с namespace filter захватил обе Time assemblies: 59 passed / 2 failed из 61 из-за ожидания заголовка AggregateException вместо первой строки inner exception в Unity log. Исправлены только LogAssert expectations; для отдельных focused runs затем использован assemblyNames.

Сохранённые allocation cases показывают 0 managed bytes за 100 steady-state ticks после прогрева при прежнем/меньшем числе timers/systems. Due buffers и cached ticker invocation list переиспользуются; изменение подписок, capacity growth и ошибки находятся вне этого steady-state утверждения. Ticker subscriber exceptions в error tests ожидаемо логируются; остальные tests не генерируют неожиданных ошибок. В logs запусков этой итерации compiler warnings не обнаружены.

Результаты/logs в игнорируемой .validation~: time-adapter-core-results.xml / time-adapter-core.log (первая попытка), time-adapter-core-final-results.xml / time-adapter-core-final.log, time-adapter-eca-results.xml / time-adapter-eca.log, time-adapter-full-results.xml / time-adapter-full.log. PlayMode/Sandbox, IL2CPP/player build и удалённый CI в этой итерации не запускались. Core/Core1/Core2 production и tests не менялись.
## Standalone TimeSystem Core v1 — 2026-09-17

Новая изолированная assembly `EcaSystems.Time.Editor.Tests` в `Tests/Editor/Systems/Time` references только `EcaSystems.Time` и TestAssemblies. Runtime assembly использует UnityEngine и не references ECA assemblies. Добавлены **42 NUnit cases**, существующие 283 cases не менялись.

Покрыты creation/lookup/Ordinal IDs/instance isolation, null/invalid IDs/options/duration/scale, все разрешённые и запрещённые lifecycle transitions, event order/count, timing values, pause/resume/reset-on-stop, restart-after-completion, destroy из всех live states, terminal old instance и ID reuse из callback. Дополнительно проверены double precision/overshoot, independent Scaled/Unscaled clocks, zero-duration, reentrant start/destroy/replacement, callback errors без потери остальных timers, повторные Wait и Awaitable continuation, отсутствие Wait в public registry.

Deterministic seam — internal constructor с time reader/active-runner callback и internal Tick(nowScaled, nowUnscaled), доступные через InternalsVisibleTo только Time test assembly. Нет sleeps, public clock/Tick или ECA test dependencies. Awaitable потребляется один раз; test-only Task используется лишь для наблюдения окончания async continuation. PlayerLoop tests работают с реальным GetCurrentPlayerLoop/SetPlayerLoop, проверяют повторную установку одного hook и исполняют установленный delegate с двумя public TimeSystem instances; исходный loop восстанавливается в TearDown.

Unity **6000.5.6f1**, два локальных batchmode EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Time focused | 42 | 42 | 0 | 0 | 0 |
| Полный regression suite | 325 | 325 | 0 | 0 | 0 |

Полный run: Core 37, Core1 77, Core2 169, Time 42. Оба запуска завершились с exit code 0. Новых compiler warnings нет; при первой компиляции остался прежний CS0108 в Core2 EcaScopeTests.Fire(int), вне scope. XML/log сохранены в игнорируемой `.validation~`: `time-v1-focused-results.xml` / `time-v1-focused.log`, `time-v1-full-results.xml` / `time-v1-full.log`.

Это EditMode compilation/runtime verification, не PlayMode scene/frame run и не IL2CPP/player build. Scene independence следует из отсутствия scene objects/dependencies; смена сцен отдельным тестом не запускалась. Удалённый GameCI не запускался. API Unity сверены с [PlayerLoop](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/LowLevel.PlayerLoop.html) и [AwaitableCompletionSource](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AwaitableCompletionSource.html).

## PR #16: устранение tick allocations — 2026-09-17

TimeSystemPlayerLoop.Update и TimerRunner.Tick переиспользуют snapshot lists из внутренних Stack pools. Capacity растёт только при необходимости; вложенный tick получает отдельный свободный buffer. В finally списки очищаются от references и возвращаются в pool, включая exception path. Snapshot до callbacks, проверка Active.Contains, пары (Timer, Version), restart/mutation protection и aggregation ошибок сохранены. Public API, lifecycle, Wait и архитектура не менялись.

42 прежних Time cases сохранены без изменений; добавлены 4 focused tests в TimeSystemTests/TimePlayerLoopTests: вложенные Tick/Update и allocation checks при прежнем/меньшем числе объектов. После 10 warm-up вызовов GC.GetAllocatedBytesForCurrentThread показывает **0 bytes** за 100 вызовов в каждом измеренном блоке: 16 timers и затем 1; 8 runners и затем 1. Setup, создание объектов, installation и NUnit assertions находятся вне измеряемого участка. Это проверка steady-state пути без callbacks/errors; allocation при capacity growth, первой новой глубине вложенности и обработке exceptions допустима.

Unity **6000.5.6f1** batchmode EditMode: Time **46/46 passed**, полный suite **329/329 passed** (Core 37, Core1 77, Core2 169, Time 46). Оба запуска: 0 failed/skipped/inconclusive, exit code 0. XML/log в игнорируемой `.validation~` с префиксами `time-allocation-focused` и `time-allocation-full`. GitHub Actions отдельно не проверялся. Из документации изменён только Testing.md.

## Структура

- `Tests/Editor/Base/` — создание Rule, registry/selector, payload, роли контекстов и Fire.
- `Tests/Editor/Commands/` — регистрация, bind, аргументы, ошибки, разрешение ID при каждом вызове.
- `Tests/Editor/Execution/` — overlap, limits, состояния, счётчики, unregister и rollback.
- `Tests/Editor/Scope/` — идентификаторы, иерархия, lifetime и закрытые операции.
- `Tests/Editor/Integration/` — Execution + Commands и Scope + Execution + Commands.
- `Tests/Editor/Support/` — небольшие recording doubles и управляемые async Actions.
- `Tests/Editor/Systems/Time/` — standalone TimeSystem, отдельная `EcaSystems.Time.Editor.Tests` с reference только на `EcaSystems.Time` и TestAssemblies; нет ECA dependencies.

`EcaSystems.Editor.Tests.asmdef` ссылается на фактическую assembly `EcaSystems.Core`, включает только Editor и использует `optionalUnityReferences: ["TestAssemblies"]`.

Основная масса Core-сценариев — EditMode: `[TestFixture]`, `[Test]`, при необходимости `[TestCase]` и `async Task`. Controlled Actions завершаются явно; ограниченное ожидание продолжений Task не зависит от кадров. TearDown освобождает управляемые Actions и при падении проверки. Reflection используется для недоступного извне начального Pending, внутреннего Bind guard и проверки generic type contracts; production API для этого не расширяется.

PlayMode нужен для настоящих frame/scene/MonoBehaviour lifecycle scenarios и будущих Unity adapters. TimeSystem Core v1 проверяет timing детерминированно и PlayerLoop install/delegate в EditMode, без scene dependencies и heavyweight PlayMode test. Runtime test assembly и placeholder tests не создаются; они появятся с первым сценарием, которому реально нужны PlayMode/сцены.

## Локальный запуск

1. Открыть Unity-проект с установленным EcaSystems и Unity Test Framework. CI использует Unity **6000.3.19f1**; локально для checkpoint доступна **6000.5.6f1**.
2. В **проектном** `Packages/manifest.json` добавить `"testables": ["com.ecasystems.framework"]` рядом с `dependencies`. Не добавлять `testables` в package.json EcaSystems. Это отдельная настройка Sandbox; его файлы эта миграция не меняет.
3. Открыть **Window → General → Test Runner**, вкладку **EditMode**, выбрать `EcaSystems.Editor.Tests` и **Run All**.
4. Смотреть ошибки в Test Runner; результат можно экспортировать в XML.

Не требуется многократно запускать полный Unity suite после каждой правки. Для bootstrap достаточно проверки структуры/компиляции и максимум одного финального локального прогона, если среда готова. Старый .NET smoke harness удалён вместе с компонентом; отдельного дублирующего стека проверок больше нет.

## GitHub Actions

Workflow: [tests.yml](../.github/workflows/tests.yml), имя **Tests**, job **Unity EditMode**. Unity **6000.3.19f1**, `game-ci/unity-test-runner@v4`, Linux runner. CI — authoritative автоматическая проверка PR. Recovery-срез до checkpoint сообщает прошлый CI результат 31/31 passed; это не результат новой ветки.

Checkout выполняется в корень. Шаг Prepare package for GameCI копирует пакет в `_ci/EcaSystemsPackage`, исключая `.git`, `.github`, `_ci` и каталоги результатов. `packageMode: true` и `projectPath: _ci/EcaSystemsPackage` обходят ограничение package в корне checkout. GameCI создаёт временный Unity project. В package manifest не добавляются зависимости на тестовый framework или `testables`.

Сейчас один **EditMode** job, без матрицы и без PlayMode job. PlayMode tests отсутствуют; job следует добавить вместе с первым настоящим Unity lifecycle-сценарием, без placeholder tests.

Триггеры: `pull_request`, `push` только в `main`, `workflow_dispatch`. Обычный push feature-ветки сам не запускает CI: пользователь открывает PR вручную. Branch protection не меняется.

Workflow читает только имена secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`; значения не хранятся в repo и не выводятся. Personal license должна быть подготовлена владельцем репозитория. `GITHUB_TOKEN` используется для check results; permissions ограничены `contents: read`, `checks: write`. PR из fork не получает Unity secrets автоматически.

Результаты смотреть в PR checks (**EcaSystems EditMode Tests**) и **Actions → Tests → run → Artifacts**: `unity-editmode-results` содержит test results и logs из `artifacts/EditMode`. Upload выполняется с `if: always()`, включая неуспешный запуск; если Unity не смог создать файлы, upload сообщит об их отсутствии. Generated artifacts не коммитятся.

Input `coverageEnabled` в текущем workflow **не задан**. Code Coverage package не добавлен в repository/package manifest, но это не гарантирует отключение coverage во временном проекте GameCI. По recovery-срезу прошлая попытка `coverageEnabled: false` породила несовместимый CLI flag `--no-coverageEnabled`, после чего input убрали; прошлый CI сообщил 31/31 passed вместе с ошибкой генерации coverage report. Workflow в checkpoint не менялся. Coverage, performance tests и optional required checks/branch protection остаются отдельными улучшениями.

## История bootstrap-миграции

Все 22 исходных сценария и internal binding guard перенесены; с дополнительными проверками — 31 NUnit case. Отдельная компиляция Core и tests с C# 9 и установленной Unity NUnit assembly прошла без ошибок/предупреждений; один дополнительный NUnit run вне Unity: 31 passed, 0 failed. Это не замена Unity CI.

Во время bootstrap локальная попытка Unity 6000.5.6f1 обнаружила два обращения к internal constructor из test assembly и остановилась до выполнения tests. Они исправлены посредством reflection; тогда повторный Unity run не выполнялся. Более поздний recovery-срез сообщает CI 31/31 passed на 6000.3.19f1.

Настройка основана на [GameCI Test runner v4](https://game.ci/docs/github/test-runner/) и [входных параметрах action](https://github.com/game-ci/unity-test-runner/blob/v4/action.yml).

## Проверки architecture checkpoint — 2026-09-11

- Раздельная компиляция Core и Editor tests с C# 9 через локальный .NET SDK: успешно. Использован существующий игнорируемый проект .validation~, новый тестовый стек в репозиторий не добавлялся.
- NUnit вне Unity с установленной Unity NUnit assembly: **37 passed, 0 failed, 0 skipped**.
- Один локальный Unity **6000.5.6f1** batchmode EditMode run в подготовленном .validation~/UnityProject: **37 passed, 0 failed, 0 skipped**, результат XML — Passed. Это другая версия и среда, чем Linux/GameCI CI на 6000.3.19f1.
- Сохранены 31 прежний case; добавлены 6 cases для общего живого Execution state, наследования/variance и разделения ролей, ScopeState lifetime/повторного ID и валидации ID.
- ScopeState внутри runtime-контекстов не тестируется как реализованная возможность: wiring отсутствует и явно отложен в Context/ToDo. Существующие integration tests продолжают проверять локальный Fire, независимость GroupState/Limit и Dispose без отмены Actions.

В этой ветке PlayMode и удалённый GameCI не запускались. Push feature-ветки не запускает workflow автоматически; PR создаёт пользователь.

## Core2 Execution — 2026-09-14

Core2 tests находятся в `Tests/Editor/Core2`, assembly `EcaSystems.Core2.Editor.Tests` с reference на `EcaSystems.Core2`. Используется существующий Unity/NUnit путь, без нового внешнего .NET test project.

Добавлен 61 NUnit case: mode validation, live State/counters, Check без admission, fresh RuleState для фаз, Pending/Running/Completed/Failed, синхронные и отложенные ошибки/null Task/createState, overlap и lifetime Limit, typed GroupRegistry, rollback, unregister/re-registration, ALL CONDITIONS → ALL EXECUTIONS, immediate/reentrant Fire, поздний Ignore/Limit admission, snapshot выбранных Groups и Base Fire без lifecycle. Тесты используют управляемые Tasks с ограниченным ожиданием и освобождением в TearDown.

Локальный полный Unity 6000.5.6f1 batchmode EditMode run: **202 passed, 0 failed, 0 skipped** (Core — 37, Core1 — 77, Core2 — 88: 27 Base + 61 Execution). Core2 компилируется в составе package с прежним asmdef и `noEngineReferences`. Первая попытка остановилась до тестов на двух конфликтах имени тестового Action с System.Action; после уточнения имён повторный полный запуск прошёл. XML и logs хранятся в игнорируемой `.validation~`, в коммит не включены.

PlayMode scenarios отсутствуют; удалённый GameCI в рамках этой итерации не запускался. Старые Core/Core1 и Core2/Base tests сохранены без изменений.

## Core2 Scope — 2026-09-15

Добавлен 21 NUnit case в `Tests/Editor/Core2/EcaScopeTests.cs`: ScopeId/state validation, default Register и typed ExecutionGroup, расширение готового IEcaExecutionRuleState через Func, пользовательский rich State/contexts, свежие состояния фаз, общий ScopeState, barrier/selection, overlap/limit, изоляция независимых ExecutionRuntime с общей Event/Rule declaration, direct reentrant Fire, Unregister активного запуска, ошибки extension и Base Fire с caller state.

Полный локальный Unity 6000.5.6f1 batchmode EditMode suite: **223 passed, 0 failed, 0 skipped**. Core — 37, Core1 — 77, Core2 — 109 (27 Base + 61 Execution + 21 Scope). Компиляция package успешна с существующим Core2 asmdef и noEngineReferences; все 202 прежних теста сохранены. XML/log: `.validation~/core2-scope-unity-results.xml` и `.validation~/core2-scope-unity.log`, не коммитятся. PlayMode scenarios отсутствуют; удалённый GameCI в этой итерации не запускался.

## Core2 Scope manager/lifetime — PR #10, 2026-09-15

21 прежний Scope case адаптирован к `EcaScope` и `EcaScopeRuntime(IEcaEventRegistry)`. Сохранены state-enrichment, custom R/contexts, default Register, barrier, reentrancy, Base-level Fire и Unregister. Добавлены 13 cases в EcaScopeRuntimeTests: manager count/lookup, auto-id/duplicates, parent/child/grandchild hierarchy, recursive/idempotent Dispose, guards всех per-scope операций, reuse/stale identity, строго локальное выполнение обоих overloads Fire, shared Rule с независимыми registries/limits/overlap, live EventRegistry, завершение active Actions после Scope/parent/manager Dispose без влияния на replacement.

Полный Unity 6000.5.6f1 batchmode EditMode suite: **236 passed, 0 failed, 0 skipped**. Core — 37, Core1 — 77, Core2 — 122 (27 Base + 61 Execution + 34 Scope). Компиляция package успешна, Base/Execution/Core/Core1 не изменены. Результаты и log — `.validation~/core2-scope-manager-unity-results.xml` / `.validation~/core2-scope-manager-unity.log`; артефакты не коммитятся. PlayMode отсутствует, удалённый GameCI отдельно не запускался.

## Core2 Commands validation — 2026-09-15

Добавлены 28 NUnit cases в EcaCommandRegistryTests, EcaCommandRunnerTests и EcaCommandsBaseRuntimeTests: registry/lookup, live registration, Bind identity/independence, typed context/args и null, async Tasks и ошибки, end-to-end Action → Command, Conditions/barrier и отдельные Base Fire calls. Тестовый composition context реализует IEcaCommandsActionContext; production Commands, Base, Execution, Scope и старые Core/Core1 не менялись.

Два локальных Unity 6000.5.6f1 batchmode EditMode запуска завершились успешно: focused Commands — **28 passed, 0 failed, 0 skipped**; полный suite — **264 passed, 0 failed, 0 skipped, 0 inconclusive**. Полный результат по assemblies: Core — 37, Core1 — 77, Core2 — 150 (27 Base + 61 Execution + 34 Scope + 28 Commands). XML/log: `.validation~/core2-commands-focused-results.xml`, `.validation~/core2-commands-focused.log`, `.validation~/core2-commands-full-results.xml`, `.validation~/core2-commands-full.log`; артефакты не коммитятся. PlayMode scenarios отсутствуют; удалённый GameCI не запускался.

## PR #12: предыдущие проверки — 2026-09-15

До упрощения transport выполнены Unity 6000.5.6f1 EditMode runs: первоначальные concept tests 22/22, Core2 172/172, полный suite 286/286; после ограничения public factory — concept 25/25, Core2 175/175, полный suite 289/289. Все без failures/skips. Эти transport tests заменены проверками прямого Emitter ниже; прежние XML/log сохранены в игнорируемой `.validation~`.

## PR #12 EventEmitter — 2026-09-15

Старые transport tests заменены 11 standalone Emitter cases; 5 Base integration cases адаптированы к прямому Fire<E> через IEcaEventEmitter. Удалён отдельный lifecycle case снятия подписки, поскольку Unbind/Dispose в новой модели не предусмотрены. Всего 16 concept cases вместо 25: binding/null/duplicate/unbound, registry contract и live validation, несовместимая metadata, ошибки handler, три unrelated E, declared BaseState с DerivedState/null, class/struct values, Base Conditions/barrier и reentrant Fire. Все остальные 150 Core2 cases сохранены. Internal Bind тестируется через InternalsVisibleTo только для EcaSystems.Core2.Editor.Tests, публичный порт содержит только Fire<E>.

Unity 6000.5.6f1, три batchmode EditMode запуска: EventEmitter — **16 passed**, все Core2 — **166 passed**, полный suite — **280 passed** (Core — 37, Core1 — 77, Core2 — 166). Везде **0 failed, 0 skipped, 0 inconclusive**, exit code 0. XML/log: `.validation~/pr12-emitter-focused-results.xml` / `pr12-emitter-focused.log`, `pr12-emitter-core2-results.xml` / `pr12-emitter-core2.log`, `pr12-emitter-full-results.xml` / `pr12-emitter-full.log`; артефакты не коммитятся. PlayMode и удалённый GameCI не запускались. Base/Execution/Scope/Commands/Core/Core1 не изменены.

## PR #13: accessibility EventEmitter — 2026-09-15

Public оставлен только IEcaEventEmitter; EcaEventEmitter стал internal sealed, IEcaEventHandler — internal. Bind остался internal. Поведение и tests не менялись; существующий InternalsVisibleTo обеспечивает доступ тестовой композиции.

Unity 6000.5.6f1 batchmode EditMode: EventEmitter **16/16 passed**, Core2 **166/166 passed**, полный suite **280/280 passed** (Core — 37, Core1 — 77, Core2 — 166). Во всех трёх запусках 0 failed/skipped/inconclusive, exit code 0. XML/log сохранены в игнорируемой `.validation~` с префиксами `pr13-accessibility-focused`, `pr13-accessibility-core2`, `pr13-accessibility-full`. Commands/Base/Execution/Scope/Core/Core1 не изменены. PlayMode и удалённый GameCI не запускались.

## Core2 Command abstraction — 2026-09-17

Добавлены 3 cases: non-generic registration с сохранением Command instance, runtime metadata/default erased bridge с derived context/args и исходным Task, heterogeneous IEcaCommand[] с разными C/A и регистрацией одним foreach. Один прежний null-registration case адаптирован к Register(IEcaCommand); остальные behavioural tests сохранены, concrete test Commands не получили metadata/casts/bridge boilerplate.

Default interface implementation IEcaCommand<C,A> реально скомпилирована и выполнена в Unity 6000.5.6f1. Три batchmode EditMode запуска: Commands **31/31 passed**, все Core2 **169/169 passed**, полный suite **283/283 passed** (Core — 37, Core1 — 77, Core2 — 169). Везде 0 failed/skipped/inconclusive, exit code 0. Новых compiler warnings нет; остаётся прежний CS0108 в EcaScopeTests.Fire(int), вне scope задачи. Проверка не включает IL2CPP/player build; PlayMode и удалённый GameCI не запускались.

XML/log сохранены в игнорируемой `.validation~` с префиксами `command-abstraction-focused`, `command-abstraction-core2`, `command-abstraction-full`. Существующие `.meta` сохранены, GUID уникальны. Base/Execution/Scope/EventEmitter/Core/Core1 не изменены; Systems не реализованы.

## Core2 Systems v1 / AEcaCommand — 2026-09-17

Новых Systems tests и новых test cases не добавлено по согласованному scope. CommandTestSupport переведён на AEcaCommand<C,A> с override Id/Run, существующие Commands registry tests используют AEcaCommand и новый test-only CommandId для задания Id. Единственный test EventRegistry адаптирован к расширенному interface. Semantic assertions сохранены.

Unity 6000.5.6f1 batchmode EditMode: существующий Core2 suite **169/169 passed**; полный suite после финальной правки Connector **283/283 passed** (Core — 37, Core1 — 77, Core2 — 169). Везде 0 failed/skipped/inconclusive, exit code 0. Systems production code скомпилирован, но отдельных runtime tests Attach/Detach/rollback в этой итерации нет; они проверены review кода, не тестовыми сценариями. Новых compiler warnings нет; прежний CS0108 в Scope tests сохранён.

XML/log: `.validation~/systems-v1-core2-results.xml` / `systems-v1-core2.log`, `systems-v1-full-results.xml` / `systems-v1-full.log`; не коммитятся. IL2CPP/player build, PlayMode и удалённый GameCI не запускались. Существующий Command .meta/GUID сохранён при rename; новые Systems metadata добавлены, GUID уникальны.
