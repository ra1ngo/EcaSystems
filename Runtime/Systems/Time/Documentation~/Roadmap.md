# TimeSystem — Roadmap

Это future ideas, не обещание API. Актуальное поведение — [Context](Context.md), приоритетные кандидаты — [ToDo](ToDo.md).

- Richer timer modes: repeat/looping, catch-up policy после большого delta, удобство restart/reset поверх текущих Start/Stop. Сначала определить callback/reentrancy semantics.
- Cancellation: Wait completion/cancel races, cleanup подписки, поведение Awaitable → Task и ownership pending work. Сейчас public cancellation/Dispose у TimeSystem нет.
- Custom scale/time channels: локальные clocks и более узкие зависимости Controller/processors. ITimeSource, resolver/provider и TimeSnapshot пока не выбраны; не переосмысливать ScaleMode без отдельной итерации.
- Additional PlayerLoop phases, conditional/frame waits, groups/tags/bulk operations — по реальным сценариям.
- Persistence/save: определить remaining-time vs absolute clock representation, восстановление IDs/state и paused timers; сохранение Awaitable/continuation нельзя считать сериализацией произвольного исполнения.
- Diagnostics/tooling: инспекция registered/running timers и pending waits, elapsed/progress, callback errors; snapshots membership не заменяют immutable state report.
- Внешний lifecycle/System integration — только будущее требование. Caller пока явно связывает exports, adapter и выбранный emitter; automatic Scope bindings и ownership transfer не реализованы.
- Возможный отдельный package/repo с сохранением независимости Time/Core от Core2; Unity dependency PlayerLoop/Awaitable является текущей осознанной границей.
