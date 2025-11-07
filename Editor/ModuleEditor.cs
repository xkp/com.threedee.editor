using UnityEngine;
using UnityEditor;
using UnityEditorInternal; // for ReorderableList
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Linq;

public class ModuleExporter : EditorWindow
{
	private string moduleId = "";

	private string moduleName = "";
	private string controllerClass = "";
	private string author = "";
	private string url = "";

	// Module Type property.
	private string moduleType = "Props";
	private readonly string[] allowedModuleTypes = new string[] { "Game", "Character", "Nature", "Props", "Other" };

	private List<ItemGroup> itemGroups = new List<ItemGroup>();
	// The exportPath is chosen by the user.
	private string exportPath = "";

	// Lists for Unity package files (copied into the module folder)
	private List<string> unityPackages = new List<string>();       // full paths to copied files
	private List<string> unityPackageNames = new List<string>();     // file names only
	private List<string> assetsToExport = new List<string>();       // full paths to copied files
	private List<string> customEditors = new List<string>();
	private List<string> dependencies = new List<string>();

	private Vector2 scrollPosition;
	private Item selectedItem = null;

	// Stores foldout states for component sections.
	private Dictionary<int, bool> compFoldouts = new Dictionary<int, bool>();

	// Allowed types for properties.
	private readonly string[] allowedTypes = new string[] { "string", "int", "float", "bool", "enum", "gameitem", "asset", "object" };

	// NEW: A dictionary to track property foldout states (keyed by property key).
	private Dictionary<string, bool> propertyFoldouts = new Dictionary<string, bool>();

	private List<Property> moduleProperties = new List<Property>();

	[System.Serializable]
	public class Item
	{
		public string id;
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
		public List<Property> properties = new List<Property>();

		public Vector3 exportTranslation = Vector3.zero;
		public Vector3 exportRotation = Vector3.zero;
		public Vector3 exportScale = Vector3.one;
	}

	[System.Serializable]
	public class Property
	{
		public string name;
		public string type;
		public string data;
		public string value;
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
		moduleId = mod.id;
		moduleName = mod.name;
		moduleType = mod.type;
		controllerClass = mod.controller;
		author = mod.author;
		url = mod.url;

		// Populate Unity package names.
		unityPackageNames.Clear();
		unityPackages.Clear();
		if (mod.packages != null)
		{
			foreach (var pkg in mod.packages)
			{
				unityPackages.Add(pkg);
				unityPackageNames.Add(Path.GetFileName(pkg));
			}
		}

		customEditors.Clear();
		if (mod.customEditors != null)
		{
			foreach (var editor in mod.customEditors)
			{
				customEditors.Add(editor);
			}
		}

		dependencies.Clear();
		if (mod.dependencies != null)
		{
			foreach (var dependency in mod.dependencies)
			{
				dependencies.Add(dependency);
			}
		}

		moduleProperties.Clear();
		if (mod.moduleProperties != null)
		{
			foreach (var property in mod.moduleProperties)
			{
				moduleProperties.Add(property);
			}
		}

		dependencies.Clear();
		if (mod.dependencies != null)
		{
			foreach (var dependency in mod.dependencies)
			{
				dependencies.Add(dependency);
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
					item.properties = new List<Property>();
					if (exportedItem.properties != null)
					{
						foreach (var ep in exportedItem.properties)
						{
							// When loading, assume these are manual properties.
							item.properties.Add(new Property
							{
								name = ep.name,
								type = ep.type,
								data = ep.data
							});
						}
					}
					group.items.Add(item);
				}
				itemGroups.Add(group);
			}
		}


		UpdateAssets();
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
	/// Returns the module folder. If the chosen exportPath’s name equals moduleName, we use exportPath directly;
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

	private string GetUnityPath(string filePath)
	{
		string assetsPath = Application.dataPath; // absolute path to Assets folder
		string fullPath = Path.GetFullPath(filePath).Replace("\\", "/");

		if (!fullPath.StartsWith(assetsPath.Replace("\\", "/")))
		{
			return string.Empty;
		}

		// Convert absolute path to Unity relative path
		return "Assets" + fullPath.Substring(assetsPath.Length);
	}

	private Vector2 _assetScroll;
	private void OnGUI()
	{
		EditorGUILayout.BeginVertical();

		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

		// --- Module Settings ---
		GUILayout.Label("Module Export Settings", EditorStyles.boldLabel);
		EditorGUILayout.BeginHorizontal();
		float sectionW = position.width * 0.5f;
		EditorGUILayout.BeginVertical("box", GUILayout.Width(sectionW));
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
		EditorGUILayout.EndVertical();
		EditorGUILayout.BeginVertical();
		author = EditorGUILayout.TextField("Author", author);
		url = EditorGUILayout.TextField("URL", url);
		EditorGUILayout.EndVertical();

		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		// --- Global Properties ---
		GUILayout.Label("GENERAL", EditorStyles.boldLabel);
		EditorGUILayout.BeginHorizontal();

		// === Module Properties Editor (replaces "Select Assets to UNBundle") ===
		{
			// Constrain the whole editor to 50% of the window width
			float sectionWidth = position.width * 0.5f;

			EditorGUILayout.BeginVertical("box", GUILayout.Width(sectionWidth));

			// Header row
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Module Properties", EditorStyles.boldLabel);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Add Property", GUILayout.Width(110)))
			{
				moduleProperties ??= new List<Property>();
				moduleProperties.Add(new Property
				{
					name = "NewProperty",
					type = allowedTypes[0], // default "string"
					data = "",
					value = ""
				});
			}
			EditorGUILayout.EndHorizontal();

			// Optional: header labels aligned to columns
			{
				// Reserve space for remove button (24) + small gap (6)
				float reserved = 24f + 20f;
				// Small internal padding/gaps between fields
				float gap = 3f;

				float usable = sectionW - reserved - (gap * 2f); // two gaps between 3 columns
				float nameW = Mathf.Max(60f, usable * 0.40f);
				float typeW = Mathf.Max(60f, usable * 0.20f);
				float valueW = Mathf.Max(60f, usable * 0.40f);

				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(nameW));
				GUILayout.Label("Type", EditorStyles.miniBoldLabel, GUILayout.Width(typeW));
				GUILayout.Label("Value", EditorStyles.miniBoldLabel, GUILayout.Width(valueW));
				GUILayout.Space(reserved); // align with remove space
				EditorGUILayout.EndHorizontal();

				if (moduleProperties == null || moduleProperties.Count == 0)
				{
					EditorGUILayout.HelpBox("No module properties yet. Click 'Add Property' to create one.", MessageType.Info);
				}
				else
				{
					_assetScroll = EditorGUILayout.BeginScrollView(_assetScroll, GUILayout.Height(90));

					for (int i = 0; i < moduleProperties.Count; i++)
					{
						var prop = moduleProperties[i];

						EditorGUILayout.BeginHorizontal("helpbox");

						// NAME (40%)
						prop.name = EditorGUILayout.TextField(prop.name, GUILayout.Width(nameW));

						GUILayout.Space(gap);

						// TYPE (20%)
						int typeIndex = System.Array.IndexOf(allowedTypes, prop.type);
						if (typeIndex < 0) { typeIndex = 0; prop.type = allowedTypes[0]; }
						typeIndex = EditorGUILayout.Popup(typeIndex, allowedTypes, GUILayout.Width(typeW));
						prop.type = allowedTypes[typeIndex];

						GUILayout.Space(gap);

						// VALUE (40%) — type-specific
						switch (prop.type)
						{
							case "object":
								prop.data = EditorGUILayout.TextField(prop.data, GUILayout.Width(valueW));
								break;

							case "gameitem":
								// Full-width button in the value column
								if (GUILayout.Button(string.IsNullOrEmpty(prop.data) ? "Edit..." : prop.data, GUILayout.Width(valueW)))
								{
									prop.data = GameItemPropertyEditor.OpenWindow(prop.data);
								}
								break;

							case "enum":
								if (GUILayout.Button(string.IsNullOrEmpty(prop.data) ? "Edit..." : prop.data, GUILayout.Width(valueW)))
								{
									prop.data = EnumPropertyEditor.OpenWindow(prop.data, CopyCustomIcon);
								}
								break;

							default:
								prop.value = EditorGUILayout.TextField(prop.value, GUILayout.Width(valueW));
								break;
						}

						// Remove button (fixed)
						GUILayout.Space(6f);
						if (GUILayout.Button("X", GUILayout.Width(24f)))
						{
							moduleProperties.RemoveAt(i);
							i--;
							EditorGUILayout.EndHorizontal();
							continue;
						}

						EditorGUILayout.EndHorizontal();
					}

					EditorGUILayout.EndScrollView();
				}
			}

			EditorGUILayout.EndVertical();
		}
		// === End Module Properties Editor ===

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
					string destPath = GetUnityPath(packageOriginalPath);
					if (string.IsNullOrEmpty(destPath))
					{
						EditorUtility.DisplayDialog("Error", "Unity packages must be inside the asset folders.", "OK");
					}
					else
					{
						unityPackages.Add(destPath);
						var packageName = Path.GetFileName(destPath);
						if (!unityPackageNames.Contains(packageName))
							unityPackageNames.Add(packageName);
					}
				}
			}
		}
		for (int i = 0; i < unityPackages.Count; i++)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(Path.GetFileName(unityPackages[i]));
			if (GUILayout.Button("Remove", GUILayout.Width(80)))
			{
				unityPackages.RemoveAt(i);
				unityPackageNames.RemoveAt(i);
				i--;
			}
			EditorGUILayout.EndHorizontal();
		}

		if (GUILayout.Button("Add Dependency"))
		{
			dependencies.Add("");
		}

		for (int i = 0; i < dependencies.Count; i++)
		{
			EditorGUILayout.BeginHorizontal();
			dependencies[i] = EditorGUILayout.TextField(dependencies[i]);
			if (GUILayout.Button("Remove", GUILayout.Width(80)))
			{
				dependencies.RemoveAt(i);
				i--;
			}
			EditorGUILayout.EndHorizontal();
		}

		if (GUILayout.Button("Add Custom Editor"))
		{
			if (string.IsNullOrEmpty(moduleName))
			{
				EditorUtility.DisplayDialog("Warning", "Please set the module name before adding a custom editor.", "OK");
			}
			else
			{
				string editorOriginalPath = EditorUtility.OpenFilePanel("Select Custom Editor", "", "zip");
				if (!string.IsNullOrEmpty(editorOriginalPath))
				{
					string destPath = GetUnityPath(editorOriginalPath);
					if (string.IsNullOrEmpty(destPath))
					{
						EditorUtility.DisplayDialog("Error", "Unity packages must be inside the asset folders.", "OK");
					}
					else
					{
						customEditors.Add(destPath);
					}
				}
			}
		}

		for (int i = 0; i < customEditors.Count; i++)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(Path.GetFileName(customEditors[i]));
			if (GUILayout.Button("Remove", GUILayout.Width(80)))
			{
				customEditors.RemoveAt(i);
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

				group.icon = IconPickerUI.DrawIconField(group.icon, (path) => {
					return CopyCustomIcon(path);
				});

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
						UpdateAssets();
					}
				}
				if (GUILayout.Button("Create Custom Item") && !string.IsNullOrEmpty(group.name))
				{
					CreateCustomItem(group);
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
					if (!string.IsNullOrEmpty(item.icon))
					{
						string fullIconPath = Path.Combine(GetAssetModuleFolder(), item.icon);
						thumbnail = LoadTextureFromFile(fullIconPath);

						if (thumbnail == null)
						{
							fullIconPath = Path.Combine(GetModuleFolder(), item.icon);
							thumbnail = LoadTextureFromFile(fullIconPath);
						}
					}

					if (thumbnail == null && item.prefab != null)
					{
						thumbnail = AssetPreview.GetAssetPreview(item.prefab);
					}

					if (thumbnail == null)
					{
						thumbnail = EditorGUIUtility.IconContent("DefaultAsset Icon").image as Texture2D;
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

			selectedItem.prefabPath = EditorGUILayout.TextField("Prefab:", selectedItem.prefabPath);

			selectedItem.icon = IconPickerUI.DrawIconField(selectedItem.icon, CopyCustomIcon); //'EditorGUILayout.TextField("Icon", group.icon);

			/*			if (GUILayout.Button("Generate Assets"))
						{
							GenerateModel(selectedItem);
							GenerateThumbnail(selectedItem);
						}
						EditorGUILayout.Space();
			*/
			// --- Unified Properties List (Collapsible) ---
			GUILayout.Label("PROPERTIES", EditorStyles.boldLabel);

			// Build a unified list of properties from the dictionary.
			List<(Property prop, string key)> unifiedProps = new List<(Property, string)>();
			if (selectedItem.properties != null)
			{
				foreach (var kvp in selectedItem.properties)
				{
					unifiedProps.Add((kvp, kvp.name));
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
				EditorGUILayout.BeginHorizontal();
				propertyFoldouts[entry.key] = EditorGUILayout.Foldout(propertyFoldouts[entry.key], header, true);
				if (GUILayout.Button("Remove", GUILayout.Width(70)))
				{
					var prop = selectedItem.properties.FirstOrDefault(p => p.name == entry.key);
					if (prop != null)
						selectedItem.properties.Remove(prop);
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
					//entry.prop.value = EditorGUILayout.TextField("Value", entry.prop.value != null ? entry.prop.value.ToString() : "");
					if (entry.prop.type == "object")
					{
						entry.prop.data = EditorGUILayout.TextField("Editor", entry.prop.data);
					}
					else if (entry.prop.type == "gameitem")
					{
						if (GUILayout.Button("Edit..."))
						{
							entry.prop.data = GameItemPropertyEditor.OpenWindow(entry.prop.data);
						}
					}
					else if (entry.prop.type == "enum")
					{
						if (GUILayout.Button("Edit..."))
						{
							entry.prop.data = EnumPropertyEditor.OpenWindow(entry.prop.data, CopyCustomIcon);
						}
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
					data = "",
				};
				selectedItem.properties.Add(newProp);
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
		UpdateAssets();

		//AskForExportFolder();
		string moduleFolder = GetModuleFolder();
		Directory.CreateDirectory(moduleFolder);

		//Copy custom assets into 
		DirectoryCopy(GetAssetModuleFolder(), moduleFolder, true);

		//export
		ExportedModule mod = new ExportedModule();
		if (string.IsNullOrEmpty(moduleId))
			moduleId = System.Guid.NewGuid().ToString().ToUpper();

		mod.id = moduleId;
		mod.name = moduleName;
		mod.type = moduleType;
		mod.controller = controllerClass;
		mod.author = author;
		mod.url = url;
		mod.packages = new List<string>(unityPackages);
		mod.dependencies = new List<string>(dependencies);
		mod.customEditors = new List<string>(customEditors);
		mod.moduleProperties = new List<Property>(moduleProperties);

		mod.itemGroups = new List<ExportedGroup>();
		foreach (var group in itemGroups)
		{
			ExportedGroup eg = new ExportedGroup();
			eg.name = group.name;
			eg.icon = group.icon;
			eg.category = group.category;
			eg.items = new List<ExportedItem>();

			foreach (var item in group.items)
			{
				ExportedItem ei = new ExportedItem();
				ei.id = item.id;
				ei.name = item.name;
				ei.description = item.description;
				ei.unique = item.unique;
				ei.notDraggable = item.notDraggable;
				ei.template = item.template;
				ei.prefab = item.prefabPath;
				ei.icon = item.icon;
				ei.icon3d = item.modelPath;
				ei.exportTranslation = item.exportTranslation;
				ei.exportRotation = item.exportRotation;
				ei.exportScale = item.exportScale;

				ei.properties = new List<ExportedProperty>();
				foreach (var kvp in item.properties)
				{
					ei.properties.Add(CopyProperty(kvp));
				}
				eg.items.Add(ei);
			}
			mod.itemGroups.Add(eg);
		}

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
		else
		{
			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			var destFolder = Path.Combine(moduleFolder, "Packages");
			Directory.CreateDirectory(destFolder);
			foreach (var package in unityPackages)
			{
				var packagePath = Path.Combine(projectRoot, package);
				if (!File.Exists(packagePath))
				{
					Debug.Log($"Orphaned package: {package}");
					continue;
				}

				var packageDest = Path.Combine(destFolder, Path.GetFileName(package));
				File.Copy(packagePath, packageDest, true);
			}
		}

		foreach (var editor in customEditors)
		{
			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			string editorZipFilePath = Path.Combine(projectRoot, editor);
			if (File.Exists(editorZipFilePath))
			{
				var editorName = Path.GetFileNameWithoutExtension(editor);
				var editorDest = Path.Combine(moduleFolder, "Editors", editorName);
				Directory.CreateDirectory(editorDest);

				System.IO.Compression.ZipFile.ExtractToDirectory(editorZipFilePath, editorDest);
				//File.Copy(editorZipFilePath, Path.Combine(editorDest, Path.GetFileName(editor)), true);
			}
		}


		string jsonFilePath = loadedModuleFilePath;
		if (string.IsNullOrEmpty(jsonFilePath))
		{
			jsonFilePath = loadedModuleFilePath = Path.Combine(Application.dataPath, "module.bgm");
		}

		string json = JsonUtility.ToJson(mod, true);
		File.WriteAllText(jsonFilePath, json);
		Debug.Log("Exported module JSON to " + jsonFilePath);

		File.Copy(loadedModuleFilePath, Path.Combine(moduleFolder, "module.bgm"), true);

		//export assets
		var assetsFromGroups = new List<string>();
		foreach (var group in itemGroups)
		{
			foreach (var item in group.items)
			{
				if (!string.IsNullOrEmpty(item.prefabPath))
					assetsFromGroups.Add(item.prefabPath);
			}
		}

		assetsFromGroups = assetsFromGroups.Distinct().ToList();
		if (assetsFromGroups.Any())
		{
			BuildBundleFromPaths(assetsFromGroups, "AssetBundle", Path.Combine(moduleFolder, "Assets"));
		}

		//build zip file
		string zipFilePath = Path.Combine(Path.GetDirectoryName(moduleFolder), moduleName + ".3dbg");
		if (File.Exists(zipFilePath))
		{
			File.Delete(zipFilePath);
		}
		System.IO.Compression.ZipFile.CreateFromDirectory(moduleFolder, zipFilePath);
		Debug.Log("Created zip file: " + zipFilePath);

		EditorUtility.RevealInFinder(zipFilePath);
	}

	public static void BuildBundleFromPaths(List<string> assetPaths, string bundleName, string outputDirectory)
	{
		if (assetPaths == null || assetPaths.Count == 0)
		{
			Debug.LogWarning("AssetBundleUtility: No asset paths provided.");
			return;
		}

		// Filter out any folder paths
		var filtered = new List<string>();
		foreach (var path in assetPaths)
		{
			if (!AssetDatabase.IsValidFolder(path))
				filtered.Add(path);
		}

		if (filtered.Count == 0)
		{
			Debug.LogWarning("AssetBundleUtility: No valid files found in provided paths.");
			return;
		}

		// Resolve output directory
		string fullOutput = outputDirectory;
		if (!System.IO.Path.IsPathRooted(fullOutput))
			fullOutput = System.IO.Path.Combine(Application.dataPath, "../", outputDirectory);
		if (!System.IO.Directory.Exists(fullOutput))
			System.IO.Directory.CreateDirectory(fullOutput);

		// Setup build map
		var buildMap = new AssetBundleBuild
		{
			assetBundleName = bundleName,
			assetNames = filtered.ToArray()
		};

		// Execute build
		BuildPipeline.BuildAssetBundles(
			fullOutput,
			new[] { buildMap },
			BuildAssetBundleOptions.None,
			EditorUserBuildSettings.activeBuildTarget
		);

		Debug.Log($"AssetBundleUtility: Built '{bundleName}' with {filtered.Count} assets at {fullOutput}");
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
		ep.data = prop.data;
		return ep;
	}

	[System.Serializable]
	public class ExportedModule
	{
		public string id;
		public string name;
		public string type;
		public string controller;
		public string author;
		public string url;

		public List<string> packages;
		public List<string> customEditors;
		public List<string> dependencies;
		public List<ExportedGroup> itemGroups;
		public List<Property> moduleProperties;
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
		public string id;
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
		public string data;
	}

	private void CreateCustomItem(ItemGroup group)
	{
		Item newItem = new Item();
		newItem.id = System.Guid.NewGuid().ToString().ToUpper();
		newItem.name = "Custom Item";
		newItem.prefab = null;
		newItem.prefabPath = "";
		newItem.icon = "";
		newItem.modelPath = "";
		newItem.properties = new List<Property>();
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
					id = System.Guid.NewGuid().ToString().ToUpper(),
					name = prefab.name,
					prefab = prefab,
					prefabPath = assetPath,
					properties = new List<Property>(),
					exportTranslation = Vector3.zero,
					exportRotation = Vector3.zero,
					exportScale = Vector3.one
				});
			}
		}
	}

	private void UpdateAssets()
	{
		var modulePath = GetModuleFolder();
		foreach (var group in itemGroups)
		{
			foreach (var item in group.items)
			{
				if (item.prefab != null)
				{
					if (string.IsNullOrEmpty(item.icon) || !File.Exists(Path.Combine(modulePath, item.icon)))
					{
						GenerateThumbnail(item);
					}

					if (string.IsNullOrEmpty(item.modelPath) || !File.Exists(Path.Combine(modulePath, item.modelPath)))
					{
						GenerateModel(item);
					}
				}
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
		string moduleFolder = GetModuleFolder();
		string assetsDirectory = Path.Combine(moduleFolder, "Assets");
		Directory.CreateDirectory(assetsDirectory);
		string modelDirectory = Path.Combine(assetsDirectory, "Models");
		Directory.CreateDirectory(modelDirectory);
		string modelPath = Path.Combine(modelDirectory, item.name + ".obj");

		if (item.prefab != null)
		{
			Mesh mesh = GetLowestLODMesh(item.prefab);
			if (mesh == null)
			{
				Debug.LogWarning($"No valid mesh found for item: {item.name}");
				return;
			}
			SaveMeshAsOBJ(mesh, modelPath, item.exportTranslation, item.exportRotation, item.exportScale);
			item.modelPath = Path.Combine("Assets", "Models", item.name + ".obj");
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
		string moduleFolder = GetModuleFolder();
		string assetsDirectory = Path.Combine(moduleFolder, "Assets");
		Directory.CreateDirectory(assetsDirectory);
		string thumbDirectory = Path.Combine(assetsDirectory, "Thumbnails");
		Directory.CreateDirectory(thumbDirectory);
		Texture2D preview = AssetPreview.GetAssetPreview(item.prefab);
		if (preview == null)
		{
			while (AssetPreview.IsLoadingAssetPreview(item.prefab.GetInstanceID()))
			{
			}

			preview = AssetPreview.GetAssetPreview(item.prefab);
		}

		if (preview == null)
		{
			preview = EditorGUIUtility.IconContent("DefaultAsset Icon").image as Texture2D;
		}

		if (preview != null)
		{
			try
			{
				byte[] pngData = preview.EncodeToPNG();
				if (pngData != null)
				{
					string thumbPath = Path.Combine(thumbDirectory, item.name + ".png");
					File.WriteAllBytes(thumbPath, pngData);
					string relativeThumbPath = Path.Combine("Assets", "Thumbnails", item.name + ".png");
					item.icon = relativeThumbPath;
				}
			}
			catch
			{
				item.icon = string.Empty;
			}
		}
	}

	private string CopyCustomIcon(string imagePath)
	{
		string moduleFolder = GetAssetModuleFolder();
		string assetsDirectory = Path.Combine(moduleFolder, "Assets");
		Directory.CreateDirectory(assetsDirectory);
		string thumbDirectory = Path.Combine(assetsDirectory, "Thumbnails");
		Directory.CreateDirectory(thumbDirectory);

		var filename = Path.GetFileName(imagePath);
		File.Copy(imagePath, Path.Combine(thumbDirectory, filename), true);

		return Path.Combine("Assets", "Thumbnails", filename);
	}

	private string GetAssetModuleFolder()
	{
		return Path.Combine(Application.dataPath, "Big Game/Module");
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
		try
		{
			return renderer is MeshRenderer meshRenderer ? meshRenderer.GetComponent<MeshFilter>()?.sharedMesh : null;
		}
		catch
		{
			return null;
		}
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

				/*				Rect r4 = new Rect(x, y, valueLabelW, EditorGUIUtility.singleLineHeight);
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
				*/
				if (prop.type == "object")
				{
					Rect r6 = new Rect(x, y, editorLabelW, EditorGUIUtility.singleLineHeight);
					EditorGUI.LabelField(r6, "Editor");
					x += editorLabelW;
					Rect r7 = new Rect(x, y, editorFieldW, EditorGUIUtility.singleLineHeight);
					prop.data = EditorGUI.TextField(r7, prop.data);
				}
				else if (prop.type == "gameitem")
				{
					if (GUILayout.Button("Edit..."))
					{
					}
				}
			}
		};

		reorderableList.onAddCallback = (ReorderableList list) =>
		{
			properties.Add(new ModuleExporter.Property { name = "NewProperty", type = "string", data = "" });
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
				if (selectedItem.properties == null || !selectedItem.properties.Any(p => p.name == key))
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
						selectedItem.properties = new List<ModuleExporter.Property>();

					selectedItem.properties.Add(new ModuleExporter.Property
					{
						name = entry.field.Name,
						type = ModuleExporter.TranslateType(entry.field.FieldType),
						data = entry.comp.GetType().Name,
					});

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
