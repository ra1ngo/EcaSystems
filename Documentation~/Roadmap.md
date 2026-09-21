# EcaSystems — возможное развитие

Эти возможности не блокируют первое практическое применение. Обязательные этапы — в [ToDo.md](ToDo.md), согласованные решения — в [Context.md](Context.md). Execution v1, Scope v1, Base context refactor и Commands v1 завершены. Ниже перечислены нереализованные возможности, требующие отдельного проектирования и практических сценариев. Документ ведётся на русском языке.

Core1 Base создан параллельно старому Core: A/Abstractions плоский, RuleState = data, RunnerContext = infrastructure, RuleRun скрывает типы от BaseEngine. В prototype уже выбран синхронный classic C# event dispatcher с immediate/reentrant Fire; ALL CONDITIONS → ALL ACTIONS принадлежит BaseEngine. Reuse через ExecutionRuleRunner и ScopeRuleRunner реализован; следующий обязательный шаг — review Core1 Execution/Scope (ToDo). Возможности ниже не реализованы этим prototype.

## Standalone TimeSystem: будущие возможности

Core v1 реализует только одноразовые ID timers и Awaitable Wait через обычную Update phase. Time ECA adapter реализован; EcaSystemsRuntime v1 реализован; следующий этап — Global State / Variables (ToDo). Дополнительные candidates, не входящие в v1:

- [ ] Repeat/looping timers, restart/reset convenience.
- [ ] Cancellation для Wait.
- [ ] Groups/tags/bulk operations.
- [ ] Local/custom scale и time channels.
- [ ] Дополнительные PlayerLoop phases.
- [ ] Conditional/frame waits.
- [ ] Timer debug/editor tooling.

## Core2: adapters внешних событий — после composition / Global State / Sandbox

Предоставить несколько способов адаптации источников к одному public порту `IEcaEventEmitter.Fire<E>(IEcaEvent<E> ecaEvent, E eventState, IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)`:

- [ ] C# events и callback APIs.
- [ ] IObservable / reactive streams.
- [ ] Polling sources.
- [ ] UnityEvent, InputAction / Unity callbacks и другие adapters по практическим сценариям.

Первый конкретный TimeEcaAdapter уже использует typed Fire<E>. Перечисленные generic adapters остаются будущими возможностями после production composition / Global State / Sandbox; универсальный binding framework, Signals и State не реализованы. Способы адаптации могут различаться; lifecycle внешних Systems/adapters не принадлежит Core. Framework helpers допустимы без обязательной общей Core abstraction.

## Core2: согласованность Runtime и registries

Текущий Execution предполагает согласованные изменения RuleRegistry / ExecutionGroupRegistry через Runtime и стабильные Rule.Id и другие metadata, влияющие на identity, после регистрации. Ручная внешняя мутация registries или изменение Rule.Id может рассинхронизировать Runtime. Scope сохраняет эти предположения; защита в этой итерации не добавляется.

- [ ] Обсудить более сильную инкапсуляцию registries и ограничение внешних mutation paths.
- [ ] Рассмотреть immutable identity metadata.
- [ ] Определить необходимость consistency validation/diagnostics.
- [ ] Согласовать recovery strategy при обнаружении рассинхронизации.

Это отдельная будущая итерация, без изменения завершённых Base/Execution API сейчас.

## Расширенное выполнение

Произвольный C# Task нельзя универсально принудительно остановить и продолжить с произвольного места. Кооперативный запрос не гарантирует остановку Action.Run или предметную очистку.

Прерывание, продолжение, пауза, Reset и сохранение выполнения, вероятно, потребуют явной модели: Action / Program состоит из Step / Command; состояние выполнения содержит текущий шаг / program counter, историю и сериализуемые данные там, где это применимо.

- [ ] Проверить модель на реальном сценарии, включая основу для сохранения/загрузки.
- [ ] При необходимости отдельно определить cancellation cleanup hook и контракты прерывания/очистки; текущий Dispose/Unregister не отменяет Actions.
- [ ] Рассматривать Queue и Reset как возможные будущие возможности, не обязательные стандартные режимы.
- [ ] Queued/deferred **Event processing** как optional future execution policy. Это отдельно от overlap Queue: Core1 Base сейчас не имеет queue/pendingEvents и сохраняет immediate/reentrant Fire.
- [ ] Оценить отдельный Group layer между Execution и Scope при реальном use case. Сейчас non-generic Group реализована внутри Core1 Execution; отдельный vertical Group layer не реализован и остаётся future mental-test.
- [ ] Оценить Priority, Retry, Timeout, MaxConcurrency, Dependencies, Sequences и Parallel при наличии оснований.
- [ ] Для каждой возможности отдельно определить ответственный слой; заранее не расширять минимальную Execution v1.

## Асинхронный / плавный Unregister

V1 немедленно удаляет Rule и Group, позволяя Actions завершиться естественно. Возможный будущий UnregisterAsync: Rule перестаёт получать новые Fire; старая Group переходит в retiring/closing; executions завершаются естественно; ожидание заканчивается после всей старой Group.

Тот же RuleId должен быть доступен для повторной регистрации до завершения старого UnregisterAsync. Новая активная Group независима от старой retiring Group; старая не резервирует RuleId в активном registry.

- [ ] Рассмотреть раздельное хранение active и retiring groups.
- [ ] Рассмотреть идентичность поколения/экземпляра.
- [ ] Удалять retiring groups по identity, а не простым Remove(ruleId), чтобы не удалить замену.
- [ ] Проектировать и тестировать отдельно; сейчас не реализовывать Closing/UnregisterAsync.

## Инспекция и история Execution

Group уже предоставляет публичный IReadOnlyList Executions. GroupState содержит только два живых счётчика.

- [ ] Рассмотреть LastExecution, PreviousExecution, History, ActiveCount, PendingCount и LastFailure.
- [ ] Выбрать между отдельным ExecutionHistory и событиями наблюдения.
- [ ] Сохранить защиту изменяемых внутренних коллекций.

## Развитие Scope

Scope v1: hierarchy определяет только время жизни; Fire всегда local-only. Следующие варианты не являются выбранной архитектурой:

- [ ] Маршрутизация событий между scopes при появлении реального сценария.
- [ ] Явный Fire в нескольких scopes и explicit cross-scope targeting как отдельные варианты routing; по умолчанию будущая команда Fire Event из Action работает в том же Scope (принятое поведение описано в [Context.md](Context.md)).
- [ ] Автоматический parent bubbling — только при подтверждённой необходимости.
- [ ] Global event bus как отдельная возможная архитектура для сравнения.
- [ ] Оптимизация через shared RuleRegistry для большого числа однотипных scopes, только после измерений.
- [ ] Расширенная инспекция scopes и инструменты дерева.
- [ ] Сериализация/сохранение ScopeId, если потребуется.
- [ ] Типы/теги scopes только при реальной необходимости.

## Отладка и наблюдение

- [ ] Наблюдать started / completed / failed и сведения об исключениях.
- [ ] Диагностический API и Debug UI для групп и активных executions.
- [ ] Визуализировать цепочки Event → Rule → Execution.

## Диагностика циклического / повторно входящего Fire

- [ ] Диагностировать Event A → Action → Event A и более длинные циклы.
- [ ] Рассмотреть event/execution trace, nested fire depth diagnostics и настраиваемые ограничения глубины, включая A → B → A.
- [ ] Сначала диагностировать, затем ограничивать, сохраняя допустимые сложные цепочки.

- [ ] Public external event callback/bridge при реальном use case. Core1 Fired теперь internal infrastructure event Dispatcher → BaseEngine, публичного gameplay callback нет; межскоуповый routing учтён в разделе развития Scope.

## Расширение Overlap

Сейчас используются только Ignore / Allow с простым switch.

- [ ] Вернуться к пользовательским режимам при практической необходимости.
- [ ] Тогда оценить Policy / Strategy / Plan без изменяемого доступа к внутренностям Group.
- [ ] Не считать расширяемость и дополнительные стандартные режимы согласованной архитектурой.

## Визуальное программирование / Editor

- [ ] Визуальный редактор Rule и представление узлами/графом.
- [ ] UI для Events, Conditions, Actions, Commands, Systems и переменных.
- [ ] Отладочная визуализация и стабильные ID для редактирования/сериализации.

## Сериализация и подготовка данных

- [ ] Rules из JSON/данных и подготовка через ScriptableObject при необходимости.
- [ ] Стабильные форматы, версии и миграции без привязки Core к формату данных.

## Сохранение / загрузка

- [ ] Сохранять Global State, таймеры и scopes/rules при практической необходимости.
- [ ] Исследовать сохранение/продолжение посреди execution через явную модель шагов/состояния выше; не обещать продолжение произвольной async Action.

## Расширенные тесты и производительность

- [ ] Нагрузочные тесты большого числа scopes/executions.
- [ ] Тесты свойств будущих политик и тесты диагностики циклов.
- [ ] Профилирование, измерения и диагностика забытых регистраций/утечек жизненного цикла.

## Создание контекстов до Systems runtime и дальнейшее развитие Commands

В старом Core контексты создаются напрямую через new. В Core1 конкретный RuleRunner создаёт State, а RunnerContext отдельно содержит infrastructure; StateBuilder/ContextFactory отсутствуют. Вертикальное расширение данных Execution/Scope уже реализовано; дальнейшую infrastructure Commands/Systems нужно обсудить отдельно (ToDo). Универсальная ContextFactory/hydration не обязательна и не выбрана. Ниже — отдельные будущие улучшения.

- [ ] Генерируемый/типизированный API контекстов.
- [ ] Расширенные providers/extensions для hydration.
- [ ] Оптимизация производительности и кеширование композиции контекстов.
- [ ] Типизированный/генерируемый Command API.
- [ ] Более строгий CommandId/key вместо string.
- [ ] Метаданные команд для визуального программирования.
- [ ] Сериализация аргументов команд.
Core2 Commands уже используют stable IEcaCommands и явные RuleState/ActionContext, без Bind; историческая Core1 модель не переносится.

Эти направления не расширяют текущую v1: результаты команд, cancellation, DI и генерация API сейчас не добавляются.

## Тестирование и удобство Rule API

- [ ] Code coverage отдельной итерацией: input coverageEnabled сейчас не задан, GameCI может включать coverage во временном проекте; отключение не гарантируется.
- [ ] Optional required CI checks / branch protection после стабилизации CI, по решению владельца.
- [ ] Дальнейшее удобство Rule API сверх реализованных Core1 Base/empty shortcuts.
- [ ] Развивать ergonomics сверх реализованного class/delegate CreateRule только по новым сценариям.
- [ ] Универсальный ContextFactory/hydration как возможное улучшение кода после выбора модели контекстов.

## Core2 Context и authoring после Fire refactor

- [ ] Выбрать composition/enrichment nullable ConditionContext и ActionContext, сохраняя их разделение и передачу exact references. Core сейчас только проводит inputs конкретного Fire.
- [ ] Обсудить typed convenience Action/Condition helpers без возвращения Context generics в IEcaRule<E,R>; capability container и required-context metadata не выбраны.
- [ ] Unity authoring/composition layer: автоматизировать materialization и initialization Rule/Condition/Action из serialized/ScriptableObject/visual definitions. Core предоставляет primitives/lifecycle, Unity скрывает ручной порядок initialization. Сейчас реализован только C# class/delegate CreateRule.

## Расширение Condition / Action

- [ ] Condition Queries: стандартизованные read/query capabilities, в том числе предоставляемые ECA-адаптерами игровых систем.
- [ ] Blueprint-style Sequence / flow composition как самостоятельный Action/flow primitive. Это отдельная идея, не общий пункт Sequences среди execution policies выше.
- [ ] Gates в духе Unreal Blueprint Gate: open / close / toggle и контролируемый пропуск execution. Ответственный слой (Actions, Commands или будущий flow/program layer) пока не определён.
- [ ] Развивать Condition и Action как самостоятельные concepts, не только как детали текущего Rule runtime.

## Управление Systems

- [ ] Rule ownership/consistency при DisconnectSystem: local Rules сейчас сохраняются, Fire отключённого Event отклоняется canonical validation; reconnect exact exports восстанавливает Fire. Автоматический Rule cleanup не реализован.

Rule creation по Event ID реализован через internal EcaRuleCreator и runtime.CreateRule. ID-based Emitter и generic Registry abstraction не добавлены.
- [ ] После Connect(EcaSystem) local Event/Command exports должны оставаться стабильными по convention. Mutation local registries может рассинхронизировать local/global registries. Отдельно выбрать freeze/snapshot/ownership/consistency semantics; сейчас automatic sync и freezing отсутствуют.

- [ ] Развить роль Core2 Namespace и пересмотреть частичное дублирование EcaSystem.Id / EcaSystemNamespace.Id: устранить или явно развести identity; согласовать namespace/stable ID semantics. Сейчас Namespace содержит только Id, automatic prefixing отсутствует.
- [ ] Решить, должен ли EcaSystem требовать хотя бы один export (Event или Command). В Systems v1 полностью пустой descriptor разрешён.
- [ ] Возможность отключить все Commands конкретной EcaSystem.
- [ ] Возможность отключить EcaSystem целиком.
- [ ] Хранить ownership exports явно, а не выводить только из namespace/string prefix.

## State

- [ ] Scoped/hierarchical SystemState после Global State/Variables: inheritance, lookup, override по global → child → grandchild/local scopes. Этап отражён в ToDo; глобальный SystemState допустим как первый простой шаг, но не финальная модель.

## Технический долг и архитектурное review

- [ ] Review реализованного Core1 reuse: Execution/Scope используют один Base pipeline и общий type-erasure bridge. Старые Base/Execution пока остаются reference implementation с дублированием orchestration.
- [ ] Base async failure handling: fire-and-forget Task в EcaBaseEngine не имеет полноценной observability/error policy. Execution observability/history учтены в разделах инспекции и отладки выше.
- [ ] Определить threading contract Core; main-thread/single-thread orchestration для Unity — вероятное направление, ещё не принятое решение.
- [ ] При необходимости развить Core1 Event ID ↔ точный EventStateType contract и immutability custom declarations; базовая EventRegistry validation уже реализована.
- [ ] ActionRegistry/ConditionRegistry только при реальном сценарии lookup/ownership; сейчас Rule хранит прямые ссылки.

Ergonomics generic Rule/Context API учтена в разделах развития контекстов и удобства Rule API; будущая ergonomics Commands — в разделе контекстов/Commands. Эти вопросы не означают реализацию нового runtime в checkpoint.
