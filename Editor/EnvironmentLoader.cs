using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class EnvironmentLoader : EditorWindow
{
	private string inputFolderPath = "";
	private string outputFolderPath = "";

	[MenuItem("Tools/Environment Loader")]
	public static void OpenWindow()
	{
		GetWindow<EnvironmentLoader>("Environment Loader");
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

		if (GUILayout.Button("Choose Output Folder"))
		{
			string path = EditorUtility.OpenFolderPanel("Select Output Folder", "", "");
			if (!string.IsNullOrEmpty(path))
			{
				outputFolderPath = path;
			}
		}

		if (!string.IsNullOrEmpty(outputFolderPath))
		{
			GUILayout.Label("Output Folder:");
			GUILayout.TextField(outputFolderPath);
		}

		GUILayout.Space(10);

		GUI.enabled = !string.IsNullOrEmpty(inputFolderPath) && !string.IsNullOrEmpty(outputFolderPath);
		if (GUILayout.Button("Accept"))
		{
			OnFoldersAccepted(inputFolderPath, outputFolderPath);
		}
		GUI.enabled = true;
	}

	private void OnFoldersAccepted(string inputFolder, string outputFolder)
	{
		var postProcess = new List<PostProcessNode>();
		ThreedeeLoader.Load(inputFolder, outputFolder, postProcess);
	}
}
