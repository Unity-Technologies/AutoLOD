// From: https://github.com/Unity-Technologies/EditorVR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace Unity.AutoLOD.Utilities
{
	/// <summary>
	/// Object related EditorVR utilities
	/// </summary>
	public static class ObjectUtils
	{
		public static HideFlags hideFlags
		{
			get { return s_HideFlags; }
			set { s_HideFlags = value; }
		}
		static HideFlags s_HideFlags = HideFlags.DontSave;
        static List<GameObject> s_RootGameObjects = new List<GameObject>();

		public static GameObject Instantiate(GameObject prefab, Transform parent = null, bool worldPositionStays = true, bool runInEditMode = true, bool active = true)
		{
			var go = UnityObject.Instantiate(prefab, parent, worldPositionStays);
			if (worldPositionStays)
			{
				var goTransform = go.transform;
				var prefabTransform = prefab.transform;
				goTransform.position = prefabTransform.position;
				goTransform.rotation = prefabTransform.rotation;
			}

			go.SetActive(active);
			if (!Application.isPlaying && runInEditMode)
			{
				SetRunInEditModeRecursively(go, runInEditMode);
				go.hideFlags = hideFlags;
			}

			return go;
		}

		public static void RemoveAllChildren(GameObject obj)
		{
			var children = new List<GameObject>();
			foreach (Transform child in obj.transform)
				children.Add(child.gameObject);

			foreach (var child in children)
				UnityObject.Destroy(child);
		}

		public static bool IsInLayer(GameObject o, string s)
		{
			return o.layer == LayerMask.NameToLayer(s);
		}

		/// <summary>
		/// Create an empty VR GameObject.
		/// </summary>
		/// <param name="name">Name of the new GameObject</param>
		/// <param name="parent">Transform to parent new object under</param>
		/// <returns>The newly created empty GameObject</returns>
		public static GameObject CreateEmptyGameObject(string name = null, Transform parent = null)
		{
			GameObject empty = null;
			if (string.IsNullOrEmpty(name))
				name = "New Game Object";

#if UNITY_EDITOR
			empty = EditorUtility.CreateGameObjectWithHideFlags(name, hideFlags);
#else
			empty = new GameObject(name);
			empty.hideFlags = hideFlags;
#endif
			empty.transform.parent = parent;
			empty.transform.localPosition = Vector3.zero;

			return empty;
		}

		public static T CreateGameObjectWithComponent<T>(Transform parent = null, bool worldPositionStays = true) where T : Component
		{
			return (T)CreateGameObjectWithComponent(typeof(T), parent, worldPositionStays);
		}

		public static Component CreateGameObjectWithComponent(Type type, Transform parent = null, bool worldPositionStays = true)
		{
#if UNITY_EDITOR
			var component = EditorUtility.CreateGameObjectWithHideFlags(type.Name, hideFlags, type).GetComponent(type);
			if (!Application.isPlaying)
				SetRunInEditModeRecursively(component.gameObject, true);
#else
			var component = new GameObject(type.Name).AddComponent(type);
#endif
			component.transform.SetParent(parent, worldPositionStays);

			return component;
		}

		public static void SetLayerRecursively(GameObject root, int layer)
		{
			var transforms = root.GetComponentsInChildren<Transform>();
			for (var i = 0; i < transforms.Length; i++)
				transforms[i].gameObject.layer = layer;
		}

		public static Bounds GetBounds(Transform[] transforms)
		{
			Bounds? bounds = null;
			foreach (var go in transforms)
			{
				var goBounds = GetBounds(go);
				if (!bounds.HasValue)
				{
					bounds = goBounds;
				}
				else
				{
					goBounds.Encapsulate(bounds.Value);
					bounds = goBounds;
				}
			}
			return bounds ?? new Bounds();
		}

		public static Bounds GetBounds(Transform transform)
		{
			var b = new Bounds(transform.position, Vector3.zero);
			var renderers = transform.GetComponentsInChildren<Renderer>();
			for (int i = 0; i < renderers.Length; i++)
			{
				var r = renderers[i];
				if (r.bounds.size != Vector3.zero)
					b.Encapsulate(r.bounds);
			}

			// As a fallback when there are no bounds, collect all transform positions
			if (b.size == Vector3.zero)
			{
				var transforms = transform.GetComponentsInChildren<Transform>();
				foreach (var t in transforms)
					b.Encapsulate(t.position);
			}

			return b;
		}

	    public static IEnumerator GetBounds(List<Renderer> renderers, Action<Bounds> callback)
	    {
	        Bounds bounds = new Bounds();
	        for (int i = 0; i < renderers.Count; i++)
	        {
	            var r = renderers[i];
	            if (i == 0)
	                bounds = r.bounds;
                else
                    bounds.Encapsulate(r.bounds);

	            yield return null;
	        }

	        callback(bounds);
	    }

		public static void SetRunInEditModeRecursively(GameObject go, bool enabled)
		{
#if UNITY_EDITOR
			var monoBehaviours = go.GetComponents<MonoBehaviour>();
			foreach (var mb in monoBehaviours)
			{
				if (mb)
					mb.runInEditMode = enabled;
			}

			foreach (Transform child in go.transform)
			{
				SetRunInEditModeRecursively(child.gameObject, enabled);
			}
#endif
		}

		public static T AddComponent<T>(GameObject go) where T : Component
		{
			return (T)AddComponent(typeof(T), go);
		}

		public static Component AddComponent(Type type, GameObject go)
		{
			var component = go.AddComponent(type);
			SetRunInEditModeRecursively(go, true);
			return component;
		}

		static IEnumerable<Type> GetAssignableTypes(Type type, Func<Type, bool> predicate = null)
		{
			var list = new List<Type>();
			ForEachType(t =>
			{
				if (type.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract && (predicate == null || predicate(t)))
					list.Add(t);
			});

			return list;
		}

		public static void ForEachAssembly(Action<Assembly> callback)
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var assembly in assemblies)
			{
				try
				{
					callback(assembly);
				}
				catch (ReflectionTypeLoadException)
				{
					// Skip any assemblies that don't load properly
					continue;
				}
			}
		}

		public static void ForEachType(Action<Type> callback)
		{
			ForEachAssembly(assembly =>
			{
				var types = assembly.GetTypes();
				foreach (var t in types)
					callback(t);
			});
		}

		public static IEnumerable<Type> GetImplementationsOfInterface(Type type)
		{
			if (type.IsInterface)
				return GetAssignableTypes(type);

			return Enumerable.Empty<Type>();
		}

		public static IEnumerable<Type> GetExtensionsOfClass(Type type)
		{
			if (type.IsClass)
				return GetAssignableTypes(type);

			return Enumerable.Empty<Type>();
		}

		public static void Destroy(UnityObject o, float t = 0f)
		{
			if (Application.isPlaying)
			{
				UnityObject.Destroy(o, t);
			}
#if UNITY_EDITOR && UNITY_EDITORVR
			else
			{
				if (Mathf.Approximately(t, 0f))
					UnityObject.DestroyImmediate(o);
				else
					VRView.StartCoroutine(DestroyInSeconds(o, t));
			}
#endif
		}

		static IEnumerator DestroyInSeconds(UnityObject o, float t)
		{
			var startTime = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup <= startTime + t)
				yield return null;

			UnityObject.DestroyImmediate(o);
		}

		/// <summary>
		/// Strip "PPtr<> and $ from a string for getting a System.Type from SerializedProperty.type
		/// TODO: expose internal SerializedProperty.objectReferenceTypeString to remove this hack
		/// </summary>
		/// <param name="type">Type string</param>
		/// <returns>Nicified type string</returns>
		public static string NicifySerializedPropertyType(string type)
		{
			return type.Replace("PPtr<", "").Replace(">", "").Replace("$", "");
		}

		/// <summary>
		/// Search through all assemblies in the current AppDomain for a class that is assignable to UnityObject and matches the given weak name
		/// TODO: expose internal SerialzedProperty.ValidateObjectReferenceValue to remove his hack
		/// </summary>
		/// <param name="name">Weak type name</param>
		/// <returns>Best guess System.Type</returns>
		public static Type TypeNameToType(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(x => x.GetTypes())
				.FirstOrDefault(x => x.Name.Equals(name) && typeof(UnityObject).IsAssignableFrom(x));
		}

#if UNITY_EDITOR
		public static IEnumerator GetAssetPreview(UnityObject obj, Action<Texture> callback)
		{
			var texture = AssetPreview.GetAssetPreview(obj);

			while (AssetPreview.IsLoadingAssetPreview(obj.GetInstanceID()))
			{
				texture = AssetPreview.GetAssetPreview(obj);
				yield return null;
			}

			if (!texture)
				texture = AssetPreview.GetMiniThumbnail(obj);

			callback(texture);
		}

	    public static void CreateAssetFromObjects(UnityObject[] objects, string path)
	    {
	        var method = typeof(AssetDatabase).GetMethod("CreateAssetFromObjects", BindingFlags.NonPublic | BindingFlags.Static);
	        method.Invoke(null, new object[] { objects, path });
	    }
#endif

	    public static IEnumerator FindObjectsOfType<T>(List<T> objects) where T : Component
	    {
	        var scene = SceneManager.GetActiveScene();
	        s_RootGameObjects.Clear();
	        scene.GetRootGameObjects(s_RootGameObjects);
	        yield return null;

	        foreach (var go in s_RootGameObjects)
	        {
	            var children = go.GetComponentsInChildren<T>();
	            objects.AddRange(children);

	            yield return null;
	        }
	    }

	    public static IEnumerator FindGameObject(string name, Action<GameObject> callback, GameObject root = null)
	    {
	        if (root)
	        {
	            if (root.name == name)
	            {
	                callback(root);
	                yield break;
	            }

	            foreach (Transform child in root.transform)
	            {
	                yield return FindGameObject(name, callback, child.gameObject);
	                if (!root)
	                    yield break;
	            }
	        }
	        else
	        {
	            var scene = SceneManager.GetActiveScene();
	            s_RootGameObjects.Clear();
                scene.GetRootGameObjects(s_RootGameObjects);
	            foreach (var go in s_RootGameObjects)
	            {
	                yield return FindGameObject(name, callback, go);
	            }
	        }

	        yield return null;
	    }
    }
}
