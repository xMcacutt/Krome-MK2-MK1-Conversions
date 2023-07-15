using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDL3_MDL2_Converter
{
    internal class BADConverter
    {
        public static void ConvertBAD(string bbiPath, string badPath)
        {
            byte[] bbiData = File.ReadAllBytes(bbiPath);
            List<string> badText = new List<string>();

            string modelName = Program.ReadString(bbiData, 0);
            badText.Add($"mesh {modelName}.mdl");
            badText.Add($"skeleton {modelName}.anm");
            badText.Add("");

            int animCount = BitConverter.ToInt32(bbiData, 0x40);

            for(int i = 0; i < animCount; i++)
            {
                uint nameOffset = BitConverter.ToUInt32(bbiData, 0x48 + i * 0x30);
                string animName = Program.ReadString(bbiData, (int)nameOffset);
                badText.Add($"anim {animName}");
                int animStart = BitConverter.ToUInt16(bbiData, 0x48 + i * 0x30 + 0xA);
                int animEnd = BitConverter.ToUInt16(bbiData, 0x48 + i * 0x30 + 0xC);
                badText.Add($"{animStart}-{animEnd},1");
                string type = BitConverter.ToInt16(bbiData, 0x48 + i * 0x30 + 0x8) == 1 ? "loop" : "stop";
                badText.Add($"cycle {type}");
                badText.Add("");
            }

            badText.Add("//end");

            File.WriteAllLines(badPath, badText);
        }
    }
}
