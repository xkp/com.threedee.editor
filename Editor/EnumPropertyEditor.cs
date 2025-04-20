using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Unity.Plastic.Newtonsoft.Json.Linq;
using System;

public class EnumPropertyEditor : EditorWindow
{
	private string inputJson = "[]";
	private List<JsonObject> jsonObjects = new List<JsonObject>();
	private Func<string, string> iconCallback;

	private class JsonObject
	{
		public string name;
		public string icon;
	}

	private static string resultJson = null; // Static variable to hold the result

	// This method opens the editor window with the provided JSON string and waits for a result
	public static string OpenWindow(string jsonString, Func<string, string> iconCallback)
	{
		EnumPropertyEditor window = GetWindow<EnumPropertyEditor>("Enum");
		if (!string.IsNullOrEmpty(jsonString))
			window.inputJson = jsonString; // Set the passed JSON string as input

		window.iconCallback = iconCallback;
		window.ParseInput();
		window.ShowModal(); // Show the window as a modal, blocking until the result is ready
		return resultJson; // Return the modified JSON after the user clicks "Accept Values"
	}

	private void ParseInput()
	{
		// Parse the input JSON array string into a list of JsonObjects
		try
		{
			JArray parsedJsonArray = JArray.Parse(inputJson);
			foreach (JObject obj in parsedJsonArray)
			{
				jsonObjects.Add(new JsonObject
				{
					name = obj["name"]?.ToString(),
					icon = obj["icon"]?.ToString()
				});
			}
		}
		catch (System.Exception)
		{
			Debug.LogError("Invalid JSON format.");
		}
	}

	private void OnGUI()
	{
		// Display and edit each JSON object in the array
		for (int i = 0; i < jsonObjects.Count; i++)
		{
			JsonObject obj = jsonObjects[i];
			GUILayout.BeginHorizontal();

			obj.name = EditorGUILayout.TextField("Name", obj.name); 

			if (GUILayout.Button("Remove", GUILayout.Width(70)))
			{
				jsonObjects.RemoveAt(i);
				i--; // Adjust index after removal
			}

			GUILayout.EndHorizontal();

			obj.icon = IconPickerUI.DrawIconField(obj.icon, (path) => {
				return iconCallback(path);
			});
		}

		GUILayout.Space(10);

		// Button to add a new object to the array
		if (GUILayout.Button("Add Enum Item"))
		{
			jsonObjects.Add(new JsonObject { name = "", icon = "" });
		}

		GUILayout.Space(20);

		// Button to process and return the modified JSON string
		if (GUILayout.Button("Accept Values"))
		{
			// Create the modified JSON array
			JArray outputJsonArray = new JArray();
			foreach (JsonObject obj in jsonObjects)
			{
				outputJsonArray.Add(new JObject
				{
					{ "name", obj.name },
					{ "icon", obj.icon }
				});
			}

			// Serialize the updated JSON array
			resultJson = outputJsonArray.ToString();

			// Copy the result to clipboard
			EditorGUIUtility.systemCopyBuffer = resultJson;

			// Close the window after returning the modified JSON
			Close();
		}
	}
}
