using System;

namespace EDNAClient.Skills.GalaxyMap
{
    internal sealed class StarSystem
    {
        public int    X    { get; }
        public int    Y    { get; }
        public int    Z    { get; }
        public string Name { get; }

        public StarSystem(int x, int y, int z, string name)
        {
            X = x; Y = y; Z = z; Name = name;
        }

        public double DistanceTo(double px, double py, double pz)
        {
            double dx = X - px, dy = Y - py, dz = Z - pz;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
