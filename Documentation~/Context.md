# EcaSystems — контекст разработки

## Назначение

EcaSystems — Unity-first UPM-фреймворк для взаимодействия независимых Systems через ECA. Core не зависит от Unity; Base пригоден как самостоятельный минимальный ECA-слой. Base context refactor, Commands v1 и их интеграция в Execution/Scope завершены.

Здесь зафиксированы актуальные согласованные решения. [ToDo.md](ToDo.md) содержит необходимые этапы до первого полноценного применения, [Roadmap.md](Roadmap.md) — необязательные будущие возможности. Документы Architecture/ сохраняют историю обсуждений и могут содержать устаревшие решения; они не переопределяют этот контекст. Постоянные архитектурные документы проекта ведутся на русском языке; имена API не переводятся.

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
- EcaEngine создаёт EcaConditionContext<TEventContext> и EcaActionContext<TEventContext>; Base не зависит от Commands. EcaContext остаётся общим хранилищем payload.
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
- Action вызывает `await context.Commands.Run("command.id", args)` и может последовательно вызвать много Commands. Контекст передаётся автоматически; Action не получает registry, runner или concrete Command.
- Runner проверяет совместимость ActionContext и объявленного TArgs с типом аргументов команды. Совместимые производные типы и null для reference/nullable допускаются; object с неподходящим объявленным типом не выполняет неявное приведение.
- Неизвестный ID, неверные context/args и null Task дают исключение с ID команды. Ошибка команды распространяется в Action и учитывается обычной моделью Failed.
- Resolve выполняется при каждом Run: старые bound API видят Unregister и повторную регистрацию. Уже запущенные Tasks завершаются естественно.
- Commands входят в существующий EcaSystems.Core.asmdef. API v1 минимален и может быть пересмотрен при проектировании Systems.

## Execution v1

Execution использует Rule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>>. Оба контекста предоставляют живой RuleExecutionGroupState, только ActionContext содержит Commands. Execution.Context хранит ActionContext.

Fire сначала выбирает Rules, получает их Groups и создаёт через new EcaExecutionConditionContext для каждой проверки. После завершения всех Conditions создаёт через new EcaExecutionActionContext только для прошедших Rules; конструктор вызывает commandRunner.Bind(this), затем Group.Fire запускает Action. Даже bind не происходит до последней Condition; при исключении в Condition Action-фаза не начинается.

Старая единая модель execution-контекста, EcaExecutionContextFactory и IEcaExecutionContextFactory удалены. Замена фабрики, универсальный ContextFactory и hydration отложены; сейчас контексты создаются напрямую через new.

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

EcaScopeEngine — Core-координатор scopes, не static singleton. В будущей Unity/application composition root один его экземпляр будет зарегистрирован как DI singleton на весь проект. Core не зависит от DI или Unity; контейнер в этой итерации не реализован.

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

## Направление Systems и терминология

Следующий отдельный этап — архитектура интеграции независимых Systems, предоставляющих Events, Conditions, Commands и расширения состояния контекста. Global State / переменные — отдельная System, не встроенная возможность Base. Первый конкретный пример — TimeSystem. При проектировании Systems нужно отдельно пересмотреть минимальный Commands API при необходимости. SystemsState, TimeSystem, EcaTimeSystem и EcaWaitCommand пока не реализуются.

Используем System, не Module. Event — декларация; источник внешний. Rule — Event + необязательная Condition + одна Action. Execution — конкретный запуск. ExecutionGroup — активные executions и состояние одной Rule за время жизни группы. RunMode — Overlap + Limit. Scope — граница владения/времени жизни с локальным Fire. Commands — отдельный минимальный слой выполнения операций из Action.

## Следующий шаг

Base context refactor, Commands v1 и их интеграция в Execution/Scope завершены. Следующий этап в ToDo — архитектура Systems; в этой итерации она не реализуется. Будущие возможности Execution и межскоуповая маршрутизация требуют отдельного согласования.
