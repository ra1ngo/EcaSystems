# Первый adapter: TimeSystem и ECA

Мы намеренно сначала реализовали standalone TimeSystem без ECA dependencies. Попытка подключить первую реальную внешнюю System выявила слабости standalone observation/lifecycle: per-Timer events заставляли бы adapter следить за каждым созданием и владеть множеством подписок.

Это полезный результат mental test. Adapter не потребовал протащить ECA внутрь Time/Core. Вместо этого эксперимент показал улучшения, полезные любому standalone caller: aggregate lifecycle observation на TimeSystem, Timer как данные, отдельные lifecycle controller и tick processor, один TimerRegistry как источник истины. TimeSystemPlayerLoop владеет Unity hook и чтением scaled/unscaled clock pair; TimeTicker обновляет clock fields до callbacks, публикует tick и изолирует ошибки подписчиков; подписка существует только при tickable work. Двухфазная обработка сначала обновляет registry data, затем исполняет due transitions без callback mutation во время enumeration. Wait использует отдельный небольшой subsystem.

Направления адаптации ясны: внешняя System → ECA через Events и IEcaEventEmitter; ECA → внешняя System через Commands. TimeSystem остаётся Unity-native и возвращает Awaitable; только Wait command переводит ожидание в Task. TimeEcaAdapter является живым bridge, EcaSystem — passive descriptor его exports (Events, Commands, State resolution functions). Шесть aggregate subscriptions заменяют слежение за каждым Timer.

EcaTimeEventState представляет immutable snapshot перехода, а не живой Timer. Immediate/reentrant handling может перезапустить таймер, но уже отправленный snapshot остаётся описанием исходного события.

## Порядок подключения

Emitter только передаёт Fire bound handler; в scope-owned path регистрацию Event валидирует runtime/RuleRegistry после lifecycle проверки EcaScope.Fire. Caller обязан выполнять:

1. runtime.ConnectSystem(system) (в standalone wiring — EcaSystemConnector.Connect(system)).
2. adapter.Connect().
3. Использование Time/Eca.
4. adapter.Disconnect().
5. runtime.DisconnectSystem(system) (в standalone wiring — EcaSystemConnector.Disconnect(system)).

adapter.Connect не может универсально проверить подключение exports через один IEcaEventEmitter. Ошибка порядка обнаруживается при Fire. Connector не должен внезапно владеть произвольными внешними subscriptions; lifecycle внешних Systems/adapters не является ответственностью Core или EcaSystemsRuntime. adapter.Disconnect снимает только external subscriptions, не останавливает standalone timers или Wait.

## Typed Event discoverability

После follow-up PR #17 отдельный typed catalog удалён. Simple Event/Command/System/Namespace registries предоставляют read-only live items, public Resolve и canonical CheckRegistered. EcaSystem хранит local Event/Command registries; Connector регистрирует exact instances в global registries. CheckRegistered проверяет same ID + ReferenceEquals, поэтому новая декларация с тем же ID/type не считается зарегистрированной.

TimeEcaSetup.CreateSystem(time) заполняет local registries отдельными internal EcaTimeEvent и семью concrete Commands. EcaTimeEvent принимает internal EcaTimeEventKey; EcaTimeEventIds централизует key → string mapping. Caller создаёт TimeEcaAdapter(time, system.Events, emitter). Adapter один раз Resolve-ит шесть Events через mapping, проверяет IEcaEvent<EcaTimeEventState> и metadata и кеширует references. Generic Event Register<E> удалён; typed validation принадлежит потребителю. Commands находятся в Eca/Commands, получают Id через internal EcaTimeCommandKey/EcaTimeCommandIds и хранят TimeSystem. Public raw ID constants отсутствуют. Lifecycle args остаются string timerId. TimeWaitArgs принадлежит standalone Time/Core (namespace EcaSystems.Time); EcaWaitCommand распаковывает его в существующий Wait(double, TimerScaleMode). Это небольшой setup, не production composition framework.

Emitter остаётся typed instance-based Fire<E>(IEcaEvent<E>, E, IEcaConditionContext = null, IEcaActionContext = null); Fire(string id, ...) не добавлен. runtime.CreateRule(...) resolve-ит canonical Event по eventId через internal EcaRuleCreator; отдельный общий typed lookup не добавлен. Generic Registry abstraction сейчас сознательно не вводится.

Использованы stable IDs time.timer.started/stopped/paused/resumed/completed/destroyed и time.timer.create/start/stop/pause/resume/destroy, time.wait. Один descriptor относится к одному time namespace; совместная регистрация нескольких таких adapters требует отдельного будущего решения identity, automatic prefixing не добавлялся.

## Что не обобщаем пока

Один adapter ещё не обосновывает универсальный external-adapter framework. Production Core2 composition реализована; далее standalone Global State/Variables + ECA и небольшой end-to-end Sandbox/PlayMode scenario. После нескольких реальных adapters можно сравнить общие потребности.

Queries, Signals, FireEvent и routing — самостоятельные будущие concepts. Generic C# event/Observable/polling/UnityEvent/InputAction adapters, cancellation, Repeat, timer groups и другие Time features не входят в эту итерацию.

TimeSystemPlayerLoop не хранит systems/runners: цепочка — PlayerLoop → TimeTicker → подписанные TimeSystem. Каждый TimeSystem имеет один TimerTickProcessor для многих timers в одном TimerRegistry. Two-phase processing, version protection и reentrant due buffers сохранены. ScaleMode/SOLID refactor отложен до отдельного обсуждения; clock resolver/provider, ITimeSource и TimeSnapshot не добавлены.


## Scope-owned emitter и Context конкретного Fire — 2026-09-20

Caller может передать TimeEcaAdapter готовый scope.EventEmitter. Scope определяет local routing и владеет emitter, привязанным к конкретному instance, не ScopeId. После Dispose stale emitter получает ObjectDisposedException; replacement с тем же ID имеет другой emitter. Вызовы immediate/reentrant. Time adapter не изменён: Fire(event,state) передаёт null/null contexts. Полная цепочка Time lifecycle → TimeEcaAdapter → реальный scoped emitter → Condition/Action покрыта integration test.

Context = input конкретного Fire: ConditionContext и ActionContext раздельны, nullable и проходят без замены references. R создаётся runtime state factory и не входит в emitter payload. Game composition, framework helper или adapter могут сформировать contexts; Core не создаёт/enrich'ит их, не владеет lifetime. Механизм context composition/enrichment ещё не выбран.

Core2 ничего не знает о lifecycle внешних систем и способе получения их событий. EcaSystem описывает ECA exports. Способы внешней адаптации могут различаться: callbacks, Unity events, observables, polling и другие. Framework может предоставлять готовые adapters/helpers, но они не являются обязательной частью Core-модели. Универсальная lifecycle adapter abstraction не вводится; Scope.Dispose не вызывает Disconnect внешнего adapter.

EcaSystemsRuntime v1 — production composition root поверх ScopeRuntime и global registries/Connector/одного CommandRunner, shared checker/action runner и internal RuleCreator. Public API: ConnectSystem, DisconnectSystem, CreateScope, CreateRule, Dispose; автоматического root Scope и public getters registries/runner нет. Runtime не создаёт contexts и не участвует в Fire. Dispose сначала закрывает scopes, затем disconnect-ит snapshot System exports, продолжая cleanup после ошибок и возвращая AggregateException; adapters не disconnect-ит. Running Actions завершаются естественно. DisconnectSystem не удаляет Rules, reconnect exact exports восстанавливает их Fire. Class/delegate CreateRule реализован; Unity/visual authoring остаётся Roadmap. После Connect local Event/Command exports стабильны по convention; изменение local registries может рассинхронизировать local/global. Freeze/snapshot/ownership/consistency не реализованы.

Time Commands используют AEcaCommand<IEcaRuleState,IEcaActionContext,A>: текущие state/context передаются явно, но сами Time operations их не используют. ActionContext nullable, Commands в нём не хранятся; Action получает stable IEcaCommands через RuleCreator. Task contract и standalone Time business semantics сохранены.

Concepts/State реализован независимо от Time: StateResolver.Resolve<T>(stateId, ruleState) передаёт RuleId/Scope state внешней функции без Bind. Actual state и его lifetime остаются во внешней System. TimeEcaSetup пока экспортирует пустой States registry; новую Time state surface эта итерация не добавляет.
