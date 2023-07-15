using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace MDL3_MDL2_Converter
{
    internal class ANMConverter
    {
        public static void ConvertANM(string anm3Path, string angPath, string anm2Path)
        {
            byte[] anmData = File.ReadAllBytes(anm3Path);
            byte[] angData = File.ReadAllBytes(angPath);

            using MemoryStream stream = new();
            ANM anm2 = new ANM();
            Dictionary<string, uint> transforms = new();
            stream.Write("ANIM"u8);

            anm2.FrameCount = BitConverter.ToUInt16(anmData, 0x6);
            stream.Write(BitConverter.GetBytes(anm2.FrameCount)); // Frame Count

            anm2.BoneDescCount = anmData[4];
            stream.Write(BitConverter.GetBytes(anm2.BoneDescCount)); // Bone Desc Count
            stream.Write(BitConverter.GetBytes(0x30)); // Bone Desc Offset
            stream.Write(BitConverter.GetBytes(0x0));
            stream.Write(new byte[] { 0x0, 0x0, 0x0, 0x19 });
            int anm3TransformInfoOffset = (int)BitConverter.ToUInt32(anmData, 0x8);
            anm2.TransformsCount = (int)BitConverter.ToUInt32(anmData, anm3TransformInfoOffset + 0x4);
            //Console.WriteLine(anm2.TransformsCount);
            stream.Write(BitConverter.GetBytes(anm2.TransformsCount)); // Transform Count

            anm2.TransformsOffset = 0x30 + anm2.BoneDescCount * 0x20;
            stream.Write(BitConverter.GetBytes(anm2.TransformsOffset)); // Transform Offset

            stream.Write(new byte[8]); // String Table Count And Offset !!!

            stream.Write(new byte[8]); // Unknown (Unused) Bytes

            for (int i = 0; i < anm2.BoneDescCount; i++)
            {
                //Console.WriteLine("Bone: " + i);
                Bone bone = new();
                int boneOffset = 0x10 + i * 0x20;
                bone.NodePos = Enumerable.Range(0, 4).Select(index => BitConverter.ToSingle(anmData, boneOffset + index * 0x4)).ToArray();
                stream.Write(anmData, boneOffset + 0x0, 0x10);
                int nameOffset = (int)BitConverter.ToUInt32(anmData, boneOffset + 0x10);
                bone.Name = Program.ReadString(anmData, nameOffset);
                stream.Write(new byte[4]); // Bone Name Offset !!!
                bone.ParentID = BitConverter.ToUInt16(anmData, boneOffset + 0x16);
                stream.Write(BitConverter.GetBytes((uint)bone.ParentID)); // Parent ID
                bone.KeyFrameCount = BitConverter.ToUInt16(anmData, boneOffset + 0x14);
                stream.Write(BitConverter.GetBytes(bone.KeyFrameCount));
                stream.Write(new byte[4]); // Keyframes Offset !!!
                anm2.Bones.Add(bone);
            }
            foreach (Bone bone in anm2.Bones)
            {
                int boneOffset = 0x10 + anm2.Bones.IndexOf(bone) * 0x20;
                //Console.WriteLine(bone.KeyFrameCount);
                int anm3KeyFrameOffset = (int)BitConverter.ToUInt32(anmData, boneOffset + 0x18);
                for (int k = 0; k < bone.KeyFrameCount; k++)
                {
                    KeyFrame keyFrame = new KeyFrame();
                    keyFrame.FrameIndex = BitConverter.ToUInt16(anmData, k * 0x8 + anm3KeyFrameOffset + 0x0);
                    int[] angIndices = new int[3];
                    for(int x = 0; x < 3; x++)
                    {
                        angIndices[x] = BitConverter.ToUInt16(anmData, k * 0x8 + anm3KeyFrameOffset + 0x2 * (x + 1));
                        ushort[] transformShorts = new ushort[4];
                        for (int n = 0; n < 4; n++)
                        {
                            transformShorts[n] = BitConverter.ToUInt16(angData, 0x10 + 0x8 * angIndices[x] + n * 0x2);
                        }
                        keyFrame.Transform[x] = DecompressVector(transformShorts); 
                        byte[] transformBytes = GenerateTransformBytes(keyFrame.Transform[x], x == 0, keyFrame.FrameIndex);
                        string hash = Convert.ToBase64String(transformBytes);
                        if (!transforms.TryAdd(hash, (uint)stream.Position))
                        {
                            keyFrame.TransformOffsets[x] = transforms[hash];
                            continue;
                        }
                        keyFrame.TransformOffsets[x] = (uint)stream.Position;
                        stream.Write(transformBytes, 0, 0x10);
                    }
                    bone.KeyFrames.Add(keyFrame);
                }
            }

            List<uint> keyFrameStartOffsets = new List<uint>();
            foreach(Bone bone in anm2.Bones)
            {
                keyFrameStartOffsets.Add((uint)stream.Position);
                foreach(KeyFrame keyFrame in bone.KeyFrames)
                {
                    foreach (uint i in keyFrame.TransformOffsets) stream.Write(BitConverter.GetBytes(i));
                }
            }

            Dictionary<string, uint> stringSet = new();
            foreach(Bone bone in anm2.Bones)
            {
                stringSet.Add(bone.Name, (uint)stream.Position);
                stream.Write(Encoding.ASCII.GetBytes(bone.Name));
                stream.WriteByte(0x0);
            }
            stream.Write("end"u8);

            byte[] anm2Data = stream.ToArray();
            stream.Close();

            for(int i = 0; i < anm2.BoneDescCount; i++)
            {
                Array.Copy(BitConverter.GetBytes(stringSet[anm2.Bones[i].Name]), 0x0, anm2Data, 0x30 + i * 0x20 + 0x10, 4);
                Array.Copy(BitConverter.GetBytes(keyFrameStartOffsets[i]), 0x0, anm2Data, 0x30 + i * 0x20 + 0x1C, 4);
            }

            using FileStream fileStream = new(anm2Path, FileMode.Create);
            fileStream.Write(anm2Data, 0, anm2Data.Length);
            fileStream.Close();
        }

        public static float[] DecompressVector(ushort[] input)
        {
            uint checker;
            uint key;
            uint buffer;
            float[] output = new float[3];

            key = (uint)input[3];
            buffer = 0;

            checker = (key >> 0xA) & 0x1f;
            if (checker != 0) buffer = ((uint)(input[0] & 0x7fff) << 0x8) | ((uint)(input[1] & 0x8000) << 0x10) | (checker + 0x70) * 0x800000;
            output[0] = Convert.ToSingle(buffer);
            buffer = 0;

            checker = (key >> 0x5) & 0x1f;
            if (checker != 0) buffer = ((uint)(input[1] & 0x7fff) << 0x8) | ((uint)(input[1] & 0x8000) << 0x10) | (checker + 0x70) * 0x800000;
            output[1] = buffer;
            buffer = 0;

            if ((key & 0x1f) != 0) buffer = ((uint)(input[2] & 0x7fff) << 0x8) | ((uint)(input[2] & 0x8000) << 0x10) | ((key & 0x1f) + 0x70) * 0x800000; ;
            output[2] = buffer;
            return output;
        }

        public static byte[] GenerateTransformBytes(float[] transform, bool isPos, int frameIndex)
        {
            byte[] bytes = new byte[0x10];
            Buffer.BlockCopy(transform, 0, bytes, 0, 0xC);
            if (isPos) Buffer.BlockCopy(BitConverter.GetBytes(frameIndex), 0, bytes, 0xC, 0x4);
            else Buffer.BlockCopy(new byte[] { 0x0, 0x0, 0x80, 0x3F }, 0, bytes, 0xC, 0x4);
            return bytes;
        }
    }

    public class ANM
    {
        public int FrameCount;
        public int BoneDescCount;
        public int TransformsCount;
        public int TransformsOffset;
        public List<Bone> Bones = new();
    }

    public class Bone
    {
        public float[] NodePos = new float[4];
        public string Name;
        public int NameOffset;
        public int ParentID;
        public int KeyFrameCount;
        public int KeyFramesOffset;
        public List<KeyFrame> KeyFrames = new();
    }

    public class KeyFrame
    {
        public int FrameIndex;
        public uint[] TransformOffsets = new uint[3];
        public float[][] Transform = new float[3][];
    }
}
