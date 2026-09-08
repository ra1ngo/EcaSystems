# EcaSystems — контекст разработки

## Назначение

EcaSystems — Unity-first UPM-фреймворк для взаимодействия независимых Systems через ECA. Core не зависит от Unity; Base пригоден как самостоятельный минимальный ECA-слой. Execution v1 и Scope v1 завершены.

Здесь зафиксированы актуальные согласованные решения. [ToDo.md](ToDo.md) содержит необходимые этапы до первого полноценного применения, [Roadmap.md](Roadmap.md) — необязательные будущие возможности. Документы Architecture/ сохраняют историю обсуждений и могут содержать устаревшие решения; они не переопределяют этот контекст. Постоянные архитектурные документы проекта ведутся на русском языке; имена API не переводятся.

## Архитектура Base

Внешний источник → EcaEvent<TEventContext> → Fire → Rules → Conditions → Actions.

EcaEvent — типизированная декларация с Id, а не C# event и не источник событий. Внешняя интеграция вызывает Fire. Выбор Rules использует Event.Id с проверкой совместимости типов. Rule состоит из Event, необязательной Condition и одной Action. Condition.Check возвращает bool; Action.Run возвращает Task завершения/ошибки, а не бизнес-результат.

- EcaRuleRegistry хранит, регистрирует и валидирует Rules.
- EcaRuleSelector выполняет выборку.
- IEcaRuleChecker / EcaRuleChecker проверяет одну Rule.
- IEcaRuleRunner / EcaRuleRunner вызывает Action.Run(context).
- EcaContextType проверяет типы контекстов.
- EcaEngine использует Base-контексты, EcaExecutionEngine — execution-контексты с живым GroupState.

## Основные инварианты

- Все Conditions одного Fire внутри одного engine проверяются до любых Actions этого Fire: сначала собираются прошедшие Rules/Groups, затем запускаются Actions. Между scopes этот инвариант не расширяется.
- EventContext — логически неизменяемые данные одного Fire; копирования нет.
- RuleId уникален в RuleRegistry; несовместимые типы контекста отклоняются.
- При ошибке регистрации ExecutionGroup engine откатывает регистрацию Rule.
- Base Action.Run(context) и RuleRunner.Run(rule, context) не принимают cancellation token.
- Core не знает о Unity, Entity, Global State и конкретных Systems.

## Execution v1

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

EcaRule<TContext> — reference type. Один и тот же объект Rule можно зарегистрировать по ссылке в нескольких scopes без клонирования. Registries, группы и счётчики независимы; Limit = 1 расходуется отдельно в каждом Scope, а Ignore/Allow применяется только внутри его группы.

Внешний API Rule в основном immutable/get-only. Однако вложенные Action/Condition могут быть изменяемыми объектами: изменение их внутреннего состояния видно всем scopes, использующим общую Rule. Присваивание локальной переменной speechRule = newRule не заменяет ранее зарегистрированный объект. API клонирования/копирования нет.

## Направление Systems и терминология

Следующий отдельный этап — архитектура интеграции независимых Systems, предоставляющих Events, Conditions, будущие Commands и расширения состояния контекста. Global State / переменные — отдельная System, не встроенная возможность Base. Первый конкретный пример — TimeSystem. Commands проектируются после появления устойчивого практического сценария и заранее не смешиваются с Base Action.

Используем System, не Module. Event — декларация; источник внешний. Rule — Event + необязательная Condition + одна Action. Execution — конкретный запуск. ExecutionGroup — активные executions и состояние одной Rule за время жизни группы. RunMode — Overlap + Limit. Scope — граница владения/времени жизни с локальным Fire. Commands — будущая отдельная модель.

## Следующий шаг

Execution v1 и Scope v1 завершены. Следующий этап в ToDo — архитектура Systems; в итерации Scope v1 она не реализуется. Будущие возможности Execution и межскоуповая маршрутизация требуют отдельного согласования.
