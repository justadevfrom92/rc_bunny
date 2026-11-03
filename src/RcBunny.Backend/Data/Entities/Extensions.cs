namespace RcBunny.Data.Entities;

// Hand-written additions to the generated entities (Generated.cs). Not mapped to columns.

public partial class Role
{
    public bool IsAdministrator => Name == "Administrator";
}

public partial class User
{
    public bool IsExternal => !string.IsNullOrEmpty(Company);

    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => w[0]));
}
