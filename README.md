# Entity Framework Core Split Snapshots

[![NuGet](https://img.shields.io/nuget/v/EntityFrameworkCore.SplitSnapshots.svg)](https://www.nuget.org/packages/EntityFrameworkCore.SplitSnapshots/)
[![Downloads](https://img.shields.io/nuget/dt/EntityFrameworkCore.SplitSnapshots.svg)](https://www.nuget.org/packages/EntityFrameworkCore.SplitSnapshots/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Eliminate merge conflicts in Entity Framework Core migrations by splitting your ModelSnapshot into per-entity files.**

## The Problem

When multiple developers work on an EF Core project in parallel, they often encounter merge conflicts in the `ModelSnapshot.cs` file. This single file represents your entire database schema, so any schema change updates it, causing conflicts when branches are merged.

### Example Scenario

```
Branch A: Developer adds User.Email property
Branch B: Developer adds Order.Status property
Result: Merge conflict in ApplicationDbContextModelSnapshot.cs 😞
```

Even though these changes affect different tables, Git cannot automatically merge the ModelSnapshot because both developers modified the same file.

## The Solution

**EntityFrameworkCore.SplitSnapshots** splits your ModelSnapshot into one file per entity:

```
Migrations/
├── ApplicationDbContextModelSnapshot.cs  ← Orchestrator (small)
└── Snapshots/
    ├── UserSnapshot.cs                   ← User entity only
    ├── OrderSnapshot.cs                  ← Order entity only
    └── ProductSnapshot.cs                ← Product entity only
```

Now when the same scenario happens:

```
Branch A: Modifies Snapshots/UserSnapshot.cs
Branch B: Modifies Snapshots/OrderSnapshot.cs
Result: Clean merge with no conflicts! 🎉
```

## Features

- ✅ **Zero merge conflicts** for changes to different entities
- ✅ **100% backward compatible** with existing migrations
- ✅ **Opt-in** - doesn't affect projects that don't use it
- ✅ **Works with all EF Core providers** (SQL Server, PostgreSQL, SQLite, etc.)
- ✅ **No runtime overhead** - design-time only
- ✅ **Future-proof** - inherits from EF Core classes, not a fork

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package EntityFrameworkCore.SplitSnapshots
```

Or via Package Manager Console:

```powershell
Install-Package EntityFrameworkCore.SplitSnapshots
```

## Quick Start

### Step 1: Enable Split Snapshots

Add to your `DbContext` configuration (typically in `OnConfiguring` or `Program.cs`):

```csharp
using EntityFrameworkCore.SplitSnapshots;

public class ApplicationDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer("YourConnectionString")
            .UseSplitSnapshots(); // 👈 Add this line
    }
}
```

Or in `Program.cs` (ASP.NET Core):

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseSplitSnapshots()); // 👈 Add this line
```

### Step 2: Register Design-Time Services

Add this attribute to any file in your DbContext project (e.g., at the top of your DbContext file):

```csharp
using Microsoft.EntityFrameworkCore.Design;

[assembly: DesignTimeServicesReference(
    "EntityFrameworkCore.SplitSnapshots.SplitSnapshotDesignTimeServices, EntityFrameworkCore.SplitSnapshots")]

namespace YourProject.Data
{
    public class ApplicationDbContext : DbContext
    {
        // ... your DbContext code
    }
}
```

### Step 3: Create Your Next Migration

```bash
dotnet ef migrations add YourMigrationName
```

That's it! Your migrations will now use split snapshots.

## File Structure

After creating a migration with split snapshots enabled, you'll see:

```
Migrations/
├── 20240216120000_InitialCreate.cs
├── 20240216120000_InitialCreate.Designer.cs
├── ApplicationDbContextModelSnapshot.cs      # Orchestrator
└── Snapshots/
    ├── UserSnapshot.cs                       # User entity
    ├── OrderSnapshot.cs                      # Order entity
    └── ProductSnapshot.cs                    # Product entity
```

### Orchestrator Snapshot

The orchestrator is a small file that coordinates the entity snapshots:

```csharp
partial class ApplicationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.0");
        
        // Entity snapshots
        Snapshots.UserSnapshot.BuildModel(modelBuilder);
        Snapshots.OrderSnapshot.BuildModel(modelBuilder);
        Snapshots.ProductSnapshot.BuildModel(modelBuilder);
    }
}
```

### Entity Snapshots

Each entity gets its own file with just its configuration:

```csharp
// Snapshots/UserSnapshot.cs
internal partial class UserSnapshot
{
    public static void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("YourProject.Models.User", b =>
        {
            b.Property<int>("Id");
            b.Property<string>("Name");
            b.Property<string>("Email");
            b.HasKey("Id");
            b.ToTable("Users");
        });
    }
}
```

## How It Works

1. **You enable split snapshots** in your DbContext configuration
2. **EF Core uses our custom scaffolder** (registered via design-time services)
3. **Migrations are generated normally** - no changes to migration files
4. **ModelSnapshot is split** into multiple files automatically
5. **Git merges are cleaner** - conflicts only when the same entity is modified

## Migration from Standard Snapshots

Already have migrations? No problem! Here's how to migrate:

### Option 1: Fresh Start (Recommended for Development)

1. Enable split snapshots (see Quick Start)
2. Delete your existing `Migrations/` folder
3. Run `dotnet ef migrations add InitialCreate`
4. Apply to database: `dotnet ef database update`

### Option 2: Preserve Migration History

1. Enable split snapshots
2. Create a new migration: `dotnet ef migrations add ConvertToSplitSnapshots`
3. The migration will be empty (no schema changes)
4. Delete old `ApplicationDbContextModelSnapshot.cs`
5. Commit the new split snapshot files

## Compatibility

| EF Core Version | .NET Version | Status |
|-----------------|--------------|--------|
| 6.0.x           | .NET 6.0     | ✅ Supported |
| 7.0.x           | .NET 7.0     | ✅ Supported |
| 8.0.x           | .NET 8.0     | ✅ Supported |
| 9.0.x           | .NET 9.0     | 🔄 Planned |

Works with all EF Core database providers:
- Microsoft SQL Server
- PostgreSQL (Npgsql)
- MySQL / MariaDB
- SQLite
- Cosmos DB
- In-Memory (for testing)

## Configuration Options

### Disable Split Snapshots

To temporarily disable split snapshots:

```csharp
// Remove or comment out:
// .UseSplitSnapshots()

// Or explicitly disable:
optionsBuilder.UseSqlServer(connectionString);
```

### Use in Some Projects, Not Others

Split snapshots are configured per-DbContext. You can have:
- Project A: Using split snapshots
- Project B: Using standard snapshots

They work independently without issues.

## Troubleshooting

### Migration Command Doesn't Generate Split Files

**Problem:** Running `dotnet ef migrations add` still generates a single ModelSnapshot.

**Solution:**
1. Verify `UseSplitSnapshots()` is called in `OnConfiguring`
2. Check that `DesignTimeServicesReference` attribute is present
3. Make sure you're targeting the correct project
4. Try rebuilding the solution

### Build Errors About Missing Types

**Problem:** Build fails with errors like "The type or namespace 'Snapshots' could not be found"

**Solution:**
- The `Snapshots/` folder and files should be automatically included in compilation
- If not, manually add them to your `.csproj`:

```xml
<ItemGroup>
  <Compile Include="Migrations\Snapshots\*.cs" />
</ItemGroup>
```

### Merge Conflicts in Orchestrator File

**Problem:** Still getting conflicts in `ApplicationDbContextModelSnapshot.cs`

**Solution:**
- This is expected when both developers add a new entity
- The conflict is minimal (just adding a line like `Snapshots.NewEntitySnapshot.BuildModel(modelBuilder);`)
- Much easier to resolve than conflicts in a monolithic snapshot

### Design-Time Services Not Loading

**Problem:** Error message about design-time services not being available

**Solution:**
1. Ensure `Microsoft.EntityFrameworkCore.Design` package is referenced
2. Check that the assembly attribute is correct and in the right project
3. Verify the package is installed: `dotnet list package`

## Performance Considerations

### Design-Time Performance

- **Migration generation:** Slightly slower due to multiple file writes (typically <100ms overhead)
- **Compilation:** Comparable to standard snapshots

### Runtime Performance

- **Zero impact** - split snapshots are design-time only
- **No database overhead** - migrations execute identically

## Best Practices

### ✅ Do

- **Enable in team environments** where parallel development is common
- **Commit the entire Snapshots/ folder** to source control
- **Include split snapshots in code reviews** just like regular migrations
- **Use consistent formatting** (let EF Core generate the files)

### ❌ Don't

- **Manually edit snapshot files** - regenerate migrations instead
- **Mix split and standard snapshots** in the same project
- **Commit merge conflict markers** in snapshot files
- **Delete the Snapshots/ folder** without regenerating migrations

## Examples

### Example 1: Basic Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSplitSnapshots());

var app = builder.Build();
```

### Example 2: Multiple Contexts

```csharp
// Different configurations for different contexts
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseSplitSnapshots()); // App context uses split

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(connectionString)); // Identity uses standard
```

### Example 3: Conditional Enablement

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    var useSplit = Environment.GetEnvironmentVariable("USE_SPLIT_SNAPSHOTS") == "true";
    
    optionsBuilder.UseSqlServer("ConnectionString");
    
    if (useSplit)
    {
        optionsBuilder.UseSplitSnapshots();
    }
}
```

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details.

### Building from Source

```bash
git clone https://github.com/yourusername/EntityFrameworkCore.SplitSnapshots.git
cd EntityFrameworkCore.SplitSnapshots
dotnet build
dotnet test
```

## FAQ

**Q: Does this work with existing migrations?**  
A: Yes! Enable split snapshots, create a new migration, and from that point forward, new migrations will use split snapshots.

**Q: Can I switch back to standard snapshots?**  
A: Yes, just remove `.UseSplitSnapshots()` and create a new migration. The split snapshot files can be deleted.

**Q: Does this affect migration execution?**  
A: No, migrations execute identically whether they were created with split or standard snapshots.

**Q: What about owned entities?**  
A: Owned entities are included in their parent entity's snapshot file.

**Q: How big can my entities be?**  
A: No practical limit. Each entity gets its own file regardless of size.

**Q: Does this work with `dotnet ef database update`?**  
A: Yes, all standard EF Core commands work normally.

**Q: Can I use this with EF6?**  
A: No, this is for EF Core only (versions 6.0+).

## Related Issues

This package addresses several long-standing EF Core issues:

- [#2268](https://github.com/dotnet/efcore/issues/2268) - Add ability to skip the top level model snapshot
- [#9976](https://github.com/dotnet/efcore/issues/9976) - Issues with model snapshot in team environment
- [#23911](https://github.com/dotnet/efcore/issues/23911) - Remove migration corrupts the model snapshot in team environments

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Support

- **Issues:** [GitHub Issues](https://github.com/yourusername/EntityFrameworkCore.SplitSnapshots/issues)
- **Discussions:** [GitHub Discussions](https://github.com/yourusername/EntityFrameworkCore.SplitSnapshots/discussions)
- **Stack Overflow:** Tag your questions with `entity-framework-core` and `split-snapshots`

## Acknowledgments

Built on top of Entity Framework Core by Microsoft. Special thanks to the EF Core team for providing excellent extension points.

---

**Made with ❤️ for teams who hate merge conflicts**
