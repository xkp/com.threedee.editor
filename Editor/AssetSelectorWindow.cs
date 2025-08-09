using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System.Linq;

public class AssetSelectorWindow : EditorWindow
{
	private TreeViewState _treeState;
	private AssetTreeView _treeView;
	private List<string> _externalSelection;

	public static void OpenWindow(List<string> selectedAssets)
	{
		var window = CreateInstance<AssetSelectorWindow>();
		window.titleContent = new GUIContent("Asset Selector");
		window.minSize = new Vector2(400, 600);
		window._externalSelection = selectedAssets;
		window.InitializeSelection();
		window.ShowModalUtility();
	}

	private void OnEnable()
	{
		if (_treeState == null)
			_treeState = new TreeViewState();
		_treeView = new AssetTreeView(_treeState);
		_treeView.Reload();
	}

	public void InitializeSelection()
	{
		if (_externalSelection == null) return;
		InitializeNode(_treeView.Root, _treeView.checkedIDs, _externalSelection);
	}

	private void InitializeNode(TreeViewItem node, HashSet<int> checkedIDs, List<string> input)
	{
		foreach (var child in node.children ?? Enumerable.Empty<TreeViewItem>())
		{
			var assetNode = child as AssetTreeView.AssetItem;
			if (assetNode != null)
			{
				if (input.Contains(assetNode.assetPath))
				{
					checkedIDs.Add(assetNode.id);
				}
			}
			// Recurse into all children regardless
			InitializeNode(child, checkedIDs, input);
		}
	}

	private void OnGUI()
	{
		GUILayout.Label("Select Assets", EditorStyles.boldLabel);
		Rect treeRect = GUILayoutUtility.GetRect(0, position.width, 0, position.height - 40);
		_treeView.OnGUI(treeRect);

		GUILayout.FlexibleSpace();
		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("OK"))
		{
			ApplySelection();
			Close();
		}
		if (GUILayout.Button("Cancel"))
		{
			Close();
		}
		EditorGUILayout.EndHorizontal();
	}

	private void ApplySelection()
	{
		if (_externalSelection == null) return;
		_externalSelection.Clear();
		// Recursively gather selections starting from root
		AddSelectedFromNode(_treeView.Root, _treeView.checkedIDs, _externalSelection);
	}

	private void AddSelectedFromNode(TreeViewItem node, HashSet<int> checkedIDs, List<string> output)
	{
		foreach (var child in node.children ?? Enumerable.Empty<TreeViewItem>())
		{
			var assetNode = child as AssetTreeView.AssetItem;
			if (assetNode != null)
			{
				if (checkedIDs.Contains(assetNode.id) && !assetNode.isFolder)
				{
					output.Add(assetNode.assetPath);
				}
			}
			// Recurse into all children regardless
			AddSelectedFromNode(child, checkedIDs, output);
		}
	}

	private void OnDisable()
	{
		ApplySelection();
	}

	private class AssetTreeView : TreeView
	{
		public HashSet<int> checkedIDs = new HashSet<int>();
		public TreeViewItem Root;

		public class AssetItem : TreeViewItem
		{
			public string assetPath;
			public bool isFolder;
			public AssetItem(int id, int depth, string name, string path, bool folder) : base(id, depth, name)
			{
				assetPath = path;
				isFolder = folder;
			}
		}

		public AssetTreeView(TreeViewState state) : base(state)
		{
			showBorder = true;
			showAlternatingRowBackgrounds = false; // uniform row color
		}

		protected override TreeViewItem BuildRoot()
		{
			var root = Root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
			int idCounter = 1;
			string[] guids = AssetDatabase.FindAssets("");
			var paths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).Distinct();
			foreach (var path in paths)
			{
				AddPathNode(path, root, ref idCounter);
			}
			SetupDepthsFromParentsAndChildren(root);
			return root;
		}

		private void AddPathNode(string path, TreeViewItem parent, ref int idCounter)
		{
			var parts = path.Split('/');
			TreeViewItem currentParent = parent;
			for (int depth = 0; depth < parts.Length; depth++)
			{
				string part = parts[depth];
				var existing = currentParent.children?.FirstOrDefault(c => c.displayName == part);
				if (existing == null)
				{
					bool isFolder = depth < parts.Length - 1;
					var node = new AssetItem(idCounter++, currentParent.depth + 1, part,
											  isFolder ? null : path,
											  isFolder);
					if (currentParent.children == null)
						currentParent.children = new List<TreeViewItem>();
					currentParent.AddChild(node);
					currentParent = node;
				}
				else
				{
					currentParent = existing;
				}
			}
		}

		protected override void RowGUI(RowGUIArgs args)
		{
			var item = (AssetItem)args.item;
			Rect rowRect = args.rowRect;
			float indent = GetContentIndent(item);
			Rect toggleRect = new Rect(rowRect.x + indent, rowRect.y, 18f, rowRect.height);

			bool isChecked = checkedIDs.Contains(item.id);
			bool newChecked = EditorGUI.Toggle(toggleRect, isChecked);
			if (newChecked != isChecked)
			{
				SetCheckedRecursive(item, newChecked);
			}

			Rect labelRect = new Rect(toggleRect.x + 18f, rowRect.y, rowRect.width - indent - 18f, rowRect.height);
			EditorGUI.LabelField(labelRect, item.displayName);
		}

		private void SetCheckedRecursive(TreeViewItem item, bool isChecked)
		{
			if (isChecked) checkedIDs.Add(item.id);
			else checkedIDs.Remove(item.id);

			if (item.children != null)
			{
				foreach (var child in item.children.Cast<AssetItem>())
				{
					SetCheckedRecursive(child, isChecked);
				}
			}
		}
	}
}
