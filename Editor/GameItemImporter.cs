using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class GameItemImporter : EditorWindow
{
	private string itemPath = "";
	private string moduleFolder = "";

	[MenuItem("Tools/Game Item Importer")]
	public static void ShowWindow()
	{
		GetWindow<GameItemImporter>("Game Item Importer");
	}

	void OnGUI()
	{
		GUILayout.Label("Game Item Importer", EditorStyles.boldLabel);

		// Item Path field with Browse button
		GUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Item Path", GUILayout.Width(80));
		itemPath = EditorGUILayout.TextField(itemPath);
		if (GUILayout.Button("Browse", GUILayout.Width(75)))
		{
			string path = EditorUtility.OpenFolderPanel("Select Item Path", "", "");
			if (!string.IsNullOrEmpty(path))
			{
				itemPath = path;
			}
		}
		GUILayout.EndHorizontal();

		// Module Folder field with Browse button
		GUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Module Folder", GUILayout.Width(80));
		moduleFolder = EditorGUILayout.TextField(moduleFolder);
		if (GUILayout.Button("Browse", GUILayout.Width(75)))
		{
			string path = EditorUtility.OpenFolderPanel("Select Module Folder", "", "");
			if (!string.IsNullOrEmpty(path))
			{
				moduleFolder = path;
			}
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(20);

		// Import button - add your import logic here
		if (GUILayout.Button("Import"))
		{
			Debug.Log("Importing items from: " + itemPath);
			Debug.Log("Using module folder: " + moduleFolder);
			// TODO: Implement your import logic here.
			var postProcess = new List<PostProcessNode>();
			BigGameLoader.Load(itemPath, moduleFolder, postProcess);
		}
	}
}
