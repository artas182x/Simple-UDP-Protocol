using System.Drawing;

namespace Client
{
    public class PaintData
    {
        public Color Color { get; set; }
        public Point StartPos { get; set; }

        public PaintData(Color color, Point point)
        {
            Color = color;
            StartPos = point;
        }
    };
}
