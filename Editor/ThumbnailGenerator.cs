// Place this file inside an "Editor" folder.
using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class ThumbnailGenerator
{
	/// <summary>
	/// Renders a prefab to a square thumbnail using a hidden camera.
	/// Works in the Editor (and in play mode) without using AssetPreview.
	/// </summary>
	/// <param name="prefab">The prefab to render.</param>
	/// <param name="size">Width/height of the thumbnail in pixels.</param>
	/// <param name="backgroundColor">Background color. If null, uses transparent.</param>
	public static Texture2D RenderPrefabThumbnail(
		GameObject prefab,
		int size = 256,
		Color? backgroundColor = null
	)
	{
		if (prefab == null)
		{
			Debug.LogError("ThumbnailGenerator: prefab is null.");
			return null;
		}

		Color bg = backgroundColor ?? new Color(0, 0, 0, 0); // transparent by default

		// Root container so we can nuke everything easily
		GameObject root = new GameObject("ThumbnailRoot");
		root.hideFlags = HideFlags.HideAndDontSave;

		// Instantiate prefab
		GameObject instance = Object.Instantiate(prefab, root.transform);
		instance.transform.localPosition = Vector3.zero;
		instance.transform.localRotation = Quaternion.identity;
		instance.transform.localScale = Vector3.one;

		// Collect renderers for bounds
		Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
		if (renderers == null || renderers.Length == 0)
		{
			Debug.LogWarning("ThumbnailGenerator: prefab has no renderers.");
			SafeDestroy(root);
			return CreateEmptyTexture(size, bg);
		}

		// Calculate combined bounds
		Bounds bounds = renderers[0].bounds;
		for (int i = 1; i < renderers.Length; i++)
		{
			bounds.Encapsulate(renderers[i].bounds);
		}

		// Create camera
		GameObject camGO = new GameObject("ThumbnailCamera");
		camGO.hideFlags = HideFlags.HideAndDontSave;
		Camera cam = camGO.AddComponent<Camera>();
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = bg;
		cam.orthographic = false;
		cam.fieldOfView = 30f;
		cam.nearClipPlane = 0.01f;
		cam.farClipPlane = 1000f;
		cam.allowHDR = false;
		cam.allowMSAA = false;

		// Optional: simple light so things aren't pitch black
		GameObject lightGO = new GameObject("ThumbnailLight");
		lightGO.hideFlags = HideFlags.HideAndDontSave;
		Light light = lightGO.AddComponent<Light>();
		light.type = LightType.Directional;
		light.intensity = 1.2f;
		light.transform.rotation = Quaternion.Euler(45f, 45f, 0f);

		// Compute camera position from bounds & FOV
		Vector3 boundsCenter = bounds.center;
		Vector3 boundsExtents = bounds.extents;

		// Use the largest dimension in X/Y so we fit the object in a square
		float radius = Mathf.Max(boundsExtents.x, boundsExtents.y);
		float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;

		// Distance needed so the radius fits within the vertical FOV
		float distance = radius / Mathf.Tan(halfFovRad);

		// Add a bit of padding
		distance *= 1.1f;

		// Position camera "in front" of object, along -Z
		Vector3 cameraPos = boundsCenter + new Vector3(0, 0, -distance);
		cam.transform.position = cameraPos;
		cam.transform.LookAt(boundsCenter);

		// Light from above-ish
		light.transform.position = boundsCenter + new Vector3(0.3f, 1.0f, -0.3f);
		light.transform.LookAt(boundsCenter);

		// Render to RenderTexture
		RenderTexture rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
		rt.antiAliasing = 4;
		cam.targetTexture = rt;

		RenderTexture prevActive = RenderTexture.active;
		RenderTexture.active = rt;

		cam.Render();

		// Read into Texture2D
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
		tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
		tex.Apply(false, false);

		// Cleanup
		cam.targetTexture = null;
		RenderTexture.active = prevActive;

		rt.Release();
		SafeDestroy(rt);
		SafeDestroy(lightGO);
		SafeDestroy(camGO);
		SafeDestroy(root);

		return tex;
	}

#if UNITY_EDITOR
    /// <summary>
    /// Renders a prefab to PNG and writes it to disk.
    /// Example path: "Assets/Thumbnails/MyPrefab.png"
    /// </summary>
    public static void SavePrefabThumbnailPNG(
        GameObject prefab,
        string assetPath,
        int size = 256,
        Color? backgroundColor = null
    )
    {
        Texture2D tex = RenderPrefabThumbnail(prefab, size, backgroundColor);
        if (tex == null)
            return;

        byte[] pngData = tex.EncodeToPNG();
        if (pngData == null || pngData.Length == 0)
        {
            Debug.LogError("ThumbnailGenerator: Failed to encode PNG.");
            return;
        }

        string fullPath = Path.GetFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");

        File.WriteAllBytes(fullPath, pngData);
        Debug.Log($"ThumbnailGenerator: Wrote {fullPath}");

        // Import into Unity if path is inside Assets
        if (assetPath.StartsWith("Assets"))
        {
            AssetDatabase.ImportAsset(assetPath);
        }
    }
#endif

	// ---------- Helpers ---------- //

	private static Texture2D CreateEmptyTexture(int size, Color bg)
	{
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
		Color[] pixels = new Color[size * size];
		for (int i = 0; i < pixels.Length; i++)
			pixels[i] = bg;
		tex.SetPixels(pixels);
		tex.Apply();
		return tex;
	}

	private static void SafeDestroy(Object obj)
	{
		if (obj == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(obj);
        else
            Object.Destroy(obj);
#else
		Object.Destroy(obj);
#endif
	}
}
