﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Toolbox.Library;
using Toolbox.Library.Forms;
using LayoutBXLYT.Cafe;

namespace LayoutBXLYT
{
    public partial class LayoutHierarchy : LayoutDocked
    {
        private LayoutEditor ParentEditor;
        private STContextMenuStrip ContexMenu;

        public LayoutHierarchy(LayoutEditor layoutEditor)
        {
            ParentEditor = layoutEditor;

            InitializeComponent();

            treeView1.BackColor = FormThemes.BaseTheme.FormBackColor;
            treeView1.ForeColor = FormThemes.BaseTheme.FormForeColor;

            var imgList = new ImageList();
            imgList.ColorDepth = ColorDepth.Depth32Bit;
            imgList.Images.Add("folder", Toolbox.Library.Properties.Resources.Folder);
            imgList.Images.Add("AlignmentPane", FirstPlugin.Properties.Resources.AlignmentPane);
            imgList.Images.Add("WindowPane", FirstPlugin.Properties.Resources.WindowPane);
            imgList.Images.Add("ScissorPane", FirstPlugin.Properties.Resources.ScissorPane);
            imgList.Images.Add("BoundryPane", FirstPlugin.Properties.Resources.BoundryPane);
            imgList.Images.Add("NullPane", FirstPlugin.Properties.Resources.NullPane);
            imgList.Images.Add("PicturePane", FirstPlugin.Properties.Resources.PicturePane);
            imgList.Images.Add("QuickAcess", FirstPlugin.Properties.Resources.QuickAccess);
            imgList.Images.Add("TextPane", FirstPlugin.Properties.Resources.TextPane);
            imgList.Images.Add("material", Toolbox.Library.Properties.Resources.materialSphere);
            imgList.Images.Add("texture", Toolbox.Library.Properties.Resources.Texture);
            imgList.Images.Add("font", Toolbox.Library.Properties.Resources.Font);

            imgList.ImageSize = new Size(22,22);
            treeView1.ImageList = imgList;

            BackColor = FormThemes.BaseTheme.FormBackColor;
            ForeColor = FormThemes.BaseTheme.FormForeColor;

            ContexMenu = new STContextMenuStrip();
        }

        private bool isLoaded = false;
        private EventHandler OnProperySelected;
        private BxlytHeader ActiveLayout;
        public void LoadLayout(BxlytHeader bxlyt, EventHandler onPropertySelected)
        {
            isLoaded = false;
            OnProperySelected = onPropertySelected;

            ActiveLayout = bxlyt;

            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();

            CreateQuickAccess(bxlyt);
            treeView1.Nodes.Add(new TreeNode("File Settings") {Tag = bxlyt });
            LoadTextures(bxlyt.Textures);
            LoadFonts(bxlyt.Fonts);
            LoadMaterials(bxlyt.GetMaterials());
            treeView1.Nodes.Add(new AnimatedPaneFolder(ParentEditor, "Animated Pane List") { Tag = bxlyt });

            LoadPane(bxlyt.RootGroup);
            LoadPane(bxlyt.RootPane);

            treeView1.EndUpdate();

            isLoaded = true;
        }

        public class AnimatedPaneFolder : TreeNodeCustom
        {
            private LayoutEditor ParentEditor;
            private bool Expanded = false;

            public AnimatedPaneFolder(LayoutEditor editor, string text) {
                ParentEditor = editor;
                Text = text;

                Nodes.Add("Empty");
            }

            public override void OnExpand()
            {
                if (Expanded) return;

                var layoutFile = (BxlytHeader)Tag;

                Nodes.Clear();

                Expanded = true;

                var animations = ParentEditor.AnimationFiles;

                foreach (var pane in layoutFile.PaneLookup.Values) {
                    string matName = "";

                    //Find materials
                    var mat = pane.TryGetActiveMaterial();
                    if (mat != null) matName = mat.Name;

                    //search archive
                    var archive = layoutFile.FileInfo.IFileInfo.ArchiveParent;
                    if (archive != null)
                    {
                        foreach (var file in archive.Files)
                        {
                            if (Utils.GetExtension(file.FileName) == ".bflan" &&
                                !animations.Any(x => x.FileName == file.FileName))
                            {
                                if (BxlanHeader.ContainsEntry(file.FileData, new string[2] { pane.Name, matName }))
                                {
                                    var paneNode = CreatePaneWrapper(pane);
                                    Nodes.Add(paneNode);
                                }
                            }
                        }
                    }

                    //Search opened animations
                    for (int i = 0; i < animations?.Count; i++) {
                        if (animations[i].ContainsEntry(pane.Name) || animations[i].ContainsEntry(matName))
                        {
                            var paneNode = CreatePaneWrapper(pane);
                            Nodes.Add(paneNode);
                        }
                    }
                }
            }
        }

        public void SelectNode(TreeNode node)
        {
            treeView1.SelectedNode = node;
            treeView1.Refresh();
        }

        public void UpdateTree()
        {
            treeView1.Refresh();
        }

        public void Reset()
        {
            treeView1.Nodes.Clear();
            isLoaded = false;
        }

        private void LoadTextures(List<string> textures)
        {
            ActiveLayout.TextureFolder = new TreeNode("Textures");
            treeView1.Nodes.Add(ActiveLayout.TextureFolder);
            for (int i = 0; i < textures.Count; i++)
            {
                TreeNode matNode = new TreeNode(textures[i]);
                matNode.Tag = i;
                matNode.ContextMenuStrip = new ContextMenuStrip();
                var menu = new STToolStipMenuItem("Rename");
                menu.Click += RenameTextureAction;
                matNode.ContextMenuStrip.Items.Add(menu);
                matNode.ImageKey = "texture";
                matNode.SelectedImageKey = "texture";
                ActiveLayout.TextureFolder.Nodes.Add(matNode);
            }
        }

        private void RenameTextureAction(object sender, EventArgs e)
        {
            var selectedNode = treeView1.SelectedNode;
            if (selectedNode == null) return;

            int index = (int)selectedNode.Tag;
            string activeTex = ActiveLayout.Textures[index];

            RenameDialog dlg = new RenameDialog();
            dlg.SetString(activeTex);

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                ActiveLayout.Textures[index] = dlg.textBox1.Text;
                selectedNode.Text = dlg.textBox1.Text;
            }
        }

        private void LoadFonts(List<string> fonts)
        {
            ActiveLayout.FontFolder = new TreeNode("Fonts");
            treeView1.Nodes.Add(ActiveLayout.FontFolder);
            for (int i = 0; i < fonts.Count; i++)
            {
                TreeNode matNode = new TreeNode(fonts[i]);
                matNode.ImageKey = "font";
                matNode.SelectedImageKey = "font";
                ActiveLayout.FontFolder.Nodes.Add(matNode);
            }
        }

        private void LoadMaterials(List<BxlytMaterial> materials)
        {
            ActiveLayout.MaterialFolder = new TreeNode("Materials");
            treeView1.Nodes.Add(ActiveLayout.MaterialFolder);
            for (int i = 0; i < materials.Count; i++)
            {
                MatWrapper matNode = new MatWrapper(materials[i].Name);
                materials[i].NodeWrapper = matNode;
                matNode.Tag = materials[i];
                matNode.ImageKey = "material";
                matNode.SelectedImageKey = "material";
                ActiveLayout.MaterialFolder.Nodes.Add(matNode);
            }
        }

  

        private void CreateQuickAccess(BxlytHeader bxlyt)
        {
            var panes = new List<BasePane>();
            var groupPanes = new List<BasePane>();
            GetPanes(bxlyt.RootPane,ref panes);
            GetGroupPanes(bxlyt.RootGroup,ref groupPanes);

            TreeNode node = new TreeNode("Quick Access");
            node.ImageKey = "QuickAcess";
            node.SelectedImageKey = "QuickAcess";
            treeView1.Nodes.Add(node);

            TreeNode nullFolder = new TreeNode("Null Panes");
            TreeNode textFolder = new TreeNode("Text Boxes");
            TreeNode windowFolder = new TreeNode("Window Panes");
            TreeNode pictureFolder = new TreeNode("Picture Panes");
            TreeNode boundryFolder = new TreeNode("Boundry Panes");
            TreeNode partsFolder = new TreeNode("Part Panes");
            TreeNode groupFolder = new TreeNode("Groups");

            node.Nodes.Add(nullFolder);
            node.Nodes.Add(textFolder);
            node.Nodes.Add(windowFolder);
            node.Nodes.Add(pictureFolder);
            node.Nodes.Add(boundryFolder);
            node.Nodes.Add(partsFolder);
            node.Nodes.Add(groupFolder);

            for (int i = 0; i < panes.Count; i++)
            {
                var paneNode = CreatePaneWrapper(panes[i]);
                if (panes[i] is BFLYT.WND1) windowFolder.Nodes.Add(paneNode);
                else if (panes[i] is BCLYT.WND1) windowFolder.Nodes.Add(paneNode);
                else if (panes[i] is BRLYT.WND1) windowFolder.Nodes.Add(paneNode);
                else if (panes[i] is BFLYT.PIC1) pictureFolder.Nodes.Add(paneNode);
                else if (panes[i] is BCLYT.PIC1) pictureFolder.Nodes.Add(paneNode);
                else if (panes[i] is BRLYT.PIC1) pictureFolder.Nodes.Add(paneNode);
                else if (panes[i] is BFLYT.BND1) boundryFolder.Nodes.Add(paneNode);
                else if (panes[i] is BCLYT.BND1) boundryFolder.Nodes.Add(paneNode);
                else if (panes[i] is BRLYT.BND1) boundryFolder.Nodes.Add(paneNode);
                else if (panes[i] is BCLYT.PRT1) partsFolder.Nodes.Add(paneNode);
                else if (panes[i] is BFLYT.PRT1) partsFolder.Nodes.Add(paneNode);
                else if (panes[i] is BRLYT.PRT1) partsFolder.Nodes.Add(paneNode);
                else if (panes[i] is BRLYT.TXT1) textFolder.Nodes.Add(paneNode);
                else if (panes[i] is BCLYT.TXT1) textFolder.Nodes.Add(paneNode);
                else if (panes[i] is BFLYT.TXT1) textFolder.Nodes.Add(paneNode);
                else nullFolder.Nodes.Add(paneNode);

                if (panes[i] is BFLYT.PRT1)
                {
                    var partPane = (BFLYT.PRT1)panes[i];
                    foreach (var property in partPane.Properties)
                    {
                        if (property.Property != null)
                        {
                            var propertyNode = CreatePaneWrapper(property.Property);
                            paneNode.Nodes.Add(propertyNode);
                        }
                    }
                }
            }

            for (int i = 0; i < groupPanes.Count; i++)
            {
                var paneNode = CreatePaneWrapper(groupPanes[i]);
                groupFolder.Nodes.Add(paneNode);
            }
        }

        private void GetPanes(BasePane pane, ref List<BasePane> panes)
        {
            panes.Add(pane);
            foreach (var childPane in pane.Childern)
                  GetPanes(childPane,ref panes);
        }

        private void GetGroupPanes(BasePane pane, ref List<BasePane> panes)
        {
            panes.Add(pane);
            foreach (var childPane in pane.Childern)
                GetPanes(childPane,ref panes);
        }

        public static PaneTreeWrapper CreatePaneWrapper(BasePane pane)
        {
            PaneTreeWrapper paneNode = new PaneTreeWrapper();
            paneNode.Text = pane.Name;
            paneNode.Tag = pane;
            paneNode.Checked = true;

            string imageKey = "";
            if (pane is BFLYT.WND1) imageKey = "WindowPane";
            else if (pane is BCLYT.WND1) imageKey = "WindowPane";
            else if (pane is BRLYT.WND1) imageKey = "WindowPane";
            else if (pane is BFLYT.PIC1) imageKey = "PicturePane";
            else if (pane is BCLYT.PIC1) imageKey = "PicturePane";
            else if (pane is BRLYT.PIC1) imageKey = "PicturePane";
            else if (pane is BFLYT.BND1) imageKey = "BoundryPane";
            else if (pane is BCLYT.BND1) imageKey = "BoundryPane";
            else if (pane is BRLYT.BND1) imageKey = "BoundryPane";
            else if (pane is BFLYT.TXT1) imageKey = "TextPane";
            else if (pane is BCLYT.TXT1) imageKey = "TextPane";
            else if (pane is BRLYT.TXT1) imageKey = "TextPane";
            else imageKey = "NullPane";

            paneNode.ImageKey = imageKey;
            paneNode.SelectedImageKey = imageKey;

            return paneNode;
        }

        private void LoadPane(BasePane pane, TreeNode parent = null)
        {
            var paneNode = CreatePaneWrapper(pane);
            pane.NodeWrapper = paneNode;

            if (parent == null)
                treeView1.Nodes.Add(paneNode);
            else
                parent.Nodes.Add(paneNode);

            foreach (var childPane in pane.Childern)
                LoadPane(childPane, paneNode);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (isLoaded)
                OnProperySelected.Invoke("Select", e);
        }

        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (isLoaded)
            {
                if (!e.Node.Checked)
                    e.Node.ForeColor = FormThemes.BaseTheme.DisabledItemColor;
                else
                    e.Node.ForeColor = treeView1.ForeColor;

                OnProperySelected.Invoke("Checked", e);
            }
        }

        private void treeView1_MouseClick(object sender, MouseEventArgs e)
        {

        }

        private void TogglePane(object sender, EventArgs e)
        {
            TogglePane(treeView1.SelectedNode);
        }

        private void TogglePane(TreeNode node)
        {
            if (node == null)
                return;

            if (node.Checked)
                node.Checked = false;
            else
                node.Checked = true;
        }

        private void treeView1_KeyDown(object sender, KeyEventArgs e)
        {
            var node = treeView1.SelectedNode;
            if (node == null || node.Tag == null)
                return;

            if (e.KeyCode == Keys.H && e.Control)
            {
                if (node.Tag is BasePane)
                    TogglePane(node);
            }
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag == null)
                return;

            if (e.Button == MouseButtons.Right)
            {
                treeView1.SelectedNode = e.Node;

                if (e.Node.Tag is BasePane)
                {
                    ContexMenu.Items.Clear();
                    ContexMenu.Items.Add(new STToolStipMenuItem("Display Panes", null, TogglePane, Keys.Control | Keys.H));
                //    ContexMenu.Items.Add(new STToolStipMenuItem("Display Children Panes", null, TogglePane, Keys.Control | Keys.H));
                    ContexMenu.Show(Cursor.Position);
                }
            }
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            //Create and expand a file format, then update the tag
            //Allows for faster loading
            if (e.Node.Tag is ArchiveFileInfo)
            {

            }

            if (e.Node is TreeNodeCustom)
                ((TreeNodeCustom)e.Node).OnExpand();
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag == null)
                return;

            if (e.Button == MouseButtons.Left) {
                if (e.Node.Tag is BasePane)
                    ParentEditor.ShowPaneEditor(e.Node.Tag as BasePane);
            }
        }

        #region NodeDragDrop

        private string NodeMap;

        private void treeView1_DragDrop(object sender, DragEventArgs e)
        {
         /*   if (e.Data.GetDataPresent(typeof(PaneTreeWrapper)))
            {
                Point targetPoint = treeView1.PointToClient(new Point(e.X, e.Y));
                TreeNode NodeOver = treeView1.GetNodeAt(targetPoint);
                TreeNode NodeMoving = (PaneTreeWrapper)e.Data.GetData(typeof(PaneTreeWrapper));

                if (NodeOver != null && (NodeOver != NodeMoving || (NodeOver.Parent != null && NodeOver.Index == (NodeOver.Parent.Nodes.Count - 1))))
                {
                    int OffsetY = this.treeView1.PointToClient(Cursor.Position).Y - NodeOver.Bounds.Top;
                    int NodeOverImageWidth = this.treeView1.ImageList.Images[NodeOver.ImageIndex].Size.Width + 8;
                    Graphics g = this.treeView1.CreateGraphics();

                    //Folder node

                    if (OffsetY < (NodeOver.Bounds.Height / 3))
                    {
                        TreeNode tnParadox = NodeOver;
                        while (tnParadox.Parent != null)
                        {
                            if (tnParadox.Parent == NodeMoving)
                            {
                                this.NodeMap = "";
                                return;
                            }

                            tnParadox = tnParadox.Parent;
                        }
                    }
                }
            }*/

            #endregion

            
            if (e.Data.GetDataPresent(typeof(PaneTreeWrapper)))
            {
                Point targetPoint = treeView1.PointToClient(new Point(e.X, e.Y));

                TreeNode targetNode = treeView1.GetNodeAt(targetPoint);
                TreeNode draggedNode = (PaneTreeWrapper)e.Data.GetData(typeof(PaneTreeWrapper));

                var draggedPane = draggedNode.Tag as BasePane;
                if (draggedPane == null || draggedPane.IsRoot)
                    return;

                TreeNode parentNode = targetNode;

                if (targetNode != null && targetNode.Parent != null)
                {
                    bool canDrop = true;
                    while (canDrop && (parentNode != null))
                    {
                        canDrop = !Object.ReferenceEquals(draggedNode, parentNode);
                        parentNode = parentNode.Parent;
                    }

                    if (!canDrop) return;

                    bool isTargetParent = targetNode.Equals(draggedNode.Parent);

                    //Remove it's previous parent
                    draggedPane.Parent.Childern.Remove(draggedPane);
                    draggedNode.Remove();

                    //Adjust the parent to the parent's parent
                    Console.WriteLine("isTargetParent " + isTargetParent);
                    if (isTargetParent)
                    {
                        var parentPane = targetNode.Tag as BasePane;
                        if (parentPane.IsRoot) return;

                        var upperParentNode = targetNode.Parent;
                        var upperParentPane = upperParentNode.Tag as BasePane;

                        draggedPane.ResetParentTransform(upperParentPane);
                        draggedPane.Parent = upperParentPane;

                        upperParentPane.Childern.Add(draggedPane);

                        upperParentNode.Nodes.Add(draggedNode);
                        upperParentNode.Expand();
                    }
                    else //Set the target node as the parent
                    {
                        var parentPane = targetNode.Tag as BasePane;
                        draggedPane.ResetParentTransform(parentPane);
                        draggedPane.Parent = parentPane;

                        parentPane.Childern.Add(draggedPane);

                        targetNode.Nodes.Add(draggedNode);
                        targetNode.Expand();
                    }

                    ParentEditor.UpdateViewport();
                }
            }
        }

        private void treeView1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }
    }
}
