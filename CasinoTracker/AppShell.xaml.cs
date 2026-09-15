using CasinoTracker.Views;

namespace CasinoTracker;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("sessiondetail", typeof(SessionDetailPage));
        Routing.RegisterRoute("casinoedit", typeof(CasinoEditPage));
        Routing.RegisterRoute("menuitemedit", typeof(MenuitemEditPage));
        Routing.RegisterRoute("gameedit", typeof(GameEditPage));
    }
}
