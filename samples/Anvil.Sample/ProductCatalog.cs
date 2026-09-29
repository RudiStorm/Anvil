namespace Anvil.Sample;

public sealed record Product(int Id, string Name, string Description);

public sealed record DashboardMetric(string Label, string Value, string Change);

public sealed record ActivityItem(string Initials, string Description, string Time, string Tone);

public sealed class ProductCatalog
{
    private readonly object gate = new();
    private readonly List<Product> products =
    [
        new(1, "Espresso", "A concentrated coffee with a rich crema."),
        new(2, "Pour-over", "A clean and balanced hand-brewed coffee."),
        new(3, "Cold brew", "A smooth coffee brewed slowly with cold water.")
    ];

    public IEnumerable<Product> Search(string? query)
    {
        lock (gate)
        {
            return (string.IsNullOrWhiteSpace(query)
                ? products
                : products.Where(product => product.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }
    }

    public Product? Find(int id)
    {
        lock (gate) return products.FirstOrDefault(product => product.Id == id);
    }

    public Product Add(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        lock (gate)
        {
            var product = new Product(
                products.Count == 0 ? 1 : products.Max(item => item.Id) + 1,
                name.Trim(),
                description.Trim());
            products.Add(product);
            return product;
        }
    }

    public bool Remove(int id)
    {
        lock (gate)
        {
            var product = products.FirstOrDefault(item => item.Id == id);
            return product is not null && products.Remove(product);
        }
    }

    public IReadOnlyList<DashboardMetric> Metrics =>
    [
        new("Net revenue", "$48,290", "+12.8%"),
        new("Orders", "1,284", "+8.4%"),
        new("Avg. order value", "$37.61", "+3.2%"),
        new("Refund rate", "1.8%", "-0.6%")
    ];

    public IReadOnlyList<ActivityItem> RecentActivity =>
    [
        new("ML", "Maya Lewis placed order #10482", "8 min ago", "violet"),
        new("AK", "Alex Kim updated the Espresso listing", "34 min ago", "blue"),
        new("JR", "Jordan Reed refunded order #10477", "1 hr ago", "orange"),
        new("SP", "Sam Patel joined the operations team", "3 hrs ago", "green")
    ];
}
