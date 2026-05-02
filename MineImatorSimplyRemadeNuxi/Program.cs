using MineImatorSimplyRemadeNuxi.mineImator;

namespace MineImatorSimplyRemadeNuxi;

public static class Program
{
    public static App App { get; private set; }
    
    /// <summary>
    /// Project-level bend style setting. Default is Blocky.
    /// </summary>
    public static BendStyle ProjectBendStyle { get; set; } = BendStyle.Blocky;
    
    private static void Main()
    {
        App = new App();
        App.Run();
    }
}