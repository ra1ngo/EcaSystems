# Тестирование EcaSystems

Тесты используют Unity Test Framework + NUnit. Production Runtime не содержит test-only кода. Карта всех 22 сценариев бывшего `EcaSystemsSmokeTest` находится в [TestMigration.md](TestMigration.md); дополнительные проверки покрывают валидацию Rule, Pending, расход Limit при ошибке и внутреннюю защиту Bind из старого .NET harness.

## State opaque payload + standalone Variables Core V1 — 2026-09-23

Добавлены **32 test cases**: EcaStatePayloadTests (11), Connector payload cases (5), standalone EcaVariablesSystemTests (16). State tests проверяют exact RuleState/payload forwarding, null payload/result, отсутствие caching, strict shape, duplicate ID независимо от T/shape, точный declared type, validation order и external exceptions. Connector tests проверяют original delegate identity, Connect/Disconnect/Reconnect без вызова resolver, shape/delegate mismatch и rollback обоих направлений. Connector production algorithm не менялся: расширена canonical registration validation в StateRegistry.

Variables tests покрывают int/float/bool/string (включая null), declaration/default/current/old, ordinal ID, duplicate/missing/invalid IDs, unsupported types, exact type без conversions, Try-only-missing semantics, три setter и отсутствие mutation/event при ошибках. Проверены неизменность DefaultValue, отдельные snapshots, float NaN equality и snapshot при reentrant callback. Отдельная EcaSystems.Variables.Editor.Tests assembly ссылается только на EcaSystems.Variables; runtime assembly references пусты, noEngineReferences=true.

Единственная адаптация прежних tests — explicit Func<IEcaRuleState, object> cast для null callback в Register validation test: с двумя overload literal null неоднозначен для C#. Assertion ArgumentNullException сохранён. Остальные прежние tests/assertions не ослаблялись. Core/Core1, Time, Fire/Execution/Scope production не менялись.

Unity **6000.5.6f1**, runs выполнены последовательно:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Focused State (existing + payload) | 25 | 25 | 0 | 0 | 0 |
| Focused Connector | 19 | 19 | 0 | 0 | 0 |
| Focused Variables | 16 | 16 | 0 | 0 | 0 |
| Полный Core2 | 280 | 280 | 0 | 0 | 0 |
| Полный EditMode | 480 | 480 | 0 | 0 | 0 |

Все exit code 0. Full suite: Core 37, Core1 77, Core2 280, Time 53, Time/Eca 17, Variables 16. При compilation повторён прежний CS0108 (EcaScopeTests.Fire(int), строка 60); также остаётся warning пустой EcaSystems.Unity assembly. Новых compiler warnings/errors нет. XML/log локально: `.validation~/variables-state`, `variables-connector`, `variables-focused`, `variables-core2`, `variables-full`, с суффиксами `-results.xml` и `.log`. PlayMode/IL2CPP/remote CI не запускались.

## PR #21: non-generic RuleState layers — 2026-09-23

Добавлены два focused tests в EcaStateTests: `ResolverReadsExecutionLayerWithoutKnowingEventType` и `ResolverUsesScopeAndRuleCoordinatesAcrossUnrelatedEventTypes`. Один non-generic resolver читает ExecutionGroupState, per-Scope и per-Scope+Rule coordinates для unrelated int/string EventState. Проверены exact references стандартных states, сохранение typed EventState и optional layering Base/Execution/Scope. Existing runtime Connect/Disconnect/Reconnect test использует IEcaScopeRuleState без E; assertions сохранены.

13 test methods переименованы без изменения тел/assertions: EcaBaseRuntimeTests (3 Fire_*), EcaCommandsBaseRuntimeTests (5 Fire_*), EcaExecutionRuntimeTests (UsesBaseBarrierAndIgnoresExecutionModeAndLifecycle, AcceptsBaseOnlyTypesAndSharedRegistry), EcaScopeRuntimeTests (UsesOnlyLocalRegistryAndCallerState), EcaScopeTests (UsesCallerStateAndBaseBarrierWithoutScopeExtensionOrExecution) получили ForceFire_*; BaseCallerStateFireForwardsNullWithoutCreatingContexts в EcaFireContextTests стал ForceFire_BaseCallerStateForwardsNullWithoutCreatingContexts. Normal Fire tests, включая PreservesGenericExtensibilityForOtherPayloadAndContexts с дополнительной negative ForceFire assertion, не переименованы.

Unity **6000.5.6f1**, фактически завершённые runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Focused EcaStateTests | 14 | 14 | 0 | 0 | 0 |
| Полный Core2 | 264 | 264 | 0 | 0 | 0 |
| Полный EditMode | 448 | 448 | 0 | 0 | 0 |

Все exit code 0. Полный suite: Core 37, Core1 77, Core2 264, Time 53, Time/Eca 17. При перекомпиляции остаётся прежний CS0108 в EcaScopeTests.Fire(int), строка 60; Unity предупреждает о пустой EcaSystems.Unity assembly. Новых compiler warnings/errors нет. XML/log локально: `.validation~/pr21-nongeneric-focused`, `pr21-nongeneric-core2`, `pr21-nongeneric-full` (суффиксы `-results.xml` и `.log`). PlayMode/IL2CPP/remote CI не запускались. Production изменения ограничены двумя interface files; StateResolver API, constructors/data, runtime semantics и существующие .meta GUID не менялись.

## PR #21 follow-up: State ID / RuleId invariant / Scope composition — 2026-09-23

Добавлены 14 cases: State IDs/declared contract (4), Connector ID conflict/canonical registration (4), normal Execution null/wrong RuleId в Condition и Action state creation (4), ForceFire null/wrong RuleId до callbacks (2). Проверены несколько ID одного T, разные declared types, exact type без assignable fallback, отсутствие вызова resolver при несовместимости, canonical ID + Type + exact delegate (включая covariant delegate с другим declared type). Прежние forwarding/no-cache/external exception, State rollback, ForceFire и Scope isolation assertions сохранены.

Existing EcaStateTests и EcaSystemConnectorTests мигрированы на explicit string IDs. Проверка null RuleState выполняется с зарегистрированным ID после lookup/type validation. В BaseCallerStateFireForwardsNullWithoutCreatingContexts исправлен только RuleId fixture с default `rule` на фактический `base`; exact-reference/null-context assertions не изменены. Production Time/Core, Time/Eca, Core/Core1 и EventEmitter не менялись.

Unity **6000.5.6f1**, фактически завершённые EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Первый Core2, до исправления старой RuleId fixture | 262 | 261 | 1 | 0 | 0 |
| Итоговый focused Core2 assembly | 262 | 262 | 0 | 0 | 0 |
| Полный EditMode | 446 | 446 | 0 | 0 | 0 |

Первый run exit code 2: новый invariant обнаружил неверный RuleId в указанной fixture. Оба итоговых runs exit code 0. Полный suite: Core 37, Core1 77, Core2 262, Time 53, Time/Eca 17; все прошли. При перекомпиляции повторён существующий CS0108 в EcaScopeTests.Fire(int), строка 60. Unity также сообщает о пустой EcaSystems.Unity assembly; новых compiler warnings/errors нет. XML/log: `.validation~/pr21-state-id-core2`, `pr21-state-id-core2-final`, `pr21-state-id-full` (суффиксы `-results.xml` и `.log`). PlayMode/IL2CPP/remote CI не запускались.

## Core2 State / ForceFire / RuleId / Scope composition — 2026-09-21

Добавлены 11 cases: EcaStateTests (8) и расширение Connector suite с 7 до 10. Проверены typed State identity, duplicate/missing/null validation, exact RuleState forwarding, global/per-Rule/per-Scope+Rule access, отсутствие result caching/ownership/Dispose внешних объектов, stable capabilities обоих authoring helpers, atomic one-time initialization, Connect/Disconnect/Reconnect, canonical delegate identity, rollback с State exports, ForceFire barrier/bypass и обычный emitter admission. Existing Disconnect rollback теперь также проверяет восстановление exact State registration. Fault injection для Connect создаёт last-step System conflict после prevalidation; компенсируются только операции Connector, injected unrelated registration не удаляется.

Прежние Core2 tests адаптированы к Base RuleId, technical ForceFire и обязательному States registry descriptor/Connector. Стандартные Scope factories передают фактический rule.Id; custom Scope states сохраняют его из execution state, Execution test factories — из своего Rule. Scope/Execution assertions изоляции, Limit/Overlap, lifecycle, reentrancy и shared services сохранены. Existing Action initialization tests получили StateResolver dependency. Time/Eca setup/tests передают пустой States registry; Time business logic не менялась.

Unity **6000.5.6f1**, фактически завершённые EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Первичный Core2 после API migration, до новых cases | 237 | 237 | 0 | 0 | 0 |
| Focused State + Connector + RuleCreator + Scope + Execution | 72 | 72 | 0 | 0 | 0 |
| Полный Core2 | 248 | 248 | 0 | 0 | 0 |
| Полный EditMode | 432 | 432 | 0 | 0 | 0 |

Все exit code 0. Полный suite: Core 37, Core1 77, Core2 248, Time 53, Time/Eca 17. Повторён прежний compiler warning CS0108 в EcaScopeTests.Fire(int), теперь строка 60; новых compiler warnings/errors нет. XML/log локально в .validation~/state-initial, state-focused, state-core2, state-full. PlayMode/IL2CPP/remote CI не запускались. Исторические записи ForceFire → Fire ниже относятся к прежнему API; актуальное разделение — ordinary Fire и technical ForceFire.

## Core2 RuleCreator и state-aware Commands — 2026-09-21

Test scope расширен на 17 cases: EcaRuleCreatorTests (14) и EcaCommandStateTests (3). Покрыты facade/class/delegate authoring, canonical Event resolve и metadata errors, optional Condition, custom R через Scope factory, однократная Initialize, stable Commands, отдельная registration/Scope isolation, shared checker/runner, async execution lifecycle, state/context/args identity, несовместимые типы, null context и overlapping Tasks после unregister.

Прежние 220 Core2 cases сохранены. EcaCommandRegistryTests, EcaCommandRunnerTests, EcaCommandsBaseRuntimeTests и CommandTestSupport мигрированы с Bind/captured context на explicit state/context. Obsolete Bind(null) assertion заменён проверкой обязательного RuleState; null ActionContext отдельно покрыт новым bridge test. Остальные lookup/args/exception/Task/barrier assertions сохранены. EcaFireContextTests, EcaScopeRuntimeTests, EcaScopeTests получили shared services в constructor. TimeEcaAdapterTests мигрирован на state-aware Run и новый constructor ScopeRuntime; lifecycle/Wait assertions сохранены, Commands проверены с null context.

Unity **6000.5.6f1**, фактически завершённые EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Первичный Core2 после миграции, до новых tests | 220 | 220 | 0 | 0 | 0 |
| Focused RuleCreator + Commands до добавления последнего class Action case | 47 | 47 | 0 | 0 | 0 |
| Полный Core2, окончательные tests | 237 | 237 | 0 | 0 | 0 |
| Time/Eca | 17 | 17 | 0 | 0 | 0 |
| Полный EditMode | 421 | 421 | 0 | 0 | 0 |

Все runs exit code 0. Финальные focused fixtures внутри Core2: Creator 14, Command state 3, Command registry 10, Command runner 15, Commands/Base 6. Полный suite: Core 37, Core1 77, Core2 237, Time 53, Time/Eca 17. При перекомпиляции повторён прежний CS0108 в EcaScopeTests.Fire(int); новых compiler warnings/errors нет. XML/log локально: .validation~/rule-creator-initial, rule-creator-focused, rule-creator-core2, rule-creator-time-eca, rule-creator-full. PlayMode/IL2CPP/remote CI не запускались. Исторические descriptions binding ниже описывают прежний API, а не текущий Core2.

## EcaSystemsRuntime v1 — 2026-09-20

Добавлены **10 cases** EcaSystemsRuntimeTests: отсутствие automatic root, Connect до/после CreateScope, end-to-end Register → scoped emitter → Action, live canonical registry и Disconnect/Reconnect с сохранением Rule/Group, делегирование validation, изоляция scopes и разных Runtime, recursive Dispose/stale emitter/disposed-first validation, естественное завершение running Action, snapshot cleanup всех Systems с AggregateException и idempotence после failure. Cleanup проверяется через custom local EventRegistry passive descriptor, без production injection API.

Существующие EcaSystemConnectorTests (7 cases) и TimeEcaAdapterTests (17 cases) изменены только механически: Attach/Detach → Connect/Disconnect, имена tests/helpers и комментарии; assertions и прежние guarantees сохранены.

Unity **6000.5.6f1**, фактически завершённые EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Первый focused Connector + Runtime | 17 | 16 | 1 | 0 | 0 |
| Повторный focused Connector + Runtime | 17 | 17 | 0 | 0 | 0 |
| Полный Core2 | 220 | 220 | 0 | 0 | 0 |
| Time/Eca | 17 | 17 | 0 | 0 | 0 |
| Полный EditMode | 404 | 404 | 0 | 0 | 0 |

Первый failure был в ожидании нового test probe: успешный Disconnect читает local Events дважды (validation и removal), отказавший — один раз. Исправлено только ожидание счётчика; production-код не менялся. Первый run exit code 2; все последующие exit code 0. Focused: Connector 7/7 + Runtime 10/10. Полный suite: Core 37, Core1 77, Core2 220, Time 53, Time/Eca 17. При перекомпиляции повторён прежний CS0108 в EcaScopeTests.Fire(int); новых compiler warnings/errors нет.

XML/log сохранены локально в .validation~/ с префиксами systems-v1-focused, systems-v1-focused-final, systems-v1-core2, systems-v1-time-eca, systems-v1-full. PlayMode/IL2CPP/remote CI не запускались.

## PR #18 follow-up: emitter только transport/binding — 2026-09-20

EcaEventEmitter больше не содержит Registry, Event validation или lifecycle callback; semantic validation выполняется Scope.Fire → ExecutionRuntime → RuleRegistry. Исторические описания standalone emitter validation ниже относятся к прежнему контракту.

В EcaEventEmitterTests содержательно заменены четыре cases:
- Fire_RejectsDifferentInstanceWithSameIdAndType → Fire_ForwardsExactEventStateAndBothContextReferences.
- Fire_RejectsNullAndUnregisteredEventsBeforeCallbackAndSeesLaterRegistration → Fire_ForwardsNullEventWithoutSemanticValidation.
- Fire_RejectsLyingMetadataEvenWhenRegistryCheckPasses → Fire_DoesNotInspectEventMetadata.
- Fire_UsesSuppliedRegistryContractForEachCall → Fire_ForwardsOmittedAndExplicitNullContexts.

ConstructionAndBind case переименован в Bind_RejectsNullAndAllowsFirstValidBinding: obsolete null Registry constructor assertion удалён, Bind(null)/первый Bind сохранены. Unit suite больше не использует Registry; остальные assertions binding, arbitrary E, declared type, payload identity и unchanged handler exception сохранены. RecordingHandler дополнен записью двух contexts. Base integration изменена только механически на parameterless emitter. Scope integration усилена сравнением ArgumentNullException/ParamName для direct/emitter null Event; canonical/unregistered/metadata и stale/disposed/replacement coverage сохранено. Production TimeEcaAdapter не менялся.

Unity **6000.5.6f1**, фактически завершённые EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Focused Emitter + Base integration + FireContext | 34 | 34 | 0 | 0 | 0 |
| Полный Core2 | 210 | 210 | 0 | 0 | 0 |
| Time/Eca | 17 | 17 | 0 | 0 | 0 |
| Полный EditMode | 394 | 394 | 0 | 0 | 0 |

Focused: EcaEventEmitterTests 12, EcaEventEmitterBaseRuntimeTests 5, EcaFireContextTests 17. Все четыре запуска завершились с exit code 0. При первой перекомпиляции повторён прежний CS0108 в EcaScopeTests.Fire(int); новых compiler warnings/errors нет. XML/log: .validation~/pr18-transport-focused, pr18-transport-core2, pr18-transport-time-eca, pr18-transport-full. PlayMode/IL2CPP/remote CI не запускались.

## Core2 Fire/Context + Scope-owned EventEmitter — 2026-09-20

Добавлены **18 cases**: 17 в EcaFireContextTests и 1 в TimeEcaAdapterTests. Проверяются разные concrete contexts для одного IEcaRule<E,R>, exact references, null/null и независимо optional contexts, разные RuleState для одного event-only Fire с condition barrier, отдельные local emitters parent/child/sibling, stale emitter после ScopeId reuse (включая disposed failure до null/unregistered event validation), эквивалентность direct/emitter, immediate reentrant Allow/Ignore/Limit и поздний admission после nested Fire из Condition, recursive Dispose без отмены Action и потери её context, live canonical registry/metadata, null contexts Base caller-state Fire. Time integration использует настоящий scope.EventEmitter и проверяет шесть lifecycle forwards с null contexts и изоляцией другого Scope.

### Миграция существующих тестов

Все прежние **193 Core2 + 16 Time/Eca cases сохранены**. Не удалялись tests/assertions ради обхода failures; существующие barrier, order, state factory, lifetime, failures, Commands, unregister и Scope isolation гарантии сохранены.

- Только механическая API migration: EcaBaseRuntimeTests, EcaRuleRunnerTests, EcaCommandsBaseRuntimeTests, EcaExecutionGroupTests, EcaScopeTests, EcaScopeRuntimeTests. Убраны C/A type arguments, normal Fire стал event-only; caller-state Fire сохранил R. TypeOf assertions проверяют новое имя Group<E,R>, ожидаемые значения/identity/status/count не менялись.
- BaseTestSupport, ExecutionTestSupport, CommandTestSupport: Rule implements IEcaRule<E,R>; test Condition/Action реализуют base context signature и сами cast'ят context для существующих test delegates. Это только test-level интерпретация, production typed helpers/context bridge не добавлены.
- EventTestSupport: signature Handle расширена; test runtime теперь передаёт полученные contexts вместо создания своих. EcaEventEmitterTests и EcaEventEmitterBaseRuntimeTests не изменены и прошли, включая arbitrary multi-E/declared generic preservation.
- EcaRegistryTests.RuleRegistry_GetByEvent_RejectsIncompatibleSpecializations переименован в RejectsIncompatibleStateAndEventSpecializations. Устаревшие assertions о несовместимости C/A неприменимы к согласованному новому контракту: сохранён incompatible R check, добавлены event-only lookup и incompatible E rejection после canonical replacement. Разные C/A теперь явно разрешены и покрыты новыми identity tests.
- EcaExecutionRegistryTests.Registry_MissingAndIncompatibleTypesAreExplicit: C/A rejection заменён event-only Get/TryGet identity и incompatible E Get/TryGet assertions; прежние missing/R/ID assertions сохранены.
- EcaExecutionRuntimeTests.Fire_PreservesGenericExtensibilityForOtherPayloadAndContexts: incompatible R assertion перенесён с normal Fire (который больше не принимает R) на caller-state Base overload. Остальные изменения этого файла механические; state/payload/counters assertions сохранены.
- TimeEcaAdapterTests: test emitter принимает optional contexts; к существующим forwards добавлены assertions null/null. Добавлен real Scope integration case; прежние expectations не ослаблены. Time/Eca production и Time/Core не изменены.

Перечисленные изменения поведения test harness/контракта и усиление coverage нельзя считать исключительно механической заменой signatures; они отражают явно согласованную модель Fire.

### Фактические запуски Unity 6000.5.6f1

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Core2 после миграции старых tests | 193 | 193 | 0 | 0 | 0 |
| Core2 с новыми focused tests | 210 | 210 | 0 | 0 | 0 |
| Focused Time/Eca | 17 | 17 | 0 | 0 | 0 |
| Полный EditMode regression | 394 | 394 | 0 | 0 | 0 |

Полный suite: Core 37, Core1 77, Core2 210, standalone Time 53, Time/Eca 17. Все четыре завершённых test runs дали exit code 0. Первая промежуточная compile-проверка остановилась до запуска tests на CS0411 (остаточная generic signature ValidateRule<E,R,C,A>); исправлено до успешных runs. При перекомпиляции Core2 повторён прежний CS0108: EcaScopeTests.Fire(int) скрывает ExecutionTestFixture.Fire(int). Новых compiler warnings нет; full run дополнительных warnings не вывел.

XML/log находятся в игнорируемой .validation~: fire-context-core2-migration, fire-context-core2-final, fire-context-time-eca, fire-context-full; первоначальный compile log — fire-context-core2.log. PlayMode, IL2CPP и удалённый CI не запускались. Core/Core1, Commands production и вся TimeSystem architecture не изменены.

## PR #17 follow-up: Time/Eca structure и PlayerLoop separation — 2026-09-18

Commands перенесены в Eca/Commands, TimeWaitArgs — в standalone Time/Core (namespace EcaSystems.Time); GUID всех перемещённых файлов сохранены. Internal EcaTimeEvent и local internal Event/Command key → string mappings заменяют вложенную declaration и public raw ID constants. Строковые runtime IDs не изменились. Добавлены 3 Time/Eca tests: полное и уникальное соответствие event keys, command keys (включая отклонение неизвестного key), принадлежность TimeWaitArgs standalone assembly. Прежние tests проверяют concrete EcaTimeEvent, canonical caching, Commands delegation, lifecycle snapshots, Connect/Disconnect и Wait.

TimeSystemPlayerLoop владеет Unity hook/initialization и передаёт одну пару clocks в TimeTicker. PlayerLoop tests адаптированы к этой границе без ослабления assertions: idempotent installation, сохранение чужих entries, общие ticks независимых instances, nested ticks и 0 managed bytes на 100 warmed updates при прежнем/меньшем количестве активных Systems. TimeTicker сохраняет cached subscriber snapshot, clocks-before-callbacks и failure isolation. TimerTickProcessor, ScaleMode/lifecycle architecture и Core/Core1/Core2 не менялись.

Unity **6000.5.6f1**, фактически выполненные batchmode EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Focused Time/Core assembly | 53 | 53 | 0 | 0 | 0 |
| Focused Time/Eca assembly | 16 | 16 | 0 | 0 | 0 |
| Focused TimePlayerLoopTests | 4 | 4 | 0 | 0 | 0 |
| Полный regression suite | 376 | 376 | 0 | 0 | 0 |

Полный suite: Core 37, Core1 77, Core2 193, standalone Time 53, Time/Eca 16. Все четыре запуска завершились с exit code 0. После focused Time/Eca run mapping assertions усилены с EquivalentTo до EqualTo для проверки соответствия порядку ключей; финальный полный suite выполнил усиленные assertions. В логах этих запусков нет compiler errors/warnings. XML/log находятся в игнорируемой .validation~ с префиксами pr17-structure-core, pr17-structure-eca, pr17-structure-playerloop, pr17-structure-full. PlayMode, IL2CPP и удалённый CI не запускались. ScaleMode/SOLID cleanup остаётся отдельной будущей итерацией.

## PR #17 follow-up: canonical registries и Time/Eca cleanup — 2026-09-18

Четыре simple registries используют live read-only items, public Resolve и exact-instance CheckRegistered. Generic Event Register<E> удалён; typed metadata проверяется потребителем. EcaSystem хранит local Event/Command registries, Connector передаёт exact references и отклоняет foreign replacements до Detach mutation. EventEmitter production/API, RuleRegistry/ExecutionGroupRegistry и standalone Time/Core не менялись.

Добавлены 24 Core2 cases: 16 для четырёх registries (live view, identity, lookup, duplicate, Ordinal, unregister/re-registration, null/invalid IDs), 7 для System/Connector (local/global identity, unrelated registrations, foreign export/namespace replacement, Attach/Detach rollback, обязательные constructor dependencies), 1 для canonical-only Fire. Прежние Event tests адаптированы к non-generic registration и identity. Rule metadata validation проверяется на canonical зарегистрированной declaration, не на чужом instance.

Time/Eca прежние 8 cases переведены на local registries/concrete Commands; добавлены 5 cases для однократного resolve/cache, missing events, несовместимых interface/metadata и null constructor dependencies. EcaTimeEventState сохраняет immutable/reentrant snapshot semantics; metadata GUID сохранён при rename. TimeEcaEvents/TimeEcaCommandIds удалены, команды проверяются по concrete types. Всего добавлено **29 cases**, standalone Time tests сохранены без изменений.

Unity **6000.5.6f1**, фактически выполненные EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Focused Core2 registry/System/EventEmitter | 62 | 62 | 0 | 0 | 0 |
| Focused Time/Eca | 13 | 13 | 0 | 0 | 0 |
| Полный regression suite | 373 | 373 | 0 | 0 | 0 |

Полный suite: Core 37, Core1 77, Core2 193, standalone Time 53, Time/Eca 13. После финального review сохранена прежняя System ID validation в Connector.Detach через Resolve; повторный полный run pr17-cleanup-final также дал 373/373 passed, 0 failed/skipped/inconclusive. Все четыре запуска завершились с exit code 0. Standalone Time отдельно не запускался, поскольку его code/tests не менялись; все 53 cases выполнены полным suite. Первоначальная компиляция повторила прежний CS0108 в EcaScopeTests.Fire(int), новых compiler warnings нет.

XML/log находятся в игнорируемой .validation~ с префиксами pr17-registry-focused, pr17-time-eca, pr17-cleanup-full, pr17-cleanup-final. PlayMode/IL2CPP и удалённый CI не запускались. Политика rollback по-прежнему требует стабильной configuration и exception-safe registry operations; tests инъецируют отказ до mutation, не утверждают универсальную атомарность произвольного custom registry.
## TimeSystem refactor + Time/Eca — 2026-09-18

Актуальная архитектура заменяет исторические TimerRunner/snapshot детали ниже: TimeTicker, один TimerRegistry, TimerController/TimerTickProcessor с due-only двухфазным batch и отдельные WaitRegistry/WaitTimer/WaitTickProcessor. Предыдущие 46 standalone cases адаптированы к system-level lifecycle events и frame-visible Timer data, без потери lifecycle/error/reentrancy coverage. Добавлены 7 архитектурных cases: subscription/unsubscription по Timer/Wait work, ticker clocks и изоляция failures, Timer error без starvation Wait/другой System, новые Timer/Wait из callbacks, обновление всех frame values до callbacks, Pause/Destroy по текущим clocks и failing Wait continuation без starvation.

Добавлена отдельная assembly EcaSystems.Time.Eca.Editor.Tests в Tests/Editor/Systems/Time/Eca: references EcaSystems.Time, EcaSystems.Time.Eca, EcaSystems.Core2 и TestAssemblies. Её 8 cases проверяют шесть typed Event exports по identity, семь Commands, все lifecycle forwards, immutable reentrant completion snapshot, Connect/Disconnect/reconnect, Attach-before-Connect и Disconnect-before-Detach, Wait Task для двух scale modes, validation/typed args. Используется test-only IEcaEventEmitter double с реальным EcaBaseEventRegistry; production Core2 composition/internal emitter binding не добавлялись. Standalone test assembly не зависит от adapter/Core2.

Unity **6000.5.6f1**, фактически завершённые batchmode EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Focused standalone Time, final | 53 | 53 | 0 | 0 | 0 |
| Focused Time/Eca | 8 | 8 | 0 | 0 | 0 |
| Полный regression suite | 344 | 344 | 0 | 0 | 0 |

Все три финальных запуска — exit code 0. Полный suite: Core 37, Core1 77, Core2 169, Time 53, Time/Eca 8. До них первый запуск с namespace filter захватил обе Time assemblies: 59 passed / 2 failed из 61 из-за ожидания заголовка AggregateException вместо первой строки inner exception в Unity log. Исправлены только LogAssert expectations; для отдельных focused runs затем использован assemblyNames.

Сохранённые allocation cases показывают 0 managed bytes за 100 steady-state ticks после прогрева при прежнем/меньшем числе timers/systems. Due buffers и cached ticker invocation list переиспользуются; изменение подписок, capacity growth и ошибки находятся вне этого steady-state утверждения. Ticker subscriber exceptions в error tests ожидаемо логируются; остальные tests не генерируют неожиданных ошибок. В logs запусков этой итерации compiler warnings не обнаружены.

Результаты/logs в игнорируемой .validation~: time-adapter-core-results.xml / time-adapter-core.log (первая попытка), time-adapter-core-final-results.xml / time-adapter-core-final.log, time-adapter-eca-results.xml / time-adapter-eca.log, time-adapter-full-results.xml / time-adapter-full.log. PlayMode/Sandbox, IL2CPP/player build и удалённый CI в этой итерации не запускались. Core/Core1/Core2 production и tests не менялись.
## Standalone TimeSystem Core v1 — 2026-09-17

Новая изолированная assembly `EcaSystems.Time.Editor.Tests` в `Tests/Editor/Systems/Time` references только `EcaSystems.Time` и TestAssemblies. Runtime assembly использует UnityEngine и не references ECA assemblies. Добавлены **42 NUnit cases**, существующие 283 cases не менялись.

Покрыты creation/lookup/Ordinal IDs/instance isolation, null/invalid IDs/options/duration/scale, все разрешённые и запрещённые lifecycle transitions, event order/count, timing values, pause/resume/reset-on-stop, restart-after-completion, destroy из всех live states, terminal old instance и ID reuse из callback. Дополнительно проверены double precision/overshoot, independent Scaled/Unscaled clocks, zero-duration, reentrant start/destroy/replacement, callback errors без потери остальных timers, повторные Wait и Awaitable continuation, отсутствие Wait в public registry.

Deterministic seam — internal constructor с time reader/active-runner callback и internal Tick(nowScaled, nowUnscaled), доступные через InternalsVisibleTo только Time test assembly. Нет sleeps, public clock/Tick или ECA test dependencies. Awaitable потребляется один раз; test-only Task используется лишь для наблюдения окончания async continuation. PlayerLoop tests работают с реальным GetCurrentPlayerLoop/SetPlayerLoop, проверяют повторную установку одного hook и исполняют установленный delegate с двумя public TimeSystem instances; исходный loop восстанавливается в TearDown.

Unity **6000.5.6f1**, два локальных batchmode EditMode runs:

| Run | Total | Passed | Failed | Skipped | Inconclusive |
| --- | ---: | ---: | ---: | ---: | ---: |
| Time focused | 42 | 42 | 0 | 0 | 0 |
| Полный regression suite | 325 | 325 | 0 | 0 | 0 |

Полный run: Core 37, Core1 77, Core2 169, Time 42. Оба запуска завершились с exit code 0. Новых compiler warnings нет; при первой компиляции остался прежний CS0108 в Core2 EcaScopeTests.Fire(int), вне scope. XML/log сохранены в игнорируемой `.validation~`: `time-v1-focused-results.xml` / `time-v1-focused.log`, `time-v1-full-results.xml` / `time-v1-full.log`.

Это EditMode compilation/runtime verification, не PlayMode scene/frame run и не IL2CPP/player build. Scene independence следует из отсутствия scene objects/dependencies; смена сцен отдельным тестом не запускалась. Удалённый GameCI не запускался. API Unity сверены с [PlayerLoop](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/LowLevel.PlayerLoop.html) и [AwaitableCompletionSource](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AwaitableCompletionSource.html).

## PR #16: устранение tick allocations — 2026-09-17

TimeSystemPlayerLoop.Update и TimerRunner.Tick переиспользуют snapshot lists из внутренних Stack pools. Capacity растёт только при необходимости; вложенный tick получает отдельный свободный buffer. В finally списки очищаются от references и возвращаются в pool, включая exception path. Snapshot до callbacks, проверка Active.Contains, пары (Timer, Version), restart/mutation protection и aggregation ошибок сохранены. Public API, lifecycle, Wait и архитектура не менялись.

42 прежних Time cases сохранены без изменений; добавлены 4 focused tests в TimeSystemTests/TimePlayerLoopTests: вложенные Tick/Update и allocation checks при прежнем/меньшем числе объектов. После 10 warm-up вызовов GC.GetAllocatedBytesForCurrentThread показывает **0 bytes** за 100 вызовов в каждом измеренном блоке: 16 timers и затем 1; 8 runners и затем 1. Setup, создание объектов, installation и NUnit assertions находятся вне измеряемого участка. Это проверка steady-state пути без callbacks/errors; allocation при capacity growth, первой новой глубине вложенности и обработке exceptions допустима.

Unity **6000.5.6f1** batchmode EditMode: Time **46/46 passed**, полный suite **329/329 passed** (Core 37, Core1 77, Core2 169, Time 46). Оба запуска: 0 failed/skipped/inconclusive, exit code 0. XML/log в игнорируемой `.validation~` с префиксами `time-allocation-focused` и `time-allocation-full`. GitHub Actions отдельно не проверялся. Из документации изменён только Testing.md.

## Структура

- `Tests/Editor/Base/` — создание Rule, registry/selector, payload, роли контекстов и Fire.
- `Tests/Editor/Commands/` — регистрация, bind, аргументы, ошибки, разрешение ID при каждом вызове.
- `Tests/Editor/Execution/` — overlap, limits, состояния, счётчики, unregister и rollback.
- `Tests/Editor/Scope/` — идентификаторы, иерархия, lifetime и закрытые операции.
- `Tests/Editor/Integration/` — Execution + Commands и Scope + Execution + Commands.
- `Tests/Editor/Support/` — небольшие recording doubles и управляемые async Actions.
- `Tests/Editor/Systems/Time/` — standalone TimeSystem, отдельная `EcaSystems.Time.Editor.Tests` с reference только на `EcaSystems.Time` и TestAssemblies; нет ECA dependencies.

`EcaSystems.Editor.Tests.asmdef` ссылается на фактическую assembly `EcaSystems.Core`, включает только Editor и использует `optionalUnityReferences: ["TestAssemblies"]`.

Основная масса Core-сценариев — EditMode: `[TestFixture]`, `[Test]`, при необходимости `[TestCase]` и `async Task`. Controlled Actions завершаются явно; ограниченное ожидание продолжений Task не зависит от кадров. TearDown освобождает управляемые Actions и при падении проверки. Reflection используется для недоступного извне начального Pending, внутреннего Bind guard и проверки generic type contracts; production API для этого не расширяется.

PlayMode нужен для настоящих frame/scene/MonoBehaviour lifecycle scenarios и будущих Unity adapters. TimeSystem Core v1 проверяет timing детерминированно и PlayerLoop install/delegate в EditMode, без scene dependencies и heavyweight PlayMode test. Runtime test assembly и placeholder tests не создаются; они появятся с первым сценарием, которому реально нужны PlayMode/сцены.

## Локальный запуск

1. Открыть Unity-проект с установленным EcaSystems и Unity Test Framework. CI использует Unity **6000.3.19f1**; локально для checkpoint доступна **6000.5.6f1**.
2. В **проектном** `Packages/manifest.json` добавить `"testables": ["com.ecasystems.framework"]` рядом с `dependencies`. Не добавлять `testables` в package.json EcaSystems. Это отдельная настройка Sandbox; его файлы эта миграция не меняет.
3. Открыть **Window → General → Test Runner**, вкладку **EditMode**, выбрать `EcaSystems.Editor.Tests` и **Run All**.
4. Смотреть ошибки в Test Runner; результат можно экспортировать в XML.

Не требуется многократно запускать полный Unity suite после каждой правки. Для bootstrap достаточно проверки структуры/компиляции и максимум одного финального локального прогона, если среда готова. Старый .NET smoke harness удалён вместе с компонентом; отдельного дублирующего стека проверок больше нет.

## GitHub Actions

Workflow: [tests.yml](../.github/workflows/tests.yml), имя **Tests**, job **Unity EditMode**. Unity **6000.3.19f1**, `game-ci/unity-test-runner@v4`, Linux runner. CI — authoritative автоматическая проверка PR. Recovery-срез до checkpoint сообщает прошлый CI результат 31/31 passed; это не результат новой ветки.

Checkout выполняется в корень. Шаг Prepare package for GameCI копирует пакет в `_ci/EcaSystemsPackage`, исключая `.git`, `.github`, `_ci` и каталоги результатов. `packageMode: true` и `projectPath: _ci/EcaSystemsPackage` обходят ограничение package в корне checkout. GameCI создаёт временный Unity project. В package manifest не добавляются зависимости на тестовый framework или `testables`.

Сейчас один **EditMode** job, без матрицы и без PlayMode job. PlayMode tests отсутствуют; job следует добавить вместе с первым настоящим Unity lifecycle-сценарием, без placeholder tests.

Триггеры: `pull_request`, `push` только в `main`, `workflow_dispatch`. Обычный push feature-ветки сам не запускает CI: пользователь открывает PR вручную. Branch protection не меняется.

Workflow читает только имена secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`; значения не хранятся в repo и не выводятся. Personal license должна быть подготовлена владельцем репозитория. `GITHUB_TOKEN` используется для check results; permissions ограничены `contents: read`, `checks: write`. PR из fork не получает Unity secrets автоматически.

Результаты смотреть в PR checks (**EcaSystems EditMode Tests**) и **Actions → Tests → run → Artifacts**: `unity-editmode-results` содержит test results и logs из `artifacts/EditMode`. Upload выполняется с `if: always()`, включая неуспешный запуск; если Unity не смог создать файлы, upload сообщит об их отсутствии. Generated artifacts не коммитятся.

Input `coverageEnabled` в текущем workflow **не задан**. Code Coverage package не добавлен в repository/package manifest, но это не гарантирует отключение coverage во временном проекте GameCI. По recovery-срезу прошлая попытка `coverageEnabled: false` породила несовместимый CLI flag `--no-coverageEnabled`, после чего input убрали; прошлый CI сообщил 31/31 passed вместе с ошибкой генерации coverage report. Workflow в checkpoint не менялся. Coverage, performance tests и optional required checks/branch protection остаются отдельными улучшениями.

## История bootstrap-миграции

Все 22 исходных сценария и internal binding guard перенесены; с дополнительными проверками — 31 NUnit case. Отдельная компиляция Core и tests с C# 9 и установленной Unity NUnit assembly прошла без ошибок/предупреждений; один дополнительный NUnit run вне Unity: 31 passed, 0 failed. Это не замена Unity CI.

Во время bootstrap локальная попытка Unity 6000.5.6f1 обнаружила два обращения к internal constructor из test assembly и остановилась до выполнения tests. Они исправлены посредством reflection; тогда повторный Unity run не выполнялся. Более поздний recovery-срез сообщает CI 31/31 passed на 6000.3.19f1.

Настройка основана на [GameCI Test runner v4](https://game.ci/docs/github/test-runner/) и [входных параметрах action](https://github.com/game-ci/unity-test-runner/blob/v4/action.yml).

## Проверки architecture checkpoint — 2026-09-11

- Раздельная компиляция Core и Editor tests с C# 9 через локальный .NET SDK: успешно. Использован существующий игнорируемый проект .validation~, новый тестовый стек в репозиторий не добавлялся.
- NUnit вне Unity с установленной Unity NUnit assembly: **37 passed, 0 failed, 0 skipped**.
- Один локальный Unity **6000.5.6f1** batchmode EditMode run в подготовленном .validation~/UnityProject: **37 passed, 0 failed, 0 skipped**, результат XML — Passed. Это другая версия и среда, чем Linux/GameCI CI на 6000.3.19f1.
- Сохранены 31 прежний case; добавлены 6 cases для общего живого Execution state, наследования/variance и разделения ролей, ScopeState lifetime/повторного ID и валидации ID.
- ScopeState внутри runtime-контекстов не тестируется как реализованная возможность: wiring отсутствует и явно отложен в Context/ToDo. Существующие integration tests продолжают проверять локальный Fire, независимость GroupState/Limit и Dispose без отмены Actions.

В этой ветке PlayMode и удалённый GameCI не запускались. Push feature-ветки не запускает workflow автоматически; PR создаёт пользователь.

## Core2 Execution — 2026-09-14

Core2 tests находятся в `Tests/Editor/Core2`, assembly `EcaSystems.Core2.Editor.Tests` с reference на `EcaSystems.Core2`. Используется существующий Unity/NUnit путь, без нового внешнего .NET test project.

Добавлен 61 NUnit case: mode validation, live State/counters, Check без admission, fresh RuleState для фаз, Pending/Running/Completed/Failed, синхронные и отложенные ошибки/null Task/createState, overlap и lifetime Limit, typed GroupRegistry, rollback, unregister/re-registration, ALL CONDITIONS → ALL EXECUTIONS, immediate/reentrant Fire, поздний Ignore/Limit admission, snapshot выбранных Groups и Base Fire без lifecycle. Тесты используют управляемые Tasks с ограниченным ожиданием и освобождением в TearDown.

Локальный полный Unity 6000.5.6f1 batchmode EditMode run: **202 passed, 0 failed, 0 skipped** (Core — 37, Core1 — 77, Core2 — 88: 27 Base + 61 Execution). Core2 компилируется в составе package с прежним asmdef и `noEngineReferences`. Первая попытка остановилась до тестов на двух конфликтах имени тестового Action с System.Action; после уточнения имён повторный полный запуск прошёл. XML и logs хранятся в игнорируемой `.validation~`, в коммит не включены.

PlayMode scenarios отсутствуют; удалённый GameCI в рамках этой итерации не запускался. Старые Core/Core1 и Core2/Base tests сохранены без изменений.

## Core2 Scope — 2026-09-15

Добавлен 21 NUnit case в `Tests/Editor/Core2/EcaScopeTests.cs`: ScopeId/state validation, default Register и typed ExecutionGroup, расширение готового IEcaExecutionRuleState через Func, пользовательский rich State/contexts, свежие состояния фаз, общий ScopeState, barrier/selection, overlap/limit, изоляция независимых ExecutionRuntime с общей Event/Rule declaration, direct reentrant Fire, Unregister активного запуска, ошибки extension и Base Fire с caller state.

Полный локальный Unity 6000.5.6f1 batchmode EditMode suite: **223 passed, 0 failed, 0 skipped**. Core — 37, Core1 — 77, Core2 — 109 (27 Base + 61 Execution + 21 Scope). Компиляция package успешна с существующим Core2 asmdef и noEngineReferences; все 202 прежних теста сохранены. XML/log: `.validation~/core2-scope-unity-results.xml` и `.validation~/core2-scope-unity.log`, не коммитятся. PlayMode scenarios отсутствуют; удалённый GameCI в этой итерации не запускался.

## Core2 Scope manager/lifetime — PR #10, 2026-09-15

21 прежний Scope case адаптирован к `EcaScope` и `EcaScopeRuntime(IEcaEventRegistry)`. Сохранены state-enrichment, custom R/contexts, default Register, barrier, reentrancy, Base-level Fire и Unregister. Добавлены 13 cases в EcaScopeRuntimeTests: manager count/lookup, auto-id/duplicates, parent/child/grandchild hierarchy, recursive/idempotent Dispose, guards всех per-scope операций, reuse/stale identity, строго локальное выполнение обоих overloads Fire, shared Rule с независимыми registries/limits/overlap, live EventRegistry, завершение active Actions после Scope/parent/manager Dispose без влияния на replacement.

Полный Unity 6000.5.6f1 batchmode EditMode suite: **236 passed, 0 failed, 0 skipped**. Core — 37, Core1 — 77, Core2 — 122 (27 Base + 61 Execution + 34 Scope). Компиляция package успешна, Base/Execution/Core/Core1 не изменены. Результаты и log — `.validation~/core2-scope-manager-unity-results.xml` / `.validation~/core2-scope-manager-unity.log`; артефакты не коммитятся. PlayMode отсутствует, удалённый GameCI отдельно не запускался.

## Core2 Commands validation — 2026-09-15

Добавлены 28 NUnit cases в EcaCommandRegistryTests, EcaCommandRunnerTests и EcaCommandsBaseRuntimeTests: registry/lookup, live registration, Bind identity/independence, typed context/args и null, async Tasks и ошибки, end-to-end Action → Command, Conditions/barrier и отдельные Base Fire calls. Тестовый composition context реализует IEcaCommandsActionContext; production Commands, Base, Execution, Scope и старые Core/Core1 не менялись.

Два локальных Unity 6000.5.6f1 batchmode EditMode запуска завершились успешно: focused Commands — **28 passed, 0 failed, 0 skipped**; полный suite — **264 passed, 0 failed, 0 skipped, 0 inconclusive**. Полный результат по assemblies: Core — 37, Core1 — 77, Core2 — 150 (27 Base + 61 Execution + 34 Scope + 28 Commands). XML/log: `.validation~/core2-commands-focused-results.xml`, `.validation~/core2-commands-focused.log`, `.validation~/core2-commands-full-results.xml`, `.validation~/core2-commands-full.log`; артефакты не коммитятся. PlayMode scenarios отсутствуют; удалённый GameCI не запускался.

## PR #12: предыдущие проверки — 2026-09-15

До упрощения transport выполнены Unity 6000.5.6f1 EditMode runs: первоначальные concept tests 22/22, Core2 172/172, полный suite 286/286; после ограничения public factory — concept 25/25, Core2 175/175, полный suite 289/289. Все без failures/skips. Эти transport tests заменены проверками прямого Emitter ниже; прежние XML/log сохранены в игнорируемой `.validation~`.

## PR #12 EventEmitter — 2026-09-15

Старые transport tests заменены 11 standalone Emitter cases; 5 Base integration cases адаптированы к прямому Fire<E> через IEcaEventEmitter. Удалён отдельный lifecycle case снятия подписки, поскольку Unbind/Dispose в новой модели не предусмотрены. Всего 16 concept cases вместо 25: binding/null/duplicate/unbound, registry contract и live validation, несовместимая metadata, ошибки handler, три unrelated E, declared BaseState с DerivedState/null, class/struct values, Base Conditions/barrier и reentrant Fire. Все остальные 150 Core2 cases сохранены. Internal Bind тестируется через InternalsVisibleTo только для EcaSystems.Core2.Editor.Tests, публичный порт содержит только Fire<E>.

Unity 6000.5.6f1, три batchmode EditMode запуска: EventEmitter — **16 passed**, все Core2 — **166 passed**, полный suite — **280 passed** (Core — 37, Core1 — 77, Core2 — 166). Везде **0 failed, 0 skipped, 0 inconclusive**, exit code 0. XML/log: `.validation~/pr12-emitter-focused-results.xml` / `pr12-emitter-focused.log`, `pr12-emitter-core2-results.xml` / `pr12-emitter-core2.log`, `pr12-emitter-full-results.xml` / `pr12-emitter-full.log`; артефакты не коммитятся. PlayMode и удалённый GameCI не запускались. Base/Execution/Scope/Commands/Core/Core1 не изменены.

## PR #13: accessibility EventEmitter — 2026-09-15

Public оставлен только IEcaEventEmitter; EcaEventEmitter стал internal sealed, IEcaEventHandler — internal. Bind остался internal. Поведение и tests не менялись; существующий InternalsVisibleTo обеспечивает доступ тестовой композиции.

Unity 6000.5.6f1 batchmode EditMode: EventEmitter **16/16 passed**, Core2 **166/166 passed**, полный suite **280/280 passed** (Core — 37, Core1 — 77, Core2 — 166). Во всех трёх запусках 0 failed/skipped/inconclusive, exit code 0. XML/log сохранены в игнорируемой `.validation~` с префиксами `pr13-accessibility-focused`, `pr13-accessibility-core2`, `pr13-accessibility-full`. Commands/Base/Execution/Scope/Core/Core1 не изменены. PlayMode и удалённый GameCI не запускались.

## Core2 Command abstraction — 2026-09-17

Добавлены 3 cases: non-generic registration с сохранением Command instance, runtime metadata/default erased bridge с derived context/args и исходным Task, heterogeneous IEcaCommand[] с разными C/A и регистрацией одним foreach. Один прежний null-registration case адаптирован к Register(IEcaCommand); остальные behavioural tests сохранены, concrete test Commands не получили metadata/casts/bridge boilerplate.

Default interface implementation IEcaCommand<C,A> реально скомпилирована и выполнена в Unity 6000.5.6f1. Три batchmode EditMode запуска: Commands **31/31 passed**, все Core2 **169/169 passed**, полный suite **283/283 passed** (Core — 37, Core1 — 77, Core2 — 169). Везде 0 failed/skipped/inconclusive, exit code 0. Новых compiler warnings нет; остаётся прежний CS0108 в EcaScopeTests.Fire(int), вне scope задачи. Проверка не включает IL2CPP/player build; PlayMode и удалённый GameCI не запускались.

XML/log сохранены в игнорируемой `.validation~` с префиксами `command-abstraction-focused`, `command-abstraction-core2`, `command-abstraction-full`. Существующие `.meta` сохранены, GUID уникальны. Base/Execution/Scope/EventEmitter/Core/Core1 не изменены; Systems не реализованы.

## Core2 Systems v1 / AEcaCommand — 2026-09-17

Новых Systems tests и новых test cases не добавлено по согласованному scope. CommandTestSupport переведён на AEcaCommand<C,A> с override Id/Run, существующие Commands registry tests используют AEcaCommand и новый test-only CommandId для задания Id. Единственный test EventRegistry адаптирован к расширенному interface. Semantic assertions сохранены.

Unity 6000.5.6f1 batchmode EditMode: существующий Core2 suite **169/169 passed**; полный suite после финальной правки Connector **283/283 passed** (Core — 37, Core1 — 77, Core2 — 169). Везде 0 failed/skipped/inconclusive, exit code 0. Systems production code скомпилирован, но отдельных runtime tests Attach/Detach/rollback в этой итерации нет; они проверены review кода, не тестовыми сценариями. Новых compiler warnings нет; прежний CS0108 в Scope tests сохранён.

XML/log: `.validation~/systems-v1-core2-results.xml` / `systems-v1-core2.log`, `systems-v1-full-results.xml` / `systems-v1-full.log`; не коммитятся. IL2CPP/player build, PlayMode и удалённый GameCI не запускались. Существующий Command .meta/GUID сохранён при rename; новые Systems metadata добавлены, GUID уникальны.
