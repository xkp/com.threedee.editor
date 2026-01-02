using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class GameImporter : EditorWindow
{
	private string inputFolderPath = "";
	private string outputFolderPath = "";
	private string itemPath = "";
	private string moduleFolder = "";

	[MenuItem("Tools/BG Debug/Game Importer")]
	public static void OpenWindow()
	{
		GetWindow<GameImporter>("Game Importer");
	}

	private void OnGUI()
	{
		GUILayout.Label("Select Folders", EditorStyles.boldLabel);

		if (GUILayout.Button("Choose Input Folder"))
		{
			string path = EditorUtility.OpenFolderPanel("Select Input Folder", "", "");
			if (!string.IsNullOrEmpty(path))
			{
				inputFolderPath = path;
			}
		}

		if (!string.IsNullOrEmpty(inputFolderPath))
		{
			GUILayout.Label("Input Folder:");
			GUILayout.TextField(inputFolderPath);
		}

		GUILayout.Space(5);

		if (GUILayout.Button("Choose Unity Folder"))
		{
			string path = EditorUtility.OpenFolderPanel("Select Unity Folder", "", "");
			if (!string.IsNullOrEmpty(path))
			{
				outputFolderPath = path;
			}
		}

		if (!string.IsNullOrEmpty(outputFolderPath))
		{
			GUILayout.Label("Unity Folder:");
			GUILayout.TextField(outputFolderPath);
		}

		GUILayout.Space(5);

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


		GUI.enabled = !string.IsNullOrEmpty(inputFolderPath) && !string.IsNullOrEmpty(outputFolderPath);
		if (GUILayout.Button("Import"))
		{
			OnFoldersAccepted(inputFolderPath, outputFolderPath);
		}
		GUI.enabled = true;
	}

	private void OnFoldersAccepted(string inputFolder, string outputFolder)
	{
		var postProcess = new List<PostProcessNode>();
		ThreedeeLoader.Load(inputFolder, outputFolder, postProcess);
		//TODO: do the async workif neccesary, this utility is not much in use anymore
		//BigGameLoader.Load(itemPath, moduleFolder, postProcess);
	}
}
