# VariablesSystem — Roadmap

Будущие возможности VariablesSystem, которые не входят в ближайшие обязательные итерации.

## Store tree evolution

### Reparent / изменение ParentId

В ближайшей Store-модели ParentId immutable после создания.

Позже рассмотреть явную операцию изменения parent, например MoveStore / ReparentStore.

При проектировании учесть:
- запрет циклов;
- перенос Store вместе с descendants;
- missing target parent;
- события изменения Store;
- Save/Load и persistence identity;
- влияние на templates/copies.

### Copy Store

Добавить явное копирование Store.

Отдельно решить:
- копируются только local variables или также descendants;
- копируются ли Definition/Default/Current/Old;
- создаются ли новые runtime EcaVariable instances;
- новый StoreId обязателен;
- что происходит с ParentId;
- испускаются ли events при создании копии.

### Store Templates

Добавить возможность создать template из Store и затем создавать независимые concrete Stores/groups из template.

Направление:
- Store -> template snapshot/definition;
- template не является live Store;
- из одного template можно создать множество Stores;
- concrete Store получает собственный StoreId и runtime variables;
- отдельно решить, включает ли template Current/Old или только Definitions/defaults;
- отдельно решить template subtree.

Template не должен быть live inheritance от исходного Store.

## Parent lookup / inheritance / override

Store tree в ближайшей реализации означает только ownership.

В будущем отдельно рассмотреть resolved lookup:

local -> parent -> parent.parent -> ...

Если эта возможность появится, обычный GetValue предпочтительно оставить local lookup, а hierarchical lookup сделать явным API, например GetResolvedValue / TryGetResolvedValue.

Отдельно решить mutation inherited variable:
- mutate parent;
- создать local override;
- reject mutation unless variable local.

Не реализовывать inheritance/override без отдельного архитектурного решения.

## Store events

Позже спроектировать:
- создание/удаление/перемещение Store;
- aggregate VariableChanged с source StoreId;
- bubbling/propagation только при реальном use case.

## History

В будущем OldValue может быть заменён или дополнен History.

## Enum / расширение типов

Enum и другие value types обсуждать вместе с persistence/SaveLoad contract.

## Reactive features

Reactive/computed/watch/collections не относятся к ближайшему VariablesSystem MVP.
