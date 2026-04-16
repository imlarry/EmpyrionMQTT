namespace ESB.GameApi
{
    public enum Timeouts
    {
        /// <summary>No timeout -- fire and forget.</summary>
        NoResponse = 0,
        Wait1s     = 1,
        Wait10s    = 10,
        Wait20s    = 20,
        Wait30s    = 30,
        Wait1m     = 60,
        Wait10m    = 600
    }
}
