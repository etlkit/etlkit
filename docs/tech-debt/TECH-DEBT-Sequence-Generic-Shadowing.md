# Tech Debt: `Sequence<T>` shadows `Tasks`/`Execute` instead of overriding them

## Context

`Sequence<T>` derives from the non-generic `Sequence` and redeclares both working members with
`new` (`EtlKit/src/Toolbox/ControlFlow/Sequence.cs`):

```csharp
public class Sequence : GenericTask
{
    public Action Tasks { get; set; }

    public void Execute() =>
        new CustomTask(TaskName) { TaskType = TaskType, TaskHash = TaskHash }.Execute(Tasks);
}

public class Sequence<T> : Sequence
{
    public new Action<T> Tasks { get; set; }

    public new void Execute() =>
        new CustomTask(TaskName) { TaskType = TaskType, TaskHash = TaskHash }.Execute(Tasks, Parent);
}
```

Neither member is virtual, so which pair runs is decided by the *static* type of the reference,
not by the object. Both classes are `[PublicAPI]`.

## Problem

A `Sequence<T>` reached through a `Sequence`-typed variable (a `List<Sequence>`, a parameter, a
factory return value) silently executes the wrong members:

| Caller wrote | What actually runs |
|---|---|
| `seq.Execute()` on a `Sequence`-typed reference | Base `Execute()` → base `Tasks`, which is `null` unless the caller *also* set the base property |
| `((Sequence)seqT).Tasks = ...` | Sets the base delegate; the generic `Execute()` ignores it |

The null path is the nasty one: `CustomTask.Execute(Action task)` invokes the delegate without a
null check (`EtlKit/src/Definitions/TaskBase/ControlFlow/CustomTask.cs:27-32`), and it calls
`LogStart()` *before* the invoke. The result is a `NullReferenceException` thrown after a `START`
entry was already logged, with no matching `END` — the load-process log shows a sequence that
started and vanished.

No code inside this repository hits the trap today — production code never subtypes through a base
reference, and the tests only use the static `Execute(...)` overloads or direct-typed instances.
The debt is the public API surface: any consumer composing sequences polymorphically gets
runtime behavior that contradicts what the code visibly says.

PR [#4](https://github.com/etlkit/etlkit/pull/4) documented the shadowing in XML doc comments
("Shadows `Sequence.Tasks`..."). The review agreed this narrates the trap rather than closing it
([thread](https://github.com/etlkit/etlkit/pull/4#discussion_r3639195924)) — a doc comment is not
a fix, hence this debt.

## Direction

1. **Fix dispatch (non-breaking):** make `Sequence.Execute()` virtual and turn the `new` method in
   `Sequence<T>` into an `override`. The generic override runs the generic `Tasks`; if only the
   base delegate is set, fall back to it. Base-typed callers then always reach the right code.
2. **Fail loudly on the remaining null path:** in `Sequence.Execute()` (or centrally in
   `CustomTask.Execute(Action)`), throw `InvalidOperationException` with a clear message when the
   delegate is null — mirroring the existing parameterless guard
   `CustomTask.Execute()` ("A custom task can't be used without an Action!") — instead of an NRE
   logged as a half-finished task.
3. **Longer term (breaking, park until a major release):** reconsider whether `Sequence<T>` should
   derive from `Sequence` at all. It inherits only `TaskName`/`TaskType` plumbing and pays for it
   with two shadowed members; a shared base under both (or composition) removes the dual-`Tasks`
   wart entirely.
4. Once fixed, strip the "Shadows ..." narration from the XML docs added in PR #4 so the docs
   describe behavior, not the bug.

## Sites

| File | Line | Note |
|---|---|---|
| `EtlKit/src/Toolbox/ControlFlow/Sequence.cs` | 13-16 | Base `Execute()`/`Tasks` |
| `EtlKit/src/Toolbox/ControlFlow/Sequence.cs` | 37-58 | `new Tasks` / `new Execute()` in `Sequence<T>` |
| `EtlKit/src/Definitions/TaskBase/ControlFlow/CustomTask.cs` | 27-32 | Invokes the delegate with no null guard, after `LogStart()` |
| `TestControlFlowTasks/src/SequenceTests.cs` | 15-40 | Existing coverage; only static overloads — add base-typed-reference tests with the fix |

## Related

Surfaced by the PR [#4](https://github.com/etlkit/etlkit/pull/4) review
([discussion](https://github.com/etlkit/etlkit/pull/4#discussion_r3639195924)). The XML docs in
that PR intentionally keep the current behavior documented until this debt is paid.
