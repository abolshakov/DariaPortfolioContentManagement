using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ContentManagement.Properties;

namespace ContentManagement
{
    public partial class MainForm: Form
    {
        private const string UnknownKey = "Unknown";
        private const string RootKey = "Root";
		
        private readonly bool _optimizeOnStartup = bool.Parse(ConfigurationManager.AppSettings["OptimizeImagesOnStartup"]);

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

            foreach (var project in _portfolio.Projects)
            {
                if (!string.IsNullOrEmpty(project.Image))
                    imageOwnerIdByImage.Add(project.Image, project.Id);

                foreach (var projectItem in project.Items)
                {
                    projectItem.Parent = project;

                    if (string.IsNullOrEmpty(projectItem.Image))
	                    continue;

                    imageOwnerIdByImage.Add(projectItem.Image, projectItem.Id);

                    if (projectItem.Width == 0 || projectItem.Height == 0)
                    {
	                    using (var image = PersistenceManager.LoadImage(projectItem.Image))
	                    {
		                    projectItem.UpdateImageSize(image.Size);
	                    }
                    }
                }
            }

            var removeOrphanImages = bool.Parse(ConfigurationManager.AppSettings["RemoveOrphanImages"]);

			foreach (var imagePath in PersistenceManager.GetAllImages())
            {
                var key = PersistenceManager.RelativeImagePath(imagePath).Replace("\\", "/");

                if (!imageOwnerIdByImage.TryGetValue(key, out var ownerId))
                {
	                if (!removeOrphanImages)
		                throw new InvalidOperationException(string.Format(Resources.MainForm_RedundantImageError, key));

	                PersistenceManager.DeleteImage(0, key);
                }

                ImageListAdd(imagePath, ownerId.ToString());
            }
        }

        private void InitializeTree(Portfolio portfolio)
        {
            var root = new TreeNode("Portfolio") { ImageKey = RootKey, SelectedImageKey = RootKey };

            foreach (var previewItem in portfolio.Projects)
            {
                var node = new TreeNode(
                        string.IsNullOrWhiteSpace(previewItem.Title)
                            ? Resources.MainForm_EditMe
                            : previewItem.Title)
                { ImageKey = previewItem.Id.ToString() };

                foreach (var portfolioItem in previewItem.Items)
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
		
        private async Task MainForm_Shown()
        {
	        if (!_optimizeOnStartup)
	        {
		        return;
	        }

	        using (var optimizingForm = new WaitingForm())
	        {
		        optimizingForm.Show(this);
		        Enabled = false;
		        await PersistenceManager.OptimizeImages(_portfolio).ConfigureAwait(true);
		        Enabled = true;
		        optimizingForm.Close();
	        }
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Parent == null)
            {
                propertyGrid.SelectedObject = null;
                UpdateButtonsState(null);
                UpdatePictureBoxImage();
                return;
            }

            var selection = GetImageOwner(e.Node);
            UpdateButtonsState(selection);

            treeView.SelectedNode.ImageKey = selection.Id.ToString();
            treeView.SelectedNode.SelectedImageKey = selection.Id.ToString();

            propertyGrid.SelectedObject = selection;
            UpdatePictureBoxImage(selection);
        }

        private async Task PropertyGrid_PropertyValueChanged(PropertyValueChangedEventArgs e)
        {
            if (e.ChangedItem.PropertyDescriptor == null)
                return;
            
            var newValue = e.ChangedItem.Value.ToString();
            string image = null;
            var isPreview = IsPreviewItem(treeView.SelectedNode);
            var id = isPreview 
	            ? GetProject(treeView.SelectedNode).Id 
	            : GetProjectItem(treeView.SelectedNode).Id;

            if (e.ChangedItem.PropertyDescriptor.Name == nameof(Project.Image))
            {
                UpdateTreeViewImage();
                var size = UpdatePictureBoxImage(GetImageOwner(treeView.SelectedNode));
                
                if (string.IsNullOrEmpty(newValue))
                {
                    PersistenceManager.DeleteImage(id, e.OldValue.ToString());
                }
                else if(!isPreview)
                {
					GetProjectItem(treeView.SelectedNode).UpdateImageSize(size);
                }

                image = newValue;
            }
            else
            {
                EnsureImageName(e.ChangedItem.PropertyDescriptor.Name, e.OldValue);
            }

            propertyGrid.Refresh();
            PersistenceManager.ExportData(_portfolio);

            if (!string.IsNullOrEmpty(image))
            {
	            await PersistenceManager.OptimizeImageAsync(id, image, isPreview).ConfigureAwait(true);
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            if (IsPreviewItem(treeView.SelectedNode))
            {
                var previewItem = GetProject(treeView.SelectedNode);
                var portfolioItem = new ProjectItem { Parent = previewItem };
                previewItem.Items.Add(portfolioItem);
            }
            else
            {
                _portfolio.Projects.Add(new Project());
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
                var previewItem = GetProject(node);
                var index = _portfolio.Projects.IndexOf(previewItem);

                foreach (var portfolioItem in previewItem.Items)
                {
                    if (string.IsNullOrEmpty(portfolioItem.Image))
                        continue;

                    ImageListRemoveImage(portfolioItem.Id.ToString());
                }

                _portfolio.Projects.RemoveAt(index);
                ImageListRemoveImage(previewItem.Id.ToString());
                PersistenceManager.DeleteImage(previewItem.Id, previewItem.Image);
                PersistenceManager.DeleteChildFolder(previewItem);
            }
            else
            {
                var portfolioItem = GetProjectItem(node);
                var index = portfolioItem.Parent.Items.IndexOf(portfolioItem);
                portfolioItem.Parent.Items.RemoveAt(index);
                ImageListRemoveImage(portfolioItem.Id.ToString());
                PersistenceManager.DeleteImage(portfolioItem.Id, portfolioItem.Image);
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

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            PersistenceManager.ExportData(_portfolio);
			
            using (var optimizingForm = new WaitingForm())
            {
	            optimizingForm.Show(this);
	            Enabled = false;
	            PersistenceManager.AllDone.WaitOne();
	            Enabled = true;
	            optimizingForm.Close();
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
                case Project previewItem:
                    index = _portfolio.Projects.IndexOf(previewItem);
                    total = _portfolio.Projects.Count;
                    btnAdd.Enabled = true;
                    break;
                case ProjectItem portfolioItem:
                    index = portfolioItem.Parent.Items.IndexOf(portfolioItem);
                    total = portfolioItem.Parent.Items.Count;
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

        private Project GetProject(TreeNode node)
        {
            return _portfolio.Projects.ElementAt(node.Index);
        }

        private ProjectItem GetProjectItem(TreeNode node)
        {
            return GetProject(node.Parent).Items.ElementAt(node.Index);
        }

        private void EnsureImageName(string propertyName, object oldValue)
        {
            if (IsPreviewItem(treeView.SelectedNode))
            {
                if (propertyName != nameof(Project.Title))
                    return;

                var previewItem = GetProject(treeView.SelectedNode);

                if (previewItem.Title.Equals(oldValue))
                    return;

                PersistenceManager.RenamePreviewItemImage(previewItem);
                treeView.SelectedNode.Text = previewItem.Title;
            }
            else
            {
                if (propertyName != nameof(ProjectItem.Description))
                    return;

                var portfolioItem = GetProjectItem(treeView.SelectedNode);

                if (portfolioItem.Description.Equals(oldValue))
                    return;

                PersistenceManager.RenameProjectItemImage(portfolioItem);
                treeView.SelectedNode.Text = portfolioItem.Description;
            }
        }

        private IImageOwner GetImageOwner(TreeNode node)
        {
	        return IsPreviewItem(node)
		        ? (IImageOwner)GetProject(node)
		        : GetProjectItem(node);
        }

        private void UpdateTreeViewImage()
        {
            var owner = IsPreviewItem(treeView.SelectedNode)
                ? (IImageOwner)GetProject(treeView.SelectedNode)
                : GetProjectItem(treeView.SelectedNode);

            var key = owner.Id.ToString();
            ImageListRemoveImage(key);
            if (string.IsNullOrEmpty(owner.Image))
            {
                key = UnknownKey;
            }
            else
            {
                ImageListAdd(PersistenceManager.FullImagePath(owner.Image), key);
            }

            treeView.SelectedNode.ImageKey = key;
            treeView.SelectedNode.SelectedImageKey = key;
        }

        private Size UpdatePictureBoxImage(IImageOwner imageOwner = null)
        {
	        if (pictureBox.Image != null)
	        {
		        pictureBox.Image.Dispose();
		        pictureBox.Image = null;
	        }

	        if (imageOwner==null || string.IsNullOrEmpty(imageOwner.Image))
		        return Size.Empty;

	        using (var image = PersistenceManager.LoadImage(imageOwner.Image))
	        {
		        pictureBox.Image = image.ReduceToFit(pictureBox.ClientSize.Width, pictureBox.ClientSize.Height);
		        return image.Size;
	        }
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
                var previewItem = GetProject(treeView.SelectedNode);
                var index = _portfolio.Projects.IndexOf(previewItem);
                _portfolio.Projects.RemoveAt(index);
                _portfolio.Projects.Insert(index + delta, previewItem);
            }
            else
            {
                var portfolioItem = GetProjectItem(treeView.SelectedNode);
                var index = portfolioItem.Parent.Items.IndexOf(portfolioItem);
                portfolioItem.Parent.Items.RemoveAt(index);
                portfolioItem.Parent.Items.Insert(index + delta, portfolioItem);
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
