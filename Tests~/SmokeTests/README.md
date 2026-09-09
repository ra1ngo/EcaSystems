# Запуск smoke-тестов без редактора

Из корня репозитория: `dotnet run --project Tests~/SmokeTests/SmokeTests.csproj`.
Нужен .NET SDK 10. При наличии нескольких установок используйте путь к SDK версии x64.

Проект компилирует актуальные исходники Core и `Runtime/Unity/EcaSystemsSmokeTest.cs`
с языком C# 9. Ошибки тестов дают ненулевой код выхода. Заглушки заменяют только
MonoBehaviour, логирование и ожидание следующего кадра. Этот запуск проверяет
Base/Commands/Execution/Scope, но не заменяет проверку жизненного цикла Unity.
В Unity те же проверки запускаются компонентом EcaSystemsSmokeTest при Start.
