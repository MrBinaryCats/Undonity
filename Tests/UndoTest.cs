using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Undonity;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace Undonity
{
    public class UndoTest
    {
        private GameObject _testObject;

        [SetUp]
        public void SetUp()
        {
            _testObject = new GameObject("Test Object", typeof(BoxCollider));
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_testObject);
        }

        [Test]
        public void NoChanges()
        {
            var transform = _testObject.transform;

            transform.position = Vector3.one;

            using (new Record<Transform>(transform))
            {
            }

            Assert.That(transform.position, Is.EqualTo(Vector3.one));
            UndoUtility.Undo();
            Assert.That(transform.position, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void UndoSetValue()
        {
            var transform = _testObject.transform;

            transform.position = Vector3.one;

            using (new Record<Transform>(transform))
            {
                transform.position = Vector3.zero;
            }

            Assert.That(transform.position, Is.Not.EqualTo(Vector3.one));
            UndoUtility.Undo();
            Assert.That(transform.position, Is.EqualTo(Vector3.one));

            using (new RecordComponent<Transform>(transform))
            {
                transform.position = Vector3.zero;
            }

            Assert.That(transform.position, Is.Not.EqualTo(Vector3.one));
            UndoUtility.Undo();
            Assert.That(transform.position, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void UndoMultipleSetValues()
        {
            var transform = _testObject.transform;

            transform.position = Vector3.one;
            transform.localScale = Vector3.one;

            using (new Record<Transform>(transform))
            {
                transform.position = Vector3.zero;
                transform.localScale = Vector3.zero;
            }

            Assert.That(transform.position, Is.Not.EqualTo(Vector3.one));
            Assert.That(transform.localScale, Is.Not.EqualTo(Vector3.one));
            UndoUtility.Undo();
            Assert.That(transform.position, Is.EqualTo(Vector3.one));
            Assert.That(transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void UndoMultipleComponentChanges()
        {
            var transform = _testObject.transform;
            var collider = _testObject.GetComponent<BoxCollider>();
            Assume.That(collider, Is.Not.Null);
            transform.position = Vector3.one;

            using (new Record<Transform>(transform))
            {
                transform.position = Vector3.zero;
                using (new Record<BoxCollider>(collider))
                {
                    collider.isTrigger = true;
                }
            }

            Assert.That(transform.position, Is.Not.EqualTo(Vector3.one));
            Assert.That(collider.isTrigger, Is.Not.EqualTo(false));

            UndoUtility.Undo();
            Assert.That(transform.position, Is.Not.EqualTo(Vector3.one));
            Assert.That(collider.isTrigger, Is.EqualTo(false));

            UndoUtility.Undo();
            Assert.That(transform.position, Is.EqualTo(Vector3.one));
            Assert.That(collider.isTrigger, Is.EqualTo(false));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void UndoValueChange(bool checkIfValueChanged)
        {
            var transform = _testObject.transform;
            transform.position = Vector3.one;

            //Record a change in a value in the undo scope
            using (new Record<Transform>(transform))
            {
                transform.position = Vector3.zero;
                transform.localScale = Vector3.zero;
            }

            Assert.That(transform.position, Is.Not.EqualTo(Vector3.one));

            //Change the value outside an undoable scope
            transform.position = Vector3.up;
            Assert.That(transform.position, Is.Not.EqualTo(Vector3.zero));

            //Perform an undo
            UndoUtility.Undo(checkIfValueChanged);

            //If checkIfValueChanged
            //Check the value is still the value which was made outside the scope
            //otherwise, it should be the original value
            var expectedPos = checkIfValueChanged ? transform.position : Vector3.one;
            var expectedScale = checkIfValueChanged ? transform.localScale : Vector3.one;

            Assert.That(transform.position, Is.EqualTo(expectedPos));
            Assert.That(transform.localScale, Is.EqualTo(expectedScale));
        }

        [UnityTest]
        public IEnumerator UndoDestroyComponent()
        {
            var collider = _testObject.GetComponent<BoxCollider>();

            using (var record = new RecordComponent<BoxCollider>(collider))
            {
                record.Destroy();
            }

            yield return null;

            Assert.That(_testObject.GetComponent<BoxCollider>(), Is.Null);
            UndoUtility.Undo();
            Assert.That(_testObject.GetComponent<BoxCollider>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator UndoDestroyComponentWithValues()
        {
            var collider = _testObject.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            using (var record = new RecordComponent<BoxCollider>(collider))
            {
                record.Destroy();
            }

            yield return null;

            Assume.That(_testObject.GetComponent<BoxCollider>(), Is.Null);
            UndoUtility.Undo();
            collider = _testObject.GetComponent<BoxCollider>();
            Assume.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True);
        }

        [UnityTest]
        public IEnumerator UndoDestroyGameObject()
        {
            const string goName = "MyObject";
            var go = new GameObject(goName)
            {
                transform =
                {
                    parent = _testObject.transform,
                    position = Vector3.one
                }
            };

            Assert.That(_testObject.transform.childCount, Is.EqualTo(1));
            using (var record = new RecordGameObject(go))
            {
                record.Destroy();
            }

            yield return null;
            Assert.That(_testObject.transform.childCount, Is.EqualTo(0));

            var isNull = go == null;
            Assert.That(isNull);
            UndoUtility.Undo();
            Assert.That(_testObject.transform.childCount, Is.EqualTo(1));

            go = _testObject.transform.GetChild(0).gameObject;
            Assert.That(go, Is.Not.Null);
            Assert.That(go.name, Is.EqualTo(goName));
            Assert.That(go.transform.position, Is.EqualTo(Vector3.one));

        }



        [UnityTest]
        public IEnumerator UndoDestroyGameObjectWithComponents()
        {
            const string goName = "MyObject";
            var go = new GameObject(goName, typeof(BoxCollider))
            {
                transform =
                {
                    parent = _testObject.transform,
                    position = Vector3.one
                }
            };

            Assert.That(_testObject.transform.childCount, Is.EqualTo(1));
            using (var record = new RecordGameObject(go))
            {
                record.Destroy();
            }

            yield return null;
            Assert.That(_testObject.transform.childCount, Is.EqualTo(0));

            var isNull = go == null;
            Assert.That(isNull);

            UndoUtility.Undo();
            Assert.That(_testObject.transform.childCount, Is.EqualTo(1));

            var newObj = _testObject.transform.GetChild(0);

            Assert.That(newObj.GetComponent<BoxCollider>, Is.Not.Null);

            go = _testObject.transform.GetChild(0).gameObject;
            Assert.That(go, Is.Not.Null);
            Assert.That(go.name, Is.EqualTo(goName));
            Assert.That(go.transform.position, Is.EqualTo(Vector3.one));

        }

    }
}