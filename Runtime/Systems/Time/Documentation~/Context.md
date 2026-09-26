# TimeSystem — текущий Context

TimeSystem — standalone System в Runtime/Systems/Time/Core, assembly/namespace EcaSystems.Time. Core использует Unity PlayerLoop/Awaitable, но не зависит от Core/Core1/Core2. Time/Eca — отдельный adapter/export layer с Core2 dependency. ECA Core не владеет timers, waits или lifetime TimeSystem; возможен дальнейший отдельный package/repo.

## Clock и PlayerLoop

Public TimeTicker.Instance предоставляет read-only double CurrentTime/CurrentUnscaledTime и event Action<double,double> TimeTick. Internal TimeSystemPlayerLoop устанавливает один hook в конец Unity Update, удаляет прежние собственные hooks при повторной установке и сохраняет остальные nodes. Он читает timeAsDouble/unscaledTimeAsDouble и передаёт пару clocks в ticker. Время обновляется до callbacks. Список подписчиков кешируется при subscribe/unsubscribe; текущий Publish использует стабильный snapshot. Ошибка подписчика логируется и не прерывает остальных.

Нет MonoBehaviour/GameObject/DontDestroyOnLoad и scene ownership. Public TimeSystem constructor обеспечивает установку hook; SubsystemRegistration сбрасывает ticker для нового play session. TimeSystem — обычный instance: подписывается только при Running Timer или pending Wait, отписывается при отсутствии work. Public Dispose/cancellation API нет; владелец должен учитывать длительность pending work. Использование — Unity main thread, конкурентная работа с разных потоков не поддерживается.

## Timers и registry

Public API: CreateTimer(TimerCreateOptions), Get/TryGet(string timerId), Start/Stop/Pause/Resume/DestroyTimer(string timerId). ID nonempty/non-whitespace, ordinal и уникален среди зарегистрированных timers одного instance. Duration — конечный double >= 0; только TimerScaleMode.Scaled/Unscaled. Duplicate/missing обязательного lookup и недопустимый переход дают exception.

Timer — read-only public facade данных Id/Duration/Elapsed/Remaining/Progress/ScaleMode/State/IsActive, с internal mutable state. Internal вложенный TimerRuntime хранит EndTime и Version. TimerRegistry — единственный Dictionary storage; TimerController отвечает за create/lifecycle и validation. TimerTickProcessor один на TimeSystem, обрабатывает много timers.

Create даёт Stopped без события. Start из Stopped/Completed сбрасывает Elapsed/Progress, задаёт EndTime и публикует Started. Pause допустим из Running, фиксирует текущее время; Resume из Paused пересчитывает EndTime по Remaining. Stop из Running/Paused/Completed сбрасывает Elapsed/Progress. Completion оставляет Timer в registry, Elapsed=Duration, Progress=1; повторный Start разрешён. Destroy удаляет ID до Destroyed event, после чего ID можно использовать вновь; старая ссылка остаётся читаемой. Zero-duration Start сначала Running/Started, completion произойдёт на следующем tick.

Aggregate события TimeSystem: TimerStarted, TimerStopped, TimerPaused, TimerResumed, TimerCompleted, TimerDestroyed. Payload — exact live Timer, не snapshot. Direct lifecycle callback exception распространяется после уже применённого transition, без rollback. Tick сначала обновляет данные всех running timers и собирает due entries, затем вызывает completion. Проверка Version/IsActive защищает от callback mutation/restart. Ошибки completion агрегируются после обработки остальных; timer errors не пропускают Wait batch. Reentrant buffers берутся из пула List, после прогрева обычный tick не создаёт новый snapshot array/list.

## Wait

Wait(double duration, TimerScaleMode scaleMode = Scaled) возвращает Unity Awaitable. Это отдельная capability без Timer ID и без timer lifecycle events: WaitRegistry хранит internal WaitTimer (monotonic ID, EndTime, mode, AwaitableCompletionSource), WaitTickProcessor завершает due waits. Wait удаляется до SetResult; callback/continuation ошибки агрегируются. Wait, созданный внутри Timer callback или continuation текущего tick, относится к следующему tick; zero duration также ждёт tick. Awaitable следует Unity single-await contract. Wait cancellation, frame/conditional waits отсутствуют.

Оба internal Registry имеют internal GetSnapshot(): IReadOnlyList<Timer> / IReadOnlyList<WaitTimer>, read-only frozen membership с exact references, без копирования state или completion source. Dictionary/HashSet ordering не обещается. Snapshot — explicit allocating inspection API; hot path продолжает использовать Values и reusable due buffers.

## ECA exports и adapter

TimeEcaSetup.CreateSystem(time) создаёт passive descriptor time с namespace time: 6 Events time.timer.started/stopped/paused/resumed/completed/destroyed, 7 Commands time.timer.create/start/stop/pause/resume/destroy и time.wait, пустой State registry. Internal enum keys и mappings централизуют IDs. Commands — отдельные public классы с TimeSystem dependency; lifecycle args — string timerId, create — TimerCreateOptions, wait — TimeWaitArgs из Time/Core. RuleState/context передаются по общему Commands contract, domain operations их не используют.

TimeEcaAdapter(time, localEvents, emitter) кеширует typed declarations после проверки type metadata. Явный порядок: ConnectSystem exports → adapter.Connect; adapter.Disconnect → DisconnectSystem exports. Повторный Connect запрещён, Disconnect idempotent; auto-binding нет. На aggregate событии adapter создаёт immutable EcaTimeEventState (TimerId, Duration, Elapsed, Remaining, Progress, ScaleMode, State) и вызывает IEcaEventEmitter.Fire с null contexts. Scope выбирается переданным emitter, routing не добавляется. EcaWaitCommand адаптирует Awaitable в Task через await; отдельный Wait Event не экспортируется.

## Ограничения и проверка

ScaleMode выбирается внутри Controller/processors из двух clocks; ITimeSource/provider/resolver/TimeSnapshot и SOLID redesign не реализованы. Repeat, groups, custom channels и persistence отсутствуют. Дальнейший локальный план — [ToDo](ToDo.md), вопросы — [Roadmap](Roadmap.md). Tests: EcaSystems.Time.Editor.Tests и EcaSystems.Time.Eca.Editor.Tests; точные результаты и rationale local Unity 6000.5.6f1 / CI 6000.3.19f1 находятся в [общем Testing](../../../../Documentation~/Testing.md).
