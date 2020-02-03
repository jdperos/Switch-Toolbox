﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Library.IO;
using Toolbox.Library;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;
using Toolbox.Library.Forms;
using Bfres.Structs;
using System.IO;
using System.Linq;

namespace FirstPlugin
{
    public enum BlockType : uint
    {
        Invalid = 0x00,
        EndOfFile = 0x01,
        AlignData = 0x02,
        VertexShaderHeader = 0x03,
        VertexShaderProgram = 0x05,
        PixelShaderHeader = 0x06,
        PixelShaderProgram = 0x07,
        GeometryShaderHeader = 0x08,
        GeometryShaderProgram = 0x09,
        GeometryShaderProgram2 = 0x10,
        ImageInfo = 0x11,
        ImageData = 0x12,
        MipData = 0x13,
        ComputeShaderHeader = 0x14,
        ComputeShader = 0x15,
        UserBlock = 0x16,
    }

    public class GTXFile : TreeNodeFile, IFileFormat, IContextMenuNode, ITextureContainer
    {
        public FileType FileType { get; set; } = FileType.Image;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "GTX" };
        public string[] Extension { get; set; } = new string[] { "*.gtx" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
            {
                return reader.CheckSignature(4, "Gfx2");
            }
        }

        public Type[] Types
        {
            get
            {
                List<Type> types = new List<Type>();
                return types.ToArray();
            }
        }

        public bool DisplayIcons => false;

        public List<STGenericTexture> TextureList
        {
            get
            {
                List<STGenericTexture> texList = new List<STGenericTexture>();
                foreach (STGenericTexture node in textures)
                    texList.Add(node);

                return texList;
            }
            set { }
        }

        private GTXHeader header;

        public List<byte[]> data = new List<byte[]>();
        public List<byte[]> mipMaps = new List<byte[]>();
        public List<TextureData> textures = new List<TextureData>();

        public List<GTXDataBlock> blocks = new List<GTXDataBlock>();

        public override UserControl GetEditor()
        {
            STPropertyGrid editor = new STPropertyGrid();
            editor.Text = Text;
            editor.Dock = DockStyle.Fill;
            return editor;
        }

        public override void OnAfterAdded()
        {
            if (textures.Count > 0 && this.TreeView != null)
                this.TreeView.SelectedNode = textures[0];
        }

        public override void FillEditor(UserControl control)
        {
            ((STPropertyGrid)control).LoadProperty(header);
        }

        public void Load(System.IO.Stream stream)
        {
            CanSave = true;
            Text = FileName;

            ReadGx2(new FileReader(stream));

            string name = System.IO.Path.GetFileNameWithoutExtension(Text);
            foreach (var image in textures) {
                if (Nodes.Count == 1)
                    image.Text = $"{name}";
                else
                    image.Text = $"{name}_{textures.IndexOf(image)}";
            }
        }

        public ToolStripItem[] GetContextMenuItems()
        {
            return new ToolStripItem[]
            {
                new ToolStripMenuItem("Save", null, Save, Keys.Control | Keys.S),
                new ToolStripMenuItem("Export All", null, ExportAllAction, Keys.Control | Keys.E),
            };
        }

        private void Save(object sender, EventArgs args)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.DefaultExt = "gtx";
            sfd.Filter = "Supported Formats|*.gtx;";
            sfd.FileName = FileName;

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                STFileSaver.SaveFileFormat(this, sfd.FileName);
            }
        }

        protected void ExportAllAction(object sender, EventArgs e)
        {
            if (Nodes.Count <= 0)
                return;

            string formats = FileFilters.GTX;

            string[] forms = formats.Split('|');

            List<string> Formats = new List<string>();

            for (int i = 0; i < forms.Length; i++)
            {
                if (i > 1 || i == (forms.Length - 1)) //Skip lines with all extensions
                {
                    if (!forms[i].StartsWith("*"))
                        Formats.Add(forms[i]);
                }
            }

            FolderSelectDialog sfd = new FolderSelectDialog();
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string folderPath = sfd.SelectedPath;

                BatchFormatExport form = new BatchFormatExport(Formats);
                if (form.ShowDialog() == DialogResult.OK)
                {
                    string extension = form.GetSelectedExtension();
                    extension.Replace(" ", string.Empty);

                    foreach (STGenericTexture node in Nodes)
                    {
                        ((STGenericTexture)node).Export($"{folderPath}\\{node.Text}{extension}");
                    }
                }
            }
        }

        public void Unload()
        {

        }

        public void Save(System.IO.Stream stream)
        {
            using (FileWriter writer = new FileWriter(stream, true))
            {
                writer.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;
                header.Write(writer);

                uint surfBlockType;
                uint dataBlockType;
                uint mipBlockType;

                if (header.MajorVersion == 6 && header.MinorVersion == 0)
                {
                    surfBlockType = 0x0A;
                    dataBlockType = 0x0B;
                    mipBlockType = 0x0C;
                }
                else if (header.MajorVersion == 6 || header.MajorVersion == 7)
                {
                    surfBlockType = 0x0B;
                    dataBlockType = 0x0C;
                    mipBlockType = 0x0D;
                }
                else
                    throw new Exception($"Unsupported GTX version {header.MajorVersion}");

                int imageInfoIndex = -1;
                int imageBlockIndex = -1;
                int imageMipBlockIndex = -1;

                writer.Seek(header.HeaderSize, System.IO.SeekOrigin.Begin);
                foreach (var block in blocks)
                {
                    if ((uint)block.BlockType == surfBlockType)
                    {
                        imageInfoIndex++;
                        imageBlockIndex++;
                        imageMipBlockIndex++;

                        block.data = textures[imageInfoIndex].surface.Write();
                        block.Write(writer);

                    }
                    else if ((uint)block.BlockType == dataBlockType)
                    {
                        var tex = textures[imageBlockIndex];

                        var pos = writer.Position;
                        uint Alignment = tex.surface.alignment;
                        //Create alignment block first
                        uint dataAlignment = GetAlignBlockSize((uint)pos + 32, Alignment);
                        GTXDataBlock dataAlignBlock = new GTXDataBlock(BlockType.AlignData, dataAlignment, 0, 0);
                        dataAlignBlock.Write(writer);

                        block.data = tex.surface.data;
                        block.Write(writer);
                    }
                    else if ((uint)block.BlockType == mipBlockType)
                    {
                        var tex = textures[imageMipBlockIndex];

                        var pos = writer.Position;
                        uint Alignment = tex.surface.alignment;
                        //Create alignment block first
                        uint dataAlignment = GetAlignBlockSize((uint)pos + 32, Alignment);
                        GTXDataBlock dataAlignBlock = new GTXDataBlock(BlockType.AlignData, dataAlignment, 0, 0);
                        dataAlignBlock.Write(writer);

                        if (tex.surface.mipData == null || tex.surface.mipData.Length <= 0)
                            throw new Exception("Invalid mip data!");

                        block.data = tex.surface.mipData;
                        block.Write(writer);
                    }
                    else if (block.BlockType != BlockType.AlignData)
                    {
                        block.Write(writer);
                    }
                }
            }
        }

        private static uint GetAlignBlockSize(uint DataOffset, uint Alignment)
        {
            uint alignSize = RoundUp(DataOffset, Alignment) - DataOffset - 32;

            uint z = 1;
            while (alignSize < 0)
                alignSize = RoundUp(DataOffset + (Alignment * z), Alignment) - DataOffset - 32;
            z += 1;

            return alignSize;
        }

        private static uint RoundUp(uint X, uint Y) { return((X - 1) | (Y - 1)) + 1; }

        private void ReadGx2(FileReader reader)
        {
            reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;

            header = new GTXHeader();
            header.Read(reader);

            Console.WriteLine("header size " + header.HeaderSize);

            uint surfBlockType;
            uint dataBlockType;
            uint mipBlockType;
            uint vertexShaderHeader = 0x03;
            uint vertexShaderProgram = 0x05;
            uint pixelShaderHeader = 0x06;
            uint pixelShaderProgram = 0x07;
            uint geometryShaderHeader = 0x08;
            uint geometryShaderProgram = 0x09;
            uint userDataBlock = 0x10;

            if (header.MajorVersion == 6 && header.MinorVersion == 0)
            {
                surfBlockType = 0x0A;
                dataBlockType = 0x0B;
                mipBlockType = 0x0C;
            }
            else if (header.MajorVersion == 6 || header.MajorVersion == 7)
            {
                surfBlockType = 0x0B;
                dataBlockType = 0x0C;
                mipBlockType = 0x0D;
            }
            else
                throw new Exception($"Unsupported GTX version {header.MajorVersion}");

            if (header.GpuVersion != 2)
                throw new Exception($"Unsupported GPU version {header.GpuVersion}");

            reader.Position = header.HeaderSize;

            bool blockB = false;
            bool blockC = false;

            uint ImageInfo = 0;
            uint images = 0;

            while (reader.Position < reader.BaseStream.Length)
            {
                Console.WriteLine("BLOCK POS " + reader.Position + " " + reader.BaseStream.Length);
                GTXDataBlock block = new GTXDataBlock();
                block.Read(reader);
                blocks.Add(block);

                bool BlockIsEmpty = block.BlockType == BlockType.AlignData ||
                                    block.BlockType == BlockType.EndOfFile;

                //Here we use "if" instead of "case" statements as types vary between versions
                if ((uint)block.BlockType == surfBlockType)
                {
                    ImageInfo += 1;
                    blockB = true;

                    var surface = new SurfaceInfoParse();
                    surface.Read(new FileReader(block.data));

                    if (surface.tileMode == 0 || surface.tileMode > 16)
                        throw new Exception($"Invalid tileMode {surface.tileMode}!");

                    if (surface.numMips > 14)
                        throw new Exception($"Invalid number of mip maps {surface.numMips}!");

                    TextureData textureData = new TextureData();
                    textureData.surface = surface;
                    textureData.MipCount = surface.numMips;
                    textureData.ArrayCount = surface.depth;
                    textureData.Text = "Texture" + ImageInfo;
                    Nodes.Add(textureData);
                    textures.Add(textureData);
                }
                else if ((uint)block.BlockType == dataBlockType)
                {
                    images += 1;
                    blockC = true;

                    data.Add(block.data);
                }
                else if ((uint)block.BlockType == mipBlockType)
                {
                    mipMaps.Add(block.data);
                }
                else if ((uint)block.BlockType == vertexShaderHeader)
                    Nodes.Add(new BlockDisplay(block.data) { Text = "Vertex Shader Header" });
                else if ((uint)block.BlockType == vertexShaderProgram)
                    Nodes.Add(new BlockDisplay(block.data) { Text = "Vertex Shader Program" });
                else if ((uint)block.BlockType == pixelShaderHeader)
                    Nodes.Add(new BlockDisplay(block.data) { Text = "Pixel Shader Header" });
                else if ((uint)block.BlockType == pixelShaderProgram)
                    Nodes.Add(new BlockDisplay(block.data) { Text = "Pixel Shader Program" });
                else if ((uint)block.BlockType == geometryShaderHeader)
                    Nodes.Add(new BlockDisplay(block.data) { Text = "Geometry Shader Header" });
                else if ((uint)block.BlockType == geometryShaderProgram)
                    Nodes.Add(new BlockDisplay(block.data) { Text = "Geometry Shader Program" });
                else if (!BlockIsEmpty)
                    Nodes.Add(new BlockDisplay(block.data) { Text = $"Block Type {block.BlockType.ToString("X")}" });
            }
            if (textures.Count != data.Count)
                throw new Exception($"Bad size! {textures.Count} {data.Count}");

            int curTex = 0;
            int curMip = 0;
            foreach (var node in Nodes)
            {
                if (node is TextureData)
                {
                    TextureData tex = (TextureData)node;

                    tex.surface.data = data[curTex];
                    tex.surface.bpp = GX2.surfaceGetBitsPerPixel(tex.surface.format) >> 3;
                    tex.Format = FTEX.ConvertFromGx2Format((Syroot.NintenTools.Bfres.GX2.GX2SurfaceFormat)tex.surface.format);
                    tex.Width = tex.surface.width;
                    tex.Height = tex.surface.height;

                    if (tex.surface.numMips > 1)
                        tex.surface.mipData = mipMaps[curMip++];
                    else
                        tex.surface.mipData = new byte[0];

                    if (tex.surface.mipData == null)
                        tex.surface.numMips = 1;

                    curTex++;
                }
            }
        }

        public class BlockDisplay : TreeNodeCustom
        {
            public byte[] BlockData;

            public BlockDisplay(byte[] data)
            {
                BlockData = data;
            }

            public override void OnClick(TreeView treeview)
            {
                HexEditor editor = (HexEditor)LibraryGUI.GetActiveContent(typeof(HexEditor));
                if (editor == null)
                {
                    editor = new HexEditor();
                    LibraryGUI.LoadEditor(editor);
                }
                editor.Text = Text;
                editor.Dock = DockStyle.Fill;
                editor.LoadData(BlockData);
            }
        }

        public class GTXHeader
        {
            readonly string Magic = "Gfx2";
            public uint HeaderSize;
            public uint MajorVersion;
            public uint MinorVersion;
            public uint GpuVersion;
            public uint AlignMode;

            public void Read(FileReader reader)
            {
                string Signature = reader.ReadString(4, Encoding.ASCII);
                if (Signature != Magic)
                    throw new Exception($"Invalid signature {Signature}! Expected Gfx2.");

                HeaderSize = reader.ReadUInt32();
                MajorVersion = reader.ReadUInt32();
                MinorVersion = reader.ReadUInt32();
                GpuVersion = reader.ReadUInt32(); //Ignored in 6.0
                AlignMode = reader.ReadUInt32();
            }
            public void Write(FileWriter writer)
            {
                writer.WriteSignature(Magic);
                writer.Write(HeaderSize);
                writer.Write(MajorVersion);
                writer.Write(MinorVersion);
                writer.Write(GpuVersion);
                writer.Write(AlignMode);
            }
        }
        public class GTXDataBlock
        {
            readonly string Magic = "BLK{";
            public uint HeaderSize;
            public uint MajorVersion;
            public uint MinorVersion;
            public BlockType BlockType;
            public uint Identifier;
            public uint Index;
            public uint DataSize;
            public byte[] data;

            public GTXDataBlock() { }

            public GTXDataBlock(BlockType blockType, uint dataSize, uint identifier, uint index)
            {
                HeaderSize = 32;
                MajorVersion = 1;
                MinorVersion = 0;
                BlockType = blockType;
                DataSize = dataSize;
                Identifier = identifier;
                Index = index;
                data = new byte[dataSize];
            }

            public void Read(FileReader reader)
            {
                long blockStart = reader.Position;

                string Signature = reader.ReadString(4, Encoding.ASCII);
                if (Signature != Magic)
                    throw new Exception($"Invalid signature {Signature}! Expected BLK.");

                HeaderSize = reader.ReadUInt32();
                MajorVersion = reader.ReadUInt32(); //Must be 0x01 for 6.x
                MinorVersion = reader.ReadUInt32(); //Must be 0x00 for 6.x
                BlockType = reader.ReadEnum<BlockType>(false);
                DataSize = reader.ReadUInt32();
                Identifier = reader.ReadUInt32();
                Index = reader.ReadUInt32();

                reader.Seek(blockStart + HeaderSize, System.IO.SeekOrigin.Begin);
                data = reader.ReadBytes((int)DataSize);
            }
            public void Write(FileWriter writer)
            {
                long blockStart = writer.Position;

                writer.WriteSignature(Magic);
                writer.Write(HeaderSize);
                writer.Write(MajorVersion);
                writer.Write(MinorVersion);
                writer.Write(BlockType, false);
                writer.Write(data.Length);
                writer.Write(Identifier);
                writer.Write(Index);
                writer.Seek(blockStart + HeaderSize, System.IO.SeekOrigin.Begin);

                writer.Write(data);
            }
        }
        public class TextureData : STGenericTexture
        {
            public override TEX_FORMAT[] SupportedFormats
            {
                get
                {
                    return new TEX_FORMAT[]
                    {
                        TEX_FORMAT.BC1_UNORM,
                        TEX_FORMAT.BC1_UNORM_SRGB,
                        TEX_FORMAT.BC2_UNORM,
                        TEX_FORMAT.BC2_UNORM_SRGB,
                        TEX_FORMAT.BC3_UNORM,
                        TEX_FORMAT.BC3_UNORM_SRGB,
                        TEX_FORMAT.BC4_UNORM,
                        TEX_FORMAT.BC4_SNORM,
                        TEX_FORMAT.BC5_UNORM,
                        TEX_FORMAT.BC5_SNORM,
                        TEX_FORMAT.B5G5R5A1_UNORM,
                        TEX_FORMAT.B5G6R5_UNORM,
                        TEX_FORMAT.B8G8R8A8_UNORM_SRGB,
                        TEX_FORMAT.B8G8R8A8_UNORM,
                        TEX_FORMAT.R10G10B10A2_UNORM,
                        TEX_FORMAT.R16_UNORM,
                        TEX_FORMAT.B4G4R4A4_UNORM,
                        TEX_FORMAT.R8G8B8A8_UNORM_SRGB,
                        TEX_FORMAT.R8G8B8A8_UNORM,
                        TEX_FORMAT.R8_UNORM,
                        TEX_FORMAT.R8G8_UNORM,
                        TEX_FORMAT.R32G8X24_FLOAT,
                    };
                }
            }

            public override bool CanEdit { get; set; } = true;

            public SurfaceInfoParse surface;

            public TextureData()
            {
                ImageKey = "Texture";
                SelectedImageKey = "Texture";

                CanDelete = false;
                CanReplace = true;
                CanRename = false;
            }

            public override string ExportFilter => FileFilters.GTX;
            public override string ReplaceFilter => FileFilters.GTX;

            private void ApplySurface(GX2.GX2Surface NewSurface)
            {
                surface.aa = NewSurface.aa;
                surface.alignment = NewSurface.alignment;
                surface.bpp = NewSurface.bpp;
                surface.compSel = NewSurface.compSel;
                surface.data = NewSurface.data;
                surface.depth = NewSurface.depth;
                surface.dim = NewSurface.dim;
                surface.firstMip = NewSurface.firstMip;
                surface.firstSlice = NewSurface.firstSlice;
                surface.format = NewSurface.format;
                surface.height = NewSurface.height;
                surface.imageCount = NewSurface.imageCount;
                surface.imageSize = NewSurface.imageSize;
                surface.mipData = NewSurface.mipData;
                surface.mipSize = NewSurface.mipSize;
                surface.mipOffset = NewSurface.mipOffset;
                surface.numArray = NewSurface.numArray;
                surface.numMips = NewSurface.numMips;
                surface.pitch = NewSurface.pitch;
                surface.resourceFlags = NewSurface.resourceFlags;
                surface.swizzle = NewSurface.swizzle;
                surface.tileMode = NewSurface.tileMode;
                surface.use = NewSurface.use;
                surface.width = NewSurface.width;
                surface.texRegs = NewSurface.texRegs;

                SetChannelComponents();
            }

            private STChannelType SetChannel(byte compSel)
            {
                if (compSel == 0) return STChannelType.Red;
                else if (compSel == 1) return STChannelType.Green;
                else if (compSel == 2) return STChannelType.Blue;
                else if (compSel == 3) return STChannelType.Alpha;
                else if (compSel == 4) return STChannelType.Zero;
                else return STChannelType.One;
            }

            private void SetChannelComponents()
            {
                surface.compSel = new byte[4] { 0, 1, 2, 3 };
            }

            public override void SetImageData(Bitmap bitmap, int ArrayLevel)
            {
                if (bitmap == null)
                    return; //Image is likely disposed and not needed to be applied

                RedChannel = SetChannel(surface.compSel[0]);
                GreenChannel = SetChannel(surface.compSel[1]);
                BlueChannel = SetChannel(surface.compSel[2]);
                AlphaChannel = SetChannel(surface.compSel[3]);

                surface.format = (uint)FTEX.ConvertToGx2Format(Format);
                surface.width = (uint)bitmap.Width;
                surface.height = (uint)bitmap.Height;

                if (MipCount != 1)
                {
                    MipCount = GenerateMipCount(bitmap.Width, bitmap.Height);
                    if (MipCount == 0)
                        MipCount = 1;
                }

                surface.numMips = MipCount;
                surface.mipOffset = new uint[MipCount];

                //Create image block from bitmap first
                var data = GenerateMipsAndCompress(bitmap, MipCount, Format);

                //Swizzle and create surface
                var NewSurface = GX2.CreateGx2Texture(data, Text,
                    (uint)surface.tileMode,
                    (uint)surface.aa,
                    (uint)surface.width,
                    (uint)surface.height,
                    (uint)surface.depth,
                    (uint)surface.format,
                    (uint)0,
                    (uint)surface.dim,
                    (uint)surface.numMips
                    );

                ApplySurface(NewSurface);
                IsEdited = true;
                LoadOpenGLTexture();
                LibraryGUI.UpdateViewport();
            }

            public override byte[] GetImageData(int ArrayLevel = 0, int MipLevel = 0, int DepthLevel = 0)
            {
                RedChannel = SetChannel(surface.compSel[0]);
                GreenChannel = SetChannel(surface.compSel[1]);
                BlueChannel = SetChannel(surface.compSel[2]);
                AlphaChannel = SetChannel(surface.compSel[3]);


                Console.WriteLine("");
                Console.WriteLine("// ----- GX2Surface Info ----- ");
                Console.WriteLine("  dim             = " + surface.dim);
                Console.WriteLine("  width           = " + surface.width);
                Console.WriteLine("  height          = " + surface.height);
                Console.WriteLine("  depth           = " + surface.depth);
                Console.WriteLine("  numMips         = " + surface.numMips);
                Console.WriteLine("  format          = " + surface.format);
                Console.WriteLine("  aa              = " + surface.aa);
                Console.WriteLine("  use             = " + surface.use);
                Console.WriteLine("  imageSize       = " + surface.imageSize);
                Console.WriteLine("  mipSize         = " + surface.mipSize);
                Console.WriteLine("  tileMode        = " + surface.tileMode);
                Console.WriteLine("  swizzle         = " + surface.swizzle);
                Console.WriteLine("  alignment       = " + surface.alignment);
                Console.WriteLine("  pitch           = " + surface.pitch);
                Console.WriteLine("  bits per pixel  = " + (surface.bpp << 3));
                Console.WriteLine("  bytes per pixel = " + surface.bpp);
                Console.WriteLine("  data size       = " + surface.data.Length);
                Console.WriteLine("  mip size        = " + surface.mipData.Length);
                Console.WriteLine("  realSize        = " + surface.imageSize);

                return GX2.Decode(surface, ArrayLevel, MipLevel);
            }
            private void Remove(object sender, EventArgs args) {
                ((GTXFile)Parent).Nodes.Remove(this);
            }

            public override void Export(string FileName) {
                Export(FileName);
            }

            public override void Replace(string FileName)
            {
                FTEX ftex = new FTEX();
                ftex.ReplaceTexture(FileName, Format);
                if (ftex.texture != null)
                {
                    surface.swizzle = ftex.texture.Swizzle;
                    surface.tileMode = (uint)ftex.texture.TileMode;
                    surface.format = (uint)ftex.texture.Format;
                    surface.aa = (uint)ftex.texture.AAMode;
                    surface.use = (uint)ftex.texture.Use;
                    surface.alignment = (uint)ftex.texture.Alignment;
                    surface.dim = (uint)ftex.texture.Dim;
                    surface.width = (uint)ftex.texture.Width;
                    surface.height = (uint)ftex.texture.Height;
                    surface.depth = (uint)ftex.texture.Depth;
                    surface.numMips = (uint)ftex.texture.MipCount;
                    surface.imageSize = (uint)ftex.texture.Data.Length;
                    surface.mipSize = (uint)ftex.texture.MipData.Length;
                    surface.data = ftex.texture.Data;
                    surface.mipData = ftex.texture.MipData;
                    surface.mipOffset = ftex.texture.MipOffsets;
                    surface.firstMip = ftex.texture.ViewMipFirst;
                    surface.firstSlice = 0;
                    surface.numSlices = ftex.texture.ArrayLength;
                    surface.imageCount = ftex.texture.MipCount;
                    surface.pitch = ftex.texture.Pitch;
                    surface.texRegs = GX2.CreateRegisters(surface);

                    SetChannelComponents();

                    Format = FTEX.ConvertFromGx2Format((Syroot.NintenTools.Bfres.GX2.GX2SurfaceFormat)surface.format);
                    Width = surface.width;
                    Height = surface.height;
                    MipCount = surface.numMips;
                    ArrayCount = surface.depth;

                    ImageEditorBase editor = (ImageEditorBase)LibraryGUI.GetActiveContent(typeof(ImageEditorBase));

                    if (editor != null)
                        UpdateEditor();
                }
            }
            public override void OnClick(TreeView treeView)
            {
                UpdateEditor();
            }

            public void UpdateEditor()
            {
                ImageEditorBase editor = (ImageEditorBase)LibraryGUI.GetActiveContent(typeof(ImageEditorBase));
                if (editor == null)
                {
                    editor = new ImageEditorBase();
                    editor.Dock = DockStyle.Fill;

                    LibraryGUI.LoadEditor(editor);
                }
                editor.Text = Text;
                var tex = FTEX.FromGx2Surface(surface, Text);
                tex.MipCount = MipCount;
                editor.LoadProperties(tex);
                editor.LoadImage(this);
            }
        }
        public class SurfaceInfoParse : GX2.GX2Surface
        {
            public void Read(FileReader reader)
            {
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;

                dim = reader.ReadUInt32();
                width = reader.ReadUInt32();
                height = reader.ReadUInt32();
                depth = reader.ReadUInt32();
                numMips = reader.ReadUInt32();
                format = reader.ReadUInt32();
                aa = reader.ReadUInt32();
                use = reader.ReadUInt32();
                imageSize = reader.ReadUInt32();
                imagePtr = reader.ReadUInt32();
                mipSize = reader.ReadUInt32();
                mipPtr = reader.ReadUInt32();
                tileMode = reader.ReadUInt32();
                swizzle = reader.ReadUInt32();
                alignment = reader.ReadUInt32();
                pitch = reader.ReadUInt32();
                mipOffset = reader.ReadUInt32s(13);
                firstMip = reader.ReadUInt32();
                imageCount = reader.ReadUInt32();
                firstSlice = reader.ReadUInt32();
                numSlices = reader.ReadUInt32();
                compSel = reader.ReadBytes(4);
                texRegs = reader.ReadUInt32s(5);
            }

            public byte[] Write()
            {
                MemoryStream mem = new MemoryStream();

                FileWriter writer = new FileWriter(mem);
                writer.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;
                writer.Write(dim);
                writer.Write(width);
                writer.Write(height);
                writer.Write(depth);
                writer.Write(numMips);
                writer.Write(format);
                writer.Write(aa);
                writer.Write(use);
                writer.Write(imageSize);
                writer.Write(imagePtr);
                writer.Write(mipSize);
                writer.Write(mipPtr);
                writer.Write(tileMode);
                writer.Write(swizzle);
                writer.Write(alignment);
                writer.Write(pitch);

                for (int i = 0; i < 13; i++)
                {
                    if (mipOffset.Length > i)
                        writer.Write(mipOffset[i]);
                    else
                        writer.Write(0);
                }

                writer.Write(firstMip);
                writer.Write(imageCount);
                writer.Write(firstSlice);
                writer.Write(numSlices);

                for (int i = 0; i < 4; i++)
                {
                    if (compSel != null && compSel.Length > i)
                        writer.Write(compSel[i]);
                    else
                        writer.Write((byte)0);
                }

                for (int i = 0; i < 5; i++)
                {
                    if (texRegs != null && texRegs.Length > i)
                        writer.Write(texRegs[i]);
                    else
                        writer.Write(0);
                }

                return mem.ToArray();
            }
        }
    }


}
