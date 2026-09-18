# Первый adapter: TimeSystem и ECA

Мы намеренно сначала реализовали standalone TimeSystem без ECA dependencies. Попытка подключить первую реальную внешнюю System выявила слабости standalone observation/lifecycle: per-Timer events заставляли бы adapter следить за каждым созданием и владеть множеством подписок.

Это полезный результат mental test. Adapter не потребовал протащить ECA внутрь Time/Core. Вместо этого эксперимент показал улучшения, полезные любому standalone caller: aggregate lifecycle observation на TimeSystem, Timer как данные, отдельные lifecycle controller и tick processor, один TimerRegistry как источник истины. TimeTicker отделяет Unity clock/PlayerLoop от экземпляров TimeSystem; подписка существует только при tickable work. Двухфазная обработка сначала обновляет registry data, затем исполняет due transitions без callback mutation во время enumeration. Wait использует отдельный небольшой subsystem.

Направления адаптации ясны: внешняя System → ECA через Events и IEcaEventEmitter; ECA → внешняя System через Commands. TimeSystem остаётся Unity-native и возвращает Awaitable; только Wait command переводит ожидание в Task. TimeEcaAdapter является живым bridge, EcaSystem — passive descriptor его exports. Шесть aggregate subscriptions заменяют слежение за каждым Timer.

EcaTimeEventState представляет immutable snapshot перехода, а не живой Timer. Immediate/reentrant handling может перезапустить таймер, но уже отправленный snapshot остаётся описанием исходного события.

## Порядок подключения

Emitter валидирует регистрацию Event. Пока caller обязан выполнять:

1. EcaSystemConnector.Attach(system).
2. adapter.Connect().
3. Использование Time/Eca.
4. adapter.Disconnect().
5. EcaSystemConnector.Detach(system).

Connect не может универсально проверить Attach через один IEcaEventEmitter. Ошибка порядка обнаруживается при Fire. Connector не должен внезапно владеть произвольными внешними subscriptions; общее решение ownership/composition отложено до следующей production composition стадии. Disconnect снимает только external subscriptions, не останавливает standalone timers или Wait.

## Typed Event discoverability

После follow-up PR #17 отдельный typed catalog удалён. Simple Event/Command/System/Namespace registries предоставляют read-only live items, public Resolve и canonical CheckRegistered. EcaSystem хранит local Event/Command registries; Connector регистрирует exact instances в global registries. CheckRegistered проверяет same ID + ReferenceEquals, поэтому новая декларация с тем же ID/type не считается зарегистрированной.

TimeEcaSetup.CreateSystem(time) заполняет local registries. Caller создаёт TimeEcaAdapter(time, system.Events, emitter). Adapter один раз Resolve-ит шесть Events по своим ECA_EVENT_TIMER_*_ID constants, проверяет IEcaEvent<EcaTimeEventState> и metadata и кеширует references. Generic Event Register<E> удалён; typed validation принадлежит потребителю. TimeEcaEvents/TimeEcaCommandIds больше нет; семь отдельных Commands владеют своими IDs и TimeSystem. Это небольшой setup, не production composition framework.

Emitter остаётся typed instance-based Fire<E>(IEcaEvent<E>, E); Fire(string id, ...) не добавлен. RuleCreator/CreateRule<E>(eventId, ...) и общий typed lookup обсуждаются на следующем production-composition этапе. Generic Registry abstraction сейчас сознательно не вводится.

Использованы stable IDs time.timer.started/stopped/paused/resumed/completed/destroyed и time.timer.create/start/stop/pause/resume/destroy, time.wait. Один descriptor относится к одному time namespace; совместная регистрация нескольких таких adapters требует отдельного будущего решения identity, automatic prefixing не добавлялся.

## Что не обобщаем пока

Один adapter ещё не обосновывает универсальный external-adapter framework. Сначала production Core2 composition, затем standalone Global State/Variables + ECA и небольшой end-to-end Sandbox/PlayMode scenario. После нескольких реальных adapters можно сравнить общие потребности.

State/Queries, Signals, FireEvent и routing — самостоятельные будущие concepts. Generic C# event/Observable/polling/UnityEvent/InputAction adapters, cancellation, Repeat, timer groups и другие Time features не входят в эту итерацию.
