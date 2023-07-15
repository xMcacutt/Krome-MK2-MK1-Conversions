using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDL3_MDL2_Converter
{
    internal class Component
    {
        public float[] BoundingBoxStartPos = new float[4];
        public float[] BoundingBoxSize = new float[4];
        public float[] Origin = new float[4];
        public uint ComponentNameOffset;
        public string? ComponentName;
        public uint AddlStringOffset;
        public string? AddlString;
        public uint VBoneCount;
        public ushort MeshCount;
        public uint MeshDescriptionOffset;
        public List<MeshDescription> MeshDescriptions = new();
    }
}
