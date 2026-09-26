# TimeSystem — локальный ToDo

Текущая реализация описана в [Context](Context.md). Здесь product tasks Time, не общий Core2 план.

- [x] Standalone instance timers, scaled/unscaled clocks, PlayerLoop integration, aggregate lifecycle events.
- [x] TimerController/TimerTickProcessor/TimerRegistry и отдельный Wait subsystem с Awaitable.
- [x] Start после completion, Stop reset, Pause/Resume, Destroy и reuse ID.
- [x] Version protection, reentrant reusable buffers и callback error handling.
- [x] Time/Eca adapter: 6 Events, 7 Commands, explicit caller-owned Connect/Disconnect.
- [x] Read-only membership GetSnapshot у TimerRegistry/WaitRegistry и focused tests.

Следующие кандидаты требуют отдельного сценария/согласования; в этой итерации не реализуются:

- [ ] Repeat/looping timers; отдельные restart/reset convenience только если существующих Stop/Start недостаточно.
- [ ] Wait cancellation и cleanup/lifetime contract.
- [ ] Groups/tags/bulk operations.
- [ ] Custom/local scales и time channels; отдельно оценить ScaleMode responsibilities без заранее выбранного provider API.
- [ ] Дополнительные PlayerLoop phases и frame/conditional waits.
- [ ] Debug/editor tooling.

Persistence и варианты дальнейшей интеграции — [Roadmap](Roadmap.md).
