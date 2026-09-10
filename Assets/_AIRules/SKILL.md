---
name: proprety-database
description: Apply PropertyDatabase architecture, naming, placement, registration, Unity Create menu, and enum-key lookup rules when working with PropertyDatabase or PropertyDatabaseManager.
---

# PropertyDatabase Rules

## Scope

Use these rules for work involving `PropertyDatabase`, `PropertyDatabaseManager`, `EnumStringKeyedDatabase`, property SOs, property databases, or a request to use the PropertyDatabase logic. Do not apply them to unrelated SQL, save-data, or generic database tasks.

## Architecture

The architecture has two distinct lookup layers. Do not describe them as though an enum alone selects a database.

```text
Manager receives a Key_<Family>PP
  -> PropertyDatabaseManager.GetDatabase<<Family>Database>()
  -> <Family>Database.GetByEnum(key)
  -> <Family>Property SO
```

`PropertyDatabaseManager` is on the persistent initialization object (`AInitGameObject Variant`). Its serialized `allDatabases` array is the central registry. At `Awake`, it builds this map:

```text
Concrete Database type -> Database SO instance
```

For example:

```text
QuestDatabase -> QuestDB_Main
NPCDatabase -> NPCDB_Main
ItemDefinitionDatabase -> ItemDefinitionDB_Main
```

It does **not** map `Key_Quest` or any other enum directly to a database. A manager chooses the database type in code; that database then maps its own enum key to a property entry.

The currently registered database families are ItemDefinition, Sellable, Buildable, BuildBlueprint, StoreInventory, NPC, Quest, MapData, MapNode, NPCInteraction, and Test.

### Manager access pattern

Each project-level Manager may cache a typed database in a private runtime field, but it must resolve it through the central manager:

```csharp
private QuestDatabase questDatabase;

private void ResolveDatabases()
{
    PropertyDatabaseManager databaseManager = PropertyDatabaseManager.Instance;
    if (databaseManager == null)
        return;

    if (questDatabase == null)
        questDatabase = databaseManager.GetDatabase<QuestDatabase>();
}

private QuestProperty GetProperty(Key_Quest questKey)
{
    ResolveDatabases();
    return questDatabase != null ? questDatabase.GetByEnum(questKey) : null;
}
```

The cache is allowed; an Inspector reference to a specific Database SO is not. Resolve lazily and handle a missing central manager or database safely, as the existing Managers do.

Managers may use more than one database. For example, `QuestManager` resolves `QuestDatabase` for `Key_Quest` and `MapNodeDatabase` for `Key_MapNodePP` requirements.

## Types and names

For every **new** property family, use these names:

```text
Enum:            Key_<Family>PP
Property class:  <Family>Property
Database class:  <Family>Database
Database asset:  <Family>DB_main.asset
Property asset:  <Family>PP_<Identifier>.asset
```

Use the standard base types for a new ordinary family:

```csharp
public class FooProperty : EnumStringKeyedProperty<Key_FooPP> { }

public class FooDatabase
    : EnumStringKeyedDatabase<FooProperty, Key_FooPP> { }
```

Do not rename existing legacy keys merely to match the new naming convention. Existing examples such as `Key_Quest`, `Key_NPC`, and `Key_StoreInventory` remain valid identifiers.

## Unity Create menus and initial file names

Every new ordinary Property and Database must expose the following Unity Create menu paths:

```text
Create > AllProperties > <Family>Property
Create > AllPropertyDatabases > <Family>Database
```

Use these `CreateAssetMenu` declarations so Unity starts with the correct filename prefixes:

```csharp
[CreateAssetMenu(
    fileName = "<Family>PP_",
    menuName = "AllProperties/<Family>Property")]
public class FooProperty : EnumStringKeyedProperty<Key_FooPP> { }

[CreateAssetMenu(
    fileName = "<Family>DB_",
    menuName = "AllPropertyDatabases/<Family>Database")]
public class FooDatabase
    : EnumStringKeyedDatabase<FooProperty, Key_FooPP> { }
```

Create each Property asset from `Create > AllProperties`; its default filename prefix is `<Family>PP_`.

Create each Database asset from `Create > AllPropertyDatabases`; its default filename prefix is `<Family>DB_`. The first/primary Database asset for a family must be named `<Family>DB_main.asset` after creation.

Existing assets that already use the legacy capitalized suffix `<Family>DB_Main.asset` are not renamed by this rule.

### ItemDefinitionSO exception

`ItemDefinitionSO` is the explicit property-type exception:

```text
Enum:       Key_ItemDefinitionPP
Property:   ItemDefinitionSO
Database:   ItemDefinitionDatabase
DB asset:   ItemDefinitionDB_Main.asset
SO prefix:  ItemDefinition_Item_<Identifier>.asset
```

Keep Item Definition assets under `Assets/A_Systems/InventorySystem/AllItemDefinitions/` and preserve their existing category subfolders.

## Locations

When extending an existing system, mirror its current script and SO layout; do not reorganize old assets as part of a feature.

For a new first-party system, use this default layout:

```text
Assets/Scripts/<System>/<Family>Property.cs
Assets/Scripts/<System>/<Family>Database.cs
Assets/A_Systems/<System>/AllSOs/<Family>SOs/<Family>DB_main.asset
Assets/A_Systems/<System>/AllSOs/<Family>SOs/<Family>PP_<Identifier>.asset
```

Nested category folders beneath `<Family>SOs` are allowed. The database's `scanFolder` must be the relative-to-`Assets` path for that family, for example:

```text
A_Systems/FooSystem/AllSOs/FooSOs
```

## Property data rules

- All new enum types use `Key_<Family>PP`.
- `stringKey` is left empty by default. It is not part of normal Manager lookup.
- If a future approved feature uses a non-empty `stringKey`, it must be unique within its database.
- Use another family's enum key to refer to that family's property data. Do not directly serialize a reference to another Property SO for identity or gameplay lookup.
- Direct references remain appropriate for presentation/content assets such as sprites, prefabs, audio clips, icons, and materials.
- Treat established enum values as persistent data identifiers: do not casually reorder, remove, or rename them. Append new values instead.

## Database setup and registration

Creating an SO alone does not make it queryable. Complete every applicable step:

1. Add the enum value and create the Property/Database scripts.
2. Create the Property SOs in the family's configured asset folder.
3. Create the `<Family>DB_main` asset and set its `scanFolder`.
4. In that Database asset's Inspector, run **Collect Entries From Folder**.
5. Verify that `entries` contains the expected Property SOs and that each `EnumKey` is correct.
6. Add the Database SO to `PropertyDatabaseManager.allDatabases` on `AInitGameObject Variant`.
7. Resolve the typed database through `PropertyDatabaseManager` from the relevant Manager; do not assign the database asset in that Manager's Inspector.

`Collect Entries From Folder` clears and rebuilds `entries`, then sorts entries by enum key. Changing a Property asset does not require recollection, but creating, moving, or deleting a Property asset does.

## Uniqueness and failure behavior

- Register exactly one Database SO of each concrete Database type in `allDatabases`. If duplicates are present, `PropertyDatabaseManager` keeps the first one and silently ignores later ones.
- Each Database must contain at most one Property for each enum key. Duplicate enum keys silently keep the first entry in the lookup dictionary.
- Duplicate non-empty `stringKey` values also silently keep the first entry.
- A registered Database with empty `entries`, or entries not collected from its intended `scanFolder`, is registered but cannot return the missing properties.

## Verification before handoff

For PropertyDatabase work, verify all of the following:

1. The Manager resolves the intended typed Database from `PropertyDatabaseManager`.
2. The Database appears once in `allDatabases` on `AInitGameObject Variant`.
3. `scanFolder` is valid and the collected `entries` list is complete.
4. Every expected `EnumKey` is unique and can resolve through `GetByEnum`.
5. The target scene contains the initialization object before any Manager needs the database.
