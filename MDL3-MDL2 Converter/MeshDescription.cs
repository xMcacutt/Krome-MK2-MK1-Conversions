using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDL3_MDL2_Converter
{
    internal class MeshDescription
    {
        public uint TextureNameOffset;
        public string? TextureName;
        public uint MDGOffset;
        public int TextureIndex;
        public uint MaxOffset;
        public uint StripListOffset;
        public uint StripCount;
        public ushort LineCount;
        public List<Strip> Strips = new();
    }
}
