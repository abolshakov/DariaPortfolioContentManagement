using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ContentManagement.Properties;

namespace ContentManagement
{
	public partial class MainForm: Form
	{
		private const string UnknownKey = "Unknown";
		private const string RootKey = "Root";

		private readonly Portfolio _portfolio;
		private int _expandLevel = 2;

		public MainForm()
		{
			InitializeComponent();

			_portfolio = PersistenceManager.ImportData();

			InitializeImageList();
			InitializeTree(_portfolio);
		}

		private void InitializeImageList()
		{
			imageList.Images.Add(UnknownKey, Resources.Unknown);
			imageList.Images.Add(RootKey, Resources.Portfolio);

			var imageOwnerIdByImage = new Dictionary<string, int>();

			foreach (var previewItem in _portfolio.PreviewItems)
			{
				if (!string.IsNullOrEmpty(previewItem.Image))
					imageOwnerIdByImage.Add(previewItem.Image, previewItem.Id);

				foreach (var portfolioItem in previewItem.PortfolioItems)
				{
					portfolioItem.Parent = previewItem;

					if (!string.IsNullOrEmpty(portfolioItem.Image))
						imageOwnerIdByImage.Add(portfolioItem.Image, portfolioItem.Id);
				}
			}

			foreach (var imagePath in PersistenceManager.GetAllImages())
			{
				var key = PersistenceManager.RelativeImagePath(imagePath).Replace("\\", "/");

				if (!imageOwnerIdByImage.TryGetValue(key, out var ownerId))
					throw new InvalidOperationException(string.Format(Resources.MainForm_RedundantImageError, key));

				ImageListAdd(imagePath, ownerId.ToString());
			}
		}

		private void InitializeTree(Portfolio portfolio)
		{
			var root = new TreeNode("Portfolio") { ImageKey = RootKey, SelectedImageKey = RootKey };

			foreach (var previewItem in portfolio.PreviewItems)
			{
				var node = new TreeNode(
						string.IsNullOrWhiteSpace(previewItem.Title)
							? Resources.MainForm_EditMe
							: previewItem.Title)
				{ ImageKey = previewItem.Id.ToString() };

				foreach (var portfolioItem in previewItem.PortfolioItems)
				{
					var subNode = new TreeNode(
						string.IsNullOrEmpty(portfolioItem.Description)
							? Resources.MainForm_EditMe
							: portfolioItem.Description)
					{ ImageKey = portfolioItem.Id.ToString() };
					node.Nodes.Add(subNode);
				}

				root.Nodes.Add(node);
			}

			treeView.Nodes.Add(root);
			treeView.ImageKey = UnknownKey;
			treeView.SelectedImageKey = UnknownKey;
			root.ExpandAll();
		}

		#region Event Handlers

		private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (pictureBox.Image != null)
			{
				pictureBox.Image.Dispose();
				pictureBox.Image = null;
			}

			if (e.Node.Parent == null)
			{
				propertyGrid.SelectedObject = null;
				UpdateButtonsState(null);
				return;
			}

			var selection = IsPreviewItem(e.Node)
				? (IImageOwner)GetPreviewItem(e.Node)
				: GetPortfolioItem(e.Node);

			UpdateButtonsState(selection);

			treeView.SelectedNode.ImageKey = selection.Id.ToString();
			treeView.SelectedNode.SelectedImageKey = selection.Id.ToString();

			propertyGrid.SelectedObject = selection;

			if (string.IsNullOrEmpty(selection.Image))
				return;

			using (var image = PersistenceManager.LoadImage(selection.Image))
			{
				pictureBox.Image = image.ReduceToFit(pictureBox.ClientSize.Width, pictureBox.ClientSize.Height);
			}
		}

		private void PropertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
		{
			if (e.ChangedItem.PropertyDescriptor == null)
				return;

			if (e.ChangedItem.PropertyDescriptor.Name == nameof(PreviewItem.Image))
			{
				UpdateTreeViewImage();
			}
			else
			{
				if (!RenameImage(e.ChangedItem.PropertyDescriptor.Name, e.OldValue))
					return;
			}

			propertyGrid.Refresh();
			PersistenceManager.ExportData(_portfolio);
		}

		private void BtnAdd_Click(object sender, EventArgs e)
		{
			if (IsPreviewItem(treeView.SelectedNode))
			{
				var previewItem = GetPreviewItem(treeView.SelectedNode);
				var portfolioItem = new PortfolioItem { Parent = previewItem };
				previewItem.PortfolioItems.Add(portfolioItem);
			}
			else
			{
				_portfolio.PreviewItems.Add(new PreviewItem());
			}
			var node = new TreeNode(Resources.MainForm_EditMe);
			treeView.SelectedNode.Nodes.Add(node);
			treeView.SelectedNode = node;
		}

		private void BtnUp_Click(object sender, EventArgs e)
		{
			ShiftItemIndex(true);
			PersistenceManager.ExportData(_portfolio);
		}

		private void BtnDown_Click(object sender, EventArgs e)
		{
			ShiftItemIndex(false);
			PersistenceManager.ExportData(_portfolio);
		}

		private void BtnDelete_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show(
					Resources.MainForm_MessageConfirmDelete,
					Resources.MainForm_TitleConfirmDelete,
					MessageBoxButtons.YesNo) == DialogResult.No)
				return;

			var node = treeView.SelectedNode;

			if (IsPreviewItem(node))
			{
				var previewItem = GetPreviewItem(node);
				var index = _portfolio.PreviewItems.IndexOf(previewItem);

				foreach (var portfolioItem in previewItem.PortfolioItems)
				{
					if (string.IsNullOrEmpty(portfolioItem.Image))
						continue;

					ImageListRemoveImage(portfolioItem.Id.ToString());
				}

				_portfolio.PreviewItems.RemoveAt(index);
				ImageListRemoveImage(previewItem.Id.ToString());
				PersistenceManager.DeleteImage(previewItem.Image);
				PersistenceManager.DeleteChildFolder(previewItem);
			}
			else
			{
				var portfolioItem = GetPortfolioItem(node);
				var index = portfolioItem.Parent.PortfolioItems.IndexOf(portfolioItem);
				portfolioItem.Parent.PortfolioItems.RemoveAt(index);
				ImageListRemoveImage(portfolioItem.Id.ToString());
				PersistenceManager.DeleteImage(portfolioItem.Image);
			}

			node.Parent.Nodes.Remove(node);
			PersistenceManager.ExportData(_portfolio);
		}

		private void TreeView_DoubleClick(object sender, EventArgs e)
		{
			if (treeView.SelectedNode != treeView.Nodes[0])
				return;

			switch (_expandLevel)
			{
				case 0:
					_expandLevel++;
					treeView.Nodes[0].Expand();
					break;
				case 1:
					_expandLevel++;
					treeView.ExpandAll();
					treeView.SelectedNode = treeView.Nodes[0];
					break;
				default:
					treeView.CollapseAll();
					_expandLevel = 0;
					break;
			}
		}

		#endregion

		private void ImageListAdd(string imagePath, string ownerId)
		{
			using (var bitmap = Image.FromFile(imagePath))
			{
				var thumbnail = bitmap.Resize(32, 32);
				imageList.Images.Add(ownerId, thumbnail);
			}
		}

		private void UpdateButtonsState(IImageOwner selectedItem)
		{
			if (selectedItem == null)
			{
				btnAdd.Enabled = true;
				btnUp.Enabled = false;
				btnDown.Enabled = false;
				btnDelete.Enabled = false;
				return;
			}

			var index = 0;
			var total = 0;

			switch (selectedItem)
			{
				case PreviewItem previewItem:
					index = _portfolio.PreviewItems.IndexOf(previewItem);
					total = _portfolio.PreviewItems.Count;
					btnAdd.Enabled = true;
					break;
				case PortfolioItem portfolioItem:
					index = portfolioItem.Parent.PortfolioItems.IndexOf(portfolioItem);
					total = portfolioItem.Parent.PortfolioItems.Count;
					btnAdd.Enabled = false;
					break;
			}

			var isFirst = index == 0;
			var isLast = index == total - 1;

			btnUp.Enabled = !isFirst;
			btnDown.Enabled = !isLast;
			btnDelete.Enabled = true;
		}

		private bool IsPreviewItem(TreeNode node)
		{
			return node.Parent == treeView.Nodes[0];
		}

		private PreviewItem GetPreviewItem(TreeNode node)
		{
			return _portfolio.PreviewItems.ElementAt(node.Index);
		}

		private PortfolioItem GetPortfolioItem(TreeNode node)
		{
			return GetPreviewItem(node.Parent).PortfolioItems.ElementAt(node.Index);
		}

		private bool RenameImage(string propertyName, object oldValue)
		{
			if (IsPreviewItem(treeView.SelectedNode))
			{
				if (propertyName != nameof(PreviewItem.Title))
					return false;

				var previewItem = GetPreviewItem(treeView.SelectedNode);

				if (previewItem.Title.Equals(oldValue))
					return false;

				PersistenceManager.RenamePreviewItemImage(previewItem);
				treeView.SelectedNode.Text = previewItem.Title;
			}
			else
			{
				if (propertyName != nameof(PortfolioItem.Description))
					return false;

				var portfolioItem = GetPortfolioItem(treeView.SelectedNode);

				if (portfolioItem.Description.Equals(oldValue))
					return false;

				PersistenceManager.RenamePortfolioItemImage(portfolioItem);
				treeView.SelectedNode.Text = portfolioItem.Description;
			}

			return true;
		}

		private void UpdateTreeViewImage()
		{
			var owner = IsPreviewItem(treeView.SelectedNode)
				? (IImageOwner)GetPreviewItem(treeView.SelectedNode)
				: GetPortfolioItem(treeView.SelectedNode);

			var key = owner.Id.ToString();
			ImageListRemoveImage(key);
			ImageListAdd(PersistenceManager.FullImagePath(owner.Image), key);

			treeView.SelectedNode.ImageKey = key;
			treeView.SelectedNode.SelectedImageKey = key;
		}

		private void ImageListRemoveImage(string key)
		{
			if (!imageList.Images.ContainsKey(key))
				return;

			var image = imageList.Images[key];
			imageList.Images.RemoveByKey(key);
			image?.Dispose();
		}

		private void ShiftItemIndex(bool up)
		{
			var delta = up ? -1 : 1;

			if (IsPreviewItem(treeView.SelectedNode))
			{
				var previewItem = GetPreviewItem(treeView.SelectedNode);
				var index = _portfolio.PreviewItems.IndexOf(previewItem);
				_portfolio.PreviewItems.RemoveAt(index);
				_portfolio.PreviewItems.Insert(index + delta, previewItem);
			}
			else
			{
				var portfolioItem = GetPortfolioItem(treeView.SelectedNode);
				var index = portfolioItem.Parent.PortfolioItems.IndexOf(portfolioItem);
				portfolioItem.Parent.PortfolioItems.RemoveAt(index);
				portfolioItem.Parent.PortfolioItems.Insert(index + delta, portfolioItem);
			}

			var node = treeView.SelectedNode;
			var nodeIndex = node.Index;
			var neighbor = treeView.SelectedNode.NextNode ?? treeView.SelectedNode.PrevNode;
			var neighborKey = neighbor?.ImageKey;

			var parent = node.Parent;
			parent.Nodes.RemoveAt(nodeIndex);
			parent.Nodes.Insert(nodeIndex + delta, node);
			treeView.SelectedNode = node;

			if (neighborKey != null)
			{
				neighbor.ImageKey = neighborKey;
			}
		}
	}
}
