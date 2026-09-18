# Первый adapter: TimeSystem и ECA

Мы намеренно сначала реализовали standalone TimeSystem без ECA dependencies. Попытка подключить первую реальную внешнюю System выявила слабости standalone observation/lifecycle: per-Timer events заставляли бы adapter следить за каждым созданием и владеть множеством подписок.

Это полезный результат mental test. Adapter не потребовал протащить ECA внутрь Time/Core. Вместо этого эксперимент показал улучшения, полезные любому standalone caller: aggregate lifecycle observation на TimeSystem, Timer как данные, отдельные lifecycle controller и tick processor, один TimerRegistry как источник истины. TimeTicker отделяет Unity clock/PlayerLoop от экземпляров TimeSystem; подписка существует только при tickable work. Двухфазная обработка сначала обновляет registry data, затем исполняет due transitions без callback mutation во время enumeration. Wait использует отдельный небольшой subsystem.

Направления адаптации ясны: внешняя System → ECA через Events и IEcaEventEmitter; ECA → внешняя System через Commands. TimeSystem остаётся Unity-native и возвращает Awaitable; только Wait command переводит ожидание в Task. TimeEcaAdapter является живым bridge, EcaSystem — passive descriptor его exports. Шесть aggregate subscriptions заменяют слежение за каждым Timer.

ECA payload представляет immutable snapshot перехода, а не живой Timer. Immediate/reentrant handling может перезапустить таймер, но уже отправленный snapshot остаётся описанием исходного события.

## Порядок подключения

Emitter валидирует регистрацию Event. Пока caller обязан выполнять:

1. EcaSystemConnector.Attach(adapter.System).
2. adapter.Connect().
3. Использование Time/Eca.
4. adapter.Disconnect().
5. EcaSystemConnector.Detach(adapter.System).

Connect не может универсально проверить Attach через один IEcaEventEmitter. Ошибка порядка обнаруживается при Fire. Connector не должен внезапно владеть произвольными внешними subscriptions; общее решение ownership/composition отложено до следующей production composition стадии. Disconnect снимает только external subscriptions, не останавливает standalone timers или Wait.

## Typed Event discoverability

EcaSystem.Events — non-generic export collection, а EventRegistry не предоставляет typed Resolve. TimeEcaEvents локально выдаёт шесть typed declarations, причём descriptor экспортирует те же instances. Это минимальное решение первого adapter, без изменений Core2. Общую discoverability модель стоит проверить при production composition.

Использованы stable IDs time.timer.started/stopped/paused/resumed/completed/destroyed и time.timer.create/start/stop/pause/resume/destroy, time.wait. Один descriptor относится к одному time namespace; совместная регистрация нескольких таких adapters требует отдельного будущего решения identity, automatic prefixing не добавлялся.

## Что не обобщаем пока

Один adapter ещё не обосновывает универсальный external-adapter framework. Сначала production Core2 composition, затем standalone Global State/Variables + ECA и небольшой end-to-end Sandbox/PlayMode scenario. После нескольких реальных adapters можно сравнить общие потребности.

State/Queries, Signals, FireEvent и routing — самостоятельные будущие concepts. Generic C# event/Observable/polling/UnityEvent/InputAction adapters, cancellation, Repeat, timer groups и другие Time features не входят в эту итерацию.
