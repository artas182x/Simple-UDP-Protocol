using System.IO;

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

