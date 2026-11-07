namespace CoffeeShop.Admin.Models;

public class DashboardReport
{
    public List<DailyReport> Rows { get; set; } = [];

    public int Orders => Rows.Sum(x => x.Orders);

    public decimal Revenue => Rows.Sum(x => x.Revenue);

    public decimal Cost => Rows.Sum(x => x.Cost);
}