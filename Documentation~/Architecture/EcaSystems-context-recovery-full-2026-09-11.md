> **Исторический полный snapshot контекста от 2026-09-11.** Восстановлен из base/main (`a3b448048479059d65f461f94cc489f2697a3632`) без актуализации исторического текста; может содержать устаревшие решения. Актуальные решения: [Context.md](../Context.md), [ToDo.md](../ToDo.md), [Roadmap.md](../Roadmap.md) и [короткий recovery](../EcaSystems-context-recovery-2026-09-11.md). Более новые решения имеют приоритет.

# EcaSystems — контекст для продолжения разговора в новом чате

Дата среза: 2026-09-11

Этот файл нужен, чтобы новый чат быстро восстановил контекст разработки EcaSystems и не откатывался к старым архитектурным решениям.

---

## 1. Проект

**EcaSystems** — Unity package/framework по паттерну Event–Condition–Action.

Канонический репозиторий:

```text
https://github.com/ra1ngo/EcaSystems
```

Unity:

```text
6000.5.6f1
```

UPM package. Корень git-репозитория одновременно является корнем package.

Основная идея:

```text
Event
→ Rule
→ Condition
→ Action
→ Commands
```

Один Rule содержит один Event, optional Condition и одну Action. Action может выполнять много Commands.

Ключевой invariant:

> В рамках одного `Fire` ВСЕ Conditions всех подходящих Rules должны быть проверены до запуска ЛЮБОЙ Action.

---

## 2. Base — актуальная модель

После PR #3 принят вариант:

```csharp
IEcaRule<TEventContext, TConditionContext, TActionContext>
```

Где:

```text
TEventContext      = payload Event
TConditionContext  = context Condition
TActionContext     = context Action
```

Это сознательно выбранная простая compile-time модель. От type-erasure/binding-модели внутри Rule отказались как от слишком сложной.

Есть:

```csharp
IEcaContext<TEventContext>
IEcaConditionContext<TEventContext>
IEcaActionContext<TEventContext>
```

Role-context interfaces invariant по `TEventContext`. Общий read-only `IEcaContext<out TEventContext>` может быть covariant.

Не добавлять `Base`-префиксы.

Condition и Action остаются простыми:

```csharp
public interface IEcaCondition<in TContext>
{
    bool Check(TContext context);
}

public interface IEcaAction<in TContext>
{
    Task Run(TContext context);
}
```

Commands не добавляются вторым параметром Action, а приходят через более богатый `TActionContext`.

---

## 3. Event model

`EcaEvent<TEventContext>` — декларация ECA Event: Id, metadata, payload type.

Это не C# event/source/subscription object.

Внешний адаптер вызывает:

```text
Fire(event, eventContext)
```

Routing Rules идёт по `Event.Id`.

---

## 4. Commands v1 — уже реализовано

Основные сущности:

```text
IEcaCommand
IEcaCommand<TContext, TArgs>
EcaCommandRegistry
IEcaCommandRunner
EcaCommandRunner
IEcaCommands
IEcaCommandsActionContext<TEventContext>
```

Command Id пока `string`.

Typed Command концептуально:

```csharp
public interface IEcaCommand<in TContext, in TArgs> : IEcaCommand
    where TContext : IEcaActionContext
{
    Task Run(TContext context, TArgs args);
}
```

`EcaCommandRegistry` хранит heterogeneous Commands.

`EcaCommandRunner`:
- получает Registry;
- bind'ит текущий ActionContext;
- resolve Command по string Id;
- проверяет Context/Args;
- вызывает Command.

Action видит только:

```csharp
await context.Commands.Run("some.command", args);
```

Action не получает Registry/Runner/concrete Command и не передаёт current context вручную.

---

## 5. Commands lifecycle

Правильный порядок:

```text
Fire
↓
проверить ВСЕ Conditions

↓
для passed Rule:
ExecutionGroup проверяет Limit / Overlap

↓
если execution отклонён:
ActionContext не создаётся
Commands не bind'ятся
Action не запускается

↓
если execution принят:
new ActionContext
↓
commandRunner.Bind(actionContext)
↓
BindCommands(...)
↓
create execution
↓
Action.Run(...)
```

То есть:

```text
ALL CONDITIONS
BEFORE
LIMIT/OVERLAP ACCEPTANCE
BEFORE
COMMAND BIND
BEFORE
ACTION
```

`Bind(this)` из constructor ActionContext удалён.

---

## 6. Execution v1

Execution contexts разделены:

```text
EcaExecutionConditionContext<TEventContext>
EcaExecutionActionContext<TEventContext>
```

Оба имеют:

```text
EventContext
RuleExecutionGroupState
```

Action context дополнительно имеет `Commands`.

Старый единый `EcaExecutionContext<TEventContext>` удалён.

`EcaExecutionContextFactory` удалён.

Contexts сейчас создаются напрямую через `new`.

### ContextFactory / hydration

Сейчас НЕ приоритет и НЕ blocker Systems.

Вернуться к универсальному ContextFactory/hydration позже как к улучшению архитектуры/ToDo.

---

## 7. Execution RunMode

Сейчас:

```text
Overlap:
- Ignore
- Allow

Limit:
- -1 unlimited
- 0 none
- N max actual starts
```

Ignored overlap не расходует Limit.

Statuses:

```text
Pending
Running
Completed
Failed
```

Running Actions:
- не cancel'ятся при Unregister;
- естественно завершаются;
- аналогично продолжают работу после Scope.Dispose.

Cancellation, Queue, Reset отложены.

---

## 8. Scope v1

Каждый Scope имеет свой:

```text
EcaExecutionEngine
RuleRegistry
ExecutionRegistry
Groups/State/Limits
```

Но все scopes используют один shared `IEcaCommandRunner`.

Hierarchy — ownership/lifetime, не routing.

Semantics:
- root: `scopeEngine.CreateScope()`
- child: `scope.CreateScope()`
- Fire local-only
- parent Dispose → descendants Dispose
- child Dispose не трогает parent/siblings
- Dispose idempotent
- disposed Scope unusable
- active ScopeId unique
- disposed Id можно reuse
- running Action после Dispose не cancel'ится

Нет `GlobalScope/SceneScope/NpcScope/ScopeType` в Core.

---

## 9. Rule shortcut / factory

Текущий generic Rule API слишком многословный.

Пользователь считает, что shortcut **точно нужен позже**.

Также стоит рассмотреть factory-style API, например условно:

```text
EcaExecutionRule<TEventContext>
```

или:

```csharp
EcaRule.Execution(...)
```

Но сейчас это НЕ приоритет и не blocker Systems.

Фундаментальный `IEcaRule<TEventContext,TConditionContext,TActionContext>` пока не менять.

---

## 10. Systems — следующий большой этап

Последняя идея пользователя:

```text
Runtime/Core/Systems/
├── Events/
├── State/
└── Engine/   // точный нейминг ещё не зафиксирован
```

Будущий ECA System — passive export provider:

```text
Events
Commands
State
```

Не экспортирует Actions.

System-specific Conditions пока не обязательный export: Rule Conditions сами читают State из context.

EcaSystems не имеет DI dependency. Application может использовать DI снаружи.

---

## 11. Systems / Events — идея пользователя

Нужно:

### Event Registry

Центральный:

```text
EcaEventRegistry
```

хранящий все зарегистрированные ECA Events.

### Rule registration validation

Поверх Rule registration нужна проверка:

```text
Rule.Event должен существовать в EcaEventRegistry
```

Нельзя регистрировать Rule на неизвестный System Event.

### Fire Event Command

Нужна фундаментальная Command:

```text
Action
↓
context.Commands.Run("event.fire", ...)
↓
новый ECA Fire
```

Она должна связывать Rules/Event chains внутри EcaSystems.

---

## 12. Fire Event dependency cycle — открытый вопрос

Пользователь заметил потенциальный цикл:

```text
Engine
→ FireEventCommand
→ CommandRunner
→ обратно Engine
```

Нежелательно:

```text
FireEventCommand
→ EcaSystemsEngine
→ CommandRunner
→ FireEventCommand
```

Наиболее перспективная идея:

```text
EcaEventDispatcher
или
EcaEventRunner
```

Зависимость:

```text
             EcaEventDispatcher
              /             \
             /               \
EcaSystemsEngine         FireEventCommand

EcaCommandRunner
↑
FireEventCommand
```

То есть FireEventCommand знает маленький сервис «испустить ECA event», а не весь Engine.

Другие обсуждавшиеся варианты:
- delegate/callback `Fire(...)`;
- late registration FireEventCommand после создания Engine;
- отдельный Dispatcher/Runner.

Финальное решение ещё не принято.

---

## 13. Fire Event и generic TArgs

Текущий:

```csharp
IEcaCommand<TContext, TArgs>
```

имеет фиксированный `TArgs`.

Но универсальная `"event.fire"` должна уметь:

```text
Fire<PlayerInteractContext>
Fire<SceneStartedContext>
Fire<DamageContext>
...
```

На этой команде надо проверить, не потребует ли Commands API небольшой переработки.

До реального Fire Event Commands заранее не переписывать.

---

## 14. External callback/bridge при Fire Event — Roadmap

Future feature:

> При внутреннем ECA Fire опционально дополнительно испускать классическое внешнее событие наружу через callback/bridge.

Это НЕ ближайшая базовая семантика Fire Event.

Держать как Roadmap-пункт на рассмотрение.

---

## 15. Systems / State

Пока подробно не спроектирован.

Ожидаемое направление:

```text
SystemsState
├── Time state/view
├── Dialogue state/view
├── Movement state/view
└── ...
```

Важное правило:

> В ECA context не должны попадать сами concrete System objects.

Не `context.Get<TimeSystem>()`, а ECA-facing read-only state/view.

State пока не приоритет. Вернуться после Events/Fire Event.

---

## 16. Future Time System

После Systems первый concrete System:

```text
Runtime/Systems/Time/
├── Core/
│   └── TimeSystem
└── Eca/
    ├── EcaTimeSystem
    └── EcaWaitCommand
```

`TimeSystem` standalone, `EcaTimeSystem` adapter.

Первая реальная Command — Wait.

Вероятное направление:
- scheduler;
- Unity PlayerLoop integration;
- без обязательного UniTask dependency.

Пока не реализовывать.

---

## 17. Testing — новая инфраструктура

Старый огромный:

```text
Runtime/Unity/EcaSystemsSmokeTest.cs
```

удалён после миграции.

Теперь используется:

```text
Unity Test Framework + NUnit
```

Структура примерно:

```text
Tests/
└── Editor/
    ├── Base/
    ├── Commands/
    ├── Execution/
    ├── Scope/
    ├── Integration/
    └── Support/
```

PlayMode tests пока отсутствуют, потому что Core не требует Unity lifecycle.

Будущие PlayMode:
- TimeSystem
- PlayerLoop
- Wait
- MonoBehaviour adapters
- Scene lifecycle

Work перенёс 22 старых сценария и получил **31 NUnit case**.

---

## 18. GitHub Actions / CI

Используется:

```text
GitHub Actions
GameCI unity-test-runner
Unity Test Framework
```

Workflow:

```text
.github/workflows/tests.yml
```

### PackageMode workaround

Repo root = package root, поэтому для GameCI packageMode workflow делает обычный checkout и копирует package во временную подпапку:

```text
workspace/
├── .git/
├── Runtime/
├── Tests/
├── package.json
└── _ci/
    └── EcaSystemsPackage/
        ├── Runtime/
        ├── Tests/
        └── package.json
```

GameCI:

```yaml
packageMode: true
projectPath: _ci/EcaSystemsPackage
```

Этот layout уже работает.

---

## 19. CI Unity version

Локальная Unity пользователя:

```text
6000.5.6f1
```

Из-за проблем GameCI/coverage для CI временно использована:

```text
6000.3.19f1
```

Это только CI version, не изменение локальной/целевой Unity.

Позже можно сделать version matrix.

---

## 20. CI license

GitHub Secrets:

```text
UNITY_LICENSE
UNITY_EMAIL
UNITY_PASSWORD
```

`UNITY_LICENSE` содержит **весь `.ulf` XML**, а не только `SignatureValue`.

В последнем CI run `.ulf` дал:

```text
Machine bindings don't match
```

GameCI fallback'нулся на `UNITY_EMAIL/UNITY_PASSWORD`, после чего Unity Personal activation прошла успешно.

Есть warning при return seat:

```text
Failed to return the Personal license seat
```

Если это начнёт мешать:
- проверить Unity account seats;
- release seat вручную;
- либо `game-ci return-license`.

Это CI housekeeping, не проблема тестов.

---

## 21. Последний фактический CI результат

Последний `unity-test-log.txt` показывает успешный реальный Unity EditMode run:

```text
31/31 Passed
failed = 0
skipped = 0
```

GameCI:
- создал TempProject;
- подключил package;
- скомпилировал `EcaSystems.Core`;
- скомпилировал `EcaSystems.Editor.Tests`;
- выполнил 31 test case;
- распарсил `editmode-results.xml`;
- опубликовал check;
- загрузил artifact.

То есть основная test infrastructure **работает**.

---

## 22. Coverage warning

GameCI по умолчанию всё ещё подключает:

```text
com.unity.testtools.codecoverage
```

и запускает coverage.

Последний run выдал:

```text
No coverage results were saved.
Failed to generate Code Coverage Report.
Make sure you have included at least one assembly before generating a report.
```

Но сами tests:

```text
31/31 Passed
```

Coverage пока не нужен.

Ранняя попытка `coverageEnabled: false` породила несовместимый CLI flag:

```text
--no-coverageEnabled
```

поэтому input убрали.

Coverage разбирать отдельно позже и не блокировать им разработку.

---

## 23. CI philosophy

Сейчас достаточно EditMode job.

PlayMode job пока можно не запускать, потому что PlayMode tests = 0.

Когда появятся TimeSystem/PlayerLoop/Wait — вернуть PlayMode.

Желательные triggers:

```text
pull_request
push -> main
workflow_dispatch
```

CI должен стать authoritative test run после push/PR, чтобы Work не тратил токены на многократные полные Unity test loops.

---

## 24. Git / Work workflow

Пользователь предпочитает:

```text
обсуждение в ChatGPT
→ временный инструкции-*.md
→ Work читает актуальный локальный repo
→ меняет code/docs
→ commit
→ push текущей branch
→ PR пользователь создаёт вручную
```

Work НЕ создаёт PR.

Временные:

```text
инструкции-*.md
инструкции-*.md.meta
```

должны игнорироваться `.gitignore`.

Permanent docs — на русском:

```text
Documentation~/Context.md
Documentation~/ToDo.md
Documentation~/Roadmap.md
Documentation~/Testing.md
Documentation~/TestMigration.md
```

---

## 25. Что уже завершено

Можно считать реализованными:

```text
Base context split
Commands v1
Execution v1
Scope v1
Helpers → Utils
NUnit/EditMode test migration
GitHub Actions package-mode CI infrastructure
```

Последний реальный CI run: **31/31 passed**.

---

## 26. Что сейчас не трогать

Без отдельного решения не менять:

```text
IEcaCondition<T>
IEcaAction<T>
IEcaRule<TEvent,TConditionContext,TActionContext>

Command string Id

Commands v1 API

Execution overlap/limit semantics

Scope lifecycle

Context creation through new
```

---

## 27. Ближайший план после тестов

После мелкой CI-уборки вернуться к Systems.

Порядок обсуждения:

```text
1. Systems folder/layer boundaries
2. EcaEventRegistry
3. registration wrapper: Rule.Event должен существовать в EventRegistry
4. Fire Event Command
5. избежать dependency cycle Engine ↔ FireEventCommand ↔ CommandRunner
6. EventDispatcher/EventRunner как возможное решение
7. проверить текущий Commands API на Fire Event
8. затем State
9. затем root EcaSystemsEngine
10. затем TimeSystem / EcaTimeSystem / Wait
```

ContextFactory/hydration сейчас не вставлять как blocker.

---

## 28. Дальние темы / ToDo / Roadmap

```text
Rule shortcut API — точно нужен
Rule factory API — рассмотреть

ContextFactory / hydration — улучшение позже

entity-scoped overlap semantics
global event bus / dispatcher
runtime scopes/lifetimes расширенно
context/value mapping
external callback/bridge при Fire Event
typed/generated Commands API
code coverage
performance tests
visual editor/data authoring
```

---

## 29. Предпочтения пользователя

- Не соглашаться автоматически; архитектуру надо критиковать, если есть проблема.
- Сначала обсуждение, потом код.
- Не overengineer.
- Не реализовывать deferred features без явного запроса.
- Перед изменениями кода изучать актуальные файлы проекта.
- Более поздние решения имеют приоритет над ранними.
- C# explanations можно сравнивать с JS/Vue/React, если полезно.
- Код писать компактно.
- PR пользователь создаёт вручную.
- Work должен commit + push, но не создавать PR.

---

## 30. Самая последняя точка

Последний CI log подтвердил:

```text
Unity 6000.3.19f1
EditMode
31/31 Passed
```

Лицензия успешно активировалась через fallback email/password.

Некритичные warning:
- coverage report не генерируется;
- Personal seat не удалось автоматически вернуть.

Следующая содержательная архитектурная тема:

```text
Systems → Events → Fire Event Command
```

с особым вниманием к dependency graph и отсутствию циклических ссылок.
