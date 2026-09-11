# Тестирование EcaSystems

Тесты используют Unity Test Framework + NUnit. Production Runtime не содержит test-only кода. Карта всех 22 сценариев бывшего `EcaSystemsSmokeTest` находится в [TestMigration.md](TestMigration.md); дополнительные проверки покрывают валидацию Rule, Pending, расход Limit при ошибке и внутреннюю защиту Bind из старого .NET harness.

## Структура

- `Tests/Editor/Base/` — создание Rule, registry/selector, payload, роли контекстов и Fire.
- `Tests/Editor/Commands/` — регистрация, bind, аргументы, ошибки, разрешение ID при каждом вызове.
- `Tests/Editor/Execution/` — overlap, limits, состояния, счётчики, unregister и rollback.
- `Tests/Editor/Scope/` — идентификаторы, иерархия, lifetime и закрытые операции.
- `Tests/Editor/Integration/` — Execution + Commands и Scope + Execution + Commands.
- `Tests/Editor/Support/` — небольшие recording doubles и управляемые async Actions.

`EcaSystems.Editor.Tests.asmdef` ссылается на фактическую assembly `EcaSystems.Core`, включает только Editor и использует `optionalUnityReferences: ["TestAssemblies"]`.

Основная масса Core-сценариев — EditMode: `[TestFixture]`, `[Test]`, при необходимости `[TestCase]` и `async Task`. Controlled Actions завершаются явно; ограниченное ожидание продолжений Task не зависит от кадров. TearDown освобождает управляемые Actions и при падении проверки. Reflection используется только для недоступного извне начального Pending и внутреннего Bind guard; production API для этого не расширяется.

PlayMode нужен для PlayerLoop, frames/coroutines, MonoBehaviour lifecycle, scenes и будущих Unity adapters/TimeSystem/Wait. Сейчас таких сценариев нет, поэтому Runtime test assembly и placeholder tests не создаются. С первым настоящим lifecycle-тестом добавятся `Tests/Runtime/Unity/` и `EcaSystems.Runtime.Tests.asmdef` с runtime reference и `TestAssemblies`.

## Локальный запуск

1. Открыть Unity-проект с установленным EcaSystems и Unity Test Framework. CI использует Unity **6000.5.6f1**.
2. В **проектном** `Packages/manifest.json` добавить `"testables": ["com.ecasystems.framework"]` рядом с `dependencies`. Не добавлять `testables` в package.json EcaSystems. Это отдельная настройка Sandbox; его файлы эта миграция не меняет.
3. Открыть **Window → General → Test Runner**, вкладку **EditMode**, выбрать `EcaSystems.Editor.Tests` и **Run All**.
4. Смотреть ошибки в Test Runner; результат можно экспортировать в XML.

Не требуется многократно запускать полный Unity suite после каждой правки. Для bootstrap достаточно проверки структуры/компиляции и максимум одного финального локального прогона, если среда готова. Старый .NET smoke harness удалён вместе с компонентом; отдельного дублирующего стека проверок больше нет.

## GitHub Actions

Workflow: [tests.yml](../.github/workflows/tests.yml). Unity **6000.5.6f1**, лицензия **Personal**, `game-ci/unity-test-runner@v4`, Linux runner. CI — authoritative автоматическая проверка PR; наличие workflow не означает, что первый CI run уже прошёл.

Checkout выполняется в `EcaSystemsPackage`. `packageMode: true` и `projectPath: EcaSystemsPackage` обходят ограничение GameCI для package в корне checkout. GameCI создаёт временный Unity project и включает проверяемый package в его manifest/testables. В package manifest не добавляются зависимости на тестовый framework или `testables`.

Матрица: **EditMode + PlayMode**, `fail-fast: false`. PlayMode пока не содержит тестов; это не доказательство lifecycle coverage. UTF 1.7.0 возвращает успешный код для пустого suite. Если версия UTF в GameCI обработает пустой suite иначе, временно оставить только EditMode и вернуть PlayMode с первым настоящим тестом, без fake test.

Триггеры: `pull_request`, `push` только в `main`, `workflow_dispatch`. Обычный push feature-ветки сам не запускает CI: пользователь открывает PR вручную. Branch protection не меняется.

Workflow читает только имена secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`; значения не хранятся в repo и не выводятся. Personal license должна быть подготовлена владельцем репозитория. `GITHUB_TOKEN` используется для check results; permissions ограничены `contents: read`, `checks: write`. PR из fork не получает Unity secrets автоматически.

Результаты смотреть в PR checks и **Actions → Unity tests → run → Artifacts**: `unity-EditMode-results`, `unity-PlayMode-results` содержат test results и logs. Upload выполняется с `if: always()`, включая неуспешный запуск; если Unity не смог создать файлы, upload сообщит об их отсутствии. Generated artifacts не коммитятся.

Coverage instrumentation выключен (`coverageEnabled: false`), Code Coverage package не добавлен в repository/package manifest. Ограничение upstream: текущий [CLI-скрипт packageMode](https://github.com/game-ci/cli/blob/main/dist/platforms/ubuntu/steps/test.sh) всё равно добавляет `com.unity.testtools.codecoverage` во временный project manifest, независимо от этого флага. Поэтому запрет установки пакета внутри GameCI полностью не обеспечивается штатным input; собственный fork/patch GameCI эта итерация не вводит. Coverage, performance tests и optional required checks/branch protection — отдельные будущие улучшения.

## Проверка bootstrap-миграции

Все 22 исходных сценария и internal binding guard перенесены; с дополнительными проверками — 31 NUnit case. Отдельная компиляция Core и tests с C# 9 и установленной Unity NUnit assembly прошла без ошибок/предупреждений; один дополнительный NUnit run вне Unity: 31 passed, 0 failed. Это не замена Unity CI.

Единственная локальная попытка Unity 6000.5.6f1 обнаружила два обращения к internal constructor из test assembly и остановилась до выполнения tests. Они исправлены посредством reflection; после этого прошла проверка раздельной компиляции и NUnit вне Unity. Повторный Unity run намеренно не выполнялся. Окончательные discovery/run в GameCI подтвердит PR; первый CI run пока не заявляется зелёным.

Настройка основана на [GameCI Test runner v4](https://game.ci/docs/github/test-runner/) и [входных параметрах action](https://github.com/game-ci/unity-test-runner/blob/v4/action.yml).
