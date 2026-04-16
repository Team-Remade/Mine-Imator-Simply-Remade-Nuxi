namespace MineImatorSimplyRemadeNuxi;

public static class Program
{
    public static App App { get; private set; }
    
    public static void Main()
    {
        App = new App();
        App.Run();
    }
}