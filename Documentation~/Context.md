# EcaSystems — контекст разработки

## Назначение

EcaSystems — Unity-first UPM-фреймворк для взаимодействия независимых Systems через ECA. Core не зависит от Unity; Base пригоден как самостоятельный минимальный ECA-слой. Base context refactor, Commands v1 и их интеграция в Execution/Scope завершены.

Здесь зафиксированы актуальные согласованные решения. [ToDo.md](ToDo.md) содержит необходимые этапы до первого полноценного применения, [Roadmap.md](Roadmap.md) — необязательные будущие возможности. Документы Documentation~/Architecture/ сохраняют историю обсуждений: [MentalTests](Architecture/MentalTests.md) и [полный recovery snapshot](Architecture/EcaSystems-context-recovery-full-2026-09-11.md) могут содержать устаревшие решения и не переопределяют актуальные документы. [Короткий recovery](EcaSystems-context-recovery-2026-09-11.md) остаётся актуальным кратким срезом; более новые решения имеют приоритет. Постоянные архитектурные документы проекта ведутся на русском языке; имена API не переводятся.

## Core2 State, ForceFire и Scope composition — 2026-09-21

Base `IEcaRuleState` содержит `string RuleId { get; }`. Стандартные constructors: EcaExecutionRuleState<E>(string ruleId,E eventState,EcaExecutionGroupState groupState) и EcaScopeRuleState<E>(string ruleId,E eventState,EcaExecutionGroupState groupState,EcaScopeState scopeState); пустой RuleId отклоняется. Scope создаёт execution state с фактическим rule.Id, затем передаёт RuleId в Scope state/extension factory. Custom Base/Execution factories передают фактический rule.Id; runtime проверяет non-null state и точное совпадение RuleId сразу после factory. Единый helper в ExecutionGroup проверяет Condition и Action state: ошибка Condition распространяется до Action-фазы, ошибка Action state завершает execution как Failed. ForceFire проверяет state до Condition/Action. RuleId — identity Rule и lookup key соответствующей ExecutionGroup внутри конкретного EcaExecutionRuntime. В scoped normal Execution пара ScopeId + RuleId определяет текущую Group; при ForceFire RuleId присутствует, но Group не участвует. ExecutionGroupId не добавлен, EcaExecutionGroupState хранит только ECA counters, без внешнего state.

Non-generic слои RuleState позволяют внешним Systems читать координаты без знания EventState type:

| Contract | Данные / наследование |
| --- | --- |
| `IEcaRuleState` | RuleId |
| `IEcaRuleState<E>` | IEcaRuleState + EventState |
| `IEcaExecutionRuleState` | IEcaRuleState + ExecutionGroupState |
| `IEcaExecutionRuleState<E>` | IEcaExecutionRuleState + `IEcaRuleState<E>` |
| `IEcaScopeRuleState` | IEcaExecutionRuleState + ScopeState |
| `IEcaScopeRuleState<E>` | IEcaScopeRuleState + `IEcaExecutionRuleState<E>` |

StateResolver по-прежнему принимает `Resolve<T>(stateId, IEcaRuleState ruleState)`. Внешняя функция может проверить `ruleState is IEcaScopeRuleState scoped` и получить `scoped.ScopeState.ScopeId`, либо `ruleState is IEcaExecutionRuleState execution` и получить `execution.ExecutionGroupState`, без generic E. Per-Scope lookup использует ScopeId, per-Group — ScopeId + RuleId внутри выбранного runtime. Standard implementations сохраняют прежние constructors/data; Base custom state не обязан реализовывать Execution/Scope contracts. Non-generic RuleState layers не требуют Bind или дополнительных State abstractions.

Concept `Runtime/Core2/Concepts/State` хранит способы доступа к внешнему state. Точный public API:

```csharp
public sealed class EcaStateRegistry
{
    public void Register<T>(string id, Func<IEcaRuleState, T> resolve);
    public void Register<T>(string id, Func<IEcaRuleState, object, T> resolve);
    public bool Contains(string id);
    public bool Unregister(string id);
}
public interface IEcaStateResolver
{
    T Resolve<T>(string stateId, IEcaRuleState ruleState);
    T Resolve<T>(string stateId, IEcaRuleState ruleState, object payload);
}
public sealed class EcaStateResolver : IEcaStateResolver
{
    public EcaStateResolver(EcaStateRegistry registry);
    public T Resolve<T>(string stateId, IEcaRuleState ruleState);
    public T Resolve<T>(string stateId, IEcaRuleState ruleState, object payload);
}
```

Identity регистрации — stable string ID (ordinal comparison). Один T может экспортироваться под несколькими IDs; повторный ID запрещён независимо от типа. Type — только declared contract: после ID lookup Resolve требует точное совпадение registered type с typeof(T), без assignable fallback или поиска по Type. Null/empty/whitespace ID дают ArgumentException; duplicate/missing ID и несовместимый requested type — InvalidOperationException; null function/registry/ruleState — ArgumentNullException. Проверка ID, lookup и declared type предшествуют вызову внешней функции. Каждый Resolve заново получает function и передаёт тот же RuleState reference; result не кешируется, null result допустим, external exception распространяется без замены. Resolver не хранит current RuleState, Bind/bound resolver отсутствуют.

Opaque payload overload передаёт exact object reference, включая null, без интерпретации или casts внутри Core. Shape определяется overload: no-payload registration допускает только no-payload Resolve, payload registration — только payload Resolve. Mismatch даёт InvalidOperationException без fallback. Порядок validation: ID → registration → exact T → shape → non-null RuleState → external callback. Result null допустим; external exception распространяется. Оба Register overload сохраняют исходный delegate instance. Literal null в Register требует явного delegate cast для выбора overload; прежние lambda/typed delegate вызовы и поведение no-payload API сохранены.

EcaSystem constructor теперь требует EcaStateRegistry states после commands; public States хранит exact local registry. EcaSystemConnector constructor также требует global states. Runtime владеет одним global registry/resolver. Connector переносит exact delegate instances из local States в global registry транзакционно вместе с Namespace/Events/Commands/System. Prevalidation проверяет conflicts/canonical identity; Disconnect и rollback удаляют/восстанавливают регистрации, не вызывают их функции и не Dispose returned state. Внутренняя registration хранит ID, declared Type, resolver shape и original delegate; canonical Disconnect проверяет все четыре, включая reference equality delegate. Это не дополнительный public API. Local exports, включая States, должны оставаться стабильны пока System connected.

Actual state принадлежит внешней System. Её function может игнорировать RuleState (global), использовать RuleId, ScopeId или пару ScopeId + RuleId; Core не интерпретирует эти semantics. Ни registry, ни ExecutionGroup не хранят actual external state. Отключение System удаляет доступ через resolver, но не уничтожает уже полученные внешние объекты. Rule cleanup и automatic state inheritance не добавлены.

Class-based Condition/Action используют protected State.Resolve<T>(stateId, ruleState). Один stable resolver передаётся обоим helpers через RuleCreator. Delegate CreateRule signature сохранена: прямой StateResolver argument не добавлялся; внутренние delegate adapters проходят ту же initialization. Доступ к State в этой итерации предоставляется class-based authoring.

ForceFire — отдельный технический caller-state Base bypass до уровня EcaScope. Он сохраняет ALL CONDITIONS → ALL ACTIONS и fire-and-forget Task semantics, но не использует ExecutionGroups/ExecutionMode/lifecycle. Обычный Fire/EventEmitter идёт через Groups и сохраняет admission/reentrancy/isolation. EcaScopeRuntime управляет hierarchy/lifetime и создаёт независимые per-Scope RuleRegistry/ExecutionGroupRegistry. EcaScope получает их и shared checker/runner ссылками и создаёт только EcaExecutionRuntime из этих dependencies; shared EventRegistry приходит в RuleRegistry извне.

Standalone Variables Core V1 реализован в Runtime/Systems/Variables/Core: int/float/bool/string, immutable Definition, три setter semantics и отдельный event snapshot. Variables/Eca adapter и Save/Load остаются следующими отдельными задачами. Queries, snapshots/history, ReactiveState, state inheritance и прочие deferred features не реализованы.

## Core2 Rule creation и Commands — 2026-09-21

**Action ≠ Command.** ECA Action — программируемый исполняемый блок Rule: может вызвать одну или несколько Commands, API внешней/игровой System либо выполнить произвольный C# код. Предметные операции Systems — Commands (например, ShowDialogueCommand или WaitCommand), а не специализированные ShowDialogueAction/WaitAction. Rule содержит один Event, optional Condition и одну Action. Action всегда имеет async Task Run(...); отдельного sync API нет.

`EcaSystemsRuntime.CreateRule` делегирует internal `EcaRuleCreator` в Systems/RuleCreator. Creator получает только live EventRegistry, stable IEcaCommands и stable IEcaStateResolver. Event должен уже экспортироваться подключённой System: Creator resolve'ит eventId и проверяет IEcaEvent<E>/EventStateType, затем создаёт Condition (если нужна), Action и framework-owned immutable EcaRule<E,R>. Event не создаётся. Missing Event даёт existing registry error; wrong E/metadata — ArgumentException. Метаданные минимальны: Rule Name = Id, Description = null; helpers имеют abstract Id и virtual Name/Description; delegate adapters используют <ruleId>.condition/.action.

Facade overloads:
- CreateRule<E,C,A>(id,eventId): C : AEcaCondition<EcaScopeRuleState<E>>, A : AEcaAction<EcaScopeRuleState<E>>, оба new().
- CreateRule<E,R,C,A>(id,eventId): custom R : IEcaRuleState<E>, C/A helpers для R с new().
- CreateRule<E,A>(id,eventId): default Scope state, без Condition.
- CreateRule<E>(id,eventId,action,condition = null): Task-returning delegate Action(state,context,commands) и optional bool Condition(state,context); named arguments позволяют указать condition перед action.

Class-based AEcaCondition<R> получает StateResolver через internal Initialize(stateResolver); AEcaAction<R> — Commands + StateResolver через Initialize(commands,stateResolver). Оба инициализируются ровно один успешный раз до возврата Rule. Protected State/Commands до initialization и повторный Initialize дают InvalidOperationException; null dependency — ArgumentNullException. Проверки обеих Action dependencies выполняются до присваивания, не оставляя частичную initialization. Делегаты обёрнуты внутренними adapters без reflection; Action получает тот же stable Commands instance, что class-based authoring. IEcaAction<R>.Run и существующие Rule interfaces сохранены.

Creation отделена от registration: готовый Rule можно Register в нескольких scopes с независимыми Groups. Creator не владеет RuleRegistry и не резервирует Id. Custom R создаётся existing Scope.Register(rule,mode,extendState) либо другим подходящим runtime, а не Creator. Runtime.CreateRule после Dispose отклоняется, как другие facade operations.

Framework dependencies находятся в Condition/Action instances; per-Fire данные — в RuleState и nullable ActionContext. Action вызывает `await Commands.Run(commandId, state, context, args)`. Commands в ActionContext больше нет; никаких dependency bags, DI, StateBuilder или нового processing layer. Unity materialization из serialized/visual definitions остаётся Roadmap.

## Core2 Fire/Context и Scope-owned EventEmitter — 2026-09-20

Context — nullable input конкретного Fire, а не generic-параметр Rule. IEcaRule<E,R> расширяет event-typed IEcaRule<E>; IEcaCondition<R>.Check получает IEcaConditionContext, IEcaAction<R>.Run — IEcaActionContext. Эти роли раздельны. Core не создаёт Empty contexts, не заменяет instances, не enrich'ит их и не владеет их lifetime. Конкретная Condition/Action сама интерпретирует/cast/валидирует context. Context может сформировать game composition, framework helper или внешний adapter; механизм composition/enrichment пока не выбран.

Основной путь: IEcaEventEmitter.Fire<E>(event, eventState, conditionContext = null, actionContext = null) → explicit internal IEcaEventHandler.Handle<E> конкретного EcaScope → EcaScope.Fire<E> → EcaExecutionRuntime.Fire<E>. На каждом шаге передаются те же Event/EventState и references двух contexts. Fire local + immediate/reentrant, без scopeId, envelope и очереди. Fire(event,state) означает null/null.

Каждый EcaScope создаёт собственный готовый IEcaEventEmitter EventEmitter и bind'ит его к this. ScopeRuntime передаёт общий live EventRegistry нижележащему runtime/registry graph; emitter не получает Registry. Сохранённый emitter удерживает конкретный Scope instance; после Dispose получает ObjectDisposedException, включая вызовы с уже удалённым Event или null, и никогда не перенаправляется в replacement с тем же ScopeId. Emitter только forwards вызов: oldScope.Handle → oldScope.Fire → ThrowIfDisposed естественно сохраняет приоритет disposed failure до event validation, без lifecycle callbacks в emitter. Handler ведёт в тот же Scope.Fire; public Handle нет. Dispose не отменяет уже запущенные Actions.

R остаётся registration/execution concern: Execution.Register<E,R> принимает Func<E,EcaExecutionGroupState,R>; Scope.Register<E,R> — Func<IEcaExecutionRuleState<E>,EcaScopeState,R>. Default Scope.Register<E> использует EcaScopeRuleState<E>. RuleState расширяется runtime-слоями и не передаётся через EventEmitter. IEcaRuleRegistry.GetByEvent<E> и IEcaExecutionGroupRegistry.Get<E>/TryGet<E> позволяют одному Fire выполнять Rules разных R одного Event. Typed <E,R> lookups оставлены для caller-state Base Fire/inspection. IEcaExecutionGroup<E> задаёт Check(E,IEcaConditionContext) / Run(E,IEcaActionContext); EcaExecutionGroup<E,R> сохраняет прежнюю state factory, barrier, admission и lifecycle.

Низкоуровневый ForceFire<E,R>(event,state,createState,conditionContext = null,actionContext = null) сохранён в Base/Execution/Scope. Он требует совместимого R всех matching Rules, использует caller state и Base condition barrier, обходя normal Execution admission/creation. Синхронные ошибки Base по-прежнему распространяются непосредственно. Commands используют самостоятельный state-aware typed bridge AEcaCommand<R,C,A>.

Core2 ничего не знает о lifecycle внешних систем и способе получения их событий. EcaSystem описывает ECA exports. Внешняя библиотека/System может интегрироваться через конкретный adapter/helper, и способы адаптации могут различаться: callbacks, Unity events, observables, polling и т.д. Универсальный lifecycle adapter abstraction в Core не вводится. Framework может предоставлять готовые adapters/helpers, но они не являются обязательной частью Core-модели. TimeEcaAdapter по-прежнему вызывает Fire(event,state) без contexts и может получить scope.EventEmitter.

EcaSystemsRuntime v1 реализован как production composition root, без участия в Fire. Constructor создаёт global Event/Command/State/System/Namespace registries, EcaSystemConnector, один EcaCommandRunner, один stable EcaStateResolver, один EcaBaseConditionChecker, один EcaBaseActionRunner, internal EcaRuleCreator и EcaScopeRuntime. Public API строго ограничен constructor, ConnectSystem(EcaSystem), DisconnectSystem(EcaSystem), CreateScope(string scopeId = null), CreateRule(...), Dispose(). Registries, runner и ScopeRuntime не exposed; root автоматически не создаётся. CreateScope возвращает настоящий EcaScope, child создаётся через scope.CreateScope. Contexts не создаются/enrich'ятся; Commands передаются в Action через RuleCreator, а не через ActionContext.

ConnectSystem/DisconnectSystem делегируют EcaSystemConnector.Connect/Disconnect без aliases Attach/Detach и без нового registration pipeline. Все scopes видят один live global EventRegistry, но имеют собственные Rule/Execution registries и scoped emitter. Scope, созданный до ConnectSystem, видит подключённые позднее exports. DisconnectSystem не удаляет local Rules: Fire отключённого Event отклоняется registry, повторный Connect того же System/exact exports восстанавливает Fire с прежними Groups/counters.

Dispose сначала закрывает public operations, затем ScopeRuntime, затем disconnect'ит snapshot connected Systems. Ошибки cleanup накапливаются: остальные Systems также проходят попытку Disconnect; после cleanup выбрасывается AggregateException, Runtime остаётся disposed. Повторный Dispose — no-op; ConnectSystem/DisconnectSystem/CreateScope после закрытия бросают ObjectDisposedException до проверки аргументов. Running Actions не отменяются. External adapters/subscriptions Runtime не хранит и не disconnect'ит; нормальный внешний порядок — adapter.Disconnect() → runtime.Dispose().

Class/delegate CreateRule и AEcaAction/AEcaCondition реализованы. Context composition/capability container, context-typed helpers и cross-scope routing отложены. Local exports после Connect должны быть стабильны по convention; freeze/snapshot/ownership/consistency и Rule ownership при Disconnect — Roadmap. Следующий крупный шаг — Global State / Variables.

## TimeSystem refactor и первый Time/Eca adapter — 2026-09-18

Актуальное разделение: standalone assembly/namespace `EcaSystems.Time` в `Runtime/Systems/Time/Core` и adapter assembly/namespace `EcaSystems.Time.Eca` в `Runtime/Systems/Time/Eca`. Только adapter references Core2; Time/Core не зависит от Core/Core1/Core2. Система и её standalone tests по-прежнему пригодны для отдельного package. Результат эксперимента описан в [adapters.md](adapters.md).

`TimeTicker.Instance` — единственный public singleton clock source. Public read-only CurrentTime/CurrentUnscaledTime имеют тип double; событие TimeTick передаёт оба значения после обновления. Internal TimeSystemPlayerLoop владеет RuntimeInitializeOnLoadMethod, читает Unity Time.timeAsDouble / Time.unscaledTimeAsDouble один раз за Update и передаёт пару в TimeTicker.Publish. Он устанавливает ровно один hook в конец Update, сохраняя остальные PlayerLoop nodes. Public constructor TimeSystem также обеспечивает установку hook. TimeTicker не содержит PlayerLoop boilerplate и отвечает только за clocks/tick, snapshot подписчиков и изоляцию ошибок. Повторная установка idempotent. Нет MonoBehaviour/GameObject/DontDestroyOnLoad или scene ownership. Ошибка подписчика логируется и не мешает другим подписчикам. Invocation list кэшируется при изменении подписок; кадр использует стабильный snapshot подписчиков без GetInvocationList каждый frame.

`TimeSystem` остаётся обычным instance с прежними CreateTimer/Get/TryGet/Start/Stop/Pause/Resume/DestroyTimer/Wait. Он подписывается на ticker только при наличии Running Timer или pending Wait и снимает подписку после исчезновения последней работы. Наличие active timers определяется сканированием единственного TimerRegistry; второй activeTimers collection нет. Idle instance не удерживается singleton event. Запущенная работа продолжает жить до completion/Stop/Pause/Destroy; Wait без cancellation заканчивается естественно. Main-thread semantics и граница одной play session сохраняются, public Dispose не добавлен.

Timer — data/state object с get-only для caller Id/Duration/Elapsed/Remaining/Progress/ScaleMode/State и IsActive = State == Running. Персональных lifecycle events и методов mutation больше нет. Внутренний nested TimerRuntime содержит absolute deadline и version запуска. Running timing values обновляются по ticks; getters не читают часы. Pause и Destroy(Running) фиксируют значения по текущим clocks ticker, переданным controller.

TimerController заменяет TimerCreator и владеет Create/Start/Stop/Pause/Resume/Complete/Destroy, валидацией и state transitions. Он получает оба clocks и выбирает по ScaleMode — это явно зафиксированный небольшой tech debt для будущего сужения зависимости, без нового clock framework сейчас. TimerRegistry хранит все public timers с Ordinal IDs и является единственным source of truth.

Каждый TimeSystem владеет одним TimerTickProcessor и одним TimerRegistry для многих Timer. TimerTickProcessor в первой фазе один раз перечисляет registry, пропускает inactive, обновляет frame data и собирает только due пары (Timer, Version). До окончания enumeration нет callbacks/mutations. Во второй фазе он проверяет IsActive/version и вызывает controller.Complete; callbacks могут Create/Destroy/Stop/Restart без нарушения enumeration. Ошибки собираются и выбрасываются после всех due entries. Reusable due buffers очищаются в finally; вложенные ticks используют отдельный свободный buffer. Это scratch buffer переходов, не full timer snapshot и не registry. TimerRunner/старый active-runner hub удалены.

Шесть aggregate событий принадлежат TimeSystem: TimerStarted, TimerStopped, TimerPaused, TimerResumed, TimerCompleted, TimerDestroyed с exact Timer payload. State/version фиксируются до события, подписка ticker согласуется до callback. Обычный C# event сохраняет порядок subscribers и распространение exception; completion batch продолжает остальные timers. Timer и Wait batches изолированы: ошибка одного не пропускает другой.

Сохранены validation options/ID/duration/scale, duplicate/unknown ID errors, restart-after-completion, Pause/Resume, Stop reset, Completed в registry и terminal Destroyed. Destroy удаляет регистрацию до события, ID можно сразу использовать снова; старая ссылка читаема. Zero-duration Start сначала Running/Started, completion на следующем tick. После completion Progress = 1 (также для duration 0), Stop/Start сбрасывают его в 0.

Wait — отдельные internal WaitTimer/WaitRegistry/WaitTickProcessor, без public Timer, ID lookup, WaitController и WaitCompleted event. Scan собирает due waits, затем удаляет каждый до AwaitableCompletionSource.SetResult. Ошибки continuations не мешают остальным due waits. Wait, созданный из Timer callback или Wait continuation, относится к следующему tick; текущий batch ограничен identity уже существовавших waits. Public Wait возвращает Unity Awaitable (ожидать один раз), cancellation отсутствует.

TimeEcaAdapter(TimeSystem, local IEcaEventRegistry, IEcaEventEmitter) — только live bridge. TimeEcaSetup.CreateSystem(time) создаёт passive EcaSystem с local Event/Command registries, а caller передаёт system.Events в adapter. Namespace/System Id = time. Connect подписывает шесть aggregate events; повторный Connect даёт InvalidOperationException, Disconnect допускает повторный вызов. Порядок: Connector.Connect(system) → adapter.Connect(); adapter.Disconnect() → Connector.Disconnect(system). Ошибка Fire до Connect не откатывает standalone Timer transition.

TimeEcaEvents удалён. Internal EcaTimeEvent — отдельная declaration IEcaEvent<EcaTimeEventState>, принимающая EcaTimeEventKey. Local internal EcaTimeEventKey + EcaTimeEventIds централизуют шесть прежних time.timer.* ID. Setup регистрирует EcaTimeEvent; adapter один раз Resolve-ит через key → string mapping и кеширует canonical IEcaEvent<EcaTimeEventState>, проверяя interface и EventStateType. Несовместимость обнаруживается в constructor. Raw string constants из adapter удалены. EcaTimeEventState — immutable snapshot TimerId/Duration/Elapsed/Remaining/Progress/ScaleMode/State, создаваемый до Fire; reentrant handling не меняет отправленный payload.

Семь public concrete Commands расположены в Runtime/Systems/Time/Eca/Commands с прежним namespace EcaSystems.Time.Eca и обязательной TimeSystem dependency. Id получают из internal EcaTimeCommandKey + EcaTimeCommandIds; public ID constants удалены, строковые runtime IDs не изменены. Lifecycle Run сохраняет string, параметр называется timerId. Остальные args: TimerCreateOptions и TimeWaitArgs. TimeWaitArgs перенесён в Time/Core, assembly/namespace EcaSystems.Time, без переименования и без нового Wait overload. State — IEcaRuleState, контекст — nullable IEcaActionContext; Time Commands не используют эти параметры, business result отсутствует. Только EcaWaitCommand делает Awaitable → Task bridge.

ScaleMode/SOLID cleanup отложен до отдельного обсуждения TimeSystem; ITimeSource, resolver/provider и TimeSnapshot не вводились. TimerTickProcessor, его two-phase processing, version checks, reentrant buffers и error aggregation в структурном follow-up не менялись.

Production Core2 composition собрана в EcaSystemsRuntime v1; Rule creation по Event ID реализован через runtime.CreateRule(...). Lifecycle внешних adapters не принадлежит Core/Runtime. В PR #17 follow-up изменены simple registries и registry-backed EcaSystem; Fire/Context итерация добавила optional contexts к EventEmitter, двухаргументный вызов сохранён. ID-based Fire отсутствует. Queries, Signals, routing, generic Registry/adapter abstraction и deferred Time features не реализованы.

## Core2 EventEmitter concept — 2026-09-15

Event — факт, представленный ECA для обработки правилами. Base содержит модель IEcaEvent/IEcaEvent<E>/EventRegistry; `Runtime/Core2/Concepts/EventEmitter` предоставляет прямой вход для внешних adapters. Namespace остаётся плоским `EcaSystems.Core2`.

Единственный public порт Concept: `IEcaEventEmitter.Fire<E>(IEcaEvent<E> ecaEvent, E eventState, IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)`. `EcaEventEmitter` — internal sealed transport/binding implementation без constructor dependencies; `IEcaEventHandler.Handle<E>(IEcaEvent<E> ecaEvent, E eventState, IEcaConditionContext conditionContext, IEcaActionContext actionContext)` — internal runtime-side generic callback. Concrete emitter принадлежит конкретному EcaScope, который явно реализует internal handler; внешние Systems/adapters получают только готовый IEcaEventEmitter. Emitter передаёт тот же Event/State и оба nullable contexts одному bound handler без semantic validation. Для scope-owned production path EcaScope.Fire проверяет lifecycle, а EcaExecutionRuntime → RuleRegistry.GetByEvent проверяет null Event, canonical registration и EventStateType. Registry остаётся живой dependency runtime; metadata после регистрации должна быть стабильной.

Композиция выполняет `internal EcaEventEmitter.Bind(IEcaEventHandler handler)` один раз до передачи public IEcaEventEmitter внешнему коду. Bind(null) даёт ArgumentNullException; повторный Bind и любой Fire до Bind — InvalidOperationException. В активном Scope/runtime path null Event даёт ArgumentNullException, незарегистрированный Event — InvalidOperationException, несовместимая metadata — ArgumentException. Standalone bound emitter не интерпретирует Event и передаёт даже null handler-у. Rebind/Unbind, Subscribe/Unsubscribe и multicast не добавлены. Ошибки handler распространяются синхронно. Доступ к internal binding для тестов предоставлен только сборке EcaSystems.Core2.Editor.Tests через InternalsVisibleTo.

Поток: `IEcaEventEmitter.Fire<E> → IEcaEventHandler.Handle<E> → EcaScope.Fire<E> (lifecycle) → ExecutionRuntime → RuleRegistry (Event validation)`. Один handler принимает arbitrary unrelated E и сохраняет declared generic type даже для производного runtime instance, value types и null references. Прежний переход через non-generic C# event требовал generic transport wrapper для восстановления callback; прямой Fire<E> → Handle<E> не стирает тип, поэтому wrapper, Accept и source event удалены. В новом transport нет C# event, object adapters, dynamic/reflection.

Test-only `EcaTestBaseRuntime : EcaBaseRuntime, IEcaEventHandler` один раз bind'ится к Emitter, а Handle<E> вызывает простой Fire<E> и создаёт test RuleState и передаёт полученные nullable contexts в Base ForceFire<E,R>. Production Base не знает об Emitter; Rule selection и ALL CONDITIONS → ALL ACTIONS остаются в Base. Вызовы синхронные/reentrant. Systems exports реализованы отдельно; Signals, FireEventCommand, cross-scope routing и Unity bridge здесь не реализованы. Следующая отдельная итерация external adapters описана в Roadmap; сейчас доступен только прямой вызов Fire<E>.

Историческое переименование ForceFire → Fire больше не является актуальным решением: технический caller-state overload снова называется ForceFire на Base/Execution/Scope. Обычный production scope.EventEmitter → Scope.Fire → ExecutionGroups не использует bypass. Standalone Base integration test адаптирует собственный test-only emitter к Base.ForceFire; это не production Scope flow. FireEventCommand, global bus и Unity bridge не реализованы. Core/Core1 не менялись.

## Core2 Systems v1 и Commands — 2026-09-17

`AEcaCommand` — public non-generic abstract class: Id, RuleStateType, ContextType, ArgsType и Task Run(IEcaRuleState, IEcaActionContext, object). `AEcaCommand<R,C,A>` предоставляет sealed metadata/bridge и abstract Task Run(R,C,A), где R : IEcaRuleState, C : IEcaActionContext. Concrete Command реализует Id и typed Run, без metadata/casts. Сохранён обычный class virtual dispatch для Unity compatibility; Entry, reflection/dynamic и compatibility aliases отсутствуют.

EcaCommandRegistry хранит AEcaCommand напрямую: Commands (read-only live view), Register, Unregister, Contains, Resolve и canonical CheckRegistered. Heterogeneous Commands регистрируются без R/C/A на call site. EcaCommandRunner реализует IEcaCommands и валидирует state, non-null context, declared args и null Task.

IEcaEventRegistry/EcaBaseEventRegistry предоставляют Events (read-only live dictionary values), Register(IEcaEvent), Unregister/Contains/Resolve(string), CheckRegistered(IEcaEvent). Generic Register<E> удалён: registry хранит non-generic declarations, typed validation принадлежит потребителю (Rule registration/lookup, Time adapter). Resolve unknown ID бросает InvalidOperationException; null/empty/whitespace ID — ArgumentException.

Runtime/Core2/Systems содержит EcaSystem, EcaSystemNamespace, EcaSystemRegistry, EcaSystemNamespaceRegistry, EcaSystemConnector. EcaSystem — passive descriptor с Id/Name/Description/Namespace, IEcaEventRegistry Events, EcaCommandRegistry Commands и EcaStateRegistry States. Constructor требует non-null Namespace/local registries и хранит exact references. Пустые registries разрешены; States содержит функции получения внешнего state, а не actual state.

System хранит local registries без копирования. Их contents и metadata должны оставаться стабильными, пока System connected; синхронизация дальнейших local mutations с global registries автоматически не выполняется. Один connected System соответствует уникальному Namespace; Namespace содержит только Id, automatic prefixing отсутствует.

ID задаются caller'ом целиком, без automatic namespace prefixing. Уникальность per type/per registry: Event и Command могут иметь одинаковую строку Id; System/Namespace также не конфликтуют с другими типами. Обратных ссылок Event/Command на System нет. IEcaEventEmitter не является property System; готовый scope.EventEmitter передаётся adapters внешней композицией.

Четыре simple registries имеют одинаковую форму без generic base/interface: Events/Commands/Systems/Namespaces read-only live collections, Register, Unregister, Contains, public Resolve, CheckRegistered. CheckRegistered означает same ID + ReferenceEquals с canonical stored object; null/unknown/иной instance возвращает false. Никакого сравнения по равенству metadata вместо identity нет. Dictionary использует Ordinal IDs; duplicate registration даёт InvalidOperationException. RuleRegistry/ExecutionGroupRegistry не рефакторились.

Connect проверяет conflicts до mutation и регистрирует exact instances из system.Events.Events и system.Commands.Commands: Namespace → Events → Commands → States → System. Disconnect проверяет canonical identity System, Namespace и всех exports до удаления: System → States → Commands → Events → Namespace. Foreign instance с тем же ID не удаляется. Local registries сохраняются; Connector не владеет adapter subscriptions.

Rollback обоих методов — локальный стек обратных действий только для успешно выполненных шагов. Connect удаляет добавленные регистрации в обратном порядке; Disconnect заново регистрирует удалённые исходные references в обратном порядке. После успешной компенсации исходная ошибка пробрасывается без замены; при ошибке компенсации остальные undo всё равно выполняются, AggregateException сообщает исходную и rollback errors. Новые transaction/ownership/refcount abstractions не потребовались. Гарантия возврата к прежнему состоянию относится к согласованным exception-safe registry operations без внешней/concurrent mutation; произвольный custom IEcaEventRegistry, изменяющий состояние и затем бросающий exception или отказывающий в компенсации, не может получить универсальную atomicity от этого API. Такие ошибки не скрываются.

Systems layer содержит State registrations/resolver, но не владеет actual external SystemState. Queries, enable/disable, serialization, routing и adapter binding не реализованы. Отдельный Time/Eca уже реализован. PR #17 follow-up добавляет focused registry/Connector tests, включая Connect/Disconnect rollback и canonical identity.

## Core2 Commands validation — актуализировано 2026-09-21

Commands остаются самостоятельным horizontal concept. Существующие standalone и Commands + EcaBaseRuntime tests мигрированы с Bind на явную передачу state/context, с сохранением barrier, lookup, exceptions, exact references и async guarantees. Base по-прежнему не знает о Commands.

`EcaCommandRunner : IEcaCommands` хранит только registry, не захватывает per-Fire данные. `Run<R,A>(commandId, state, context, args)` resolve'ит Command заново при каждом вызове. State обязателен: null даёт ArgumentNullException; несовместимые runtime state/non-null context — InvalidOperationException. Null ActionContext проходит bridge к typed Command, которая сама определяет допустимость null для своей операции. Args проверяются по declared generic type: object не скрывает несовместимость, typed null reference/nullable value разрешены. Invalid id/args дают ArgumentException, unknown Command/null Task — InvalidOperationException. Sync exceptions и возвращённые Tasks сохраняются; unregister не ломает уже полученный Task.

Bind/BoundCommands, IEcaCommandRunner и IEcaCommandsActionContext удалены. Один stable runner обслуживает любые executions; state/context/args не смешиваются при overlap. Scope/Execution данные находятся в RuleState, Commands не помещаются в ActionContext.

## Core2 Scope — 2026-09-15

Core2/Base, Core2/Layers/Execution и Core2/Layers/Scope завершены. Scope сочетает vertical enrichment и runtime isolation/lifetime boundary. Orchestration, Group и lifecycle принадлежат Execution; Scope не добавляет Runner/bridge/Executor. Fire/Context API обновлён 2026-09-20 без изменения Execution/Scope lifecycle.

`EcaScopeRuntime(IEcaEventRegistry events, IEcaConditionChecker conditionChecker, IEcaActionRunner actionRunner)` — manager всех активных Scope и их hierarchy. API: `ScopeCount`, `CreateScope(string scopeId = null)`, `TryGetScope(string scopeId, out EcaScope scope)`, `Dispose()`. Общие EventRegistry/checker/runner передаются через constructor; manager создаёт независимые EcaBaseRuleRegistry и EcaExecutionGroupRegistry для каждого Scope и передаёт их вместе с shared checker/runner ссылками в EcaScope. Constructor Scope создаёт только EcaExecutionRuntime из этих dependencies (его внутренний BaseRuntime остаётся деталью Execution). Shared services внутри Scope не создаются. EcaSystemsRuntime владеет одним checker и action runner для всех своих Scope. Одну Rule instance можно зарегистрировать в нескольких Scope: GroupState, active executions, overlap и lifetime Limit независимы.

`EcaScope` — один isolated Scope с собственным ExecutionRuntime. API: `EventEmitter`, `State`, `ScopeId`, `ParentScopeId`, `IsDisposed`, `CreateScope`, `Register`, `Unregister`, `Fire`, `ForceFire`, `GetGroup`, `TryGetGroup`, `Dispose`. Root имеет ParentScopeId == null; child создаётся через parent.CreateScope. Hierarchy определяет ownership/lifetime, но не распространяет Fire: parent и child обрабатывают только собственные Rules.

Null ScopeId означает auto-id: scope-1, scope-2 и далее с пропуском занятых имён; пустые/пробельные явные имена запрещены. Active ScopeId уникален в manager. Dispose сначала закрывает Scope, рекурсивно закрывает descendants, удаляет их из manager и освобождает ссылку на ExecutionRuntime; родитель, siblings и другие roots не затрагиваются при удалении child. Новые операции через disposed Scope бросают ObjectDisposedException, read-only State/identity остаются доступны. Dispose idempotent. Manager.Dispose закрывает все scopes; CreateScope после закрытия бросает ObjectDisposedException, ScopeCount == 0 и TryGetScope возвращает false, как в Core/Core1.

Уже запущенные Actions не отменяются: они завершают старые Groups и counters. Повторное использование ScopeId создаёт новый ScopeState и Execution graph; старый объект и завершение старой Action не затрагивают replacement. Internal removal проверяет reference identity, а не только строковый Id. Уже выбранные Groups текущего Fire сохраняют snapshot semantics Execution; Dispose закрывает последующие вызовы, не меняя текущую orchestration.

`EcaScopeState` содержит только обязательный непустой ScopeId; ParentScopeId и IsDisposed принадлежат EcaScope. `IEcaScopeRuleState<out E>` расширяет `IEcaExecutionRuleState<E>` свойством ScopeState. `EcaScopeRuleState<E>` сохраняет RuleId, EventState и обязательные ссылки ExecutionGroupState/ScopeState без concrete inheritance. `IEcaScopeConditionContext` / `IEcaScopeActionContext` расширяют Execution contexts и остаются пустыми extension points.

Основной `EcaScope.Register<E,R>(rule, mode, extendState)` принимает `Func<IEcaExecutionRuleState<E>, EcaScopeState, R>` с `R : IEcaScopeRuleState<E>`. Адаптер создаёт готовый EcaExecutionRuleState<E> из фактического rule.Id, payload/live GroupState и передаёт его вместе с this.State в extendState. Возвращённый R поступает в Condition/Action. Фазы используют отдельные вызовы extension function; Condition остаётся side-effect-free. Сохранён default `Register<E>(rule, mode)` для EcaScopeRuleState<E>.

Fire и inspection делегируются собственному ExecutionRuntime. ALL CONDITIONS → ALL EXECUTIONS, admission непосредственно в Run, immediate/reentrant Fire и обработка ошибок принадлежат Execution. Все Rules одного Scope получают один ScopeState. Unregister удаляет только локальную регистрацию; активные executions завершаются естественно.

ForceFire с createState — локальный pass-through в Execution/Base с Base constraints: сохраняет Base barrier, не использует Scope extendState и ExecutionMode/lifecycle/counters. Caller сам передаёт createState. StateBuilder/StateFactory отложены до реальной необходимости. EventEmitter concept подключён к конкретному Scope. FireEventCommand, cross-scope Fire, Unity bridge, cancellation и Reset/Queue не реализованы. Roadmap о внешней мутации registries/Rule.Id сохранён без новой защиты в Execution.

## Core2 Base → Execution — 2026-09-14

Актуальная итерация развивается в `Runtime/Core2`, namespace/assembly `EcaSystems.Core2`, без Unity API (`noEngineReferences`). Core2/Base сохраняет свой pipeline; Fire/Context API обновлён 2026-09-20. Core2/Layers/Execution реализован; описанные ниже Core/Core1 — отдельные reference implementations, их bridge/Scope/Commands API не определяют Core2.

### API и композиция

`EcaExecutionRuntime` использует composition с `EcaBaseRuntime` и общие экземпляры `IEcaRuleRegistry`, `IEcaConditionChecker`, `IEcaActionRunner`; Groups хранятся в `IEcaExecutionGroupRegistry` / `EcaExecutionGroupRegistry`. Constructor принимает `(rules, groups, conditionChecker, actionRunner)`. Runtime не выдаёт mutable registries; inspection доступен через `GetGroup(ruleId)` / `TryGetGroup(ruleId, out group)`.

`Register<E,R>(rule, executionMode, createState)` регистрирует Rule через Base, затем создаёт и регистрирует Group; при ошибке второго этапа регистрация Rule откатывается. `Unregister(rule)` удаляет именно зарегистрированный экземпляр Rule и его Group. Чужой экземпляр с тем же Id не удаляет Group. Активные executions продолжаются со старой Group/State; повторная регистрация создаёт независимые counters и lifetime Limit. Метаданные Rule/Event после регистрации должны оставаться стабильными. Переданные registries следует изменять согласованно через Runtime.

`IEcaExecutionGroup` предоставляет Rule/RuleId, ExecutionMode, State и read-only Executions. Typed `IEcaExecutionGroup<E,R>` и `EcaExecutionGroup<E,R>` наследуют event-only IEcaExecutionGroup<E> с Check(E,IEcaConditionContext) и Task Run(E,IEcaActionContext). Typed GroupRegistry.Get сохраняет specialization и бросает ошибку при несовместимости; typed TryGet возвращает false. Duplicate RuleId запрещён. Runtime сохраняет generic extensibility E/R; contexts проходят как nullable base contracts без преобразования, visitor и wrapper.

### Fire, barrier и reentrancy

`Fire<E>(ecaEvent, eventState, conditionContext = null, actionContext = null)` выбирает Rules в registry order, вызывает Check всех Groups и только затем Run прошедших: **ALL CONDITIONS → ALL EXECUTIONS**. Между фазами хранятся ссылки на выбранные Groups. Unregister из предыдущей Action не исключает уже выбранную Group из текущего Fire, но исключает из будущих Fire. Повторный lookup после barrier не выполняется.

Check не проверяет ExecutionMode, не резервирует slot, не создаёт Execution и не меняет counters. Admission проверяется private-функцией непосредственно внутри Run: nested Fire мог занять Group или израсходовать её Limit после внешней Condition-фазы. Fire синхронный, immediate/reentrant, без очереди; он запускает lifecycle, но не ожидает завершения Action Task. Barrier относится к одному invocation. Работа Runtime/Group предполагает последовательные вызовы на одном execution context; конкурентный доступ с разных потоков не синхронизируется.

`ForceFire<E,R>` с createState делегируется внутреннему BaseRuntime с исходными Base constraints: это Base pipeline, который проверяет все Conditions перед Actions, но не использует ExecutionMode, Group createState, lifecycle и counters. Это не пропуск Conditions.

### State, mode и lifecycle

`IEcaExecutionRuleState<out E>` расширяет Base RuleState свойством `ExecutionGroupState`; `EcaExecutionRuleState<E>` хранит RuleId, EventState и обязательную live-ссылку на `EcaExecutionGroupState`. IEcaExecutionConditionContext и IEcaExecutionActionContext остаются необязательными marker extensions; Fire принимает base IEcaConditionContext/IEcaActionContext без layer constraints.

Group хранит `Func<E, EcaExecutionGroupState, R> createState`: Check создаёт свежий State для непустой Condition, Run — свежий State для Action после допуска. Condition и Action могут получить разные экземпляры. Condition по контракту side-effect-free, не мутирует RuleState и не передаёт через него вычисленные данные в Action. Null Condition проходит без создания State. GroupState живёт вместе с одной регистрацией: Condition видит counters до текущего запуска; Action видит уже увеличенный TotalStarted. StateBuilder/StateFactory отложен.

`EcaExecutionModeOverlap` содержит Ignore и Allow. Ignore блокирует запуск при активном execution; Allow допускает несколько. `EcaExecutionMode.Limit`: -1 unlimited, 0 запрещает старт, N > 0 ограничивает lifetime TotalStarted; завершение не восстанавливает Limit, Failed также расходует его. Значения Limit < -1 и неизвестный overlap отклоняются.

`EcaExecution` содержит Id (монотонный в пределах Group), Rule/RuleId, Status и Exception; `EcaExecutionStatus`: Pending → Running → Completed/Failed. До пользовательских createState/Action execution добавлен в active Executions, переведён в Running, TotalStarted увеличен. Поэтому reentrant Fire видит занятость/расход Limit. Rejected Run возвращает CompletedTask без execution, State и изменения counters. Exceptions Action/createState, faulted Task и null Task дают Failed с Exception; finally увеличивает TotalFinished и удаляет execution из active. Lifecycle наблюдает Task и не мешает следующим прошедшим Groups. Executions — только активные запуски, без history; сохранённая внешняя ссылка позволяет увидеть финальный статус.

Ошибка Condition или её createState может прервать Fire до запуска executions; дополнительной error-policy нет. Cancellation, Reset/Queue, Fire Event command и Unity bridge в Core2 Execution не реализованы. EventEmitter принадлежит Scope и вызывает существующий Execution pipeline.

> Далее описаны исторические Core1 и Runtime/Core. Их bound Commands и context composition не являются текущим Core2 API; они не изменялись в этой итерации.

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
