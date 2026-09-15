# Тестирование EcaSystems

Тесты используют Unity Test Framework + NUnit. Production Runtime не содержит test-only кода. Карта всех 22 сценариев бывшего `EcaSystemsSmokeTest` находится в [TestMigration.md](TestMigration.md); дополнительные проверки покрывают валидацию Rule, Pending, расход Limit при ошибке и внутреннюю защиту Bind из старого .NET harness.

## Структура

- `Tests/Editor/Base/` — создание Rule, registry/selector, payload, роли контекстов и Fire.
- `Tests/Editor/Commands/` — регистрация, bind, аргументы, ошибки, разрешение ID при каждом вызове.
- `Tests/Editor/Execution/` — overlap, limits, состояния, счётчики, unregister и rollback.
- `Tests/Editor/Scope/` — идентификаторы, иерархия, lifetime и закрытые операции.
- `Tests/Editor/Integration/` — Execution + Commands и Scope + Execution + Commands.
- `Tests/Editor/Support/` — небольшие recording doubles и управляемые async Actions.

`EcaSystems.Editor.Tests.asmdef` ссылается на фактическую assembly `EcaSystems.Core`, включает только Editor и использует `optionalUnityReferences: ["TestAssemblies"]`.

Основная масса Core-сценариев — EditMode: `[TestFixture]`, `[Test]`, при необходимости `[TestCase]` и `async Task`. Controlled Actions завершаются явно; ограниченное ожидание продолжений Task не зависит от кадров. TearDown освобождает управляемые Actions и при падении проверки. Reflection используется для недоступного извне начального Pending, внутреннего Bind guard и проверки generic type contracts; production API для этого не расширяется.

PlayMode нужен для PlayerLoop, frames/coroutines, MonoBehaviour lifecycle, scenes и будущих Unity adapters/TimeSystem/Wait. Сейчас таких сценариев нет, поэтому Runtime test assembly и placeholder tests не создаются. С первым настоящим lifecycle-тестом добавятся `Tests/Runtime/Unity/` и `EcaSystems.Runtime.Tests.asmdef` с runtime reference и `TestAssemblies`.

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
