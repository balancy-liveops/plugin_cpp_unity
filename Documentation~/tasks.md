# Extensible tasks

Task definitions live in the CMS; player state lives in `Profiles.System.TasksInfo`.
Use a native SDK build containing the new task exports on every target platform.
The corresponding CMS templates must be installed/published separately; SDK source
changes do not create templates in a game's CMS. See the C++ repository's
`docs/cms_templates/ExtensibleTasks.json` for the field contract.

## Built-in types

- `TaskItem`, `TaskCompleteLevels`, `TaskCompleteLevelsStreak`: existing behavior.
- `TaskFieldNumber`: observe `fullPath` (SmartObjects.ProfileFullPath: profile + path),
  `comparison` (Equal=0, Greater=1, GreaterOrEqual=2, Lower=3,
  LowerOrEqual=4, NotEqual=5), and one float target (`value`) for int/float fields.
  Comparisons use float precision and the existing SDK float-comparison tolerance.
  Long fields are unsupported. Missing fields/profiles and invalid types do not pass.
  The current value is evaluated on activation/reload and on changes, not counted
  as a delta since activation. Less-than conditions can therefore complete immediately.
- `TaskCondition`: `unnyIdCondition` references any Conditions.Base descendant,
  including And/Or and Custom. It completes on the first true evaluation, including
  activation/reload. A missing condition does not pass. Completed progress is 1.

Both new automatic types latch completion: later changes do not reopen the task.
They reject manual progress/completion/failure operations.

`TaskInfo.Progress` remains an int for compatibility. For a field-number task it is
clamped to [0, int.MaxValue] and fractional parts are truncated. Use `NumericProgress`
(the observed numeric text) or `TryGetNumericProgress(out double)` for actual numeric
values, including fractions.

## Custom tasks

Create a concrete CMS template inheriting the abstract **LiveOps.Tasks.TaskCustom**. Add game-specific
fields and generate/register the C# model in the usual way. A direct BaseTask descendant
loads as BaseTask but has no goal behavior and does not accept custom mutations.
Existing built-in descendants keep their native tracking; overriding GetTaskType in
C# does not replace C++ behavior.

`TaskCustom.count > 0` automatically completes at the target. A zero count means the
game calls Complete explicitly. Progress is a nonnegative int; SetProgress can lower
it while InProgress, AddProgress accepts nonnegative increments. Negative values,
overflow, inactive tasks, stale runs, and terminal-state mutations return false.

```csharp
public class KillEnemiesTask : Balancy.Models.LiveOps.Tasks.TaskCustom
{
    public int EnemyType => GetIntParam("enemyType");
    private System.Action<int> _onKilled;

    public override void OnStart(Balancy.CustomTaskContext context)
    {
        _onKilled = type => {
            if (type == EnemyType) context.AddProgress();
        };
        GameEvents.EnemyKilled += _onKilled; // your game's event
    }

    public override void OnStop(Balancy.CustomTaskContext context)
    {
        GameEvents.EnemyKilled -= _onKilled;
        _onKilled = null;
    }
}

// Register before SDK initialization (or use the generated model registration).
Balancy.CMS.OnTypeRequested = template =>
    template == "Game.KillEnemiesTask" ? new KillEnemiesTask() : null;
```

Preserve/compose existing type factories if the game already registers other types.
OnStart is called on activation, failed-task restoration and profile reload.
OnStop releases subscriptions on completion, failure, deactivation, replacement and
SDK shutdown. Hooks run on Unity's main thread after native mutation, never while
holding the native mutation lock. A run that ends before queued OnStart is delivered
is skipped. OnStart exceptions are logged and OnStop is attempted for cleanup;
no reward is automatically granted on handler failure.

Keep the **CustomTaskContext**, not just TaskInfo, in asynchronous callbacks. Its
immutable RunId identifies one activation. Reactivation/restoration/reload renews
that token, so an old callback cannot update the new run. OnStop invalidates the
handler's context. The SDK persists progress/status, not arbitrary C# handler fields;
put additional game state in a player profile and reconnect events in OnStart.

## Complete C# lifecycle

```csharp
var task = Balancy.CMS.GetModelByUnnyId<Balancy.Models.LiveOps.Tasks.TaskCustom>(taskId);
Balancy.API.Tasks.ActivateTask(task, optionalGameEvent);
var info = Balancy.API.Tasks.GetTaskInfo(task);
var context = info.CreateCustomContext();

context.SetProgress(5); // or API.Tasks.SetProgress(info, 5)
context.AddProgress(1);
context.Complete();    // optional for positive-count tasks
// context.Fail();     // InProgress -> Failed
// API.Tasks.RestoreFailedTask(task); // preserves progress, creates a new run token

if (info.Status == Balancy.Models.LiveOps.TaskStatus.Completed)
    Balancy.API.Tasks.ClaimReward(task);

Balancy.API.Tasks.DeactivateTask(task);
```

State can be observed with existing `SubscribeForParamChange("progress", ...)`,
`"numericProgress"` and `"status"` subscriptions; unsubscribe when the UI is disposed.
Completing writes CompleteTime using SDK server time, for old and new task types.
Only ClaimReward grants the configured reward and moves Completed -> Claimed.
Repeated claim returns false. Claim remains controlled by the SDK, not a virtual hook.
Raw BaseData setters are not the task API and bypass task-manager invariants.

ActivateTask is an explicit restart if the definition is already active: progress,
CompleteTime and numeric progress reset. DeactivateTask deletes its player record.
RestoreFailedTask preserves progress. There is one player record per task document,
not per event: use separate documents for simultaneous independent event quests.
Own behavior is unchanged: no inventory snapshot on activation and no reopening after
completion, even if inventory falls below the target. Level tasks track LevelCompleted /
LevelFailed API events, not arbitrary writes to the Level profile property.

Client-side lifecycle validation protects integration invariants, not the authenticity
of game events on a modified client. Authoritative rewards require server validation.


### Custom progress and completion contract

`LiveOps.Tasks.TaskCustom` is an **abstract CMS template**. Create a concrete child
such as `Game.KillEnemiesTask`, then create task documents from that child. There are
no direct TaskCustom documents. The native CMS registers `prepareDefaultParser<TaskCustom>()`
without `parseList<TaskCustom>()`; the inheritance resolver uses that registered parser
for concrete descendants (including descendants through intermediate abstract templates).
CMS abstraction does not require making the SDK fallback model class abstract: native
instances of that class provide the behavior for documents of user-defined templates.

- `count` is a target for automatic completion, not a cap on stored progress.
- With `count > 0`, setting or adding progress completes the task when the result is
  **greater than or equal to** count. For count=10, both 10 and 12 complete it.
- With `count = 0`, progress never completes the task automatically. The game must
  explicitly call Complete. Zero does not cause immediate completion on activation.
- Negative count is invalid authoring data: set a minimum of zero in the CMS. As a
  runtime fallback, count < 0 behaves like zero and disables automatic completion.
- Explicit Complete is allowed for any active custom task, including a positive-count
  task below its target. It preserves progress rather than forcing it to the target.
- SetProgress replaces the current value (and can lower it while InProgress);
  AddProgress adds a nonnegative increment. Progress is a nonnegative int32; negative
  input and addition overflow are rejected. Terminal tasks reject further changes.
- For example, the game tracks kills and calls AddProgress(1) after each kill, or
  SetProgress(7) with its current total. The SDK does not detect custom gameplay events.
- Integer percentages are supported by using count=100 and progress values such as
  25, 50 and 100. Fractional custom progress such as 25.5 is not supported. For a
  count-based UI, compute the ratio as floating-point progress/count only when count>0;
  clamp it for display if needed. A manually completed task need not have a ratio of 1.
- Completion records CompleteTime but does not grant the reward. Claim grants the
  configured reward once for that completion; explicit reactivation starts a new cycle.

The target-counter design preserves existing int32 Progress compatibility and useful
UI values such as "7 of 10". Zero also supports arbitrary game-defined completion
without an additional mode field. A normalized float (0..1), explicit-only completion,
or a separate completion-mode enum are alternatives, not the current API contract.
The CMS field remains named `count`; `TargetProgress` was a naming suggestion only.

## Task, TaskInfo and CustomTaskContext

`Task` is the CMS definition. `TaskInfo` is the current player's mutable record:
status, progress, timestamps and RunId. A task document has at most one such record
per player (not one per event); before activation and after deactivation GetTaskInfo
returns null. Completed/Claimed records remain until deactivation or reactivation.
The CMS model instance is cached by the existing model system, not recreated per run.

`CreateCustomContext()` makes a lightweight C# handle containing the current TaskId
and RunId. It does not activate anything, copy player state, register subscriptions,
or call OnStart. Calling it twice for the same run returns two different objects
with identical IDs. Both can operate on that run; neither invalidates the other.
They share the same underlying TaskInfo, so completing through one prevents further
progress changes through both.

A context variable never automatically becomes null. After restart/restore/profile
reload, its old RunId no longer matches: mutation returns false and context.Info
returns null. After deactivation, context.Info is also null. After completion or
failure, context.Info may still return the terminal record while mutations fail.
OnStop also invalidates the particular context delivered to the lifecycle hooks.
Independently created contexts are still protected by native RunId/status checks.

Why a separate handle? The existing TaskInfo can be reused and updated on restart.
An old asynchronous callback holding that mutable info could accidentally modify
the *new* run. A captured context permanently targets the original run instead:

```csharp
API.Tasks.ActivateTask(task);
var info = API.Tasks.GetTaskInfo(task);
var first = info.CreateCustomContext();
var second = info.CreateCustomContext(); // same run; does not replace first
API.Tasks.ActivateTask(task);             // explicit restart
bool accepted = first.AddProgress(1);    // false: first run ended
// second.AddProgress(1) would also return false.
```

For immediate UI/game commands, a context need not be managed explicitly:

| API.Tasks method | Argument / purpose |
| --- | --- |
| ActivateTask | Task plus optional event; creates or restarts the record |
| DeactivateTask | Task; removes the record and stops tracking |
| GetTaskInfo | Task; returns its current player record or null |
| SetProgress / AddProgress | Current TaskInfo and int; custom tasks only |
| CompleteTask / FailTask | Current TaskInfo; custom tasks only |
| RestoreFailedTask | Task; restores a failed record while preserving progress |
| ClaimReward | Task; grants reward only for Completed, once per completion |

The progress/completion convenience methods capture a context at the moment they
are called. Do not use a retained mutable TaskInfo in delayed callbacks when you
intend to target an earlier run: keep the context instead. Putting forwarding methods
on TaskInfo would be a possible convenience API, but would not remove this distinction.
Claim, deactivate and restart intentionally target the current record by task ID;
they do not have the stale-run protection of context mutation methods.

OnStart/OnStop are virtual overrides, not C# events. Subscribe to game events in
OnStart and detach them in OnStop. OnStart is queued for activation, restart, failed
restoration and reloading an in-progress task. OnStop cleans up a previously started
handler on completion, failure, deactivation, replacement and SDK teardown. Claim does
not trigger another OnStop because completion already stopped tracking. The hooks run
on the Unity main thread after native mutation; a run already ended before dispatch
is skipped and therefore does not necessarily produce a start/stop pair.

Class construction still uses the existing CMS.OnTypeRequested factory and inheritance
fallback. GeneratedMain already registers the generated factory (including TestTask).
No extra callback registration is needed for generated classes. Manual replacement of
CMS.OnTypeRequested is only for custom factories and must preserve unrelated mappings.
Inheritance identifies a fallback parent; the generated factory creates the exact C#
subclass so its fields and virtual overrides are available. The SDK change adds the new
built-in task cases; it does not replace that class-resolution mechanism.

Compatibility: CustomTasks registers its native callback during SDK initialization,
even when no tasks are configured. Ship the matching native libraries with this C#
wrapper on every platform; an older binary missing balancyTasks_SetLifecycleCallback
can fail initialization. Shared native condition subscription code also changed, so
not using tasks is not by itself an isolation guarantee. See the C++ compatibility
audit for the focused regression coverage and platform limitations.
