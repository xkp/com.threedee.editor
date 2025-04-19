using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;
using System.Collections.Generic;

public class GameItemPropertyEditor : EditorWindow
{
	private string inputJson = "{\"type\": \"Other\", \"category\": \"\"}";
	private int typeValue = 0;
	private string categoryValue = "";

	private static string[] typeOptions = new string[] { "Game", "Character", "Nature", "Props", "Other" };

	private static string resultJson = null; // Static variable to hold the result

	// This method opens the editor window with the provided JSON string and waits for a result
	public static string OpenWindow(string jsonString)
	{
		GameItemPropertyEditor window = GetWindow<GameItemPropertyEditor>("GameItem");
		if (!string.IsNullOrEmpty(jsonString))
			window.inputJson = jsonString; // Set the passed JSON string as input

		try
		{
			// Parse the JSON string into a JObject
			JObject parsedJson = JObject.Parse(window.inputJson);
			var typeValueText = parsedJson["type"]?.ToString();
			window.typeValue = new List<string>(typeOptions).IndexOf(typeValueText);
			if (window.typeValue < 0)
			{
				window.typeValue = 0;
			}
			window.categoryValue = parsedJson["category"]?.ToString();
		}
		catch (System.Exception)
		{
		}

		window.ShowModal(); // Show the window as a modal, blocking until the result is ready
		return resultJson; // Return the modified JSON after the user clicks "Accept Values"
	}

	private void OnGUI()
	{
		// Type dropdown
		GUILayout.Space(10);
		EditorGUILayout.LabelField("Select Type");
		typeValue = EditorGUILayout.Popup(typeValue, typeOptions);

		// Category text field
		GUILayout.Space(10);
		EditorGUILayout.LabelField("Category");
		categoryValue = EditorGUILayout.TextField(categoryValue);

		GUILayout.Space(20);

		// Button to process and return the modified JSON string
		if (GUILayout.Button("Accept Values"))
		{
			// Create the modified JSON object
			var outputJson = new JObject
			{
				{ "type", typeValue },
				{ "category", categoryValue }
			};

			// Serialize the updated JSON
			resultJson = outputJson.ToString();

			// Copy the result to clipboard
			EditorGUIUtility.systemCopyBuffer = resultJson;

			// Close the window after returning the modified JSON
			Close();
		}
	}
}
