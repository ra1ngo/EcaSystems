# EcaSystems — контекст разработки

## Назначение

EcaSystems — Unity-first UPM-фреймворк для взаимодействия независимых Systems через ECA. Core не зависит от Unity; Base пригоден как самостоятельный минимальный ECA-слой. Base context refactor, Commands v1 и их интеграция в Execution/Scope завершены.

Здесь зафиксированы актуальные согласованные решения. [ToDo.md](ToDo.md) содержит необходимые этапы до первого полноценного применения, [Roadmap.md](Roadmap.md) — необязательные будущие возможности. Документы Documentation~/Architecture/ сохраняют историю обсуждений: [MentalTests](Architecture/MentalTests.md) и [полный recovery snapshot](Architecture/EcaSystems-context-recovery-full-2026-09-11.md) могут содержать устаревшие решения и не переопределяют актуальные документы. [Короткий recovery](EcaSystems-context-recovery-2026-09-11.md) остаётся актуальным кратким срезом; более новые решения имеют приоритет. Постоянные архитектурные документы проекта ведутся на русском языке; имена API не переводятся.

## Core2 EventEmitter concept — 2026-09-15

Event — факт, представленный ECA для обработки правилами. Base содержит модель IEcaEvent/IEcaEvent<E>/EventRegistry; `Runtime/Core2/Concepts/EventEmitter` предоставляет прямой вход для внешних adapters. Namespace остаётся плоским `EcaSystems.Core2`.

Единственный public порт Concept: `IEcaEventEmitter.Fire<E>(IEcaEvent<E> ecaEvent, E eventState)`. `EcaEventEmitter` — internal sealed implementation с конструктором от IEcaEventRegistry; `IEcaEventHandler.Handle<E>(IEcaEvent<E> ecaEvent, E eventState)` — internal runtime-side generic callback. Concrete emitter и handler принадлежат внутренней композиции EcaSystems; внешние Systems/adapters получают только готовый IEcaEventEmitter. Emitter проверяет null Event, CheckRegistered и соответствие EventStateType объявленному typeof(E), затем передаёт тот же Event/State одному handler. Registry остаётся живой dependency; metadata после регистрации должна быть стабильной.

Композиция выполняет `internal EcaEventEmitter.Bind(IEcaEventHandler handler)` один раз до передачи public IEcaEventEmitter внешнему коду. Bind(null) даёт ArgumentNullException; повторный Bind и любой Fire до Bind — InvalidOperationException. После Bind null Event даёт ArgumentNullException, незарегистрированный Event — InvalidOperationException, несовместимая metadata — ArgumentException. Rebind/Unbind, Subscribe/Unsubscribe и multicast не добавлены. Ошибки handler распространяются синхронно. Доступ к internal binding для тестов предоставлен только сборке EcaSystems.Core2.Editor.Tests через InternalsVisibleTo.

Поток: `IEcaEventEmitter.Fire<E> → validation → IEcaEventHandler.Handle<E> → Runtime.Fire<E>`. Один handler принимает arbitrary unrelated E и сохраняет declared generic type даже для производного runtime instance, value types и null references. Прежний переход через non-generic C# event требовал generic transport wrapper для восстановления callback; прямой Fire<E> → Handle<E> не стирает тип, поэтому wrapper, Accept и source event удалены. В новом transport нет C# event, object adapters, dynamic/reflection.

Test-only `EcaTestBaseRuntime : EcaBaseRuntime, IEcaEventHandler` один раз bind'ится к Emitter, а Handle<E> вызывает простой Fire<E> и адаптирует state/contexts к generic Base Fire. Production Base не знает об Emitter; Rule selection и ALL CONDITIONS → ALL ACTIONS остаются в Base. Вызовы синхронные/reentrant. Systems, Signals, State, Commands integration, FireEventCommand, routing и Unity bridge здесь не реализованы. Следующая отдельная итерация external adapters описана в Roadmap; сейчас доступен только прямой вызов Fire<E>.

Core2 `ForceFire` переименован в `Fire` во всех трёх runtime API и call sites. Overload с createState сохраняет прежнюю Base semantics, включая fire-and-forget Actions и обход ExecutionMode/lifecycle; overload без createState в Execution/Scope сохраняет layer-aware semantics. Будущий assembled runtime предоставит `Fire<E>(event, state)` для будущей FireEventCommand; сейчас этот простой adapter есть только в tests. FireEventCommand, Commands integration, Scope routing, global bus и Unity bridge не реализованы. Core/Core1 не менялись.

## Core2 Commands abstraction — 2026-09-17

`IEcaCommand` — non-generic runtime abstraction: `Id`, `Type ContextType`, `Type ArgsType`, `Task Run(IEcaActionContext context, object args)`. Typed authoring остаётся в `IEcaCommand<in C,in A>` с `C : IEcaActionContext` и `Task Run(C context, A args)`. Default interface implementation автоматически предоставляет typeof(C)/typeof(A) и bridge с casts к typed Run. Concrete Command реализует Id и typed Run; metadata и erased bridge вручную не требуются.

`EcaCommandRegistry.Register(IEcaCommand command)` заменяет generic Register. Registry хранит исходный IEcaCommand напрямую; internal Resolve возвращает его же. Промежуточные Entry удалены. Null/Id/duplicate/Ordinal semantics сохранены. Runner без изменения проверок валидирует context, declared args type и null, затем вызывает IEcaCommand.Run и отклоняет null Task. Прямой erased Run — низкоуровневый bridge; проверка совместимости выполняется Runner до вызова.

Это позволяет хранить heterogeneous `IEcaCommand[]` / `IReadOnlyList<IEcaCommand>` и регистрировать Commands разных C/A без generic information на call site. Refactor снимает blocker для будущего EcaSystemConnector; Systems не реализованы. Reflection/dynamic, replacement Entry, registration descriptor и abstract base command не добавлены.

## Core2 Commands validation — 2026-09-15

`Core2/Concepts/Commands` проверен как самостоятельный horizontal Concept: standalone registry/runner/binding и end-to-end `Commands + EcaBaseRuntime`. В validation-итерации production API не менялся; последующий refactor 2026-09-17 описан выше. Commands подключаются тестовым composition context через `IEcaCommandsActionContext.Commands`; Base получает обычный `A : IEcaActionContext` и не знает о Commands. Production integration с Execution/Scope contexts пока не реализована.

`Bind` удерживает ровно переданный ActionContext instance; bindings одного runner/registry/command не смешиваются, в том числе при overlapping async calls. Lookup выполняется заново на каждом Run, поэтому existing binding видит unregister/re-registration. Публичного Get/TryGet у CommandRegistry нет, как и в старом Core: internal Resolve проверен через публичный Run, новый inspection API не добавлялся. Базовое функциональное parity со старым Core сохранено: registration, lookup, binding, typed invocation, несколько Commands и Task.

Context проверяется по совместимости instance; args — по объявленному generic-типу. Допустимы совместимые производные типы, typed null reference args и nullable value args; object-обёртка не скрывает несовместимый declared type. Unknown id/wrong context/null Task дают InvalidOperationException, invalid id/wrong args — ArgumentException; sync exception распространяется непосредственно, faulted Task возвращается caller без новой error-policy. Base Fire остаётся fire-and-forget; Task проверяется там, где Action получает его от Commands.Run. Barrier и Conditions работают без изменений Base.

## Core2 Scope — 2026-09-15

Core2/Base, Core2/Layers/Execution и Core2/Layers/Scope завершены. Scope сочетает vertical enrichment и runtime isolation/lifetime boundary. Orchestration, Group и lifecycle принадлежат Execution; Scope не добавляет Runner/bridge/Executor. Base/Execution API не менялся.

`EcaScopeRuntime(IEcaEventRegistry events)` — manager всех активных Scope и их hierarchy. API: `ScopeCount`, `CreateScope(string scopeId = null)`, `TryGetScope(string scopeId, out EcaScope scope)`, `Dispose()`. Общий только EventRegistry; для каждого Scope manager создаёт независимые EcaBaseRuleRegistry, EcaExecutionGroupRegistry, checker/runner и EcaExecutionRuntime/BaseRuntime graph. Одну Rule instance можно зарегистрировать в нескольких Scope: GroupState, active executions, overlap и lifetime Limit независимы.

`EcaScope` — один isolated Scope с собственным ExecutionRuntime. API: `State`, `ScopeId`, `ParentScopeId`, `IsDisposed`, `CreateScope`, `Register`, `Unregister`, `Fire` (два overloads), `GetGroup`, `TryGetGroup`, `Dispose`. Root имеет ParentScopeId == null; child создаётся через parent.CreateScope. Hierarchy определяет ownership/lifetime, но не распространяет Fire: parent и child обрабатывают только собственные Rules.

Null ScopeId означает auto-id: scope-1, scope-2 и далее с пропуском занятых имён; пустые/пробельные явные имена запрещены. Active ScopeId уникален в manager. Dispose сначала закрывает Scope, рекурсивно закрывает descendants, удаляет их из manager и освобождает ссылку на ExecutionRuntime; родитель, siblings и другие roots не затрагиваются при удалении child. Новые операции через disposed Scope бросают ObjectDisposedException, read-only State/identity остаются доступны. Dispose idempotent. Manager.Dispose закрывает все scopes; CreateScope после закрытия бросает ObjectDisposedException, ScopeCount == 0 и TryGetScope возвращает false, как в Core/Core1.

Уже запущенные Actions не отменяются: они завершают старые Groups и counters. Повторное использование ScopeId создаёт новый ScopeState и Execution graph; старый объект и завершение старой Action не затрагивают replacement. Internal removal проверяет reference identity, а не только строковый Id. Уже выбранные Groups текущего Fire сохраняют snapshot semantics Execution; Dispose закрывает последующие вызовы, не меняя текущую orchestration.

`EcaScopeState` содержит только обязательный непустой ScopeId; ParentScopeId и IsDisposed принадлежат EcaScope. `IEcaScopeRuleState<out E>` расширяет `IEcaExecutionRuleState<E>` свойством ScopeState. `EcaScopeRuleState<E>` сохраняет EventState и обязательные ссылки ExecutionGroupState/ScopeState без concrete inheritance. `IEcaScopeConditionContext` / `IEcaScopeActionContext` расширяют Execution contexts и остаются пустыми extension points.

Основной `EcaScope.Register<E,R,C,A>(rule, mode, extendState)` принимает `Func<IEcaExecutionRuleState<E>, EcaScopeState, R>` с `R : IEcaScopeRuleState<E>` и Scope constraints для C/A. Адаптер создаёт готовый EcaExecutionRuleState<E> из payload/live GroupState и передаёт его вместе с this.State в extendState. Возвращённый R поступает в Condition/Action. Фазы используют отдельные вызовы extension function; Condition остаётся side-effect-free. Сохранён default `Register<E,C,A>(rule, mode)` для EcaScopeRuleState<E>.

Fire и inspection делегируются собственному ExecutionRuntime. ALL CONDITIONS → ALL EXECUTIONS, admission непосредственно в Run, immediate/reentrant Fire и обработка ошибок принадлежат Execution. Все Rules одного Scope получают один ScopeState. Unregister удаляет только локальную регистрацию; активные executions завершаются естественно.

Fire с createState — локальный pass-through в Execution/Base с Base constraints: сохраняет Base barrier, не использует Scope extendState и ExecutionMode/lifecycle/counters. Caller сам передаёт createState. StateBuilder/StateFactory остаются в ToDo. EventEmitter concept реализован независимо; его production composition со Scope, FireEventCommand, Commands integration, cross-scope Fire, Unity, cancellation и Reset/Queue не реализованы. Roadmap о внешней мутации registries/Rule.Id сохранён без новой защиты в Execution.

## Core2 Base → Execution — 2026-09-14

Актуальная итерация развивается в `Runtime/Core2`, namespace/assembly `EcaSystems.Core2`, без Unity API (`noEngineReferences`). Core2/Base завершён и остаётся неизменным. Core2/Layers/Execution реализован; описанные ниже Core/Core1 — отдельные reference implementations, их bridge/Scope/Commands API не определяют Core2.

### API и композиция

`EcaExecutionRuntime` использует composition с `EcaBaseRuntime` и общие экземпляры `IEcaRuleRegistry`, `IEcaConditionChecker`, `IEcaActionRunner`; Groups хранятся в `IEcaExecutionGroupRegistry` / `EcaExecutionGroupRegistry`. Constructor принимает `(rules, groups, conditionChecker, actionRunner)`. Runtime не выдаёт mutable registries; inspection доступен через `GetGroup(ruleId)` / `TryGetGroup(ruleId, out group)`.

`Register<E,R,C,A>(rule, executionMode, createState)` регистрирует Rule через Base, затем создаёт и регистрирует Group; при ошибке второго этапа регистрация Rule откатывается. `Unregister(rule)` удаляет именно зарегистрированный экземпляр Rule и его Group. Чужой экземпляр с тем же Id не удаляет Group. Активные executions продолжаются со старой Group/State; повторная регистрация создаёт независимые counters и lifetime Limit. Метаданные Rule/Event после регистрации должны оставаться стабильными. Переданные registries следует изменять согласованно через Runtime.

`IEcaExecutionGroup` предоставляет Rule/RuleId, ExecutionMode, State и read-only Executions. Typed `IEcaExecutionGroup<E,R,C,A>` и `EcaExecutionGroup<E,R,C,A>` имеют `Check(E, C)` и `Task Run(E, A)`. Typed GroupRegistry.Get сохраняет specialization и бросает ошибку при несовместимости; typed TryGet возвращает false. Duplicate RuleId запрещён. Runtime сохраняет generic extensibility: `R : IEcaExecutionRuleState<E>`, `C : IEcaExecutionConditionContext`, `A : IEcaExecutionActionContext`; конкретные расширенные State/contexts проходят без bridge, visitor и wrapper.

### Fire, barrier и reentrancy

`Fire<E,R,C,A>(ecaEvent, eventState, conditionContext, actionContext)` выбирает Rules в registry order, вызывает Check всех Groups и только затем Run прошедших: **ALL CONDITIONS → ALL EXECUTIONS**. Между фазами хранятся ссылки на выбранные Groups. Unregister из предыдущей Action не исключает уже выбранную Group из текущего Fire, но исключает из будущих Fire. Повторный lookup после barrier не выполняется.

Check не проверяет ExecutionMode, не резервирует slot, не создаёт Execution и не меняет counters. Admission проверяется private-функцией непосредственно внутри Run: nested Fire мог занять Group или израсходовать её Limit после внешней Condition-фазы. Fire синхронный, immediate/reentrant, без очереди; он запускает lifecycle, но не ожидает завершения Action Task. Barrier относится к одному invocation. Работа Runtime/Group предполагает последовательные вызовы на одном execution context; конкурентный доступ с разных потоков не синхронизируется.

`Fire<E,R,C,A>` с createState делегируется внутреннему BaseRuntime с исходными Base constraints: это Base pipeline, который проверяет все Conditions перед Actions, но не использует ExecutionMode, Group createState, lifecycle и counters. Это не пропуск Conditions.

### State, mode и lifecycle

`IEcaExecutionRuleState<out E>` расширяет Base RuleState свойством `ExecutionGroupState`; `EcaExecutionRuleState<E>` хранит EventState и обязательную live-ссылку на `EcaExecutionGroupState`. Context contracts — только `IEcaExecutionConditionContext` и `IEcaExecutionActionContext`, без concrete infrastructure classes.

Group хранит `Func<E, EcaExecutionGroupState, R> createState`: Check создаёт свежий State для непустой Condition, Run — свежий State для Action после допуска. Condition и Action могут получить разные экземпляры. Condition по контракту side-effect-free, не мутирует RuleState и не передаёт через него вычисленные данные в Action. Null Condition проходит без создания State. GroupState живёт вместе с одной регистрацией: Condition видит counters до текущего запуска; Action видит уже увеличенный TotalStarted. StateBuilder/StateFactory отложен.

`EcaExecutionModeOverlap` содержит Ignore и Allow. Ignore блокирует запуск при активном execution; Allow допускает несколько. `EcaExecutionMode.Limit`: -1 unlimited, 0 запрещает старт, N > 0 ограничивает lifetime TotalStarted; завершение не восстанавливает Limit, Failed также расходует его. Значения Limit < -1 и неизвестный overlap отклоняются.

`EcaExecution` содержит Id (монотонный в пределах Group), Rule/RuleId, Status и Exception; `EcaExecutionStatus`: Pending → Running → Completed/Failed. До пользовательских createState/Action execution добавлен в active Executions, переведён в Running, TotalStarted увеличен. Поэтому reentrant Fire видит занятость/расход Limit. Rejected Run возвращает CompletedTask без execution, State и изменения counters. Exceptions Action/createState, faulted Task и null Task дают Failed с Exception; finally увеличивает TotalFinished и удаляет execution из active. Lifecycle наблюдает Task и не мешает следующим прошедшим Groups. Executions — только активные запуски, без history; сохранённая внешняя ссылка позволяет увидеть финальный статус.

Ошибка Condition или её createState может прервать Fire до запуска executions; дополнительной error-policy нет. Cancellation, Reset/Queue, Commands integration, Fire Event command, EventEmitter composition и Unity bridge в Core2 Execution не реализованы.

## Core1 Abstractions → Base → Execution → Scope — 2026-09-12

Новая архитектура развивается параллельно в `Runtime/Core1`, namespace/assembly `EcaSystems.Core1`. Временная вертикальная ось — `A`: плоская `A/Abstractions`, затем `A/Base`, `A/Execution`, `A/Scope`; empty payload остаётся в Utils. Execution/Scope contracts принадлежат своим слоям. `IEcaRuleRun` перенесён в отдельный файл Abstractions и остаётся пустым opaque contract. Старый Runtime/Core, его tests и historical full snapshot не изменены. Core1 ещё не объявлен окончательной заменой старого Core; разделы ниже про старые Commands/Execution/Scope описывают reference implementation.

Event остаётся declaration-only: Id, Name, Description, EventStateType. Rule = **1 Event + optional Condition + 1 Action**, хранит прямые ссылки. Condition/Action остаются программными executable declarations с metadata и Check/Task Run. State = данные, RunnerContext = infrastructure. Marker contexts не содержат Commands, engine, registry или Scope service. Нет ActionRegistry/ConditionRegistry, StateBuilder/ContextFactory, Rule statuses, универсального ExecutionState, Systems, Commands integration, cancellation, queues и deferred Fire.

### Единый type-erasure bridge и вертикальное расширение

`EcaRuleRunner → EcaExecutionRuleRunner → EcaScopeRuleRunner` — реальное наследование. EcaRuleRunner больше не sealed. Общий occurrence visitor сохранён: CreateRun проверяет Event.Id, visitor вызывает `ValidateRule<TEventState>` и затем `protected virtual CreateTyped<TEventState>`. Поддерживаемый слой определяет точную specialization Rule, создаёт нужный State и marker infrastructure и вызывает унаследованный `CreateRunCore<TEventState, TRuleState, TConditionRunnerContext, TActionRunnerContext>`.

Единственный private `RuleRun<T, TS, TC, TA>` в Base хранит typed Rule, State, два RunnerContext, owner и ссылку на свой stateless `RunBridge<T, TS, TC, TA>`. Token не имеет Check/Run/Accept/Status. Bridge восстанавливает закрытые типы и передаёт выполнение private generic-методам RuleRunner; они вызывают независимые EcaConditionRunner/EcaActionRunner. Проверка owner сохраняется. У bridge нет данных конкретного запуска; состояние Fire не кешируется.

Компромисс C# прежний: generic-параметры невозможно восстановить прямо из opaque interface. Visitor и закрытый bridge решают это без dynamic, object payload и reflection execution. Единственный конструктор private token связывает его с соответствующим bridge, поэтому приведение после owner check безопасно. Generic-типы теперь четыре вместо одного, чтобы точно связать произвольные State и роли infrastructure. Большого bridge в Execution/Scope нет. Public API State использует конкретные классы из инструкции, без замены интерфейсами из-за ограничений C#.

Execution CreateTyped получает Group по RuleId с проверкой Rule instance, создаёт EcaExecutionRuleState<T>, вызывает общий CreateRunCore и оборачивает inner token в private non-generic ExecutionRuleRun с owner, InnerRun и Group. Check распаковывает и вызывает base.Check. RunAction передаёт `() => base.RunAction(inner)` в Group.Run. Wrapper пассивный, удерживает именно выбранное поколение Group и проверяет owner. Scope переопределяет **только** ValidateTyped/CreateTyped, создаёт Scope State/contexts и использует унаследованные CreateRunCore, GetGroup, WrapRun, Check и RunAction. Ни admission, ни Task observation, ни bridge не копируются.

### Canonical registration и Base API

Canonical `EcaBaseEngine.Register<T, TS, TC, TA>(typedRule)` вызывает `_runner.ValidateRule<T>(rule)` **до** RuleRegistry.Register. Общая validation проверяет null и EventStateType, virtual ValidateTyped проверяет точную специализацию текущего runner. RuleRegistry по-прежнему независимо проверяет Event registration/type, Action required и duplicate RuleId. При отказе совместимости registry остаётся пустым; defensive validation в CreateRun сохранена для low-level composition и изменившихся custom declarations.

Base поддерживает Rule с `EcaRuleState<T>` и Base marker contexts. Standalone Execution.Register<T> принимает ровно `EcaExecutionRuleState<T>` + Execution marker contexts; Scope.Register<T> — ровно `EcaScopeRuleState<T>` + Scope marker contexts. Эти public signatures отсекают Rules другого слоя уже компилятором. Через canonical BaseEngine с подменённым runner несовместимость отклоняется на Register. Internal ExecutionEngine.RegisterCore с Execution constraints нужен только Scope composition и не является public generic escape hatch.

Execution registration: BaseEngine validation/register → ExecutionRegistry.Register(rule, mode); при ошибке второго этапа Base registration откатывается. Runtime duplicate Register отклоняется Base RuleRegistry даже для того же instance. Отдельный low-level ExecutionRegistry допускает no-op только для того же Rule instance и эквивалентного mode; иной instance/mode вызывает ошибку. Unregister удаляет Rule по instance и её Group; повторная регистрация получает новый GroupState и lifetime Limit.

Дополнительные public API изменения Core1 Base перечислены полностью: новый ValidateRule<T>(IEcaRule) в IEcaRuleRunner/EcaRuleRunner; canonical Register/Unregister в EcaBaseEngine; EcaRuleRunner стал наследуемым, CreateRun/Check/RunAction virtual, появились protected ValidateTyped/CreateTyped/CreateRunCore. IEcaRuleRun изменил файл, но не namespace/assembly/форму контракта. Fired изменил visibility на internal. Другие Base public signatures и low-level RuleRegistry не менялись; прямой registry.Register остаётся storage API без runner validation и не является canonical runtime API. Base.Dispose сохраняет прежнюю семантику отписки, новые facade lifecycle guards принадлежат Execution/Scope.

### Event ownership, dispatch и invariant

EventRegistry заполняется извне; Fire никогда не регистрирует Event. Duplicate Id отклоняется, identity для Fire = Id + точный объявленный EventStateType. Допустима другая декларация того же Id/type. Custom metadata после регистрации должна оставаться стабильной. EventRegistry общий для scopes одной композиции, без global/static lifetime.

`EcaEventDispatcher.Fired` — **internal infrastructure C# event**; public остался Fire. Он связывает Dispatcher → BaseEngine внутри assembly, не является gameplay callback, публичной замены нет. Public external callback/bridge остаётся Roadmap. Tests не подписываются на Fired и не требуют InternalsVisibleTo. Dispatch синхронный/reentrant, без очередей.

EcaBaseEngine — единственный multi-rule pipeline: select snapshot Rules → CreateRun для всех → **Check всех** → **RunAction только passed**. Он не знает Execution/Scope. GetByEvent сохраняет порядок регистрации; всё состояние invocation локально стеку. `ALL CONDITIONS → ALL ACTIONS` действует на один Fire. Вложенный Fire из Condition может выполнить свои Actions до остальных внешних Conditions; invariant остаётся локальным каждому invocation.

Голый Base не ожидает Action Task, не наблюдает faulted Task, синхронная ошибка Action распространяется и прерывает оставшиеся Actions. Ошибка Condition прерывает весь текущий Fire до admission/Actions также в Execution/Scope. Это не Action failure и не создаёт Failed execution.

### Execution pipeline и lifecycle

Standalone EcaExecutionEngine принимает shared EventRegistry и создаёт local RuleRegistry, Dispatcher, ExecutionRegistry, ExecutionRuleRunner и **один EcaBaseEngine**. Fire просто делегирует Dispatcher. Собственного цикла select/check/action нет. TryGetGroup даёт доступ к существующей Group для наблюдения; изменяемый registry facade наружу не выдаёт. Group.Run и отдельные registry — low-level API, не замена canonical Engine path.

Точный путь: Fire → Dispatcher validation → internal Fired → BaseEngine snapshot → создание всех Execution State/inner runs/wrappers → все Conditions → для каждого passed wrapper Group.Run → Limit → Overlap → при допуске new RuleExecution(Pending) → add active → Running + TotalStarted → Action callback → await Task → Completed либо Failed + Exception → TotalFinished → remove active.

EcaRuleExecutionGroup и EcaRuleExecution **non-generic**. Group не знает конкретных State, contexts, Condition/Action runners; получает только Func<Task>. Execution хранит Id, Rule/RuleId, Status и Exception. Group.Executions — read-only представление только активных запусков, не история. Pending кратковременный, до входа в пользовательский Action уже Running. Счётчики TotalStarted/TotalFinished принадлежат GroupState одной регистрации.

`EcaExecutionMode` заменяет старое имя EcaRunMode в Core1 и существует только в Execution. Limit=-1 без ограничений, 0 запрещает старт, N>0 ограничивает TotalStarted за lifetime Group. Limit<-1 и неизвестное значение Overlap отклоняются конструктором. Ignore запрещает новый старт при активном execution; Allow допускает несколько. Conditions всё равно проверяются, включая исчерпанный/нулевой Limit. Rejected admission не создаёт execution и не меняет counters. Failed расходует Limit. Running/Started устанавливаются до callback, поэтому nested Fire сразу видит занятость и расход Limit.

Group поглощает synchronous throw, faulted Task, null Task и отменённый пользовательский Task как Failed + Exception. Успех даёт Completed. TotalFinished увеличивается ровно один раз, execution удаляется из active при обоих исходах. Остальные passed Rules продолжают запускаться; Fire не ждёт completion, а lifecycle Task наблюдается внутри Group. Cancellation API не добавлен.

### Вертикальный State и Scope composition

Реальное наследование данных: `EcaRuleState<T>` (EventState) → `EcaExecutionRuleState<T>` (+ RuleExecutionGroupState) → `EcaScopeRuleState<T>` (+ ScopeState). Соответствующие covariant interfaces следуют той же вертикали. Condition/Action получают один State instance, на следующий Rule/Fire создаётся новый. GroupState живёт между Fire одной регистрации; ScopeState только с ScopeId живёт вместе с конкретным Scope. State не хранит runtime owners/services. Condition/Action contravariant по State/contexts; Rule/Event invariant.

Создание Scope: проверить parent и ScopeId → new ScopeState → new ExecutionRegistry → new ScopeRuleRunner(groups, state) → internal ExecutionEngine(shared events, groups, scope runner) → local Dispatcher/RuleRegistry/BaseEngine → new Scope → зарегистрировать hierarchy. Каждый Scope имеет независимые Dispatcher, RuleRegistry, ExecutionRegistry, ScopeRuleRunner и BaseEngine через свой ExecutionEngine. Один Rule instance можно зарегистрировать в нескольких scopes, но Group/GroupState/Limit/active executions/RuleState независимы. Общий только EventRegistry.

Scope hierarchy = ownership/lifetime. Fire строго local: parent, children и siblings не получают его автоматически. Parent Dispose каскадно закрывает descendants; sibling вне удаляемой ветви сохраняется. ScopeEngine.Dispose закрывает roots и потомков. Dispose idempotent; active ScopeId уникален, autogenerated scope-N пропускает занятые ID. Повторный ID создаёт новый ScopeState. Проверка instance защищает новый Scope от stale Dispose; disposed/stale parent не создаёт child.

Execution/Scope Dispose запрещает новые Fire/Register/Unregister; disposed Scope также запрещает CreateScope. Local BaseEngine отписывается, ExecutionRegistry очищается. Unregister/Dispose **не отменяет активные Tasks**: callback/inner run удерживают State и старую Group до естественного Completed/Failed. Старое завершение не меняет GroupState новой регистрации/нового Scope. Уже выбранные runs текущего Fire сохраняют snapshot и Group references даже при изменении регистрации во время Condition/Action; lifecycle guards относятся к новым вызовам facade.

### Проверки и следующий шаг

Core1 Base regression адаптирован к canonical Register и internal Fired. Новые tests покрывают вертикальные State, фазовый порядок до admission, Limit/Overlap, lifecycle/errors, несколько TEventState, foreign tokens, регистрационную матрицу Base/Execution/Scope, re-registration, shared EventRegistry, local Fire, hierarchy и Tasks после Dispose. Результаты локальных прогонов записаны в актуальном recovery.

Точный Scope nested trace: `Check A1 → Check A2 → A1 starts → Check B1 → Check B2 → B1 starts:nested → B2 → A1 continues → A2`; после явного завершения async B1 добавляется `B1 completes`. Sibling не вызывается. Отдельно проверен синхронный Execution trace `Check A → A starts → Check B → B:value → A continues → Empty`.

Следующий этап — review Core1 Execution/Scope. Commands через RunnerContext и Systems требуют отдельного обсуждения. Public external callback, queued/deferred Fire, cycle depth/trace diagnostics, explicit cross-scope Fire, cancellation cleanup hook, Inspection/history и optional Group vertical layer остаются Roadmap, а не частью текущей реализации.

## Архитектура Base существующего Runtime/Core

Внешний источник → EcaEvent<TEventContext> → Fire → Rules → Conditions → Actions.

EcaEvent — типизированная декларация с Id, а не C# event и не источник событий. Внешняя интеграция вызывает Fire. Выбор Rules использует Event.Id с проверкой совместимости типов. Rule состоит из Event, необязательной Condition и одной Action. Condition.Check возвращает bool; Action.Run возвращает Task завершения/ошибки, а не бизнес-результат.

- EcaRuleRegistry хранит, регистрирует и валидирует Rules.
- EcaRuleSelector выполняет выборку.
- IEcaRuleChecker / EcaRuleChecker проверяет одну Rule.
- IEcaRuleRunner / EcaRuleRunner вызывает Action.Run(context).
- Rule и registry сравнивают явный typeof(TEventContext) с Event.EventContextType; извлечение payload через reflection удалено.
- IEcaRule<TEventContext, TConditionContext, TActionContext>, EcaRule и EcaRuleConfig явно разделяют payload и две роли контекста; constraints связывают обе роли с одним TEventContext.
- IEcaContext — общий marker; IEcaContext<TEventContext> предоставляет EventContext. IEcaConditionContext и IEcaActionContext задают разные роли. Префикс Base не вводится.
- IEcaCondition<TContext>.Check(context) и IEcaAction<TContext>.Run(context) остаются простыми; у Run один аргумент, без Commands в контракте Action.
- EcaBaseEngine (прежнее имя EcaEngine) — самостоятельный минимальный engine и reference implementation Base pipeline, а не root engine проекта. Execution/Scope используют свою инфраструктуру. Он сам создаёт только EcaConditionContext<TEventContext> и EcaActionContext<TEventContext>; Register/Unregister принимают ровно эту пару. Base не зависит от Commands. EcaContext остаётся общим хранилищем payload. Дублирование orchestration с Execution требует последующего review.
- Общие Rule-модель и RuleRegistry не ограничены стандартными контекстами EcaBaseEngine. Универсальная ContextFactory/hydration не обязательна и не выбрана; конкретный механизм создания/расширения более богатых context types нужно определить до полноценного Systems runtime (см. «ScopeState и граница runtime wiring»).
- IEcaConditionContext<TEventContext>, IEcaActionContext<TEventContext> и наследующие их Commands/Execution role-интерфейсы invariant по TEventContext: роль строго связана с реальным payload Rule. Общий read-only IEcaContext<out TEventContext> остаётся covariant.
- Selector проверяет Event.Id, точный payload и точную пару типов Condition/Action; неявного расширения DerivedEventContext до BaseEventContext нет.
- EcaRuleChecker принимает только ConditionContext, EcaRuleRunner — только ActionContext. Selector выбирает Rule с указанной парой context types.
- Вспомогательные классы находятся в Runtime/Core/Utils, namespace остаётся EcaSystems.Core.

## Основные инварианты

- Все Conditions одного Fire внутри одного engine проверяются до любых Actions этого Fire: сначала собираются прошедшие Rules/Groups, затем запускаются Actions. Между scopes этот инвариант не расширяется.
- EventContext — логически неизменяемые данные одного Fire; копирования нет.
- RuleId уникален в RuleRegistry; несовместимые типы контекста отклоняются.
- При ошибке регистрации ExecutionGroup engine откатывает регистрацию Rule.
- Base Action.Run(context) и RuleRunner.Run(rule, context) не принимают cancellation token.
- Core не знает о Unity, Entity, Global State и конкретных Systems.

## Commands v1

Commands доступны только Action через IEcaCommandsActionContext и его типизированный вариант. Отдельного CommandsConditionContext нет. IEcaCommand<TContext, TArgs> ограничен IEcaActionContext; Run(context, args) возвращает Task без результата и cancellation.

- ID команды — string; пустые/пробельные ID и активные дубликаты отклоняются.
- EcaCommandRegistry хранит heterogeneous Commands, поддерживает Register и Unregister(string commandId). Registry не привязывает контекст и не выполняет команды.
- EcaCommandRunner использует registry и возвращает bound IEcaCommands через Bind(actionContext). Каждая привязка хранит свой контекст; общего изменяемого current context нет.
- В Execution bind происходит после всех Conditions одного Fire и только после допуска конкретной Group по Limit/Overlap. Отклонённый Fire не создаёт ActionContext, Execution, не вызывает Bind и не расходует Limit.
- Конструктор EcaExecutionActionContext не вызывает runner. Group сначала полностью создаёт контекст, затем привязывает Commands единственным internal вызовом; чтение Commands до привязки, null и повторная привязка отклоняются. Публичного setter нет.
- Action вызывает `await context.Commands.Run("command.id", args)` и может последовательно вызвать много Commands. Контекст передаётся автоматически; Action не получает registry, runner или concrete Command.
- Runner проверяет совместимость ActionContext и объявленного TArgs с типом аргументов команды. Совместимые производные типы и null для reference/nullable допускаются; object с неподходящим объявленным типом не выполняет неявное приведение.
- Неизвестный ID, неверные context/args и null Task дают исключение с ID команды. Ошибка команды распространяется в Action и учитывается обычной моделью Failed.
- Resolve выполняется при каждом Run: старые bound API видят Unregister и повторную регистрацию. Уже запущенные Tasks завершаются естественно.
- Commands входят в существующий EcaSystems.Core.asmdef. API v1 минимален и может быть пересмотрен при проектировании Systems.

## Execution v1

Execution использует Rule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>>. Общий IEcaExecutionContext<TEventContext> : IEcaContext<TEventContext> объявляет живой RuleExecutionGroupState. Condition/Action интерфейсы наследуют этот контракт и соответствующую Base/Commands роль, сохраняя invariance по payload. Только ActionContext содержит Commands. Execution.Context хранит ActionContext. Это общий контракт данных, а не возвращение удалённого единого concrete execution-контекста.

Fire сначала выбирает Rules, получает их Groups и создаёт через new EcaExecutionConditionContext для каждой проверки. После завершения всех Conditions передаёт payload и общий runner в Group.Fire для прошедших Rules. Group проверяет Limit/Overlap, затем создаёт через new ActionContext, вызывает runner.Bind(context), сохраняет bound Commands, создаёт Execution и запускает Action. При исключении в Condition Action-фаза не начинается.

Порядок: все Conditions → допуск по Limit/Overlap → new ActionContext → Bind Commands → создание Execution → Action.

Execution/Scope v1 создают конкретные execution contexts напрямую через new, поэтому public Register/Fire pipeline работает только с точной парой IEcaExecutionConditionContext<TEventContext> / IEcaExecutionActionContext<TEventContext>. Более богатые context types верхнего Systems-слоя автоматически не поддерживаются. Это сознательное временное ограничение, а не финальная модель расширения. Решение о создании/расширении контекстов необходимо до полноценного Systems runtime; оно не обязано быть универсальной ContextFactory/hydration.

Старая единая concrete модель execution-контекста, EcaExecutionContextFactory и IEcaExecutionContextFactory удалены. Универсальная фабрика не вводится в checkpoint; способ расширения контекстов нужно рассмотреть вместе с архитектурой capabilities.

Execution отслеживает конкретные, в том числе длительные, запуски Rule:

EcaExecutionEngine → EcaRuleExecutionRegistry → EcaRuleExecutionGroup<TEventContext> → EcaRuleExecution<TEventContext>.

Каждая Group принадлежит одной Rule, содержит RunMode, живой GroupState и активные executions. Group напрямую использует общий IEcaRuleRunner и управляет жизненным циклом: создание → Running → Completed/Failed → IncrementFinished → удаление из активного списка. MarkRunning увеличивает Started до Run. Исключения, включая OperationCanceledException, считаются обычными Failed; null Task тоже означает ошибку.

EcaRuleExecution содержит Id, RuleId, Rule, Context, Status и Exception. Состояния: Pending → Running → Completed | Failed. Pending намеренно оставлен как краткое начальное состояние. Публичный IReadOnlyList<EcaRuleExecution<TEventContext>> Executions — активный список, а не история.

RunMode — единый объект настройки:

| Настройка | Семантика |
| --- | --- |
| Overlap.Ignore | Игнорировать новый запуск, пока в группе есть активный execution |
| Overlap.Allow | Разрешать одновременные executions внутри группы |
| Limit = -1 | Без ограничений; значение по умолчанию |
| Limit = 0 | Не создавать executions |
| Limit = N > 0 | Не более N фактических стартов за время жизни группы |
| Limit < -1 | ArgumentOutOfRangeException |

Group проверяет Limit до создания execution, затем применяет switch Overlap. Игнорируемый Fire не расходует Limit. Engine проверяет Conditions до применения Limit/Overlap группой. GroupState содержит только живые счётчики EcaRuleExecutionTotalStarted и EcaRuleExecutionTotalFinished. Ошибка расходует фактический старт и тоже увеличивает Finished.

### Немедленный Unregister

Unregister(rule) удаляет Rule из RuleRegistry и сразу удаляет Group из ExecutionRegistry. Запущенные Actions не отменяются и не ожидаются. Тот же RuleId можно немедленно зарегистрировать заново.

Если удаление Rule не удалось, возвращается false, Group не изменяется. Null отклоняется валидацией RuleRegistry. Успешное удаление возвращает true после удаления Group.

Старые async-операции удерживают необходимые ссылки и завершаются в старой Group. Повторная регистрация создаёт независимую Group с новым GroupState и временем жизни Limit. Старые и новые Actions могут временно пересекаться даже при Ignore. Завершение старой Group не изменяет новую. Это сознательное упрощение v1; асинхронный/плавный Unregister остаётся в Roadmap.

### Принятые ограничения

Once выражается как Limit = 1, DoN — Limit = N. Привязка к времени жизни игры/сцены/объекта определяется временем жизни Scope. Для Overlap достаточно Ignore и Allow; Policy / Strategy / Plan / Scheduler отложены.

Cancellation API, IEcaCancellableAction и IEcaExecutionExecutor удалены из Execution v1. Произвольный Task нельзя универсально остановить; CancellationToken — кооперативный запрос. Предметная очистка не гарантирует остановку Action.Run. Универсальный Reset над Task приводил к чрезмерно сложной семантике.

Reset, прерывание, pause/resume и сохранение execution могут потребовать явную модель шагов/команд/истории со счётчиком команд и сериализуемым состоянием. Это отдельное будущее проектирование. Queue отложен до реальной необходимости. В v1 нет Closing, UnregisterAsync, истории executions, расширенной инспекции, Priority, Retry, Timeout, MaxConcurrency, Dependencies, Sequences и Parallel. Согласованная Execution v1 не расширяется этой итерацией.

## Scope v1

EcaScope — граница владения и времени жизни вокруг собственного EcaExecutionEngine. Engine скрыт внутри Scope; изменяемые registries наружу не выдаются. Каждый Scope имеет собственные EcaRuleRegistry и EcaRuleExecutionRegistry, независимые ExecutionGroup, GroupState, активные executions и время жизни Limit.

EcaScopeEngine принимает один общий IEcaCommandRunner. При создании Scope он создаёт EcaExecutionEngine с этим runner и передаёт engine во внутренний конструктор Scope. Все scopes используют один EcaCommandRunner/EcaCommandRegistry, сохраняя независимые RuleRegistry, ExecutionRegistry, Groups и GroupState. Scope не привязывает Commands и не знает concrete Commands.

EcaScopeEngine — Core-координатор scopes, не static singleton. EcaSystems не зависит от DI и не определяет способ хранения root engine. Приложение само выбирает время жизни и композицию: обычное поле, bootstrap, service locator, VContainer/Zenject или другой DI-контейнер. Это находится вне framework; DI не является выбранной архитектурой EcaSystems.

- scopeEngine.CreateScope() создаёт root с ParentScopeId = null; неявный root/global/default Scope не создаётся.
- scope.CreateScope() делегирует создание child координатору; ParentScopeId равен ScopeId родителя.
- ScopeId уникален среди активных scopes одного EcaScopeEngine. Явные пустые/пробельные ID вызывают ArgumentException; дубликат активного ID — InvalidOperationException.
- Null ID означает генерацию scope-1, scope-2 и далее с пропуском уже занятых ID.
- Parent/child hierarchy задаёт только владение и каскадное время жизни.
- Fire всегда локален: событие получает только Scope, на котором явно вызван Fire. Нет bubbling, маршрутизации к parent/global, broadcast и общей шины событий.
- Dispose немедленно закрывает Scope для Register, Unregister, обоих Fire и CreateScope: они бросают ObjectDisposedException. Повторный Dispose безопасен.
- Dispose parent каскадно закрывает всех descendants; записи потомков удаляются до записи родителя. Dispose child сохраняет parent и siblings.
- Запущенные Actions не отменяются и не ожидаются; существующие async-операции сами удерживают нужные ссылки и завершаются естественно. Scope освобождает ссылку на свой engine.
- ScopeCount отражает число активных scopes; TryGetScope возвращает именно активный объект и false после его удаления.
- Освободившийся ScopeId можно использовать снова. Удаление проверяет object identity, поэтому старый объект не удаляет новый с тем же ID.
- EcaScopeEngine.Dispose закрывает все roots и их descendants; ScopeCount становится 0. Повторный Dispose безопасен, CreateScope после него бросает ObjectDisposedException; lookup остаётся доступным.
- В Core нет SceneScope/NpcScope/EntityScope, ScopeType и Unity lifecycle wrappers. Назначение Scope определяет приложение.

### Общая Rule definition и ссылки

EcaRule<TEventContext, TConditionContext, TActionContext> — reference type. Один и тот же объект Rule можно зарегистрировать по ссылке в нескольких scopes без клонирования. Registries, группы и счётчики независимы; Limit = 1 расходуется отдельно в каждом Scope, а Ignore/Allow применяется только внутри его группы.

Внешний API Rule в основном immutable/get-only. Однако вложенные Action/Condition могут быть изменяемыми объектами: изменение их внутреннего состояния видно всем scopes, использующим общую Rule. Присваивание локальной переменной speechRule = newRule не заменяет ранее зарегистрированный объект. API клонирования/копирования нет.

### ScopeState и граница runtime wiring

EcaScopeState находится в Runtime/Core/Scope и содержит только get-only ScopeId. EcaScope.State владеет этим объектом, а ScopeId делегирует чтение ему. Каждый Scope, включая повторную регистрацию того же ID после Dispose, получает новый State; старый объект остаётся пригодным для чтения. State не содержит EcaScope, engine или сервисов. У конструктора State null/пустой/пробельный ID недопустим; генерация ID остаётся задачей EcaScopeEngine.

IEcaScopeContext<TEventContext> : IEcaExecutionContext<TEventContext> добавляет ScopeState и остаётся invariant. Направление расширения данных: Base → Execution → Scope → Systems → будущие слои. Отдельные Scope Condition/Action contracts и concrete contexts пока не вводятся.

**ScopeState ещё не доступен из контекстов реального Scope.Fire.** Scope делегирует ExecutionEngine, который выбирает точные execution-role типы и создаёт EcaExecutionConditionContext, а Group создаёт EcaExecutionActionContext через new. Локальное добавление ScopeState в эти типы создало бы зависимость нижнего Execution от Scope. Корректное расширение создания и выбора контекстов оставлено следующей архитектурной итерации; универсальной ContextFactory/hydration и временного service environment нет. Наличие IEcaScopeContext не означает поддержку его в Register/Fire pipeline.

Конкретный механизм создания/расширения более богатых context types ещё не выбран, но его необходимо определить до полноценного Systems runtime: Scope уже требует ScopeState, а Systems позже должен добавить SystemState. Универсальная hydration/factory система при этом не обязательна. Временный service locator или скрытый runtime service внутри data context не решают эту архитектурную проблему и не допускаются как обход ограничения.

### Будущая команда Fire Event

Принята same-scope семантика: будущая команда Fire Event, вызванная из Action, по умолчанию должна испускать Event в том же Scope, где выполняется текущая Action. Explicit cross-scope targeting может быть рассмотрен позднее (см. развитие Scope в Roadmap). Это договорённость о поведении, а не выбор механизма routing или способа доступа к Scope; Fire Event в текущем PR не реализуется.

## Две оси архитектуры перед Systems

Вертикальная ось — feature/layer enrichment: Base, Commands, Execution, Scope, Systems и возможный Inspection/Debug. Это практические блоки проекта, не строгая линейная Clean Architecture. До MVP по возможности сохраняем понятную структуру папок.

Горизонтальная ось — самостоятельные capabilities/concepts: Events, Conditions, Actions, Commands, State, Rules, Context, Execution. Развитие отдельной возможности не должно требовать прохождения всего runtime через одну композиционную сущность.

Rule полезен как декларативная/authoring композиция Event + optional Condition + Action. Core1 проверяет разделение деклараций, регистрации Event, синхронного EventDispatcher и независимых runners; выбранная Base-модель описана выше. Старый runtime не заменён. Более широкие subscriptions/bindings и ownership Systems требуют отдельного проектирования после Execution reuse.

System рассматривается как ECA-адаптер независимой игровой системы, организационная сущность и ownership boundary. Events, Commands и State должны оставаться самостоятельными возможностями, а не существовать только через System как центральный runtime container. Events + Commands + State и возможные Condition Queries — направление интеграции, не окончательный IEcaSystem API. Conditions/Actions должны развиваться самостоятельно; старая формулировка «system-specific Conditions не нужны» больше не является принятым ограничением.

Старый Context задуман как носитель данных; bound Commands внутри ActionContext смешивают данные и runtime services. Commands v1 сохранён. В Core1 разделение явно выражено через RuleState = data и RunnerContext = infrastructure. Способ доступа к Commands следующего слоя ещё не выбран; engine, Scope, dispatcher, registries и runners в RuleState не добавляются.

Global State / Variables планируется отдельной system/capability. Сейчас SystemState предполагается глобальным как первый простой этап, но это не финальная модель. После Global Variables нужно спроектировать scope-aware/hierarchical SystemState: global → child → grandchild/local, inheritance/lookup/override. Scoped SystemState сейчас не реализуется и не тождественен минимальному EcaScopeState.

### Структура будущих готовых Systems пакета

Готовые системы самого пакета предполагается размещать рядом с Runtime/Core, в Runtime/Systems. Каждая может быть разделена на `Runtime/Systems/<System>/Core` — самостоятельный функционал системы, и `Runtime/Systems/<System>/Eca` — адаптер/мост к EcaSystems. Будущие примеры — Time и Global Variables / Global State; сейчас они не реализуются.

Эта договорённость относится к готовым системам пакета. Сторонние и клиентские Unity-системы могут иметь любую архитектуру и расположение файлов; ECA-модуль служит адаптером к ним и не требует такой структуры от внешнего кода.

Используем System, не Module. Event — декларация; источник внешний. Rule — Event + необязательная Condition + одна Action. Execution — конкретный запуск. ExecutionGroup — активные executions и состояние одной Rule за время жизни группы. RunMode — Overlap + Limit. Scope — граница владения/времени жизни с локальным Fire. Commands — отдельный минимальный слой выполнения операций из Action.

## Следующий шаг

Текущая итерация завершает Core1 Execution/Scope поверх единого BaseEngine. Следующий этап — review; Commands/Systems обсуждаются отдельно, затем TimeSystem/Wait и Global Variables. EventRegistry и синхронный dispatcher реализованы только в Core1 Base; Fire Event Command и Systems отсутствуют. DI и размещение root engine остаются решением приложения.

## Тестовая инфраструктура

Unity Test Framework + NUnit; Core проверяется в EditMode. GitHub Actions запускает один EditMode job на Unity 6000.3.19f1 через GameCI packageMode с копией пакета в _ci/EcaSystemsPackage. Триггеры: PR, push main, workflow_dispatch. PlayMode job отсутствует до появления lifecycle-сценариев. Coverage input не задан; отсутствие input не гарантирует отключение coverage внутри GameCI. Recovery-срез сообщает о предыдущем CI результате 31/31, а не о проверке этой ветки. CI остаётся authoritative проверкой; фактические проверки checkpoint — в [Testing.md](Testing.md).

Core1 уже содержит минимальные Base/empty Rule shortcuts; дальнейшая ergonomics и factory-style Rule API остаются возможностями Roadmap.
