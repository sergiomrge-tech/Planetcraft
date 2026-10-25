namespace Planetcraft.Core
{
    public static class TiFormat
    {
        static readonly string[] Prefixes = { "", "k", "M", "G", "T", "P", "E" };

        public static string Format(double value, string unit = "Ti")
        {
            int i = 0;
            while (value >= 1000d && i < Prefixes.Length - 1)
            {
                value /= 1000d;
                i++;
            }
            return $"{value:0.##} {Prefixes[i]}{unit}";
        }
    }
}
