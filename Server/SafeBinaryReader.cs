using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleUDPProtocol
{
    public class SafeBinaryReader : BinaryReader
    {
        public SafeBinaryReader(Stream input)
            : base(input)
        {
        }

        public override float ReadSingle()
        {
            float num = base.ReadSingle();
            num = ValidateFloat(num);
            return num;
        }

        
        public static float ValidateFloat(float num)
        {
            if (float.IsInfinity(num) || float.IsNaN(num))
                return 0;
            return num;
        }
    }
}

