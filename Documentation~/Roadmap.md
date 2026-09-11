# EcaSystems — возможное развитие

Эти возможности не блокируют первое практическое применение. Обязательные этапы — в [ToDo.md](ToDo.md), согласованные решения — в [Context.md](Context.md). Execution v1, Scope v1, Base context refactor и Commands v1 завершены. Ниже перечислены нереализованные возможности, требующие отдельного проектирования и практических сценариев. Документ ведётся на русском языке.

## Расширенное выполнение

Произвольный C# Task нельзя универсально принудительно остановить и продолжить с произвольного места. Кооперативный запрос не гарантирует остановку Action.Run или предметную очистку.

Прерывание, продолжение, пауза, Reset и сохранение выполнения, вероятно, потребуют явной модели: Action / Program состоит из Step / Command; состояние выполнения содержит текущий шаг / program counter, историю и сериализуемые данные там, где это применимо.

- [ ] Проверить модель на реальном сценарии, включая основу для сохранения/загрузки.
- [ ] При необходимости отдельно определить контракты прерывания и очистки.
- [ ] Рассматривать Queue и Reset как возможные будущие возможности, не обязательные стандартные режимы.
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

- [ ] Optional bridge/callback: внутренний ECA Fire при необходимости дополнительно испускает внешнее классическое событие/callback/event bus notification. Межскоуповый routing учтён в разделе развития Scope.

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

Контексты пока создаются напрямую через new. Механизм context creation/enrichment необходимо определить до полноценного Systems runtime: Scope уже требует ScopeState, а Systems позже добавит SystemState. Это текущая архитектурная задача из [ToDo.md](ToDo.md), а не улучшение только после Systems. Универсальная ContextFactory/hydration не обязательна и не выбрана. Ниже — отдельные будущие улучшения контекстов и Commands, требующие своего проектирования.

- [ ] Генерируемый/типизированный API контекстов.
- [ ] Расширенные providers/extensions для hydration.
- [ ] Оптимизация производительности и кеширование композиции контекстов.
- [ ] Типизированный/генерируемый Command API.
- [ ] Более строгий CommandId/key вместо string.
- [ ] Метаданные команд для визуального программирования.
- [ ] Сериализация аргументов команд.
- [ ] После следующей архитектурной итерации пересмотреть Commands runtime и bound Commands как service внутри ActionContext совместно с context enrichment/hydration.

Эти направления не расширяют текущую v1: результаты команд, cancellation, DI и генерация API сейчас не добавляются.

## Тестирование и удобство Rule API

- [ ] Code coverage отдельной итерацией: input coverageEnabled сейчас не задан, GameCI может включать coverage во временном проекте; отключение не гарантируется.
- [ ] Optional required CI checks / branch protection после стабилизации CI, по решению владельца.
- [ ] Rule shortcut API обязательно нужен позже; не текущий blocker.
- [ ] Рассмотреть factory-style Rule API.
- [ ] Универсальный ContextFactory/hydration как возможное улучшение кода после выбора модели контекстов.

## Расширение Condition / Action

- [ ] Condition Queries: стандартизованные read/query capabilities, в том числе предоставляемые ECA-адаптерами игровых систем.
- [ ] Blueprint-style Sequence / flow composition как самостоятельный Action/flow primitive. Это отдельная идея, не общий пункт Sequences среди execution policies выше.
- [ ] Gates в духе Unreal Blueprint Gate: open / close / toggle и контролируемый пропуск execution. Ответственный слой (Actions, Commands или будущий flow/program layer) пока не определён.
- [ ] Развивать Condition и Action как самостоятельные concepts, не только как детали текущего Rule runtime.

## Управление Systems

- [ ] Возможность отключить все Commands конкретной EcaSystem.
- [ ] Возможность отключить EcaSystem целиком.
- [ ] Хранить ownership exports явно, а не выводить только из namespace/string prefix.
- [ ] Отдельно спроектировать namespaces для Command/Event IDs и возможные stable IDs/GUIDs.

## State

- [ ] Scoped/hierarchical SystemState после Global State/Variables: inheritance, lookup, override по global → child → grandchild/local scopes. Этап отражён в ToDo; глобальный SystemState допустим как первый простой шаг, но не финальная модель.

## Технический долг и архитектурное review

- [ ] Пересмотреть дублирование orchestration EcaBaseEngine / EcaExecutionEngine и роль самостоятельного Base engine.
- [ ] Base async failure handling: fire-and-forget Task в EcaBaseEngine не имеет полноценной observability/error policy. Execution observability/history учтены в разделах инспекции и отладки выше.
- [ ] Определить threading contract Core; main-thread/single-thread orchestration для Unity — вероятное направление, ещё не принятое решение.
- [ ] Event ID ↔ EventContext type canonical contract и future EventRegistry validation.

Ergonomics generic Rule/Context API учтена в разделах развития контекстов и удобства Rule API; пересмотр Commands как service — в разделе контекстов/Commands. Эти вопросы не означают реализацию нового runtime в checkpoint.
