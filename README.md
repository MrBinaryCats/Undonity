# PropyCopy
This utility allows copying and pasting of fields ands components even if their names or types do not match.

## Copy Components
PropyCopy allows you to copy the value of fields within a component and paste those values to a different component, even if the components types do not match. So long as the as the field type and name matches the a value from the copied component will be pasted to the new.

![CopyToComponent](https://user-images.githubusercontent.com/85991229/153758195-252e27f8-67b0-46c9-b515-8f980a177663.gif)

## Copy Fields
PropyCopy allows you to copy the value of a single field and paste that value onto another.
![CopyField](https://user-images.githubusercontent.com/85991229/153758214-ea50d026-2cff-4dbd-93a2-8d00ab16760e.gif)


## Notes

### Deep Copy
PropyCopy allows you to copy entire object fields, however when pasting an object PropyCopy will try to paste fields based on the named path of each field.

![DeepCopy](https://user-images.githubusercontent.com/85991229/153758221-f3e6d5c7-3456-41fe-be77-b872a6bbe701.gif)

In the above example `Somedata` is copied and pasted to `My Other Data`. All the data is copied across except for the `Somedata.Other Nested Data` fields, as even though `My Other Data.Named Something Else` has the same field types; it does not have the same named path.   

### Transform Rotation
Copy and pasting to and from the Transform's rotation field may not give you the expected results. This is because Rotation is of type `Quaternion` but displayed as the `Vector3` Euler angle.

If you copy Rotation of `0,90,0` you actually copy:
```json
{
  "x": 0.0,
  "y": 0.7071068,
  "z": 0.0,
  "w": 0.7071068
}
```
If you were to paste this to a Vector3 field, the `x`,`y` and `z` components would get set to `0.0`,`0.7071068` and `0.0` respectively.

If you were to paste a `Vector3` of value `0,90,0` the Quaternion x,y, and z component would get set to `0`,`90` and `0` respectively. Which means the Euler angle would be `0,179.1,0` 

