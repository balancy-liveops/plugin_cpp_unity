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
  LowerOrEqual=4, NotEqual=5), and a numeric target. `numberType=0` uses `value`
  for int/float fields; `numberType=1` uses `longValue` for signed int64 fields.
  Long profile fields and targets can use the SDK's decimal-string representation.
  Numeric integer pairs compare exactly; fractional values use the existing SDK
  float-comparison tolerance. Missing fields/profiles and invalid types do not pass.
  The current value is evaluated on activation/reload and on changes, not counted
  as a delta since activation. Less-than conditions can therefore complete immediately.
- `TaskCondition`: `unnyIdCondition` references any Conditions.Base descendant,
  including And/Or and Custom. It completes on the first true evaluation, including
  activation/reload. A missing condition does not pass. Completed progress is 1.

Both new automatic types latch completion: later changes do not reopen the task.
They reject manual progress/completion/failure operations.

`TaskInfo.Progress` remains an int for compatibility. For a field-number task it is
clamped to [0, int.MaxValue] and fractional parts are truncated. Use `NumericProgress`
(the exact numeric text) or `TryGetNumericProgress(out long/double)` for actual numeric
values. int64 values are never routed through double for comparison or persistence.

## Custom tasks

Create a CMS template inheriting **LiveOps.Tasks.TaskCustom**. Add game-specific
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
