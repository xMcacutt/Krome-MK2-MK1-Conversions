﻿// See https://aka.ms/new-console-template for more information

using System.ComponentModel;
using System.IO;
using System.Text;
using MoreLinq;

namespace MDL3_MDL2_Converter
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string mdl3Path = args[0];
            string mdgPath = args[1];
            string mdl2Path = args[2];
            if (args.Length > 3) ANMConverter.ConvertANM(args[3], args[4], args[5]);

            byte[] mdl3Data = File.ReadAllBytes(mdl3Path);
            byte[] mdgData = File.ReadAllBytes(mdgPath);

            MDL mdl3 = new();
            MDL mdl2 = new();
            Console.WriteLine("Loading Data From MDL3");
            mdl3.ComponentCount = BitConverter.ToUInt16(mdl3Data, 0x4);
            mdl3.TextureCount = BitConverter.ToUInt16(mdl3Data, 0x6);
            mdl3.RefPointCount = BitConverter.ToUInt16(mdl3Data, 0xA);
            mdl3.MatrixCount = BitConverter.ToUInt16(mdl3Data, 0x8);
            if (mdl3.MatrixCount == 0) mdl3.MatrixCount++;
            mdl3.AnimNodeCount = (ushort)((short)mdl3.MatrixCount - 1);
            mdl3.MeshCount = BitConverter.ToUInt16(mdl3Data, 0xE);
            mdl3.StripCount = BitConverter.ToUInt16(mdl3Data, 0x1E);
            mdl3.BoundingBoxStartPos = new float[]
            {
                BitConverter.ToSingle(mdl3Data, 0x30),
                BitConverter.ToSingle(mdl3Data, 0x34),
                BitConverter.ToSingle(mdl3Data, 0x38),
                BitConverter.ToSingle(mdl3Data, 0x3C)
            };
            mdl3.BoundingBoxSize = new float[]
            {
                BitConverter.ToSingle(mdl3Data, 0x40),
                BitConverter.ToSingle(mdl3Data, 0x44),
                BitConverter.ToSingle(mdl3Data, 0x48),
                BitConverter.ToSingle(mdl3Data, 0x4C)
            };
            mdl3.ComponentDescriptionsOffset = BitConverter.ToUInt16(mdl3Data, 0x50);
            mdl3.TextureListOffset = BitConverter.ToUInt32(mdl3Data, 0x54);
            mdl3.RefPointsOffsetsOffset = BitConverter.ToUInt32(mdl3Data, 0x58);
            mdl3.ObjectLookupTable = BitConverter.ToUInt32(mdl3Data, 0x68);
            mdl3.StringTableOffset = BitConverter.ToUInt16(mdl3Data, (int)mdl3.ComponentDescriptionsOffset + 0x34);
            mdl3.AnimNodeDataOffset = BitConverter.ToUInt16(mdl3Data, 0x5C);

            List<string> textureNames = new();
            for(int ti = 0; ti < mdl3.TextureCount; ti++)
            {
                textureNames.Add(ReadString(mdl3Data, BitConverter.ToInt32(mdl3Data, (int)mdl3.TextureListOffset + (ti * 0x4))));
            }

            Console.WriteLine("Loading Data From MDG");
            using var sr = File.OpenRead(mdgPath);
            for (int ci = 0; ci < mdl3.ComponentCount; ci++)
            {
                Component component = new();
                mdl3.Components.Add(component);
            }
            for(int ti = 0; ti < mdl3.TextureCount; ti++)
            {
                for(int ci = 0; ci < mdl3.ComponentCount; ci++)
                {
                    int meshRef = BitConverter.ToInt32(mdl3Data, (int)mdl3.ObjectLookupTable + (ti * 0x4 * mdl3.ComponentCount) + (ci * 0x4));
                    if (meshRef == 0) continue;
                    while (meshRef != 0)
                    {
                        MeshDescription meshDesc = new()
                        {
                            MDGOffset = (uint)meshRef,
                            TextureIndex = ti,
                        };
                        meshDesc.StripCount = BitConverter.ToUInt16(mdgData, (int)meshDesc.MDGOffset + 0x6);
                        int currentPos = (int)meshDesc.MDGOffset;
                        sr.Position = currentPos;
                        for (int si = 0; si < meshDesc.StripCount; si++)
                        {
                            Strip strip = new();
                            int[] identifiers = new int[] { 0x6C, 0x68, 0x6A, 0x65, 0x6E };

                            byte[] buffer = new byte[4];
                            byte[] startMarker = new byte[] { 0x0, 0x80, 0x02, 0x6C };
                            byte[] endMarker1 = new byte[] { 0xFF, 0xFF, 0x0, 0x1 };
                            while (!buffer.SequenceEqual(startMarker))
                            {
                                sr.Read(buffer, 0, 4);
                            }
                            sr.Read(buffer, 0, 1);
                            strip.VertexCount = buffer[0];
                            strip.floatData = new byte[0xC * strip.VertexCount];
                            strip.charData = new byte[0x4 * strip.VertexCount];
                            strip.shortData = new byte[0x8 * strip.VertexCount];
                            strip.byteData = new byte[0x4 * strip.VertexCount];
                            sr.Seek(0x27, SeekOrigin.Current);
                            strip.floatDataStart = (uint)sr.Position;
                            //Console.WriteLine("float start: " + sr.Position.ToString("X"));
                            sr.Read(strip.floatData, 0, 0xC * strip.VertexCount);
                            sr.Seek(0x2, SeekOrigin.Current);
                            sr.Read(buffer, 0, 2);
                            //Console.WriteLine(sr.Position.ToString("X"));
                            //Console.WriteLine("char start: " + sr.Position.ToString("X"));
                            if (buffer[1] == 0x65)
                            {
                                strip.charData = new byte[strip.VertexCount * 0x4];
                                //Console.WriteLine("short start: " + sr.Position.ToString("X"));
                                for (int i = 0; i < strip.VertexCount; i++) sr.Read(strip.shortData, i * 0x8, 0x4);
                            }
                            else
                            {
                                sr.Read(strip.charData, 0, 0x4 * strip.VertexCount);
                                sr.Seek(0x4, SeekOrigin.Current);
                                //Console.WriteLine("short start: " + sr.Position.ToString("X"));
                                sr.Read(strip.shortData, 0, 0x8 * strip.VertexCount);
                            }
                            //Console.WriteLine("byte start: " + sr.Position.ToString("X"));
                            sr.Seek(0x4, SeekOrigin.Current);
                            sr.Read(strip.byteData, 0, 0x4 * strip.VertexCount);
                            //Console.WriteLine("byte end: " + sr.Position.ToString("X"));
                            meshDesc.Strips.Add(strip);
                        }
                        mdl3.Components[ci].MeshCount += 1;
                        meshRef = BitConverter.ToInt32(mdgData, meshRef + 0xC);
                        mdl3.Components[ci].MeshDescriptions.Add(meshDesc);
                    }
                }
                //Console.WriteLine(((int)mdl3.ObjectLookupTable + (ti * 0x4) + (ci * mdl3.TextureCount * 4)).ToString("X"));
                //Console.WriteLine(meshRef.ToString("X"));
                
            }
            sr.Close();

            Console.WriteLine("Recreating String Table");
            int endOfStringTable = (int)BitConverter.ToUInt32(mdl3Data, 0x64) == 0 ? mdl3Data.Length : (int)BitConverter.ToUInt32(mdl3Data, 0x64);
            List<string> mdl3Strings = GenerateNewStringTable(mdl3Data[(int)mdl3.StringTableOffset..endOfStringTable]);

            byte[] zeroInt = new byte[] { 0, 0, 0, 0 };

            using var stream = new MemoryStream();
            Console.WriteLine("Generating MDL2 Header");
            stream.Write(Encoding.ASCII.GetBytes("MDL2"));
            stream.Write(BitConverter.GetBytes(mdl3.MatrixCount));
            stream.Write(BitConverter.GetBytes(mdl3.ComponentCount));
            stream.Write(BitConverter.GetBytes(mdl3.RefPointCount));
            stream.Write(BitConverter.GetBytes(mdl3.AnimNodeCount));
            stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //COMPONENT DESC OFFSET
            stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //REF POINTS OFFSET
            stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //ANIM NODE DATA OFFSET
            stream.Write(zeroInt);
            stream.Write(zeroInt);
            foreach (float f in mdl3.BoundingBoxStartPos) stream.Write(BitConverter.GetBytes(f));
            foreach (float f in mdl3.BoundingBoxSize) stream.Write(BitConverter.GetBytes(f));
            stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //DICTIONARY COUNT
            stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //DICTIONARY OFFSET
            stream.Write(new byte[] { 0x1, 0x2, 0x3, 0x3, 0x5, 0x0, 0x0, 0x0 });
            DateTimeOffset now = DateTime.Now;
            stream.Write(BitConverter.GetBytes((int)now.ToUnixTimeMilliseconds()));
            stream.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }); //ORIGINAL FILE STRING OFFSET !
            stream.Write(zeroInt);
            stream.Write(new byte[] { 0x5, 0x0, 0x1, 0x0 });
            stream.Write(new byte[] { 0x10, 0x0, 0x0, 0x0 });
            stream.Write(zeroInt);
            stream.Write(zeroInt);
            stream.Write(zeroInt);
            Console.WriteLine("Generating MDL2 Components");
            mdl2.ComponentDescriptionsOffset = (uint)stream.Position;
            foreach (Component c in mdl3.Components)
            {
                int componentOffset = (mdl3.Components.IndexOf(c) * 0x40) + (int)mdl3.ComponentDescriptionsOffset;
                //BOUNDING BOX POS
                stream.Write(mdl3Data, componentOffset + 0x0, 0x30);
                c.ComponentName = ReadString(mdl3Data, BitConverter.ToInt32(mdl3Data, componentOffset + 0x30));
                stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //COMPONENT NAME
                stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //COMPONENT ADDL
                stream.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF});
                stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //BONE COUNT
                stream.Write(new byte[] { 0x0, 0x0 });
                stream.Write(BitConverter.GetBytes(c.MeshCount)); //MESH COUNT 
                stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //MESH DESCRIPTION OFFSET
                stream.Write(zeroInt);
                stream.Write(zeroInt);
            }
            Console.WriteLine("Generating MDL2 RefPoints");
            if(mdl3.RefPointCount != 0) mdl2.RefPointsOffsetMDL2 = (uint)stream.Position;
            List<string> refPointNames = new List<string>();
            for (int i = 0; i < mdl3.RefPointCount; i++)
            {
                int refPointOffset = BitConverter.ToInt32(mdl3Data, (int)mdl3.RefPointsOffsetsOffset + (i * 0x4));
                stream.Write(mdl3Data, refPointOffset + 0x0, 0x10);
                Console.WriteLine(BitConverter.ToInt32(mdl3Data, refPointOffset + 0x10).ToString("X"));
                refPointNames.Add(ReadString(mdl3Data, BitConverter.ToInt32(mdl3Data, refPointOffset + 0x10)));
                stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }); //REF POINT NAME
                stream.Write(zeroInt); //MAY BE WRONG (SPEAK TO KANA)
                stream.Write(mdl3Data, refPointOffset + 0x18, 0x8);
            }
            Console.WriteLine("Generating MDL2 Meshes");
            foreach (Component c in mdl3.Components)
            {
                c.MeshDescriptionOffset = (uint)stream.Position;
                foreach(MeshDescription m in c.MeshDescriptions)
                {
                    stream.Write(new byte[] { 0, 0, 0, 0 }); //TEXTURE NAME
                    stream.Write(new byte[] { 0, 0, 0, 0 }); //STRIP LIST OFFSET
                    stream.Write(new byte[] { 0, 0, 0, 0 }); //MAX OFFSET
                    stream.Write(BitConverter.GetBytes(m.StripCount)); //STRIP COUNT
                }
            }
            foreach(Component c in mdl3.Components)
            {
                foreach (MeshDescription m in c.MeshDescriptions)
                {
                    m.StripListOffset = (uint)stream.Position;
                    stream.Write(new byte[] { 0, 0 }); //MESH LINE COUNT
                    stream.Write(new byte[] { 0x0, 0x10 });
                    stream.Write(zeroInt);
                    foreach (Strip s in m.Strips)
                    {
                        stream.Write(new byte[] { 0x0, 0x80, 0x02, 0x6C });
                        stream.Write(new byte[] { s.VertexCount, 0x0, 0x0, 0x0 });
                        stream.Write(zeroInt);
                        stream.Write(zeroInt);
                        stream.Write(new byte[] { 0x01, 0x0, 0x0, 0x0 });
                        stream.Write(new byte[] { s.VertexCount, 0x80, 0x0, 0x0 });
                        stream.Write(new byte[] { 0x00, 0x40, 0x3E, 0x30, 0x12, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x01, 0x00, 0x01 });
                        stream.Write(new byte[] { 0x02, 0x80, s.VertexCount, 0x68 });
                        stream.Write(s.floatData, 0, s.floatData.Length);
                        stream.Write(new byte[] { 0x03, 0x80, s.VertexCount, 0x6A });
                        stream.Write(s.charData, 0, s.charData.Length);
                        stream.Write(new byte[] { 0x04, 0x80, s.VertexCount, 0x65 });
                        stream.Write(s.shortData, 0, s.shortData.Length);
                        stream.Write(new byte[] { 0x05, 0x80, s.VertexCount, 0x6E });
                        stream.Write(s.byteData, 0, s.byteData.Length);
                        stream.Write(new byte[] { 0xFF, 0xFF, 0x00, 0x01, 0x00, 0x00, 0x00, 0x14 });
                    }
                    m.LineCount = (ushort)((stream.Position - m.StripListOffset) / 0x10);
                    stream.Write(new byte[] { 0x60, 0x0, 0x0, 0x0 });
                    stream.Write(zeroInt);
                    stream.Write(zeroInt);
                    stream.Write(zeroInt);
                }
            }
            stream.Write(zeroInt);
            stream.Write(zeroInt);
            stream.Write(zeroInt);
            stream.Write(zeroInt);
            if (mdl3.AnimNodeDataOffset != 0)
            {
                if (mdl3.AnimNodeCount != 0) mdl2.AnimNodeDataOffset = (uint)stream.Position;
                for (int ai = 0; ai < mdl3.AnimNodeCount; ai++)
                {
                    stream.Write(mdl3Data, (int)mdl3.AnimNodeDataOffset + (0x10 * ai), 0x10);
                }
            }

            Console.WriteLine("Generating MDL2 Strings");
            mdl2.StringTableOffset = (uint)stream.Position;
            Dictionary<string, int> stringMap = new();
            // WRITE IN STRING TABLE
            foreach(string s in mdl3Strings)
            {
                stringMap.Add(s, (int)stream.Position);
                stream.Write(Encoding.ASCII.GetBytes(s));
                stream.Write(new byte[] { 0x0 });
            }
            mdl2.StringTableStringCount = (uint)stringMap.Count + 1;
            stream.Write("end"u8);
            stream.Close();

            byte[] bytes = stream.ToArray();

            Console.WriteLine("Running Second Pass");
            //COMPONENT DESC OFFSET
            Array.Copy(BitConverter.GetBytes(mdl2.ComponentDescriptionsOffset), 0, bytes, 0xC, 4);
            //REF POINTS OFFSET
            Array.Copy(BitConverter.GetBytes(mdl2.RefPointsOffsetMDL2), 0, bytes, 0x10, 4);
            //ANIM NODE DATA OFFSET
            Array.Copy(BitConverter.GetBytes(mdl2.AnimNodeDataOffset), 0, bytes, 0x14, 4);
            //DICTIONARY COUNT
            Array.Copy(BitConverter.GetBytes(mdl2.StringTableStringCount), 0, bytes, 0x40, 4);
            //DICTIONARY OFFSET
            Array.Copy(BitConverter.GetBytes(mdl2.StringTableOffset), 0, bytes, 0x44, 4);

            int componentIndex = 0;
            foreach(Component c in mdl3.Components)
            {
                //COMPONENT NAME
                Array.Copy(BitConverter.GetBytes(stringMap[c.ComponentName]), 0, bytes, mdl2.ComponentDescriptionsOffset + (0x50 * componentIndex) + 0x30, 4);
                //MESH DESCRIPTION OFFSET
                Array.Copy(BitConverter.GetBytes(c.MeshDescriptionOffset), 0, bytes, mdl2.ComponentDescriptionsOffset + (0x50 * componentIndex) + 0x44, 4);
                
                int meshIndex = 0;
                foreach(MeshDescription m in c.MeshDescriptions)
                {
                    int mStartPos = (int)c.MeshDescriptionOffset + (0x10 * meshIndex);
                    //TEXTURE NAME
                    Array.Copy(BitConverter.GetBytes(stringMap[textureNames[m.TextureIndex]]), 0, bytes, mStartPos + 0x0, 4);
                    //STRIP LIST OFFSET
                    Array.Copy(BitConverter.GetBytes(m.StripListOffset), 0, bytes, mStartPos + 0x4, 4);
                    //MESH LINE COUNT
                    Array.Copy(BitConverter.GetBytes(m.LineCount), 0, bytes, m.StripListOffset, 2);
                    meshIndex++;
                }
                componentIndex++;
            }

            //COMPONENT ADDL //
            //BONE COUNT //

            for (int ri = 0; ri < mdl3.RefPointCount; ri++)
            {
                //REF POINT NAME
                Array.Copy(BitConverter.GetBytes(stringMap[refPointNames[ri]]), 0, bytes, mdl2.RefPointsOffsetMDL2 + (0x20 * ri) + 0x10, 4);
            }

            using FileStream fileStream = new(mdl2Path, FileMode.Create);
            fileStream.Write(bytes, 0, bytes.Length);
            fileStream.Close();
            Console.WriteLine("Done");
        }

        public static List<string> GenerateNewStringTable(byte[] mdl3Table)
        {
            byte sep = 0x0;
            byte[][] bytes = mdl3Table
                .Split(sep)
                .Select(s => s.ToArray())
                .ToArray();
            List<string> strings = new();
            foreach (byte[] stringBytes in bytes)
            {
                if (stringBytes.Length == 0) continue;
                strings.Add(Encoding.ASCII.GetString(stringBytes, 0, stringBytes.Length));
            }
            return strings;
        }

        public static string ReadString(byte[] bytes, int position)
        {
            int endOfString = Array.IndexOf<byte>(bytes, 0x0, position);
            if (endOfString == position) return string.Empty;
            string s = Encoding.ASCII.GetString(bytes, position, endOfString - position);
            return s.Replace(" ", @"___");
        }
    }
}