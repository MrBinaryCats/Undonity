using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Undonity
{
    public static class UndoUtility
    {
        public static readonly Queue<Snapshot> Queue = new();

        public static void Undo(bool checkIfValueChanged = true)
        {
            if (Queue == null || Queue.Count == 0)
            {
                return;
            }

            var snapshot = Queue.Dequeue();

            snapshot.Undo(checkIfValueChanged);
        }
    }


    public abstract class Snapshot
    {
        public abstract void Undo(bool checkIfValueChanged = true);
    }

    public class Record<T> : IDisposable where T : class
    {
        public class SnapshotObject : Snapshot
        {
            public readonly List<(MemberInfo mi, object initialValue, object setValue)> Changes;
            public T Target;

            public SnapshotObject(T obj)
            {
                Target = obj;
                Changes = new List<(MemberInfo, object, object)>();
            }

            public override void Undo(bool checkIfValueChanged = true)
            {
                foreach (var (mi, initialValue, setValue) in Changes)
                {
                    switch (mi)
                    {
                        case FieldInfo fi when !checkIfValueChanged || Equals(fi.GetValue(Target), setValue):
                            fi.SetValue(Target, initialValue);
                            break;
                        case PropertyInfo pi when !checkIfValueChanged || Equals(pi.GetValue(Target), setValue):
                            pi.SetValue(Target, initialValue);
                            break;
                    }
                }
            }
        }

        protected readonly T Object;
        protected readonly (MemberInfo fi, object initialValue)[] Members;

        public Record(T @object)
        {
            Object = @object;
            var type = Object.GetType();
            const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var members = type.GetFields(bindingFlags).Cast<MemberInfo>()
                .Concat(type.GetProperties(bindingFlags)).ToArray();

            Members = new (MemberInfo fi, object value)[members.Length];
            for (var index = 0; index < members.Length; index++)
            {
                var memberInfo = members[index];
                if (memberInfo.GetCustomAttribute<ObsoleteAttribute>() != null)
                {
                    continue;
                }

                Members[index] = memberInfo switch
                {
                    FieldInfo fi => (memberInfo, fi.GetValue(@object)),
                    PropertyInfo {CanRead: true, CanWrite: true} pi => (memberInfo, pi.GetValue(@object)),
                    _ => Members[index]
                };
            }
        }

        public virtual void Dispose()
        {
            SnapshotObject snapshot = null;
            foreach (var (mi, initialValue) in Members)
            {
                var newValue = mi switch
                {
                    FieldInfo fi => fi.GetValue(Object),
                    PropertyInfo pi => pi.GetValue(Object),
                    _ => null
                };
                if (Equals(initialValue, newValue)) continue;
                snapshot ??= new SnapshotObject(Object);
                snapshot.Changes.Add((mi, initialValue, newValue));
            }

            if (snapshot != null)
            {
                UndoUtility.Queue.Enqueue(snapshot);
            }
        }
    }

    public class RecordComponent<T> : Record<T> where T : Component
    {
        public class ComponentSnapshot : SnapshotObject
        {
            public GameObject TargetGameObject;

            public override void Undo(bool checkIfValueChanged = true)
            {
                var newObj = TargetGameObject.AddComponent(typeof(T));
                Target = newObj as T;
                ReapplyValues();
            }

            public void ReapplyValues()
            {
                base.Undo(false);
            }
            public ComponentSnapshot(T obj) : base(obj)
            {
            }
        }

        private readonly GameObject _attachedObject;
        public bool HasBeenDestroyed;

        public RecordComponent(T component) : base(component)
        {
            _attachedObject = component.gameObject;
        }

        public void Destroy()
        {
            UnityEngine.Object.Destroy(Object);
            HasBeenDestroyed = true;
        }

        public override void Dispose()
        {
            if (HasBeenDestroyed)
            {
                var snapshot = new ComponentSnapshot(Object)
                    {TargetGameObject = _attachedObject};
                foreach (var (fi, initialValue) in Members)
                {
                    snapshot.Changes.Add(new ValueTuple<MemberInfo, object, object>(fi, initialValue, null));
                }

                UndoUtility.Queue.Enqueue(snapshot);
            }
            else
            {
                base.Dispose();
            }
        }
    }

    public class RecordGameObject : Record<GameObject> 
    {
        private class GameObjectSnapshot : SnapshotObject
        {
            public Transform Parent;
            public List<(Type,Snapshot)> ComponentSnapShots;
            public string Name;

            public override void Undo(bool checkIfValueChanged = true)
            {
                Target = new GameObject(Name);
                Target.GetComponent<Transform>().parent = Parent;

                var dict = new Dictionary<Type, int>();
                foreach (var (type, _) in ComponentSnapShots)
                {
                    if (!dict.ContainsKey(type))
                    {
                        dict[type] = 0;
                    }

                    dict[type]++;
                }
                foreach (var (type,snapshot) in ComponentSnapShots)
                {
                    var comps = Target.GetComponents(type);
                    var compsnap = (snapshot as RecordComponent<Component>.ComponentSnapshot);
                    compsnap.TargetGameObject = Target;
                    if (comps.Length < dict[type] )
                    {
                        compsnap.Target = Target.AddComponent(type);
                    }
                    else
                    {
                        compsnap.Target = comps[^1];
                    }
                    compsnap.ReapplyValues();
                }
                base.Undo(false);
            }

            public GameObjectSnapshot(GameObject obj) : base(obj)
            {
            }
        }

        private readonly Transform _parentObject;
        private string _name;
        private bool _hasBeenDestroyed;
        private List<(Type,Snapshot)> _componentSnapShots;

        public RecordGameObject(GameObject gameObject) : base(gameObject)
        {
            _name = gameObject.name;
            _parentObject = gameObject.transform.parent;
        }

        public void Destroy()
        {
            var comps = Object.GetComponents<Component>();
            _componentSnapShots = new List<(Type, Snapshot)>();
            foreach (var component in comps)
            {
                
                using ( var record = new RecordComponent<Component>(component))
                {
                    record.HasBeenDestroyed = true;
                }
                _componentSnapShots.Add((component.GetType(),UndoUtility.Queue.Dequeue()));
            }
            UnityEngine.Object.Destroy(Object);
            _hasBeenDestroyed = true;
        }

        public override void Dispose()
        {
            if (_hasBeenDestroyed)
            {
                var snapshot = new GameObjectSnapshot(Object)
                    {Parent = _parentObject, ComponentSnapShots = _componentSnapShots, Name = _name};

                UndoUtility.Queue.Enqueue(snapshot);
            }
            else
            {
                base.Dispose();
            }
        }
    }

    
    public class RecordValue<T> : IDisposable where T : IEquatable<T>
    {
        private class SnapshotField : Snapshot
        {
            public T Value;
            public Action<T> Set;

            public override void Undo(bool checkIfValueChanged = true)
            {
                Set(Value);
            }
        }


        private readonly T _initialValue;
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        public RecordValue(Func<T> get, Action<T> set)
        {
            _initialValue = get.Invoke();
            _get = get;
            _set = set;
        }

        public void Dispose()
        {
            if (!EqualityComparer<T>.Default.Equals(_initialValue, _get.Invoke()))
            {
                UndoUtility.Queue.Enqueue(new SnapshotField {Set = _set, Value = _initialValue});
            }
        }
    }
}