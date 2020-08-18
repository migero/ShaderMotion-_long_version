#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;
using GameObjectRecorder = UnityEditor.Animations.GameObjectRecorder;

namespace ShaderMotion {
[System.Serializable] // this is serializable to survive code reload
public class AnimRecorder {
	static string[] axes = new[]{"x", "y", "z", "w"};
	static int[,] boneMuscles = new int[HumanTrait.BoneCount, 3];
	static AnimRecorder() {
		for(int i=0; i<HumanTrait.BoneCount; i++)
			for(int j=0; j<3; j++)
				boneMuscles[i,j] = HumanTrait.MuscleFromBone(i, j);
	}

	// serializable
	Animator animator;
	Transform hips;
	GameObjectRecorder recorder;
	Transform[] proxies;

	public static implicit operator bool(AnimRecorder r) {
		return !object.ReferenceEquals(r, null) && r.recorder;
	}
	public AnimRecorder(Animator animator) {
		this.animator    = animator;
		this.hips        = animator.GetBoneTransform(HumanBodyBones.Hips);
		this.recorder    = new GameObjectRecorder(animator.gameObject);
		this.proxies  = new Transform[HumanTrait.BoneCount];

		var hideFlags = HideFlags.DontSaveInEditor; // | HideFlags.HideInHierarchy
		var proxyRoot = EditorUtility.CreateGameObjectWithHideFlags("_bones_", hideFlags).transform;
		proxyRoot.SetParent(animator.transform, false);
		for(int i=0; i<HumanTrait.BoneCount; i++) {
			proxies[i] = EditorUtility.CreateGameObjectWithHideFlags(HumanTrait.BoneName[i], hideFlags).transform;
			proxies[i].SetParent(proxyRoot, false);
		}
		bindProxies();
	}
	public void Dispose() {
		var destroy = EditorApplication.isPlaying ? (System.Action<Object>)Object.Destroy : (System.Action<Object>)Object.DestroyImmediate;
		if(recorder) {
			recorder.ResetRecording();
			destroy(recorder);
		}
		if(proxies[0])
			destroy(proxies[0].parent.gameObject);
		recorder = null;
	}
	void bindProxies() {
		{
			var path = AnimationUtility.CalculateTransformPath(proxies[0], animator.transform);
			for(int j=0; j<3; j++)
				recorder.Bind(EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition."+axes[j]));
			for(int j=0; j<4; j++)
				recorder.Bind(EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation."+axes[j]));
		}
		for(int i=1; i<HumanTrait.BoneCount; i++) {
			if(!animator.GetBoneTransform((HumanBodyBones)i))
				continue; // skip recording missing bones
			var path = AnimationUtility.CalculateTransformPath(proxies[i], animator.transform);
			for(int j=0; j<3; j++)
				if(boneMuscles[i, j] >= 0)
					recorder.Bind(EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition."+axes[j]));
		}
	}
	void getProxyCurves(AnimationClip clip, AnimationCurve[] rootCurves, AnimationCurve[] muscleCurves) {
		{
			var path = AnimationUtility.CalculateTransformPath(proxies[0], animator.transform);
			for(int j=0; j<3; j++)
				rootCurves[0+j] = AnimationUtility.GetEditorCurve(clip,
					EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition."+axes[j]));
			for(int j=0; j<4; j++)
				rootCurves[3+j] = AnimationUtility.GetEditorCurve(clip,
					EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation."+axes[j]));
		}
		for(int i=1; i<HumanTrait.BoneCount; i++) {
			var path = AnimationUtility.CalculateTransformPath(proxies[i], animator.transform);
			for(int j=0; j<3; j++)
				if(boneMuscles[i, j] >= 0)
					muscleCurves[boneMuscles[i, j]] = AnimationUtility.GetEditorCurve(clip,
						EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition."+axes[j]));
		}
	}
	void clearProxyCurves(AnimationClip clip) {
		// SetCurve can clear m_LocalPosition.xyz in a single call
		for(int i=0; i<HumanTrait.BoneCount; i++) {
			var path = AnimationUtility.CalculateTransformPath(proxies[i], animator.transform);
			clip.SetCurve(path, typeof(Transform), "m_LocalPosition", null);
			if(i == 0)
				clip.SetCurve(path, typeof(Transform), "m_LocalRotation", null);
		}
	}
	void setHumanCurves(AnimationClip clip, AnimationCurve[] rootCurves, AnimationCurve[] muscleCurves) {
		// AnimationClip.SetCurve is faster than AnimationUtility.SetEditorCurve
		for(int i=0; i<3; i++)
			clip.SetCurve("", typeof(Animator), "RootT."+axes[i], rootCurves[0+i]);
		for(int i=0; i<4; i++)
			clip.SetCurve("", typeof(Animator), "RootQ."+axes[i], rootCurves[3+i]);
		for(int i=0; i<HumanTrait.MuscleCount; i++)
			clip.SetCurve("", typeof(Animator), HumanTrait.MuscleName[i], muscleCurves[i]);
	}

	// non serializable
	[System.NonSerialized]
	HumanPose humanPose;
	HumanPoseHandler poseHandler;

	public void TakeSnapshot(float deltaTime) {
		if(poseHandler == null)
			poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
		poseHandler.GetHumanPose(ref humanPose);

		proxies[0].rotation = humanPose.bodyRotation;
		proxies[0].localPosition =
			animator.transform.InverseTransformVector(Vector3.Scale(hips.parent.lossyScale, 
				humanPose.bodyPosition * animator.humanScale - animator.transform.position));

		for(int i=1; i<HumanTrait.BoneCount; i++) {
			var pos = Vector3.zero;
			for(int j=0; j<3; j++)
				if(boneMuscles[i, j] >= 0)
					pos[j] = humanPose.muscles[boneMuscles[i, j]];
			proxies[i].localPosition = pos;
		}
		recorder.TakeSnapshot(deltaTime);
	}
	public void SaveToClip(AnimationClip clip, float fps=60) {
		if(!recorder.isRecording) {
			clip.ClearCurves();
			clip.frameRate = fps;
			return;
		}

		recorder.SaveToClip(clip, fps);

		var rootCurves = new AnimationCurve[7];
		var muscleCurves = new AnimationCurve[HumanTrait.MuscleCount];
		getProxyCurves(clip, rootCurves, muscleCurves);
		clearProxyCurves(clip);

		// set BakeIntoPose = true, BasedUpon = origin
		var so = new SerializedObject(clip);
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendOrientation").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionY").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionXZ").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_KeepOriginalOrientation").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_KeepOriginalPositionY").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_KeepOriginalPositionXZ").boolValue = true;
		so.ApplyModifiedProperties();

		setHumanCurves(clip, rootCurves, muscleCurves);
	}
}
// a simple UI for AnimRecorder
class AnimRecorderWindow : EditorWindow {
	[MenuItem("CONTEXT/Animator/RecordAnimation")]
	static void RecordAnimation(MenuCommand command) {
		var animator = (Animator)command.context;
		var window = EditorWindow.GetWindow<AnimRecorderWindow>("RecordAnimation");
		window.Show();
		window.animator = animator;
	}

	AnimRecorder recorder = null;
	Animator animator = null;
	AnimationClip clip = null;
	int frameRate = 30;
	void OnGUI() {
		animator = (Animator)EditorGUILayout.ObjectField("Animator", animator, typeof(Animator), true);
		clip = (AnimationClip)EditorGUILayout.ObjectField("Clip", clip, typeof(AnimationClip), false);
		frameRate = EditorGUILayout.IntSlider("Frame rate", frameRate, 1, 120);

		if(recorder == null) {
			if(GUILayout.Button("Start")) {
				recorder = new AnimRecorder(animator);
			}
		} else if(!EditorApplication.isPlaying) {
			recorder.Dispose();
			recorder = null;
		} else {
			if(GUILayout.Button("Stop")) {
				clip.ClearCurves();
				recorder.SaveToClip(clip, frameRate);
				recorder.Dispose();
				recorder = null;
				AssetDatabase.SaveAssets();
			}
		}
	}
	void Update() {
		if(!EditorApplication.isPlaying || EditorApplication.isPaused)
			return;
		if(recorder != null)
			recorder.TakeSnapshot(Time.deltaTime);
	}
}
}
#endif