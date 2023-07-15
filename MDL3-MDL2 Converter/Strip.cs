using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDL3_MDL2_Converter
{
    internal class Strip
    {
        public uint floatDataStart;
        public uint MDGDataEndOffset;
        public byte VertexCount;
        public byte[]? floatData;
        public byte[]? charData;
        public byte[]? shortData;
        public byte[]? byteData;
    }
}
