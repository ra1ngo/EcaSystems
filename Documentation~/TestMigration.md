# Карта миграции smoke tests

Все 22 исходных сценария перенесены. Base/Commands/Execution/Scope fixtures проверяют соответствующий слой; Integration — композицию слоёв. Все тесты EditMode. Условия и сообщения исходных проверок сохранены, ошибки теперь останавливают тест через NUnit.

| Исходный метод | NUnit fixture / test | Уровень |
| --- | --- | --- |
| `TestBaseEmptyContext` | `EcaEngineTests.Fire_EmptyContext_RunsUntilUnregistered` | unit / layer |
| `TestBaseEventContext` | `EcaEngineTests.Fire_PassesEventContextToAction` | unit / layer |
| `TestBaseConditionFalse` | `EcaEngineTests.Fire_FalseCondition_PreventsAction` | unit / layer |
| `TestBaseChecksAllConditionsBeforeActions` | `EcaEngineTests.Fire_ChecksAllConditionsBeforeAnyAction` | unit / layer |
| `TestDuplicateRuleId` | `EcaEngineTests.Register_DuplicateRuleId_Throws` | unit / layer |
| `TestBaseContextSplit` | `EcaEngineTests.Fire_SeparatesConditionAndActionContexts` | unit / layer |
| `TestGenericRegistryAndVariance` | `EcaRuleRegistryTests.Registry_CustomContexts_PreservesExactRolesAndVariance` | unit / layer |
| `TestExecutionIgnore` | `EcaExecutionEngineTests.Fire_OverlapIgnore_TracksOnlyAcceptedExecutions` | unit / layer |
| `TestExecutionAllow` | `EcaExecutionEngineTests.Fire_OverlapAllow_SharesLiveStateAcrossConcurrentExecutions` | unit / layer |
| `TestExecutionFailedAction` | `EcaExecutionEngineTests.Fire_ThrowingAction_BalancesCountersAndRemovesExecution` | unit / layer |
| `TestExecutionConditionOrder` | `EcaExecutionEngineTests.Fire_ChecksAllConditionsBeforeAnyAction` | unit / layer |
| `TestExecutionUnregister` | `EcaExecutionEngineTests.Unregister_RunningAction_SurvivesIndependentReplacement` | unit / layer |
| `TestRunModes` | `EcaRuleExecutionGroupTests.Fire_Limits_CountOnlyActualStarts` | unit / layer |
| `TestFailureStatus` | `EcaRuleExecutionGroupTests.Run_FaultedAction_RecordsOriginalFailure` | unit / layer |
| `TestRegistryValidation` | `EcaRuleExecutionGroupTests.Register_ValidatesGroupsAndRollsBackRuleOnFailure` | unit / layer |
| `TestScopeLifetime` | `EcaScopeTests.Dispose_ManagesHierarchyIdentityAndClosedOperations` | unit / layer |
| `TestScopeIsolation` | `EcaScopeExecutionTests.Fire_SharedRule_KeepsScopeStateLimitsAndRegistriesIndependent` | integration |
| `TestScopeLocalFire` | `EcaScopeExecutionTests.Fire_RemainsLocalToParentOrChild` | integration |
| `TestScopeRunningDispose` | `EcaScopeExecutionTests.Dispose_RunningAction_CompletesWithoutAffectingReplacement` | integration |
| `TestCommandsRegistry` | `EcaCommandRegistryTests.Run_BoundCommands_ValidateAndResolveAgainstCurrentRegistry` | unit / layer |
| `TestBindAcceptance` | `EcaCommandExecutionTests.Fire_BindsExactlyOncePerAcceptedExecution` | integration |
| `TestCommandsOrderAndScopes` | `EcaCommandExecutionTests.Fire_ConditionsPrecedeBindAndSequentialCommandsAcrossScopes` | integration |

Дополнительно перенесён `Tests~/SmokeTests/Program.cs: TestInternalBinding` → `EcaCommandBindingTests.BindCommands_RejectsNullAndRebindingWithoutReplacingCommands` (Commands, unit).

Добавлены проверки создания/валидации Rule, совпадения Event.Id у разных объектов, Pending и Failed + Limit. У двух исходных проверок допустимых null/nullable args вместо безусловного PASS теперь проверяются записанные аргументы. Полезного production/sample поведения в smoke-компоненте не было.
