#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace ShaderMotion.Addon {
class VRCA3Descriptor {
	public SerializedObject serializedObject;
	public static VRCA3Descriptor FromGameObject(GameObject go) {
		foreach(var mono in go.GetComponents<MonoBehaviour>()) {
			var so = new SerializedObject(mono);
			if(so.FindProperty("customizeAnimationLayers") != null && so.FindProperty("customExpressions") != null)
				return new VRCA3Descriptor{serializedObject=so};
		}
		return null;
	}

	public const int FX = 5;
	public void MergeAnimationLayer(int type, AnimatorController source) {
		serializedObject.FindProperty("customizeAnimationLayers").boolValue = true;
		var baseAnimationLayers = serializedObject.FindProperty("baseAnimationLayers");
		var animationLayer = Enumerable.Range(0, baseAnimationLayers?.arraySize??0)
			.Select(i => baseAnimationLayers.GetArrayElementAtIndex(i))
			.FirstOrDefault(x => x.FindPropertyRelative("type")?.intValue == (int?)type);
		if(animationLayer != null) {
			var isDefault = animationLayer.FindPropertyRelative("isDefault");
			var animatorController = animationLayer.FindPropertyRelative("animatorController");
			var target = (AnimatorController)animatorController.objectReferenceValue;
			if(!isDefault.boolValue && target && target != source) {
				AnimatorControllerUtil.CopyParameters(source, target);
				AnimatorControllerUtil.CopyLayers(source, target);
				Debug.Log($"{target} is updated");
			} else {
				isDefault.boolValue = false;
				animatorController.objectReferenceValue = source;
			}
		}
		serializedObject.ApplyModifiedProperties();
	}
	public void MergeExpressions(ScriptableObject sourceMenu, ScriptableObject sourceParams) {
		serializedObject.FindProperty("customExpressions").boolValue = true;
		var expressionsMenu = serializedObject.FindProperty("expressionsMenu");
		var expressionParameters = serializedObject.FindProperty("expressionParameters");
		var targetMenu = (ScriptableObject)expressionsMenu.objectReferenceValue;
		var targetParams = (ScriptableObject)expressionParameters.objectReferenceValue;
		if(targetMenu && targetMenu != sourceMenu) {
			var sourceSO = new SerializedObject(sourceMenu);
			var targetSO = new SerializedObject(targetMenu);
			CopyArrayElementsByName(sourceSO.FindProperty("controls"), targetSO.FindProperty("controls"));
			targetSO.ApplyModifiedProperties();
			Debug.Log($"{targetMenu} is updated");
		} else
			expressionsMenu.objectReferenceValue = sourceMenu;
		if(targetParams && targetParams != sourceParams) {
			var sourceSO = new SerializedObject(sourceParams);
			var targetSO = new SerializedObject(targetParams);
			CopyArrayElementsByName(sourceSO.FindProperty("parameters"), targetSO.FindProperty("parameters"));
			targetSO.ApplyModifiedProperties();
			Debug.Log($"{targetParams} is updated");
		} else
			expressionParameters.objectReferenceValue = sourceParams;
		serializedObject.ApplyModifiedProperties();
	}

	static void CopyRecursive(SerializedProperty source, SerializedProperty target) {
		var trunc = source.propertyPath.Length+1;
		foreach(SerializedProperty sourceProp in source) {
			var targetProp = target.FindPropertyRelative(sourceProp.propertyPath.Substring(trunc));
			if(targetProp != null)
				switch (targetProp.propertyType) {
					case SerializedPropertyType.Integer: targetProp.intValue = sourceProp.intValue; break;
					case SerializedPropertyType.Boolean: targetProp.boolValue = sourceProp.boolValue; break;
					case SerializedPropertyType.Float: targetProp.floatValue = sourceProp.floatValue; break;
					case SerializedPropertyType.String: targetProp.stringValue = sourceProp.stringValue; break;
					case SerializedPropertyType.ObjectReference: targetProp.objectReferenceValue = sourceProp.objectReferenceValue; break;
					case SerializedPropertyType.Enum: targetProp.enumValueIndex = sourceProp.enumValueIndex; break;
					case SerializedPropertyType.ArraySize: targetProp.intValue = sourceProp.intValue; break;
					// TODO: other types
				}
		}
	}
	static void CopyArrayElementsByName(SerializedProperty source, SerializedProperty target) {
		var sourceArr = Enumerable.Range(0, source.arraySize).Select(source.GetArrayElementAtIndex);
		var targetMap = Enumerable.Range(0, target.arraySize).Select(target.GetArrayElementAtIndex)
			.GroupBy(x => x.FindPropertyRelative("name").stringValue).ToDictionary(g => g.Key, g => g.First());
		foreach(var sourceElem in sourceArr) {
			var name = sourceElem.FindPropertyRelative("name").stringValue;
			if(string.IsNullOrEmpty(name))
				continue;
			if(!targetMap.TryGetValue(name, out var targetElem)) {
				target.InsertArrayElementAtIndex(target.arraySize);
				targetElem = target.GetArrayElementAtIndex(target.arraySize-1);
			}
			CopyRecursive(sourceElem, targetElem);
		}
	}
}
}
#endif