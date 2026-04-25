namespace EDNAClient.Core
{
    internal interface IPlayfieldObserver
    {
        void OnPlayfieldLoaded(string solarSystem, string playfield, double x, double y, double z);
    }
}
