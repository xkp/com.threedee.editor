using UnityEngine;
using UnityEditor;
using UnityEditorInternal; // for ReorderableList
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Codice.Utils;

public class ModuleExporter : EditorWindow
{
	private string moduleName = "";
	// Instead of a string, we now have a MonoScript reference.
	private string controllerClass = "";

	// Module Type property.
	private string moduleType = "Props";
	private readonly string[] allowedModuleTypes = new string[] { "Game", "Character", "Nature", "Props", "Other" };

	private List<ItemGroup> itemGroups = new List<ItemGroup>();
	// The exportPath is chosen by the user.
	private string exportPath = "";

	// Global properties stored as a list.
	private List<Property> globalProperties = new List<Property>();

	// Lists for Unity package files (copied into the module folder)
	private List<string> unityPackages = new List<string>();       // full paths to copied files
	private List<string> unityPackageNames = new List<string>();     // file names only

	private Vector2 scrollPosition;
	private Item selectedItem = null;

	// Stores foldout states for component sections.
	private Dictionary<int, bool> compFoldouts = new Dictionary<int, bool>();

	// Allowed types for properties.
	private readonly string[] allowedTypes = new string[] { "string", "int", "float", "bool", "object" };

	//A dictionary to track property foldout states (keyed by property key).
	private Dictionary<string, bool> propertyFoldouts = new Dictionary<string, bool>();

	// Where the module's assets will be stored
	private readonly string projectAssetsPath = "Assets/BigGame/ModuleAssets";


	[System.Serializable]
	public class Item
	{
		public int id;
		public string name;
		public string description;
		public bool unique = false;
		public bool notDraggable = false;
		public bool template = false;
		public GameObject prefab;
		public string prefabPath;
		public string icon;    // Path to the generated thumbnail (relative to the module folder)
		public string modelPath;
		// All properties are now stored in a single dictionary.
		// For component properties, the key is typically "ComponentName.FieldName" and its Property.component is set.
		// For manual properties, we generate a unique key.
		public Dictionary<string, Property> properties = new Dictionary<string, Property>();

		public Vector3 exportTranslation = Vector3.zero;
		public Vector3 exportRotation = Vector3.zero;
		public Vector3 exportScale = Vector3.one;
	}

	[System.Serializable]
	public class Property
	{
		public string name;
		public string type;
		public object value;
		public string editor;
		public string component;
	}

	[System.Serializable]
	public class ItemGroup
	{
		public string name;
		public string icon;
		public string category;
		public List<Item> items = new List<Item>();

		// For collapsibility (not serialized)
		[System.NonSerialized]
		public bool isExpanded = true;
	}

	[MenuItem("Tools/Big Game Exporter")]
	public static void ShowWindow()
	{
		ModuleExporter window = GetWindow<ModuleExporter>("Big Game Exporter");
		window.minSize = new Vector2(600, 400);
		window.InitModule();
	}

	private string loadedModuleFilePath = "";
	private void InitModule()
	{
		AskForExportFolder();

		// Ask the user if they want to load an existing module file.
		if (EditorUtility.DisplayDialog("Module Editor", "Would you like to load an existing module file?", "Yes", "No"))
		{
			string moduleFilePath = EditorUtility.OpenFilePanel("Select Module File", Application.dataPath, "bgm");
			if (!string.IsNullOrEmpty(moduleFilePath))
			{
				loadedModuleFilePath = moduleFilePath;
				LoadModuleFromFile(moduleFilePath);
			}
		}
	}

	private void LoadModuleFromFile(string filePath)
	{
		// Read the JSON file (assuming it is a valid ExportedModule JSON).
		string json = File.ReadAllText(filePath);
		ExportedModule mod = JsonUtility.FromJson<ExportedModule>(json);

		// Populate module settings.
		moduleName = mod.name;
		moduleType = mod.type;
		controllerClass = mod.controller;

		// Populate global properties.
		globalProperties.Clear();
		if (mod.properties != null)
		{
			foreach (var kvp in mod.properties)
			{
				var ep = kvp.Value;
				globalProperties.Add(new Property
				{
					name = ep.name,
					type = ep.type,
					value = ep.value,
					editor = ep.editor,
					component = ep.component
				});
			}
		}

		// Populate Unity package names.
		unityPackageNames.Clear();
		unityPackages.Clear();
		if (mod.packages != null)
		{
			foreach (var pkg in mod.packages)
			{
				unityPackageNames.Add(pkg);
			}
		}

		// Populate item groups.
		itemGroups.Clear();
		if (mod.itemGroups != null)
		{
			foreach (var exportedGroup in mod.itemGroups)
			{
				ItemGroup group = new ItemGroup();
				group.name = exportedGroup.name;
				group.icon = exportedGroup.icon;
				group.items = new List<Item>();
				group.category = exportedGroup.category;

				foreach (var exportedItem in exportedGroup.items)
				{
					Item item = new Item();
					item.id = exportedItem.id;
					item.name = exportedItem.name;
					item.description = exportedItem.description;
					item.unique = exportedItem.unique;
					item.notDraggable = exportedItem.notDraggable;
					item.template = exportedItem.template;
					item.prefabPath = exportedItem.prefab;
					item.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.prefabPath);
					item.icon = exportedItem.icon;
					item.modelPath = exportedItem.icon3d;
					item.exportTranslation = exportedItem.exportTranslation;
					item.exportRotation = exportedItem.exportRotation;
					item.exportScale = exportedItem.exportScale;

					// Reconstruct properties.
					item.properties = new Dictionary<string, Property>();
					if (exportedItem.properties != null)
					{
						foreach (var ep in exportedItem.properties)
						{
							// When loading, assume these are manual properties.
							item.properties.Add(ep.name, new Property
							{
								name = ep.name,
								type = ep.type,
								value = ep.value,
								editor = ep.editor,
								component = ep.component
							});
						}
					}
					group.items.Add(item);
				}
				itemGroups.Add(group);
			}
		}

		Debug.Log("Loaded module from " + filePath);
		Repaint();
	}

	private void AskForExportFolder()
	{
		exportPath = Path.Combine(Application.persistentDataPath, "BigGame");

		if (!Directory.Exists(exportPath))
			Directory.CreateDirectory(exportPath);
	}

	/// <summary>
	/// Returns the module folder. If the chosen exportPath�s name equals moduleName, we use exportPath directly;
	/// otherwise we create a subfolder.
	/// </summary>
	private string GetModuleFolder()
	{
		if (string.Equals(Path.GetFileName(exportPath), moduleName))
		{
			return exportPath;
		}
		else
		{
			return Path.Combine(exportPath, moduleName);
		}
	}

	public static string TranslateType(System.Type type)
	{
		if (type == typeof(string) || type == typeof(char))
			return "string";
		if (type == typeof(int) || type == typeof(short) || type == typeof(long))
			return "int";
		if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
			return "float";
		if (type == typeof(bool))
			return "bool";
		// For any type that doesn't match, we default to "object"
		return "object";
	}

	private void OnGUI()
	{
		EditorGUILayout.BeginVertical();

		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

		// --- Module Settings ---
		GUILayout.Label("Module Export Settings", EditorStyles.boldLabel);
		moduleName = EditorGUILayout.TextField("Module Name", moduleName);
		controllerClass = EditorGUILayout.TextField("Controller Class", controllerClass);

		int moduleTypeIndex = System.Array.IndexOf(allowedModuleTypes, moduleType);
		if (moduleTypeIndex < 0)
		{
			moduleTypeIndex = 0;
			moduleType = allowedModuleTypes[0];
		}
		moduleTypeIndex = EditorGUILayout.Popup("Module Type", moduleTypeIndex, allowedModuleTypes);
		moduleType = allowedModuleTypes[moduleTypeIndex];

		EditorGUILayout.Space();

		// --- Global Properties ---
		GUILayout.Label("GENERAL", EditorStyles.boldLabel);
		EditorGUILayout.BeginHorizontal();

		EditorGUILayout.BeginVertical();
		if (GUILayout.Button("Edit Global Properties"))
		{
			// Reuse the custom popup for global properties.
			CustomPropertiesPopup.ShowPopup(globalProperties);
		}
		for (int i = 0; i < globalProperties.Count; i++)
		{
			Property prop = globalProperties[i];
			GUILayout.Label($"{prop.name} ({prop.type}): {prop.value}");
		}
		EditorGUILayout.EndVertical();

		EditorGUILayout.BeginVertical();
		if (GUILayout.Button("Add Unity Package"))
		{
			if (string.IsNullOrEmpty(moduleName))
			{
				EditorUtility.DisplayDialog("Warning", "Please set the module name before adding a Unity package.", "OK");
			}
			else
			{
				string packageOriginalPath = EditorUtility.OpenFilePanel("Select Unity Package", "", "unitypackage");
				if (!string.IsNullOrEmpty(packageOriginalPath))
				{
					string moduleFolder = GetModuleFolder();
					Directory.CreateDirectory(moduleFolder);
					string packagesFolder = Path.Combine(moduleFolder, "Packages");
					Directory.CreateDirectory(packagesFolder);

					string destPath = Path.Combine(packagesFolder, Path.GetFileName(packageOriginalPath));
					File.Copy(packageOriginalPath, destPath, true);

					unityPackages.Add(destPath);
					var packageName = Path.GetFileName(destPath);
					if (!unityPackageNames.Contains(packageName))
						unityPackageNames.Add(packageName);
				}
			}
		}
		for (int i = 0; i < unityPackages.Count; i++)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(Path.GetFileName(unityPackages[i]));
			if (GUILayout.Button("Remove", GUILayout.Width(80)))
			{
				if (File.Exists(unityPackages[i]))
				{
					File.Delete(unityPackages[i]);
				}
				unityPackages.RemoveAt(i);
				unityPackageNames.RemoveAt(i);
				i--;
			}
			EditorGUILayout.EndHorizontal();
		}
		EditorGUILayout.EndVertical();
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		// --- Item Groups ---
		GUILayout.Label("ITEM GROUPS", EditorStyles.boldLabel);
		if (string.IsNullOrEmpty(moduleName))
		{
			EditorGUILayout.HelpBox("Set a Module Name before adding Item Groups.", MessageType.Warning);
		}
		if (GUILayout.Button("Add Item Group") && !string.IsNullOrEmpty(moduleName))
		{
			itemGroups.Add(new ItemGroup());
		}

		EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

		// Left Panel: List of item groups and their items.
		EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.6f));
		List<ItemGroup> groupsToRemove = new List<ItemGroup>();
		for (int i = 0; i < itemGroups.Count; i++)
		{
			ItemGroup group = itemGroups[i];
			EditorGUILayout.BeginHorizontal();
			group.isExpanded = EditorGUILayout.Foldout(group.isExpanded,
				string.IsNullOrEmpty(group.name) ? "New Group" : group.name, true);
			if (GUILayout.Button("Remove Group", GUILayout.Width(100)))
			{
				groupsToRemove.Add(group);
			}
			EditorGUILayout.EndHorizontal();

			if (group.isExpanded)
			{
				EditorGUI.indentLevel++;
				group.name = EditorGUILayout.TextField("Name", group.name);

				group.icon = IconPickerUI.DrawIconField(group.icon, CopyCustomIcon); //'EditorGUILayout.TextField("Icon", group.icon);
				
				group.category = EditorGUILayout.TextField("Category", group.category);

				if (string.IsNullOrEmpty(group.name))
				{
					EditorGUILayout.HelpBox("Set a name before adding items to this group.", MessageType.Warning);
				}
				if (GUILayout.Button("Add Items from Folder") && !string.IsNullOrEmpty(group.name))
				{
					string folderPath = EditorUtility.OpenFolderPanel("Select Prefab Folder", "", "");
					if (!string.IsNullOrEmpty(folderPath))
					{
						AddItemsFromFolder(group, folderPath);
					}
				}
				if (GUILayout.Button("Create Custom Item") && !string.IsNullOrEmpty(group.name))
				{
					CreateCustomItem(group);
				}
				if (GUILayout.Button("Generate Assets for Group"))
				{
					GenerateModelsForGroup(group);
				}
				int columns = Mathf.Max(1, Mathf.FloorToInt((position.width * 0.6f - 20f) / 70f));
				int count = 0;
				EditorGUILayout.BeginVertical();
				List<Item> itemsToRemove = new List<Item>();
				for (int j = 0; j < group.items.Count; j++)
				{
					if (count == 0)
						EditorGUILayout.BeginHorizontal();
					Item item = group.items[j];
					EditorGUILayout.BeginVertical(GUILayout.Width(70));
					Texture2D thumbnail = null;
					if (item.prefab != null)
					{
						thumbnail = AssetPreview.GetAssetPreview(item.prefab);
					}
					if (thumbnail == null)
					{
						if (!string.IsNullOrEmpty(item.icon))
						{
							string fullIconPath = Path.Combine(GetModuleFolder(), item.icon);
							thumbnail = LoadTextureFromFile(fullIconPath);
						}
						if (thumbnail == null)
						{
							thumbnail = EditorGUIUtility.IconContent("DefaultAsset Icon").image as Texture2D;
						}
					}
					if (GUILayout.Button(thumbnail ?? Texture2D.blackTexture, GUILayout.Width(64), GUILayout.Height(64)))
					{
						selectedItem = item;
					}
					if (GUILayout.Button("Remove", GUILayout.Width(64)))
					{
						itemsToRemove.Add(item);
					}
					EditorGUILayout.EndVertical();
					count++;
					if (count >= columns || j == group.items.Count - 1)
					{
						EditorGUILayout.EndHorizontal();
						count = 0;
					}
				}
				foreach (var item in itemsToRemove)
				{
					group.items.Remove(item);
				}
				EditorGUILayout.EndVertical();
				EditorGUI.indentLevel--;
			}
		}
		foreach (var group in groupsToRemove)
		{
			itemGroups.Remove(group);
		}
		EditorGUILayout.EndVertical();

		if (selectedItem != null)
		{
			bool found = false;
			foreach (var group in itemGroups)
			{
				if (group.isExpanded && group.items.Contains(selectedItem))
				{
					found = true;
					break;
				}
			}
			if (!found)
			{
				selectedItem = null;
			}
		}

		// Right Panel: Item Editor.
		EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
		if (selectedItem != null)
		{
			Texture2D thumb = null;
			if (selectedItem.prefab != null)
			{
				thumb = AssetPreview.GetAssetPreview(selectedItem.prefab);
			}
			if (thumb == null)
			{
				if (!string.IsNullOrEmpty(selectedItem.icon))
				{
					string fullIconPath = Path.Combine(GetModuleFolder(), selectedItem.icon);
					thumb = LoadTextureFromFile(fullIconPath);
				}
				if (thumb == null)
				{
					thumb = EditorGUIUtility.IconContent("DefaultAsset Icon").image as Texture2D;
				}
			}
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(thumb, GUILayout.Width(128), GUILayout.Height(128));

			EditorGUILayout.BeginVertical();
			selectedItem.exportTranslation = EditorGUILayout.Vector3Field("Translation", selectedItem.exportTranslation);
			selectedItem.exportRotation = EditorGUILayout.Vector3Field("Rotation", selectedItem.exportRotation);
			selectedItem.exportScale = EditorGUILayout.Vector3Field("Scale", selectedItem.exportScale);
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();

			GUILayout.Label($"EDITING: {selectedItem.name}", EditorStyles.boldLabel);
			selectedItem.name = EditorGUILayout.TextField("Item Name", selectedItem.name);
			selectedItem.description = EditorGUILayout.TextField("Description", selectedItem.description);
			selectedItem.unique = EditorGUILayout.Toggle("Unique", selectedItem.unique);
			selectedItem.notDraggable = EditorGUILayout.Toggle("Not Visual", selectedItem.notDraggable);
			selectedItem.template = EditorGUILayout.Toggle("Is Template", selectedItem.template);

			GUILayout.Label("ASSETS", EditorStyles.boldLabel);

			selectedItem.prefabPath = EditorGUILayout.TextField("Prefab Path", selectedItem.prefabPath);
			if (!string.IsNullOrEmpty(selectedItem.modelPath))
			{
				EditorGUILayout.LabelField("Model Path:", selectedItem.modelPath);
			}

			if (GUILayout.Button("Generate Assets"))
			{
				GenerateModel(selectedItem);
				GenerateThumbnail(selectedItem);
			}
			EditorGUILayout.Space();

			// --- Unified Properties List (Collapsible) ---
			GUILayout.Label("PROPERTIES", EditorStyles.boldLabel);

			// Build a unified list of properties from the dictionary.
			List<(Property prop, string key)> unifiedProps = new List<(Property, string)>();
			if (selectedItem.properties != null)
			{
				foreach (var kvp in selectedItem.properties)
				{
					unifiedProps.Add((kvp.Value, kvp.Key));
				}
			}

			// Display each property with a collapsible foldout.
			foreach (var entry in unifiedProps)
			{
				// Ensure a foldout state exists for this property.
				if (!propertyFoldouts.ContainsKey(entry.key))
				{
					propertyFoldouts[entry.key] = true;
				}

				// Build a header for the foldout.
				string header = $"{entry.prop.name} ({entry.prop.type})";
				if (!string.IsNullOrEmpty(entry.prop.component))
				{
					header += $" - {entry.prop.component}";
				}
				EditorGUILayout.BeginHorizontal();
				propertyFoldouts[entry.key] = EditorGUILayout.Foldout(propertyFoldouts[entry.key], header, true);
				if (GUILayout.Button("Remove", GUILayout.Width(70)))
				{
					selectedItem.properties.Remove(entry.key);
				}
				EditorGUILayout.EndHorizontal();

				if (propertyFoldouts[entry.key])
				{
					EditorGUILayout.BeginVertical("box");
					entry.prop.name = EditorGUILayout.TextField("Name", entry.prop.name);
					int typeIndex = System.Array.IndexOf(allowedTypes, entry.prop.type);
					if (typeIndex < 0)
					{
						typeIndex = 0;
						entry.prop.type = allowedTypes[0];
					}


					typeIndex = EditorGUILayout.Popup("Type", typeIndex, allowedTypes);
					entry.prop.type = allowedTypes[typeIndex];
					entry.prop.value = EditorGUILayout.TextField("Value", entry.prop.value != null ? entry.prop.value.ToString() : "");
					if (entry.prop.type == "object")
					{
						entry.prop.editor = EditorGUILayout.TextField("Editor", entry.prop.editor);
					}
					EditorGUILayout.EndVertical();
				}
			}

			EditorGUILayout.Space();

			// Two buttons to add new properties.
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add Property"))
			{
				// Create a new manual property with a unique key.
				string key = "custom_" + System.Guid.NewGuid().ToString();
				Property newProp = new Property()
				{
					name = "NewProperty",
					type = "string",
					value = "",
					editor = "",
					component = ""
				};
				selectedItem.properties.Add(key, newProp);
			}

			if (GUILayout.Button("Add Component Property"))
			{
				ComponentPropertiesPopup.ShowPopup(selectedItem);
			}
			EditorGUILayout.EndHorizontal();
		}
		else
		{
			GUILayout.Label("No item selected", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Click on an item thumbnail from the left panel to view and edit its details here.", MessageType.Info);
		}
		EditorGUILayout.EndVertical();

		EditorGUILayout.EndHorizontal();

		EditorGUILayout.EndScrollView();

		// EXPORT MODULE button.
		if (GUILayout.Button("EXPORT MODULE", GUILayout.Height(50)))
		{
			ExportModule();
		}

		EditorGUILayout.EndVertical();
	}

	public static uint ComputeFNV1aHash(string text)
	{
		const uint offsetBasis = 2166136261;
		const uint prime = 16777619;
		uint hash = offsetBasis;
		foreach (char c in text)
		{
			hash ^= c;
			hash *= prime;
		}
		return hash;
	}

	private void ExportModule()
	{
		AskForExportFolder();
		string moduleFolder = GetModuleFolder();
		Directory.CreateDirectory(moduleFolder);

		// Build your ExportedModule object, create the JSON file, etc.
		ExportedModule mod = new ExportedModule();
		// ... populate mod ...

		// Save the JSON file
		string jsonFilePath = loadedModuleFilePath;
		if (string.IsNullOrEmpty(jsonFilePath))
		{
			jsonFilePath = loadedModuleFilePath = Path.Combine(Application.dataPath, "module.bgm");
		}
		string json = JsonUtility.ToJson(mod, true);
		File.WriteAllText(jsonFilePath, json);
		Debug.Log("Exported module JSON to " + jsonFilePath);
		File.Copy(loadedModuleFilePath, Path.Combine(moduleFolder, Path.GetFileName(loadedModuleFilePath)), true);

		// Copy the assets from the project asset folder into the module folder.
		// For example, copy projectAssetsPath into a subfolder called "ModuleAssets" in moduleFolder.
		string tempAssetsDestination = Path.Combine(moduleFolder, "ModuleAssets");
		DirectoryCopy(projectAssetsPath, tempAssetsDestination, true);

		// For Game modules, if you still need to copy the project template, leave that here.
		if (moduleType == "Game")
		{
			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			string destTemplateFolder = Path.Combine(moduleFolder, "Template");
			Directory.CreateDirectory(destTemplateFolder);
			string sourceAssets = Path.Combine(projectRoot, "Assets");
			string sourceProjectSettings = Path.Combine(projectRoot, "ProjectSettings");

			DirectoryCopy(sourceAssets, Path.Combine(destTemplateFolder, "Assets"), true);
			if (Directory.Exists(sourceProjectSettings))
			{
				DirectoryCopy(sourceProjectSettings, Path.Combine(destTemplateFolder, "ProjectSettings"), true);
			}

			string sourcePackages = Path.Combine(projectRoot, "Packages");
			if (Directory.Exists(sourcePackages))
			{
				DirectoryCopy(sourcePackages, Path.Combine(destTemplateFolder, "Packages"), true);
			}

			Debug.Log("Copied template files to " + destTemplateFolder);
		}

		// Create the zip file from moduleFolder
		string zipFilePath = Path.Combine(Path.GetDirectoryName(moduleFolder), moduleName + ".3dbg");
		if (File.Exists(zipFilePath))
		{
			File.Delete(zipFilePath);
		}
		System.IO.Compression.ZipFile.CreateFromDirectory(moduleFolder, zipFilePath);
		Debug.Log("Created zip file: " + zipFilePath);
		EditorUtility.RevealInFinder(zipFilePath);
	}

	private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
	{
		DirectoryInfo dir = new DirectoryInfo(sourceDirName);
		if (!dir.Exists)
		{
			throw new DirectoryNotFoundException("Source directory does not exist: " + sourceDirName);
		}
		DirectoryInfo[] dirs = dir.GetDirectories();
		if (!Directory.Exists(destDirName))
		{
			Directory.CreateDirectory(destDirName);
		}
		FileInfo[] files = dir.GetFiles();
		foreach (FileInfo file in files)
		{
			string temppath = Path.Combine(destDirName, file.Name);
			file.CopyTo(temppath, true);
		}
		if (copySubDirs)
		{
			foreach (DirectoryInfo subdir in dirs)
			{
				string temppath = Path.Combine(destDirName, subdir.Name);
				DirectoryCopy(subdir.FullName, temppath, copySubDirs);
			}
		}
	}

	private ExportedProperty CopyProperty(Property prop)
	{
		ExportedProperty ep = new ExportedProperty();
		ep.name = prop.name;
		ep.type = prop.type;
		ep.value = prop.value != null ? prop.value.ToString() : "";
		ep.editor = prop.editor;
		ep.component = prop.component;
		return ep;
	}

	[System.Serializable]
	public class ExportedModule
	{
		public int id;
		public string name;
		public string type;
		public Dictionary<string, ExportedProperty> properties;
		public string controller;
		public List<string> packages;
		public List<ExportedGroup> itemGroups;
	}

	[System.Serializable]
	public class ExportedGroup
	{
		public string name;
		public string icon;
		public string category;
		public List<ExportedItem> items;
	}

	[System.Serializable]
	public class ExportedItem
	{
		public int id;
		public string name;
		public string description;
		public string category;
		public bool unique = false;
		public bool notDraggable = false;
		public bool template = false;
		public string prefab;
		public string icon;
		public string icon3d;
		public List<ExportedProperty> properties;
		public Vector3 exportTranslation = Vector3.zero;
		public Vector3 exportRotation = Vector3.zero;
		public Vector3 exportScale = Vector3.one;
	}

	[System.Serializable]
	public class ExportedProperty
	{
		public string name;
		public string type;
		public object value;
		public string editor;
		public string component;
	}

	private void CreateCustomItem(ItemGroup group)
	{
		Item newItem = new Item();
		newItem.id = Random.Range(1, 1000);
		newItem.name = "Custom Item";
		newItem.prefab = null;
		newItem.prefabPath = "";
		newItem.icon = "";
		newItem.modelPath = "";
		newItem.properties = new Dictionary<string, Property>();
		newItem.exportTranslation = Vector3.zero;
		newItem.exportRotation = Vector3.zero;
		newItem.exportScale = Vector3.one;
		group.items.Add(newItem);
	}

	private void AddItemsFromFolder(ItemGroup group, string folderPath)
	{
		string relativePath = "Assets" + folderPath.Substring(Application.dataPath.Length);
		string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { relativePath });
		foreach (string guid in guids)
		{
			string assetPath = AssetDatabase.GUIDToAssetPath(guid);
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
			if (prefab != null)
			{
				group.items.Add(new Item
				{
					id = Random.Range(1, 1000),
					name = prefab.name,
					prefab = prefab,
					prefabPath = assetPath,
					properties = new Dictionary<string, Property>(),
					exportTranslation = Vector3.zero,
					exportRotation = Vector3.zero,
					exportScale = Vector3.one
				});
			}
		}
	}

	private void GenerateModelsForGroup(ItemGroup group)
	{
		foreach (var item in group.items)
		{
			GenerateModel(item);
			GenerateThumbnail(item);
		}
		Debug.Log($"Models and thumbnails for group '{group.name}' extracted successfully!");
	}

	private void GenerateModel(Item item)
	{
		// Use the project asset folder rather than the export folder.
		string modelsDirectory = Path.Combine(projectAssetsPath, "Models");
		if (!Directory.Exists(modelsDirectory))
			Directory.CreateDirectory(modelsDirectory);

		string modelPath = Path.Combine(modelsDirectory, item.name + ".obj");

		if (item.prefab != null)
		{
			Mesh mesh = GetLowestLODMesh(item.prefab);
			if (mesh == null)
			{
				Debug.LogWarning($"No valid mesh found for item: {item.name}");
				return;
			}
			SaveMeshAsOBJ(mesh, modelPath, item.exportTranslation, item.exportRotation, item.exportScale);
			// Store the relative path relative to the projectAssetsPath.
			item.modelPath = Path.Combine("Models", item.name + ".obj");
		}
	}

	private void SaveMeshAsOBJ(Mesh mesh, string path, Vector3 translation, Vector3 rotation, Vector3 scale)
	{
		Matrix4x4 transformation = Matrix4x4.TRS(translation, Quaternion.Euler(rotation), scale);
		Vector3[] transformedVertices = new Vector3[mesh.vertices.Length];
		for (int i = 0; i < mesh.vertices.Length; i++)
		{
			transformedVertices[i] = transformation.MultiplyPoint3x4(mesh.vertices[i]);
		}
		Vector3[] transformedNormals = new Vector3[mesh.normals.Length];
		for (int i = 0; i < mesh.normals.Length; i++)
		{
			transformedNormals[i] = transformation.MultiplyVector(mesh.normals[i]).normalized;
		}
		StringBuilder sb = new StringBuilder();
		sb.AppendLine($"o {mesh.name}");
		foreach (Vector3 v in transformedVertices)
			sb.AppendLine($"v {v.x} {v.y} {v.z}");
		foreach (Vector3 n in transformedNormals)
			sb.AppendLine($"vn {n.x} {n.y} {n.z}");
		foreach (Vector2 uv in mesh.uv)
			sb.AppendLine($"vt {uv.x} {uv.y}");
		for (int i = 0; i < mesh.triangles.Length; i += 3)
			sb.AppendLine($"f {mesh.triangles[i] + 1} {mesh.triangles[i + 1] + 1} {mesh.triangles[i + 2] + 1}");
		File.WriteAllText(path, sb.ToString());
	}

	private void GenerateThumbnail(Item item)
	{
		if (item.prefab == null)
			return;

		string thumbDirectory = Path.Combine(projectAssetsPath, "Thumbnails");
		if (!Directory.Exists(thumbDirectory))
			Directory.CreateDirectory(thumbDirectory);

		Texture2D preview = AssetPreview.GetAssetPreview(item.prefab);
		if (preview == null)
		{
			preview = EditorGUIUtility.IconContent("DefaultAsset Icon").image as Texture2D;
		}
		if (preview != null)
		{
			byte[] pngData = preview.EncodeToPNG();
			if (pngData != null)
			{
				string thumbPath = Path.Combine(thumbDirectory, item.name + ".png");
				File.WriteAllBytes(thumbPath, pngData);
				// Store the relative path relative to projectAssetsPath.
				item.icon = Path.Combine("Thumbnails", item.name + ".png");
			}
		}
	}

	private string CopyCustomIcon(string imagePath)
	{
		string moduleFolder = GetModuleFolder();
		string assetsDirectory = Path.Combine(moduleFolder, "Assets");
		Directory.CreateDirectory(assetsDirectory);
		string thumbDirectory = Path.Combine(assetsDirectory, "Thumbnails");
		Directory.CreateDirectory(thumbDirectory);

		var filename = Path.GetFileName(imagePath);
		File.Copy(imagePath, Path.Combine(thumbDirectory, filename));

		return Path.Combine("Assest", "Thumbnails", filename);
	}

	private Mesh GetLowestLODMesh(GameObject prefab)
	{
		LODGroup lodGroup = prefab.GetComponent<LODGroup>();
		if (lodGroup != null && lodGroup.GetLODs().Length > 0)
		{
			return ExtractMesh(lodGroup.GetLODs()[lodGroup.GetLODs().Length - 1].renderers[0]);
		}
		MeshRenderer meshRenderer = prefab.GetComponentInChildren<MeshRenderer>();
		return meshRenderer != null ? ExtractMesh(meshRenderer) : prefab.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh;
	}

	private Mesh ExtractMesh(Renderer renderer)
	{
		return renderer is MeshRenderer meshRenderer ? meshRenderer.GetComponent<MeshFilter>()?.sharedMesh : null;
	}

	private Texture2D LoadTextureFromFile(string filePath)
	{
		if (!File.Exists(filePath))
			return null;
		byte[] data = File.ReadAllBytes(filePath);
		Texture2D tex = new Texture2D(2, 2);
		tex.LoadImage(data);
		return tex;
	}

	private bool IsSimpleType(System.Type type)
	{
		if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
			return true;
		if (type == typeof(Vector2) || type == typeof(Vector3) ||
			type == typeof(Vector4) || type == typeof(Color))
			return true;
		return false;
	}
}

public class CustomPropertiesPopup : EditorWindow
{
	private List<ModuleExporter.Property> properties;
	private ReorderableList reorderableList;
	private readonly string[] allowedTypes = new string[] { "string", "int", "float", "bool", "object" };

	public static void ShowPopup(List<ModuleExporter.Property> properties)
	{
		CustomPropertiesPopup window = ScriptableObject.CreateInstance<CustomPropertiesPopup>();
		window.properties = properties;
		window.InitReorderableList();
		window.titleContent = new GUIContent("Edit Global Properties");
		window.minSize = new Vector2(400, 300);
		window.ShowUtility();
	}

	private void InitReorderableList()
	{
		reorderableList = new ReorderableList(properties, typeof(ModuleExporter.Property), true, true, true, true);
		reorderableList.drawHeaderCallback = (Rect rect) =>
		{
			EditorGUI.LabelField(rect, "Global Properties");
		};

		reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
		{
			if (index < properties.Count)
			{
				var prop = properties[index];
				float labelW = 50f;
				float nameFieldW = 150f;
				float typeLabelW = 40f;
				float typeFieldW = 80f;
				float valueLabelW = 50f;
				float valueFieldW = 150f;
				float editorLabelW = 50f;
				float editorFieldW = 80f;
				float x = rect.x;
				float y = rect.y + 2;

				Rect r = new Rect(x, y, labelW, EditorGUIUtility.singleLineHeight);
				EditorGUI.LabelField(r, "Name");
				x += labelW;
				r = new Rect(x, y, nameFieldW, EditorGUIUtility.singleLineHeight);
				prop.name = EditorGUI.TextField(r, prop.name);
				x += nameFieldW;

				Rect r2 = new Rect(x, y, typeLabelW, EditorGUIUtility.singleLineHeight);
				EditorGUI.LabelField(r2, "Type");
				x += typeLabelW;
				int typeIndex = System.Array.IndexOf(allowedTypes, prop.type);
				if (typeIndex < 0)
				{
					typeIndex = 0;
					prop.type = allowedTypes[0];
				}
				Rect r3 = new Rect(x, y, typeFieldW, EditorGUIUtility.singleLineHeight);
				typeIndex = EditorGUI.Popup(r3, typeIndex, allowedTypes);
				prop.type = allowedTypes[typeIndex];
				x += typeFieldW;

				Rect r4 = new Rect(x, y, valueLabelW, EditorGUIUtility.singleLineHeight);
				EditorGUI.LabelField(r4, "Value");
				x += valueLabelW;
				Rect r5 = new Rect(x, y, valueFieldW, EditorGUIUtility.singleLineHeight);
				string currentVal = prop.value != null ? prop.value.ToString() : "";
				string newVal = EditorGUI.TextField(r5, currentVal);
				if (newVal != currentVal)
				{
					prop.value = newVal;
				}
				x += valueFieldW;

				if (prop.type == "object")
				{
					Rect r6 = new Rect(x, y, editorLabelW, EditorGUIUtility.singleLineHeight);
					EditorGUI.LabelField(r6, "Editor");
					x += editorLabelW;
					Rect r7 = new Rect(x, y, editorFieldW, EditorGUIUtility.singleLineHeight);
					prop.editor = EditorGUI.TextField(r7, prop.editor);
				}
			}
		};

		reorderableList.onAddCallback = (ReorderableList list) =>
		{
			properties.Add(new ModuleExporter.Property { name = "NewProperty", type = "string", value = "", editor = "", component = "" });
		};

		reorderableList.onRemoveCallback = (ReorderableList list) =>
		{
			properties.RemoveAt(list.index);
		};
	}

	private void OnGUI()
	{
		reorderableList.DoLayoutList();
		if (GUILayout.Button("Close"))
		{
			Close();
		}
	}
}

public class ComponentPropertiesPopup : EditorWindow
{
	private ModuleExporter.Item selectedItem;
	private Vector2 scrollPos;
	// A list of available fields to add. Each entry contains the component, its field, and a key string.
	private List<(MonoBehaviour comp, FieldInfo field, string key)> availableFields = new List<(MonoBehaviour, FieldInfo, string)>();
	// Allowed types.
	private readonly string[] allowedTypes = new string[] { "string", "int", "float", "bool", "object" };

	public static void ShowPopup(ModuleExporter.Item item)
	{
		ComponentPropertiesPopup window = ScriptableObject.CreateInstance<ComponentPropertiesPopup>();
		window.selectedItem = item;
		window.titleContent = new GUIContent("Add Component Property");
		window.position = new Rect(Screen.width / 2, Screen.height / 2, 400, 300);
		window.PopulateAvailableFields();
		window.ShowUtility();
	}

	private void PopulateAvailableFields()
	{
		availableFields.Clear();
		if (selectedItem.prefab == null)
			return;

		MonoBehaviour[] comps = selectedItem.prefab.GetComponents<MonoBehaviour>();
		foreach (var comp in comps)
		{
			if (comp == null) continue;
			FieldInfo[] allFields = comp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
			foreach (var field in allFields)
			{
				// Skip Unity internal fields.
				if (field.DeclaringType == typeof(MonoBehaviour) ||
					(field.DeclaringType.Namespace != null && field.DeclaringType.Namespace.StartsWith("UnityEngine")))
					continue;
				// Only allow simple types.
				if (!IsSimpleType(field.FieldType))
					continue;
				string key = comp.GetType().Name + "." + field.Name;
				if (selectedItem.properties == null || !selectedItem.properties.ContainsKey(key))
				{
					availableFields.Add((comp, field, key));
				}
			}
		}
	}

	private bool IsSimpleType(System.Type type)
	{
		if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
			return true;
		if (type == typeof(Vector2) || type == typeof(Vector3) ||
			type == typeof(Vector4) || type == typeof(Color))
			return true;
		return false;
	}

	private void OnGUI()
	{
		if (selectedItem.prefab == null)
		{
			EditorGUILayout.HelpBox("No prefab available.", MessageType.Warning);
			if (GUILayout.Button("Close"))
			{
				Close();
			}
			return;
		}

		GUILayout.Label("Available Component Properties", EditorStyles.boldLabel);
		scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
		if (availableFields.Count == 0)
		{
			EditorGUILayout.HelpBox("No available properties to add.", MessageType.Info);
		}
		else
		{
			foreach (var entry in availableFields)
			{
				if (GUILayout.Button(entry.key))
				{
					object defaultVal = entry.field.GetValue(entry.comp);
					if (selectedItem.properties == null)
						selectedItem.properties = new Dictionary<string, ModuleExporter.Property>();
					selectedItem.properties[entry.key] = new ModuleExporter.Property
					{
						name = entry.field.Name,
						type = ModuleExporter.TranslateType(entry.field.FieldType),
						value = defaultVal,
						editor = "",
						component = entry.comp.GetType().Name
					};
					PopulateAvailableFields();
					Close();
					break;
				}
			}
		}
		EditorGUILayout.EndScrollView();
		if (GUILayout.Button("Close"))
		{
			Close();
		}
	}
}
