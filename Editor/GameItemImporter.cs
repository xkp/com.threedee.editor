using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class GameItemImporter : EditorWindow
{
	private string itemPath = "";
	private string moduleFolder = "";
	private string assetFolder = "";

	// Debug UI state
	private enum RunState { Idle, Running, Done, Failed, Canceled }
	private RunState _state = RunState.Idle;
	private string _log = "";
	private Vector2 _logScroll;
	private Exception _error;

	// Async tracking
	private Task _runTask;
	private CancellationTokenSource _cts;

	[MenuItem("Tools/BG Debug/Game Item Importer")]
	public static void ShowWindow()
	{
		GetWindow<GameItemImporter>("Game Item Importer");
	}

	private void OnEnable()
	{
		EditorApplication.update += Tick;
	}

	private void OnDisable()
	{
		EditorApplication.update -= Tick;
		CancelRun();
		_cts?.Dispose();
		_cts = null;
	}

	private void Tick()
	{
		// Keep UI updating while async work runs
		if (_state == RunState.Running) Repaint();
	}

	void OnGUI()
	{
		GUILayout.Label("Game Item Importer", EditorStyles.boldLabel);

		DrawPathRow("Item Path", ref itemPath);
		DrawPathRow("Module Folder", ref moduleFolder);
		DrawPathRow("Asset Folder", ref assetFolder);


		GUILayout.Space(12);

		// State line
		EditorGUILayout.LabelField("State", _state.ToString());

		if (_state == RunState.Failed && _error != null)
			EditorGUILayout.HelpBox(_error.ToString(), MessageType.Error);

		GUILayout.Space(8);

		// Buttons
		using (new EditorGUILayout.HorizontalScope())
		{
			using (new EditorGUI.DisabledScope(_state == RunState.Running))
			{
				if (GUILayout.Button("Import", GUILayout.Height(28)))
					StartRun(resetLog: false);

				if (GUILayout.Button("Run again (reset)", GUILayout.Height(28)))
					StartRun(resetLog: true);
			}

			using (new EditorGUI.DisabledScope(_state != RunState.Running))
			{
				if (GUILayout.Button("Cancel", GUILayout.Height(28)))
					CancelRun();
			}
		}

		GUILayout.Space(10);

		// Log
		GUILayout.Label("Log", EditorStyles.boldLabel);
		_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(140));
		EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
		EditorGUILayout.EndScrollView();
	}

	private void DrawPathRow(string label, ref string value)
	{
		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.LabelField(label, GUILayout.Width(90));
			value = EditorGUILayout.TextField(value);

			if (GUILayout.Button("Browse", GUILayout.Width(75)))
			{
				string path = EditorUtility.OpenFolderPanel($"Select {label}", "", "");
				if (!string.IsNullOrEmpty(path))
					value = path;
			}
		}
	}

	private void StartRun(bool resetLog)
	{
		if (string.IsNullOrWhiteSpace(itemPath) || string.IsNullOrWhiteSpace(moduleFolder))
		{
			Append("Please set both Item Path and Module Folder.");
			return;
		}

		if (resetLog) _log = "";
		_error = null;

		// Cancel any previous run
		CancelRun();
		_cts?.Dispose();
		_cts = new CancellationTokenSource();

		_state = RunState.Running;
		Append($"Importing items from: {itemPath}");
		Append($"Using module folder: {moduleFolder}");

		var postProcess = new List<PostProcessNode>();

		// Fire-and-track (don’t await in OnGUI)
		_runTask = RunImportAsync(itemPath, moduleFolder, assetFolder, postProcess, _cts.Token);

		// Optional: immediately repaint
		Repaint();
	}

	private void CancelRun()
	{
		if (_state == RunState.Running)
		{
			_cts?.Cancel();
			Append("Cancel requested...");
		}
	}

	private async Task RunImportAsync(string itemPath, string moduleFolder, string assetPath, List<PostProcessNode> postProcess, CancellationToken ct)
	{
		try
		{
			// If BigGameLoader.Load does NOT accept a token, remove ct and Cancel button becomes “best effort”.
			// Preferred signature:
			//   Task BigGameLoader.Load(string itemPath, string moduleFolder, List<PostProcessNode> postProcess, CancellationToken ct)

			await BigGameLoader.Load(itemPath, moduleFolder, assetPath, postProcess /*, ct*/);

			_state = RunState.Done;
			Append("Import complete.");
		}
		catch (OperationCanceledException)
		{
			_state = RunState.Canceled;
			Append("Import canceled.");
		}
		catch (Exception ex)
		{
			_state = RunState.Failed;
			_error = ex;
			Append("Import failed: " + ex.Message);
		}
		finally
		{
			Repaint();
		}
	}

	private void Append(string msg)
	{
		_log += (string.IsNullOrEmpty(_log) ? "" : "\n") + msg;
		// Also to Console if you want:
		// Debug.Log(msg);
	}
}
