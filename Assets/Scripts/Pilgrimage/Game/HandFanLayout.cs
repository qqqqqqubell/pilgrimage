using System;

namespace Pilgrimage
{
    public struct FanSlot
    {
        public float X, Y, Angle;
    }

    public static class HandFanLayout
    {
        public static FanSlot Calculate(int index, int count, float width, float cardWidth)
        {
            if (count <= 0 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            float spacing = count == 1 ? 0 : Math.Min(cardWidth * 0.72f, Math.Max(0, width - cardWidth) / (count - 1));
            float normalized = count == 1 ? 0 : (2f * index / (count - 1) - 1);
            return new FanSlot { X = (index - (count - 1) / 2f) * spacing,
                Y = -normalized * normalized * 38, Angle = -normalized * Math.Min(18, count * 2.8f) };
        }
    }
}
