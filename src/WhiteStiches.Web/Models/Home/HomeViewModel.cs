using WhiteStiches.Core.Entities.Content;

namespace WhiteStiches.Web.Models.Home;

public class HomeViewModel
{
    /// <summary>The active homepage hero banner, or null — in which case the view renders its built-in hero.</summary>
    public Banner? Hero { get; init; }
}
