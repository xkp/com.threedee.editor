using System;
using UnityEditor;
using UnityEngine;
using System.Linq;

public static class IconPickerUI
{
	private static string[] predefinedIcons = { "none", "custom", "animals", "enemy", "grass", "logic", "npc", "stone", "tree", "weapons" };
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

		GUILayout.Label("", GUILayout.Width(10));
		GUILayout.Label("Icon", EditorStyles.boldLabel, GUILayout.Width(118));

		selectedIndex = EditorGUILayout.Popup(selectedIndex, predefinedIcons, GUILayout.Width(218));
		if (selectedIndex == 1)
		{
			GUILayout.Label(currentValue, GUILayout.ExpandWidth(true));
		}
		else
		{
			currentValue = predefinedIcons[selectedIndex];
		}

		if (GUILayout.Button("Upload Custom Icon", GUILayout.Width(220)))
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
