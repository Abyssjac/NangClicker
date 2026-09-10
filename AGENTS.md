# WastelandU Agent Instructions

## PropertyDatabase rules

Before analysing, creating, or modifying a feature that mentions `PropertyDatabase`, `PropertyDatabaseManager`, `property database logic`, `propertyDataabse`, or `使用 propertyDatabase 逻辑`, read [Assets/_AIRules/SKILL.md](Assets/_AIRules/SKILL.md).

Do not apply this rule to an unrelated generic database, SQL, or save-data task merely because it uses the words `database` or `DB`.

## Default collaboration and change authority

### Before explicit implementation approval

- Default to read-only analysis and discussion.
- Do not modify any project file, scene, prefab, ScriptableObject, Inspector assignment, project setting, package/configuration, or external state.
- Read-only code search, asset inspection, and architecture analysis are allowed.
- Identify missing requirements, risks, and reasonable options before proposing changes.
- Do not begin implementation merely because a solution is understood. Wait for an explicit instruction to modify or implement.

### Implementation approval

- File changes require an explicit request such as `修改项目`, `开始实现`, `请实现`, or `输出代码并修改`.
- `输出代码` alone means provide code in the reply only; it does not authorize file edits.
- Unless the user explicitly expands the scope, implementation may modify only requested source scripts and closely related source-code files.
- Do not use MCP or the Unity UI to create, delete, or edit scenes, GameObjects, Prefabs, ScriptableObjects, Inspector references, or project settings unless the user explicitly authorizes those asset operations.
- Do not add, delete, duplicate, move, or assign SO assets by default.
- Read-only Unity/Inspector inspection is allowed when useful; persistent Unity changes are not.

### Required handoff after every implementation

- Summarize every changed file and why it changed.
- State required Inspector setup; explicitly say `无需 Inspector 设置` when none is needed.
- Provide concrete testing steps and state what was or was not verified.
- Clearly identify any manual Unity, scene, Prefab, or SO steps that remain for the user.
