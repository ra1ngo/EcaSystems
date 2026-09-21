# EcaSystems

EcaSystems — Unity-first framework / UPM-пакет для связи независимых игровых систем через Event–Condition–Action. Core не зависит от Unity. Проект находится **до MVP**: архитектура и public API ещё меняются.

## Назначение и текущий Core2

Фреймворк связывает независимые Dialogue, Time, Inventory, Quest и другие Systems. System экспортирует Events и Commands через passive EcaSystem; внешний adapter получает scope.EventEmitter. Lifecycle внешних Systems/adapters остаётся ответственностью game composition. Core2 не зависит от Unity; Time/Core — отдельная standalone Unity assembly.

**Action ≠ Command.** Action — программируемый блок Rule: он может вызвать несколько Commands, напрямую обратиться к API игровой System или выполнить произвольный C# код. Предметные операции Systems — Commands, например ShowDialogueCommand и WaitCommand; отдельные ShowDialogueAction/WaitAction для таких операций не являются моделью EcaSystems. Rule содержит один Event, optional Condition и одну Action. Action всегда async на уровне контракта: Task Run(...), без sync overload.

EcaSystemsRuntime — composition root с global registries, одним stable IEcaCommands, shared EcaBaseConditionChecker/EcaBaseActionRunner и ScopeRuntime. Root автоматически не создаётся. Scope владеет локальными Rule/Execution registries и emitter. Все Conditions текущего Fire проверяются до Actions; Fire local/immediate/reentrant. Dispose не отменяет running Actions.

## Создание Rule

System должна быть подключена до CreateRule. Internal EcaRuleCreator resolve'ит canonical Event по eventId, создаёт Condition и Action, инициализирует Action и возвращает framework EcaRule<E,R>. Event при этом не создаётся. Создание Rule и регистрация в Scope — разные операции:

```csharp
runtime.ConnectSystem(system);
var scope = runtime.CreateScope("root");
var rule = runtime.CreateRule<MyEventState, MyCondition, MyAction>(
    id: "intro", eventId: "game.started");
scope.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
```

MyCondition наследует AEcaCondition<EcaScopeRuleState<MyEventState>>, MyAction — AEcaAction<EcaScopeRuleState<MyEventState>>; оба имеют parameterless constructor. Без Condition используется CreateRule<E,A>. Advanced CreateRule<E,R,C,A> поддерживает custom RuleState; его создание при Fire остаётся за registration state factory. Один Rule instance можно зарегистрировать в нескольких scopes с независимыми Groups.

Delegate authoring использует тот же Creator:

```csharp
var rule = runtime.CreateRule<MyEventState>(
    id: "intro", eventId: "game.started",
    condition: (state, context) => state.EventState.Enabled,
    action: async (state, context, commands) =>
    {
        await commands.Run("dialogue.show", state, context, "intro");
        await commands.Run("dialogue.show", state, context, "next");
    });
```

Condition можно опустить или передать null. Action delegate всегда возвращает Task. AEcaAction предоставляет protected Commands после однократного internal Initialize; обычный CreateRule path гарантирует initialization до публикации Rule. Class-based Action вызывает тот же `Commands.Run(commandId,state,context,args)`.

Commands больше не bind'ятся к ActionContext. EcaCommandRunner : IEcaCommands ничего per-Fire не захватывает; RuleState и nullable ActionContext передаются явно при каждом Run. AEcaCommand<R,C,A> предоставляет typed bridge и metadata через обычный class virtual dispatch. Context — внешний input, а Scope/Execution данные остаются в RuleState. Commands не помещаются в ActionContext.

Global State/Variables, Unity authoring automation, JSON/visual definitions и routing пока отложены. Исторические Runtime/Core и Runtime/Core1 сохранены отдельно; их API не определяет текущий Core2.

## ECA: академический термин и игровая практика

**Event–Condition–Action (ECA) — академически устоявшийся паттерн/структура active rules**, исторически связанная с active databases и event-driven architecture. Классическая форма:

```text
WHEN Event
IF Condition
DO Action
```

Поэтому проект использует общие термины **Event / Condition / Action**, а не зависящие от конкретного редактора Trigger или Instruction. Вводный источник: [Event–condition–action, Wikipedia](https://en.wikipedia.org/wiki/Event_condition_action).

В игровой среде академическое название ECA, по наблюдению в приведённых материалах, освещено заметно слабее, чем сами похожие механизмы. Близкая модель давно встречается в игровых редакторах и no-code/visual-scripting инструментах под собственными именами. Это сопоставление идей, а не утверждение, что каждый продукт официально называет свою модель ECA.

### Warcraft III World Editor / Trigger Editor

Классический Trigger буквально разделён на **Events, Conditions, Actions**. Это наиболее прямой пример сходства терминологии: событие инициирует проверку условий и выполнение действий. Источники: [Postmortem: Defense of the Ancients](https://www.gamedeveloper.com/design/postmortem-i-defense-of-the-ancients-i-), [пример триггера на форуме Blizzard](https://us.forums.blizzard.com/en/warcraft3/t/wc3-world-editor-attacks-a-unit-event/4571).

### Construct 2/3

Event sheets организуют conditions и actions: условия проверяют ситуацию и отбирают подходящие объекты, после чего выполняются действия. Слово Event здесь обозначает элемент event sheet и не полностью совпадает с декларацией Event в EcaSystems. Источники по Construct 3: [Events](https://www.construct.net/en/make-games/manuals/construct-3/project-primitives/events), [How events work](https://www.construct.net/en/make-games/manuals/construct-3/project-primitives/events/how-events-work).

### GDevelop

Standard Event содержит conditions, actions и sub-events. В Core instruction обозначает condition или action. Это близкая структура с собственной моделью вложенности. Источники: [StandardEvent](https://docs.gdevelop.io/GDCore%20Documentation/classgd_1_1_standard_event.html), [классы event system](https://docs.gdevelop.io/GDCore%20Documentation/group___events.html).

### Game Creator (Unity)

Используется другая терминология: Triggers, Conditions, Instructions/Actions. Trigger слушает Event, Conditions выбирают ветвь, Instructions/Actions выполняют операции. Источники: [Visual Scripting](https://docs.gamecreator.io/gamecreator/visual-scripting/), [Conditions](https://docs.gamecreator.io/gamecreator/visual-scripting/conditions/).

### RPG Maker

Собственная eventing-терминология включает Events, Event Commands, Conditional Branch, switches и variables. Это пример реактивного игрового eventing и условного command flow, который здесь не объявляется «официальным ECA». Источники: [MZ: Graphics, Mapping & Eventing](https://rpgmakerweb.com/blog/rpg-maker-mz-preview-2-graphics-mapping-eventing), [Eventing a Push/Pull System](https://rpgmakerweb.com/blog/eventing-a-push-pull-system).

Список пока служит исследовательским и контекстным материалом проекта; позднее его можно сократить или вынести в отдельный документ.

## Документация и тесты

- [Context](Documentation~/Context.md) — актуальные решения и ограничения.
- [ToDo](Documentation~/ToDo.md) — необходимые этапы до первого применения.
- [Roadmap](Documentation~/Roadmap.md) — дальнейшие возможности и открытые вопросы.
- [Testing](Documentation~/Testing.md) — запуск тестов и фактическая конфигурация CI.
- [Исторические Mental Tests](Documentation~/Architecture/MentalTests.md) — прежние рассуждения, не source of truth.

Core проверяется NUnit/EditMode тестами. CI запускает один EditMode job на Unity 6000.3.19f1; PlayMode появится вместе с реальными Unity lifecycle-сценариями.
