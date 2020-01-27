using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syroot.NintenTools.Bfres;
using Syroot.NintenTools.Bfres.GX2;
using Bfres.Structs;
using System.IO;

namespace FirstPlugin.Forms
{
    public partial class RenderStateEditor : UserControl
    {
        public RenderStateEditor()
        {
            InitializeComponent();
        }

        RenderState activeRenderState;

        public void LoadRenderState(FMAT mat, RenderState renderState)
        {
            StreamWriter dump = new StreamWriter("C:/Users/Jon/Desktop/Dump/" + mat.Text + "_RenderState.csv");
            dump.AutoFlush = true;

            dump.WriteLine("AlphaControl.RenderState,"      + renderState.FlagsMode);
            dump.WriteLine("AlphaControl.AlphaTestEnabled," + renderState.AlphaTestEnabled);
            dump.WriteLine("AlphaControl.AlphaFunc,"        + renderState.AlphaFunc);
            dump.WriteLine("AlphaControl.AlphaRef,"         + renderState.AlphaRefValue);

            dump.WriteLine("BlendControl.BlendMode,"                + renderState.FlagsBlendMode);
            dump.WriteLine("BlendControl.ColorSourceBlend,"         + renderState.ColorSourceBlend);
            dump.WriteLine("BlendControl.ColorCombine,"             + renderState.ColorCombine);
            dump.WriteLine("BlendControl.ColorDestinationBlend,"    + renderState.ColorDestinationBlend);
            dump.WriteLine("BlendControl.AlphaSourceBlend,"         + renderState.AlphaSourceBlend);
            dump.WriteLine("BlendControl.AlphaCombine,"             + renderState.AlphaCombine);
            dump.WriteLine("BlendControl.AlphaDestinationBlend,"    + renderState.AlphaDestinationBlend);
            dump.WriteLine("BlendControl.SeparateAlphaBlend,"       + renderState.SeparateAlphaBlend);
            dump.WriteLine("BlendControl.BlendColor," + 
                renderState.BlendColor.X + "," + 
                renderState.BlendColor.Y + "," + 
                renderState.BlendColor.Z + "," + 
                renderState.BlendColor.W + ",");
            dump.WriteLine("BlendControl.BlendTarget,"              + renderState.BlendTarget);

            dump.WriteLine("ColorControl.MultiWriteEnabled,"    + renderState.MultiWriteEnabled);
            dump.WriteLine("ColorControl.ColorBufferEnabled,"   + renderState.ColorBufferEnabled);
            dump.WriteLine("ColorControl.BlendEnableMask,"      + renderState.BlendEnableMask);
            dump.WriteLine("ColorControl.LogicOp,"              + renderState.LogicOp);

            dump.WriteLine("DepthControl.DepthTestEnabled,"     + renderState.DepthTestEnabled);
            dump.WriteLine("DepthControl.DepthWriteEnabled,"    + renderState.DepthWriteEnabled);
            dump.WriteLine("DepthControl.DepthFunc,"            + renderState.DepthFunc);
            dump.WriteLine("DepthControl.StencilTestEnabled,"   + renderState.StencilTestEnabled);
            dump.WriteLine("DepthControl.BackStencilEnabled,"   + renderState.BackStencilEnabled);
            dump.WriteLine("DepthControl.FrontStencilFunc,"     + renderState.FrontStencilFunc);
            dump.WriteLine("DepthControl.FrontStencilFail,"     + renderState.FrontStencilFail);
            dump.WriteLine("DepthControl.FrontStencilZPass,"    + renderState.FrontStencilZPass);
            dump.WriteLine("DepthControl.FrontStencilZFail,"    + renderState.FrontStencilZFail);
            dump.WriteLine("DepthControl.BackStencilFunc,"      + renderState.BackStencilFunc);
            dump.WriteLine("DepthControl.BackStencilFail,"      + renderState.BackStencilFail);
            dump.WriteLine("DepthControl.BackStencilZPass,"     + renderState.BackStencilZPass);
            dump.WriteLine("DepthControl.BackStencilZFail,"     + renderState.BackStencilZFail);

            dump.WriteLine("PolygonControl.CullFront,"                  + renderState.CullFront);
            dump.WriteLine("PolygonControl.CullBack,"                   + renderState.CullBack);
            dump.WriteLine("PolygonControl.FrontFace,"                  + renderState.FrontFace);
            dump.WriteLine("PolygonControl.PolygonModeEnabled,"         + renderState.PolygonModeEnabled);
            dump.WriteLine("PolygonControl.PolygonModeFront,"           + renderState.PolygonModeFront);
            dump.WriteLine("PolygonControl.PolygonModeBack,"            + renderState.PolygonModeBack);
            dump.WriteLine("PolygonControl.PolygonOffsetFrontEnabled,"  + renderState.PolygonOffsetFrontEnabled);
            dump.WriteLine("PolygonControl.PolygonOffsetBackEnabled,"   + renderState.PolygonOffsetBackEnabled);
            dump.WriteLine("PolygonControl.PolygonLineOffsetEnabled,"   + renderState.PolygonLineOffsetEnabled);

            activeRenderState = renderState;

            stPropertyGrid1.LoadProperty(renderState, OnPropertyChanged);
        }

        public void OnPropertyChanged()
        {
            
        }
    }
}
