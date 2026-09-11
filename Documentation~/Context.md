# EcaSystems — контекст разработки

## Назначение

EcaSystems — Unity-first UPM-фреймворк для взаимодействия независимых Systems через ECA. Core не зависит от Unity; Base пригоден как самостоятельный минимальный ECA-слой. Base context refactor, Commands v1 и их интеграция в Execution/Scope завершены.

Здесь зафиксированы актуальные согласованные решения. [ToDo.md](ToDo.md) содержит необходимые этапы до первого полноценного применения, [Roadmap.md](Roadmap.md) — необязательные будущие возможности. Документы Documentation~/Architecture/ сохраняют историю обсуждений: [MentalTests](Architecture/MentalTests.md) и [полный recovery snapshot](Architecture/EcaSystems-context-recovery-full-2026-09-11.md) могут содержать устаревшие решения и не переопределяют актуальные документы. [Короткий recovery](EcaSystems-context-recovery-2026-09-11.md) остаётся актуальным кратким срезом; более новые решения имеют приоритет. Постоянные архитектурные документы проекта ведутся на русском языке; имена API не переводятся.

## Архитектура Base

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

Rule полезен как декларативная/authoring композиция Event + optional Condition + Action. Однако текущие routing/storage/execution слишком сосредоточены на Rule как центральном runtime primitive. Следующий шаг — сравнить текущий RuleSelector-based runtime с Event → bindings/subscriptions, определить связь Event ↔ Rule/Condition/Action ↔ Execution и проверить её на Fire Event/routing без циклического ownership. EventDispatcher/EventBus/EventRuntime пока не выбраны, текущий runtime не заменён.

System рассматривается как ECA-адаптер независимой игровой системы, организационная сущность и ownership boundary. Events, Commands и State должны оставаться самостоятельными возможностями, а не существовать только через System как центральный runtime container. Events + Commands + State и возможные Condition Queries — направление интеграции, не окончательный IEcaSystem API. Conditions/Actions должны развиваться самостоятельно; старая формулировка «system-specific Conditions не нужны» больше не является принятым ограничением.

Context задуман как носитель данных. Bound Commands внутри ActionContext могут смешивать данные и runtime services. Commands v1 пока сохранён; после следующей архитектурной итерации нужно критически пересмотреть это решение вместе с context enrichment/hydration. Engine, Scope, dispatcher, registries и runners в новые Context/State не добавляются.

Global State / Variables планируется отдельной system/capability. Сейчас SystemState предполагается глобальным как первый простой этап, но это не финальная модель. После Global Variables нужно спроектировать scope-aware/hierarchical SystemState: global → child → grandchild/local, inheritance/lookup/override. Scoped SystemState сейчас не реализуется и не тождественен минимальному EcaScopeState.

### Структура будущих готовых Systems пакета

Готовые системы самого пакета предполагается размещать рядом с Runtime/Core, в Runtime/Systems. Каждая может быть разделена на `Runtime/Systems/<System>/Core` — самостоятельный функционал системы, и `Runtime/Systems/<System>/Eca` — адаптер/мост к EcaSystems. Будущие примеры — Time и Global Variables / Global State; сейчас они не реализуются.

Эта договорённость относится к готовым системам пакета. Сторонние и клиентские Unity-системы могут иметь любую архитектуру и расположение файлов; ECA-модуль служит адаптером к ним и не требует такой структуры от внешнего кода.

Используем System, не Module. Event — декларация; источник внешний. Rule — Event + необязательная Condition + одна Action. Execution — конкретный запуск. ExecutionGroup — активные executions и состояние одной Rule за время жизни группы. RunMode — Overlap + Limit. Scope — граница владения/времени жизни с локальным Fire. Commands — отдельный минимальный слой выполнения операций из Action.

## Следующий шаг

Ближайший этап — архитектурный refactor перед Systems по двум осям выше. Только после него возвращаемся к Events + Systems implementation, затем TimeSystem/Wait и Global Variables. Checkpoint не реализует EventRegistry, Fire Event Command, dispatcher/bus, новую Rule/Event runtime-модель или Systems. DI и размещение root engine остаются решением приложения.

## Тестовая инфраструктура

Unity Test Framework + NUnit; Core проверяется в EditMode. GitHub Actions запускает один EditMode job на Unity 6000.3.19f1 через GameCI packageMode с копией пакета в _ci/EcaSystemsPackage. Триггеры: PR, push main, workflow_dispatch. PlayMode job отсутствует до появления lifecycle-сценариев. Coverage input не задан; отсутствие input не гарантирует отключение coverage внутри GameCI. Recovery-срез сообщает о предыдущем CI результате 31/31, а не о проверке этой ветки. CI остаётся authoritative проверкой; фактические проверки checkpoint — в [Testing.md](Testing.md).

Rule shortcut API обязательно нужен позже; factory-style Rule API стоит рассмотреть. Оба направления не блокируют текущий этап и сейчас не реализуются.
