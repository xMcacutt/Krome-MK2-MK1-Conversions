using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDL3_MDL2_Converter
{
    internal class MDL
    {
        public ushort MatrixCount;
        public ushort ComponentCount;
        public ushort RefPointCount;
        public ushort TextureCount;
        public uint TextureListOffset;
        public ushort AnimNodeCount;
        public uint ComponentDescriptionsOffset;
        public uint ObjectLookupTable;
        public uint RefPointsOffsetsOffset;
        public uint RefPointsOffsetMDL2;
        public uint AnimNodeDataOffset;
        public float[] BoundingBoxStartPos = new float[4];
        public float[] BoundingBoxSize = new float[4];
        public uint StringTableStringCount;
        public uint StringTableOffset;
        public uint CreationTime;
        public uint OldFilePathStringOffset;
        public ushort StripCount;
        public ushort MeshCount;
        public List<Component> Components = new();
    }
}
