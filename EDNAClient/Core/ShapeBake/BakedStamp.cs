namespace EDNAClient.Core.ShapeBake
{
    // Single shape stamp from shapes.bake. Occupancy is unpacked from the
    // file's packed-bit form into a flat bool[] indexed
    //   i = x * Resolution * Resolution + y * Resolution + z
    // for fast iteration during the splat pass in TomographyScanner.
    public sealed class BakedStamp
    {
        public string Name { get; }
        public int Resolution { get; }
        public bool[] Occupied { get; }
        public int FilledCount { get; }

        public BakedStamp(string name, int resolution, bool[] occupied, int filledCount)
        {
            Name = name;
            Resolution = resolution;
            Occupied = occupied;
            FilledCount = filledCount;
        }

        public bool IsSet(int x, int y, int z) =>
            Occupied[x * Resolution * Resolution + y * Resolution + z];
    }
}
