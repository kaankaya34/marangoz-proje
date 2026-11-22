namespace web.Models;

public static class Roles
{
    public const string Customer = "Customer";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public const string ManagerOrAdmin = Manager + "," + Admin;

    public static readonly string[] All = { Customer, Manager, Admin };

    public static string DisplayName(string role) => role switch
    {
        Admin => "Admin",
        Manager => "Yönetici",
        _ => "Müşteri"
    };
}
