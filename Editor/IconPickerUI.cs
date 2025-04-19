using System;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;

public static class IconPickerUI
{
	private static string[] predefinedIcons = { "none", "custom", "animals", "enemy", "grass", "logic", "npc", "stone", "tree", "weapons", "objective" };
	private static int selectedIndex = 0;

	public static string DrawIconField(string currentValue, Func<string, string> uploadFn)
	{
		if (string.IsNullOrEmpty(currentValue))
			selectedIndex = 0;
		else 
		{
			var index = Array.IndexOf(predefinedIcons, currentValue);
			if (index >= 0)
				selectedIndex = index;
			else
				selectedIndex = 1; //custom
		}

		EditorGUILayout.BeginHorizontal();

		GUILayout.Label("Icon:", GUILayout.Width(148));

		selectedIndex = EditorGUILayout.Popup(selectedIndex, predefinedIcons, GUILayout.Width(218));
		if (selectedIndex == 1)
		{
			GUIStyle style = new GUIStyle(EditorStyles.label)
			{
				wordWrap = false, // Ensure it doesn't wrap, we want single-line truncation
				alignment = TextAnchor.MiddleRight, // Align text to the left
				
			};

			// Calculate the available width for the label
			var availableWidth = 150f;
			Rect labelRect = new Rect(10, 10, availableWidth, 20); // Position and width

			// If the label text is too long, add ellipsis at the start
			string displayText = Path.GetFileName(currentValue);
			if (style.CalcSize(new GUIContent(displayText)).x > availableWidth)
			{
				displayText = "..." + displayText.Substring(currentValue.Length - Mathf.FloorToInt((availableWidth / style.CalcSize(new GUIContent(currentValue)).x) * currentValue.Length));
			}

			// Draw the label with the new text
			//EditorGUI.LabelField(labelRect, displayText, style); 

			GUILayout.Label(displayText);
		}
		else
		{
			currentValue = predefinedIcons[selectedIndex];
		}

		if (GUILayout.Button("Upload...", GUILayout.Width(120)))
		{
			string path = EditorUtility.OpenFilePanel("Select Icon Image", "", "png,jpg,jpeg");
			if (!string.IsNullOrEmpty(path))
			{
				string relativePath = uploadFn(path);
				currentValue = relativePath;
				selectedIndex = 1;
			}
		}
		EditorGUILayout.EndHorizontal();
		return currentValue;
	}
}
