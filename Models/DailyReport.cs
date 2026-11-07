namespace CoffeeShop.Admin.Models;

public class DailyReport
{
    public DateTime Date { get; set; }

    public int Orders { get; set; }

    public decimal Revenue { get; set; }

    public decimal Cost { get; set; }
}