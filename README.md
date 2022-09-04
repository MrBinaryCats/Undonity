# Undonity
This utility allows you to record and undo changes during runtime.

## Recording Changes

Recording changes is easy, simply create a new record and pass in the object you wish to record.

Any changes you make to the object within the scope will be recorded to a `snapshot`
```csharp
using (new Undonity.Record<Transform>(transform))
{
    transform.position = Vector3.zero;
}
```

## Undo
Undoing your recorded changes is simple, simply call
```csharp
Undonity.UndoUtility.Undo()
```
and the most recent change will be undone.  Call it again and the next `snapshot` in the stack will be undone

## Destroying Objects
Undonity supports destroying of gameobjects and components.
```csharp
using (var record = new RecordGameObject(go))
{
    record.Destroy();
}
```
```csharp
using (var record = new RecordComponent<BoxCollider>(collider))
{
    record.Destroy();
}
```
When a destroyed object is undone, it is restored with its original values.