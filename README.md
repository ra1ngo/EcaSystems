# EcaSystems

EcaSystems — Unity-first framework / UPM-пакет для связи независимых игровых систем через Event–Condition–Action. Core не зависит от Unity. Проект находится **до MVP**: архитектура и public API ещё меняются.

## Назначение

Фреймворк не заменяет Dialogue, Time, Inventory, Quest и другие игровые системы. Внешняя система может быть полностью самодостаточной, в том числе сторонним кодом из Asset Store или GitHub. Поверх неё предполагается ECA-адаптер, связывающий её с остальной игрой. Направление интеграционной поверхности — Events, Commands, State и, возможно, Condition Queries; окончательный контракт Systems ещё не выбран.

Events EcaSystems — внутренние типизированные декларации событий; конкретное возникновение передаётся через Fire с payload. Они не обязаны быть C# events или UnityEvents. Rule описывает `Event + optional Condition + Action`. Commands позволяют Action запрашивать операции без прямой зависимости от конкретной внешней системы. Global Variables — важный будущий сценарий отдельной system/capability, пока не реализованный.

Сейчас существуют минимальный самостоятельный EcaBaseEngine, Commands v1, Execution v1 с Overlap/Limit и Scope v1 с локальным Fire и независимым временем жизни. Все Conditions одного Fire внутри engine проверяются до любых Actions. Scope hierarchy управляет lifetime; Dispose не отменяет уже запущенные Actions.

## Архитектурный checkpoint

Проект рассматривается по двум осям:

- Layers/features: Base → Commands → Execution → Scope → Systems и возможные будущие слои.
- Самостоятельные concepts: Events, Conditions, Actions, Commands, State, Rules, Context, Execution.

Это удобные блоки возможностей, а не строгая линейная Clean Architecture. Композиционные сущности Rule и System не должны автоматически становиться обязательной центральной runtime-моделью для всех capabilities. Ближайший этап — пересмотр Rule-centric runtime и отношений между событиями, привязками и выполнением перед реализацией Systems.

Checkpoint добавляет общий Execution context contract и минимальные ScopeState/Scope context contract. ScopeState пока не передаётся в runtime-контексты Scope.Fire: способ расширения контекстов оставлен открытым. Универсальная context factory не вводилась. EventRegistry, Fire Event Command, EventDispatcher/EventBus и новая Rule/Event runtime-модель не реализованы.

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
