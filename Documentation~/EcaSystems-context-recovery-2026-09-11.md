# EcaSystems — контекст для продолжения работы

Дата актуализации: 2026-09-12, Core1 Execution + Scope.

Этот recovery-файл обновлён после Core1 Execution/Scope. Актуальные решения определяет [Context.md](Context.md), ближайшие этапы — [ToDo.md](ToDo.md), будущие возможности — [Roadmap.md](Roadmap.md). Следующий этап — review Core1 Execution/Scope; Commands/Systems только после отдельного обсуждения.

[Полный исторический recovery snapshot](Architecture/EcaSystems-context-recovery-full-2026-09-11.md) сохранён отдельно из base/main; он может содержать устаревшие решения и не переопределяет этот краткий срез и более новые решения.

## Core1: Abstractions → Base → Execution → Scope

Реализована параллельная вертикаль в Runtime/Core1, namespace/assembly EcaSystems.Core1. Временная ось A, плоская Abstractions, отдельные Base/Execution/Scope. IEcaRuleRun теперь отдельный пустой interface в Abstractions, IEcaRuleRunner остаётся в Base. Старый Runtime/Core, его tests и historical full snapshot не менялись. Core1 остаётся prototype до review, не окончательной заменой старого Core.

Event declaration-only, Rule = 1 Event + optional Condition + 1 Action, прямые ссылки. Condition/Action — программные executable declarations с metadata. RuleState = data, RunnerContext = infrastructure; marker contexts не содержат Commands/services. EcaEventStateEmpty без Value и Base/empty shortcuts сохранены. Нет Systems, Commands integration, cancellation, queues, StateBuilder/ContextFactory, ActionRegistry/ConditionRegistry, Rule statuses и универсального ExecutionState.

### Изменения Base и общий bridge

EcaRuleRunner больше не sealed; CreateRun/Check/RunAction virtual. Общий occurrence visitor вызывает ValidateRule<T>, затем protected virtual CreateTyped<T>. Слой создаёт State и contexts и передаёт их в унаследованный protected CreateRunCore<T, TS, TC, TA>. Единственный private RuleRun<T, TS, TC, TA>/RunBridge<T, TS, TC, TA> находится в Base. Token хранит typed Rule, State, две роли infrastructure и owner, но не выполняется сам. Bridge восстанавливает типы и вызывает методы RuleRunner, которые используют независимые ConditionRunner/ActionRunner. Private pairing и owner check защищают приведение. Dynamic/reflection execution нет.

EcaExecutionRuleRunner наследует EcaRuleRunner: получает Group, создаёт Execution State и общий inner token, оборачивает его в пассивный ExecutionRuleRun(owner, inner, group). Check → base.Check(inner), RunAction → group.Run(() => base.RunAction(inner)). EcaScopeRuleRunner наследует Execution runner и переопределяет только compatibility и typed creation Scope State/contexts. GetGroup/WrapRun, Check/RunAction, admission/lifecycle и большой Base bridge переиспользуются. Копий RunBridge по слоям нет.

C# не позволяет извлечь неизвестные generic-параметры прямо из opaque interface, поэтому сохранены visitor/adapter с четырьмя generic-параметрами. Это предусмотренное инструкцией расширение; замена concrete State contracts интерфейсами не потребовалась. IEcaRuleRun перенёс файл, сохранив namespace/assembly. Новые Base public API: ValidateRule<T>(IEcaRule), canonical EcaBaseEngine.Register/Unregister, наследуемый runner и protected hooks/core. Других Base public signatures не меняли, кроме требуемой visibility Fired.

### Registration и dispatch

Fired теперь **internal infrastructure C# event**, не публичный callback. Fire остаётся public, немедленный и reentrant. Replacement public event и InternalsVisibleTo не добавлены. External callback — только Roadmap.

Canonical BaseEngine.Register вызывает validation текущего runner до RuleRegistry.Register. Validation проверяет EventStateType и точную specialization; несовместимость — ошибка Register, не Fire. Defensive check в CreateRun сохранён. Low-level RuleRegistry остаётся storage без знания runner. Standalone Execution.Register<T> принимает только EcaExecutionRuleState<T>/Execution contexts, Scope.Register<T> — только EcaScopeRuleState<T>/Scope contexts; чужие специализации не компилируются через эти public signatures. Internal Execution RegisterCore нужен Scope composition, public escape hatch отсутствует.

Execution Register: BaseEngine validate/register → ExecutionRegistry.Register(rule, mode), при ошибке второго этапа rollback Base. Canonical duplicate Register отвергается. Low-level ExecutionRegistry допускает no-op только для того же Rule instance и равного mode; другой instance/mode запрещён. Unregister удаляет Rule по identity и Group; re-register создаёт новую Group/GroupState/Limit lifetime. Custom metadata после регистрации предполагается стабильной.

EventRegistry shared и заполняется извне; unknown Fire fails и никогда не регистрирует Event. Identity = Id + точный EventStateType. Каждая runtime-композиция имеет local Dispatcher/RuleRegistry/ExecutionRegistry/runner/BaseEngine.

### Exact Execution pipeline

EcaExecutionEngine(shared events) создаёт local RuleRegistry, Dispatcher, ExecutionRegistry, ExecutionRuleRunner и один EcaBaseEngine. Facade Fire делегирует Dispatcher. Только BaseEngine выполняет select snapshot → CreateRun всех → Check всех → RunAction passed. Engine не знает Execution/Scope и не содержит их admission. TryGetGroup позволяет наблюдать Group без выдачи изменяемого registry.

После всех Conditions Execution wrapper вызывает Group.Run: Limit → Overlap → rejected без counters/execution, либо new RuleExecution(Pending) → add active → Running + TotalStarted → Action callback → await Task → Completed либо Failed + Exception → TotalFinished ровно один раз → remove active. Group и RuleExecution non-generic; Group не знает типы State/contexts и получает Func<Task>.

EcaExecutionMode — Core1 имя прежнего EcaRunMode. Limit=-1 unlimited, 0 запрещает старт, N>0 ограничивает TotalStarted за lifetime Group; <-1 и неизвестный Overlap отклоняются. Ignore запрещает старт при active execution, Allow допускает несколько. Conditions проверяются даже при rejected admission; Failed расходует Limit. Running и Started устанавливаются до callback для корректного nested Ignore/Limit.

Sync throw, faulted Task, null Task и отменённый пользовательский Task становятся Failed; Group поглощает Action failure, остальные passed Rules продолжаются. Fire не ждёт completion. У голого Base сохранены прежние sync exception propagation и ненаблюдаемый Task. Condition exception прерывает Fire до любых его admissions/Actions и не создаёт execution.

### Exact Scope composition и lifetime

Вертикальные классы: EcaRuleState<T> (EventState) → EcaExecutionRuleState<T> (+ RuleExecutionGroupState) → EcaScopeRuleState<T> (+ ScopeState). Condition/Action получают один State на Rule/Fire. GroupState общий для Fire одной регистрации; ScopeState только с ScopeId общий для конкретного Scope. State не хранит engine/registry/Scope/service.

CreateScope: validate parent/ScopeId → new ScopeState → new ExecutionRegistry → new ScopeRuleRunner(groups, state) → internal ExecutionEngine(shared events, groups, runner) → local Dispatcher/RuleRegistry/BaseEngine → new Scope → hierarchy registration. Общий между scopes только EventRegistry. Same Rule instance допустим в нескольких scopes; Groups/GroupState/Limit/active executions/RuleState независимы.

Hierarchy = ownership/lifetime, Fire строго local, без parent/children/sibling propagation. Dispose parent каскадно закрывает descendants, ScopeEngine.Dispose — roots; sibling вне удаляемой ветви сохраняется. Dispose idempotent. Active ScopeId уникален, scope-N пропускает занятые ID; reused ID создаёт новый State, stale instance не удаляет замену и не создаёт child.

Execution/Scope Dispose запрещает новые Fire/Register/Unregister; Scope также CreateScope. BaseEngine отписывается, execution registry очищается. Active Actions не cancel: старые callbacks/runs удерживают Group/State и завершаются естественно. Старое completion/failure не меняет новую Group/Scope. Уже выбранные runs текущего Fire сохраняют snapshot/Group references при Unregister/Dispose во время invocation; ограничения facade относятся к новым вызовам.

Точный Scope trace теста: **Check A1 → Check A2 → A1 starts → Check B1 → Check B2 → B1 starts:nested → B2 → A1 continues → A2**. B1 остаётся pending Task, после явного завершения добавляется **B1 completes**, sibling не вызывается. Синхронный Execution trace: **Check A → A starts → Check B → B:value → A continues → Empty**. Invariant ALL CONDITIONS → ALL ACTIONS действует отдельно на каждый Fire, включая вызовы из Condition.

### Проверки 2026-09-12

- Runtime Core1 и отдельная Core1 tests assembly компилируются с C# 9 без ошибок/предупреждений.
- NUnit вне Unity: Core1 **75 passed, 0 failed, 0 skipped**; старый Core **37 passed, 0 failed, 0 skipped**.
- Полный Unity **6000.5.6f1** batchmode EditMode: **112 passed, 0 failed, 0 skipped**, Core1 75 + прежние 37, exit code 0.
- Новые сценарии покрывают State verticality, shared State, all-checks-before-admission, Limit/Overlap, sync/async/null Task failures, registration rollback, re-register, несколько TEventState, nested Fire, Scope isolation/hierarchy/shared events, active Tasks после Dispose и матрицу совместимости слоёв.
- Tests не подписываются на Fired. Reflection используется только в тестах для проверки public API/visibility и кратковременного Pending, не в execution runtime.
- Локальные игнорируемые артефакты: `.validation~/execution-scope-unity-results.xml`, `.validation~/execution-scope-unity.log`; NUnit XML — `.validation~/core1-Core1Tests-nunit.xml` и `.validation~/core1-Compile-nunit.xml`.
- Локальная Unity отличается от CI 6000.3.19f1. Удалённый GameCI и PlayMode не запускались.

Следующий шаг — review Core1 Execution/Scope. Commands через RunnerContext и Systems обсуждаются отдельно. External callback, deferred/queued Fire, trace/depth diagnostics, cross-scope Fire, cancellation cleanup hook, optional Group vertical layer, Inspection/history остаются будущими возможностями Roadmap; Save/Load не дублируется.

## Проект и сохранённый Runtime/Core

EcaSystems — Unity-first UPM framework для связи независимых игровых систем через ECA. Core не зависит от Unity. Репозиторий: [ra1ngo/EcaSystems](https://github.com/ra1ngo/EcaSystems). Корень репозитория одновременно является корнем package.

- EcaBaseEngine — новое имя прежнего минимального EcaEngine. Это самостоятельный Base pipeline и reference implementation, а не главный/root engine. Execution/Scope используют собственную инфраструктуру; дублирование orchestration требует review.
- Rule сохраняет три явных типа: IEcaRule<TEventContext, TConditionContext, TActionContext>. Event — декларация с ID и payload type; Condition.Check возвращает bool, Action.Run(context) — Task.
- Selector проверяет Event.Id, точный payload и точную пару role-context types. В пределах одного Fire одного engine все Conditions проверяются до любых Actions.
- Role-context interfaces invariant; read-only IEcaContext<out TEventContext> covariant.
- Общий invariant IEcaExecutionContext<TEventContext> объявляет RuleExecutionGroupState. Execution Condition/Action interfaces наследуют его и свою Base/Commands роль. Единый concrete EcaExecutionContext и старая фабрика не возвращены.
- Commands v1: string ID, heterogeneous registry, runner.Bind(actionContext), bound API доступен Action. Нет результатов/cancellation в command API. Resolve выполняется при каждом Run.
- Порядок Execution: все Conditions → допуск Limit/Overlap → new ActionContext → Bind Commands вне конструктора → создание Execution → Action. Отклонённый Fire не создаёт ActionContext и не вызывает Bind.
- Group принадлежит регистрации Rule; GroupState живой, содержит Started/Finished. Overlap — Ignore/Allow, Limit — -1/0/N. Pending → Running → Completed/Failed; исключение, включая cancellation exception, считается Failed.
- Unregister немедленно удаляет Rule/Group, но не отменяет уже запущенные Actions. Повторная регистрация того же ID получает независимую Group и Limit.
- Каждый Scope имеет отдельные engines/registries/groups, общий только CommandRunner. Общую Rule можно регистрировать по ссылке, state независим; вложенные изменяемые Condition/Action при этом остаются общими объектами.
- Scope hierarchy — ownership/lifetime. Fire локален; parent Dispose каскаден; Actions завершаются естественно. ScopeId уникален среди активных scopes, после Dispose переиспользуется с проверкой object identity.

## ScopeState: что действительно сделано

EcaScope.State хранит отдельный EcaScopeState только с get-only ScopeId; Scope.ScopeId читает его. Новое время жизни Scope создаёт новый State даже при том же ID. State не содержит engine, EcaScope или сервисов.

IEcaScopeContext<TEventContext> наследует IEcaExecutionContext<TEventContext> и добавляет ScopeState. Это подготовленный контракт, **не runtime wiring**. Scope.Fire по-прежнему поддерживает точную пару Execution role types; ExecutionEngine и Group создают конкретные контексты через new. Добавлять Scope-зависимость вниз в Execution или временный factory/service environment нельзя. Создание/выбор расширенных контекстов оставлены следующей архитектурной итерации. Универсальная ContextFactory/hydration не выбрана.

Механизм создания/расширения контекстов необходимо определить до полноценного Systems runtime: Scope уже требует ScopeState, Systems позже добавит SystemState. Универсальная фабрика не обязательна; временный service locator или скрытый runtime service внутри data context недопустимы.

## Направления после review Execution/Scope

Вертикальная ось — features/layers: Base, Commands, Execution, Scope, Systems, возможный Inspection/Debug. Это не строгая линейная Clean Architecture; понятную структуру папок желательно сохранить до MVP.

Горизонтальная ось — самостоятельные Events, Conditions, Actions, Commands, State, Rules, Context, Execution. Core1 сохраняет Rule как декларативную композицию и разделяет dispatcher/runners. Более широкие bindings/subscriptions могут потребовать отдельного сравнения после validation.

Развить relation Event ↔ Rule/Condition/Action ↔ Execution на выбранных EventDispatcher и RuleRun seam. System — организационный адаптер и ownership boundary; Events/Commands/State не должны существовать только через System как центральный runtime container.

Для будущей команды Fire Event уже принято: вызов из Action по умолчанию испускает Event в том же Scope, где выполняется Action. Explicit cross-scope targeting — возможное дальнейшее расширение. Реализация Fire Event сейчас не добавляется.

Bound Commands в старом ActionContext смешивают данные и сервисы; текущий API сохранён до отдельного решения. Core1 явно разделяет RuleState = data и RunnerContext = infrastructure. Новые State не получают сервисы.

Только после этого возвращаемся к Events + Systems implementation, затем TimeSystem/Wait и Global Variables как отдельной system/capability. SystemState сейчас предполагается глобальным первым этапом. После Global State/Variables нужно отдельно определить hierarchical/scoped lookup, inheritance и override: global → child → grandchild/local. Это не минимальный EcaScopeState.

Готовые Systems пакета предполагаются рядом с Runtime/Core: `Runtime/Systems/<System>/Core` для самостоятельного функционала и `Runtime/Systems/<System>/Eca` для адаптера. Примеры — Time и Global Variables / Global State. Внешние/клиентские Unity-системы могут иметь любую структуру; ECA-модуль лишь адаптирует их.

## Сохранённые границы старого checkpoint

В старом Runtime/Core не реализованы Systems, EventRegistry, Fire Event Command, dispatcher/bus/runtime, subscriptions/bindings, TimeSystem/Wait, GlobalVariables, scoped SystemState, GUID/stable ID API, DI/service locator/singleton. EventRegistry и синхронный dispatcher теперь есть только в Core1. Старые Commands API, Execution overlap/limit и Scope lifecycle сохранены. Root engine хранит приложение своим способом.

Комментарии об архитектуре — обычные block comments, не XML documentation нестабильного public API. Core1 имеет минимальные Base/empty Rule shortcuts; дальнейшее удобство API, factory-style API и универсальная hydration — возможности дальнейшего обсуждения.

[README](../README.md) содержит введение и исследовательский блок про академический ECA, Warcraft III, Construct, GDevelop, Game Creator и RPG Maker. [MentalTests](Architecture/MentalTests.md) перенесён в историческую Architecture и не является source of truth.

## Тесты и CI: фактическое состояние

Источник конфигурации — [.github/workflows/tests.yml](../.github/workflows/tests.yml): один Unity EditMode job на ubuntu-latest, Unity 6000.3.19f1, GameCI v4. Checkout в корень; rsync готовит копию в _ci/EcaSystemsPackage; packageMode: true, projectPath указывает на эту копию. Триггеры — pull_request, push только main, workflow_dispatch. Feature push сам CI не запускает.

PlayMode tests/job пока отсутствуют. Добавить с реальными PlayerLoop/Unity lifecycle-сценариями. Secrets — UNITY_LICENSE, UNITY_EMAIL, UNITY_PASSWORD; значения не выводить. Check name — EcaSystems EditMode Tests, artifact — unity-editmode-results из artifacts/EditMode.

Input coverageEnabled не задан. Прежний recovery-срез сообщал, что попытка false породила несовместимый CLI flag --no-coverageEnabled; input удалили. Отсутствие input не гарантирует отключение coverage во временном GameCI-проекте. Coverage рассматривать отдельно; checkpoint workflow не меняет.

История до checkpoint: recovery сообщал CI 31/31 passed на Unity 6000.3.19f1, вместе с проблемой генерации coverage report и возврата Personal seat. Это историческое сообщение, не новая проверка CI.

Проверки checkpoint: Core и тесты компилируются отдельно с C# 9; NUnit вне Unity — 37/37 passed; один локальный Unity 6000.5.6f1 EditMode run — 37/37 passed, без пропусков. Локальная Unity отличается от версии CI. PlayMode и удалённый GameCI для этой ветки не запускались. Подробности — [Testing.md](Testing.md).

## Рабочий процесс

Пользователь обсуждает архитектуру, затем передаёт временную инструкцию. Более поздние решения имеют приоритет; перед изменениями нужно читать актуальный код и Git-состояние. Не реализовывать отложенные возможности без запроса. Постоянная документация ведётся на русском.

Core1 Execution/Scope выполняется в новой ветке `codex/core1-execution-scope` от актуального main, с commit и push. Временный instructions-файл и его .meta удаляются перед final commit. PR пользователь создаёт вручную.
