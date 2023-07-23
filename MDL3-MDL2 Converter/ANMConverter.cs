using MoreLinq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
            stream.Write(new byte[] { 0x19, 0x0, 0x0, 0x0 });
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
                bone.NodePos = Enumerable.Range(0, 3).Select(index => BitConverter.ToSingle(anmData, boneOffset + index * 0x4)).ToArray();
                stream.Write(anmData, boneOffset + 0x0, 0xC);
                stream.Write(new byte[4]);
                int nameOffset = (int)BitConverter.ToUInt32(anmData, boneOffset + 0x10);
                bone.Name = Program.ReadString(anmData, nameOffset);
                stream.Write(new byte[4]); // Bone Name Offset !!!
                bone.ParentID = BitConverter.ToUInt16(anmData, boneOffset + 0x16);
                if (bone.ParentID == 0xFF) stream.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
                else stream.Write(BitConverter.GetBytes((uint)bone.ParentID)); // Parent ID
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
                    angIndices[0] = BitConverter.ToUInt16(anmData, k * 0x8 + anm3KeyFrameOffset + 0x2);
                    angIndices[1] = BitConverter.ToUInt16(anmData, k * 0x8 + anm3KeyFrameOffset + 0x4);
                    angIndices[2] = BitConverter.ToUInt16(anmData, k * 0x8 + anm3KeyFrameOffset + 0x6);
                    for (int i = 0; i < 3; i++)
                    {
                        short[] transformShorts = new short[4];
                        transformShorts[0] = BitConverter.ToInt16(angData, 0x10 + 0x8 * angIndices[i] + 0x0);
                        transformShorts[1] = BitConverter.ToInt16(angData, 0x10 + 0x8 * angIndices[i] + 0x2);
                        transformShorts[2] = BitConverter.ToInt16(angData, 0x10 + 0x8 * angIndices[i] + 0x4);
                        transformShorts[3] = BitConverter.ToInt16(angData, 0x10 + 0x8 * angIndices[i] + 0x6);
                        if (i == 0)
                        {
                            keyFrame.Transform[i] = new float[4];//DecompressVector(transformShorts);
                        }
                        if (i == 1)
                        {
                            keyFrame.Transform[i] = new float[4];
                            for(int s = 0; s < 4; s++)
                            {
                                keyFrame.Transform[i][s] = transformShorts[s];
                                keyFrame.Transform[i][s] /= 16384;
                            }
                        }
                        if (i == 2) keyFrame.Transform[2] = ConvertScaling(transformShorts);
                        byte[] transformBytes = GenerateTransformBytes(keyFrame.Transform[i], i == 0, keyFrame.FrameIndex);
                        string hash = Convert.ToBase64String(transformBytes);
                        if (!transforms.TryAdd(hash, (uint)stream.Position))
                        {
                            keyFrame.TransformOffsets[i] = transforms[hash];
                            continue;
                        }
                        keyFrame.TransformOffsets[i] = (uint)stream.Position;
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
            uint stringTableOffset = (uint)stream.Position;
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
                Array.Copy(BitConverter.GetBytes((uint)stringSet.Count), 0, anm2Data, 0x20, 0x4);
                Array.Copy(BitConverter.GetBytes(stringTableOffset), 0, anm2Data, 0x24, 0x4);
            }

            using FileStream fileStream = new(anm2Path, FileMode.Create);
            fileStream.Write(anm2Data, 0, anm2Data.Length);
            fileStream.Close();
        }

        public static float[] DecompressVector(short[] input)
        {
            float[] output = new float[3];
            for (int i = 0; i < 3; i++)
            {
                int ecx = 0;
                int edx = (input[3] >> (0xA - (0x5 * i))) & 0x1F;
                if (edx != 0)
                {
                    int eax = ecx = input[i];
                    eax &= 0x7FFF;
                    ecx = ((int)(ecx & 0xFFFF8000) << 8) | eax;
                    eax = edx + 0x70;
                    ecx = ecx << 0x8 | (eax << 0x17);
                }
                output[i] = BitConverter.ToSingle(BitConverter.GetBytes(ecx));
            }
            return output;
        }

        public static float[] ConvertScaling(short[] input)
        {
            List<float> output = new()
            {
                1.0f,
                1.0f,
                1.0f,
                0.0f
            };
            return output.ToArray();
        }

        public static byte[] GenerateTransformBytes(float[] transform, bool isPos, int frameIndex)
        {
            byte[] bytes = new byte[0x10];
            if (isPos)
            {
                Buffer.BlockCopy(transform, 0x0, bytes, 0x0, 0xC);
                Buffer.BlockCopy(BitConverter.GetBytes(frameIndex), 0, bytes, 0xC, 0x4);
                return bytes;
            }
            Buffer.BlockCopy(transform, 0, bytes, 0, 0x10);
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
        public float[] NodePos = new float[3];
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
