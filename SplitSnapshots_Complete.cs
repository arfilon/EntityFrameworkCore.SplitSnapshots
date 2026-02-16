// ============================================================================
// EntityFrameworkCore.SplitSnapshots
// Complete C# Implementation
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.SplitSnapshots
{
    // ========================================================================
    // Configuration Extension
    // ========================================================================

    /// <summary>
    /// Options extension for configuring split model snapshots.
    /// </summary>
    public class SplitSnapshotsOptionsExtension : IDbContextOptionsExtension
    {
        private DbContextOptionsExtensionInfo? _info;

        /// <summary>
        /// Gets or sets whether split snapshots are enabled.
        /// </summary>
        public bool UseSplitSnapshots { get; set; }

        /// <summary>
        /// Gets information/metadata about the extension.
        /// </summary>
        public DbContextOptionsExtensionInfo Info 
            => _info ??= new ExtensionInfo(this);

        /// <summary>
        /// Applies services to the service collection.
        /// </summary>
        public void ApplyServices(IServiceCollection services)
        {
            // No runtime services needed - this is design-time only
        }

        /// <summary>
        /// Validates the extension configuration.
        /// </summary>
        public void Validate(IDbContextOptions options)
        {
            // No validation needed
        }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(IDbContextOptionsExtension extension)
                : base(extension)
            {
            }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => "using split snapshots";

            public override int GetServiceProviderHashCode() => 0;

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
                => other is ExtensionInfo;

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                debugInfo["SplitSnapshots:Enabled"] 
                    = ((SplitSnapshotsOptionsExtension)Extension).UseSplitSnapshots.ToString();
            }
        }
    }

    /// <summary>
    /// Extension methods for configuring split snapshots.
    /// </summary>
    public static class SplitSnapshotsDbContextOptionsBuilderExtensions
    {
        /// <summary>
        /// Configures the context to use split model snapshots, which generates
        /// one snapshot file per entity to reduce merge conflicts.
        /// </summary>
        /// <param name="optionsBuilder">The options builder.</param>
        /// <returns>The options builder for chaining.</returns>
        public static DbContextOptionsBuilder UseSplitSnapshots(
            this DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder == null)
                throw new ArgumentNullException(nameof(optionsBuilder));

            var extension = optionsBuilder.Options.FindExtension<SplitSnapshotsOptionsExtension>()
                ?? new SplitSnapshotsOptionsExtension();

            extension.UseSplitSnapshots = true;

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return optionsBuilder;
        }
    }

    // ========================================================================
    // Split Snapshot Generator
    // ========================================================================

    /// <summary>
    /// Extends CSharpSnapshotGenerator to support generating entity-level snapshots.
    /// Inherits from EF Core's generator to reuse all generation logic.
    /// </summary>
    public class SplitCSharpSnapshotGenerator : CSharpSnapshotGenerator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SplitCSharpSnapshotGenerator"/> class.
        /// </summary>
        /// <param name="dependencies">The dependencies.</param>
        public SplitCSharpSnapshotGenerator(
            CSharpSnapshotGeneratorDependencies dependencies)
            : base(dependencies)
        {
        }

        /// <summary>
        /// Generates a snapshot file for a single entity.
        /// </summary>
        /// <param name="modelSnapshotNamespace">The namespace for the snapshot.</param>
        /// <param name="contextType">The DbContext type.</param>
        /// <param name="entitySnapshotName">The name of the entity snapshot class.</param>
        /// <param name="entityType">The entity type to generate snapshot for.</param>
        /// <returns>The generated C# code.</returns>
        public virtual string GenerateEntitySnapshot(
            string modelSnapshotNamespace,
            Type contextType,
            string entitySnapshotName,
            IEntityType entityType)
        {
            if (modelSnapshotNamespace == null)
                throw new ArgumentNullException(nameof(modelSnapshotNamespace));
            if (contextType == null)
                throw new ArgumentNullException(nameof(contextType));
            if (entitySnapshotName == null)
                throw new ArgumentNullException(nameof(entitySnapshotName));
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            var builder = new IndentedStringBuilder();

            // File header
            builder.AppendLine("// <auto-generated />");
            GenerateFileHeader(builder);
            builder.AppendLine();

            // Namespace
            builder.AppendLine($"namespace {modelSnapshotNamespace}.Snapshots");
            builder.AppendLine("{");

            using (builder.Indent())
            {
                // Class declaration with DbContext attribute
                builder.AppendLine("#nullable disable");
                builder.AppendLine();
                builder.AppendLine("/// <summary>");
                builder.AppendLine($"/// Model snapshot for {entityType.ClrType?.Name ?? entityType.Name} entity.");
                builder.AppendLine("/// </summary>");
                builder.AppendLine($"[DbContext(typeof({Code.Reference(contextType)}))]");
                builder.AppendLine($"internal partial class {entitySnapshotName}");
                builder.AppendLine("{");

                using (builder.Indent())
                {
                    // Static method to build this entity
                    builder.AppendLine("/// <summary>");
                    builder.AppendLine("/// Builds the model for this entity.");
                    builder.AppendLine("/// </summary>");
                    builder.AppendLine("/// <param name=\"modelBuilder\">The model builder.</param>");
                    builder.AppendLine("public static void BuildModel(ModelBuilder modelBuilder)");
                    builder.AppendLine("{");

                    using (builder.Indent())
                    {
                        // Use base class protected method to generate entity
                        // This ensures we use EF's actual generation logic
                        GenerateEntityType("modelBuilder", entityType, builder);
                    }

                    builder.AppendLine("}");
                }

                builder.AppendLine("}");
            }

            builder.AppendLine("}");

            return builder.ToString();
        }

        /// <summary>
        /// Generates the orchestrator snapshot that references all entity snapshots.
        /// </summary>
        /// <param name="modelSnapshotNamespace">The namespace for the snapshot.</param>
        /// <param name="contextType">The DbContext type.</param>
        /// <param name="modelSnapshotName">The name of the model snapshot class.</param>
        /// <param name="model">The model to generate snapshot for.</param>
        /// <returns>The generated C# code.</returns>
        public virtual string GenerateOrchestratorSnapshot(
            string modelSnapshotNamespace,
            Type contextType,
            string modelSnapshotName,
            IModel model)
        {
            if (modelSnapshotNamespace == null)
                throw new ArgumentNullException(nameof(modelSnapshotNamespace));
            if (contextType == null)
                throw new ArgumentNullException(nameof(contextType));
            if (modelSnapshotName == null)
                throw new ArgumentNullException(nameof(modelSnapshotName));
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var builder = new IndentedStringBuilder();

            // File header
            builder.AppendLine("// <auto-generated />");
            GenerateFileHeader(builder);
            builder.AppendLine();

            // Namespace
            builder.AppendLine($"namespace {modelSnapshotNamespace}");
            builder.AppendLine("{");

            using (builder.Indent())
            {
                // Class declaration
                builder.AppendLine("#nullable disable");
                builder.AppendLine();
                builder.AppendLine("/// <summary>");
                builder.AppendLine("/// Orchestrator model snapshot that coordinates entity snapshots.");
                builder.AppendLine("/// </summary>");
                builder.AppendLine($"[DbContext(typeof({Code.Reference(contextType)}))]");
                builder.AppendLine($"partial class {modelSnapshotName} : ModelSnapshot");
                builder.AppendLine("{");

                using (builder.Indent())
                {
                    builder.AppendLine("/// <summary>");
                    builder.AppendLine("/// Builds the complete model by invoking entity snapshots.");
                    builder.AppendLine("/// </summary>");
                    builder.AppendLine("/// <param name=\"modelBuilder\">The model builder.</param>");
                    builder.AppendLine("protected override void BuildModel(ModelBuilder modelBuilder)");
                    builder.AppendLine("{");

                    using (builder.Indent())
                    {
                        // Generate model-level annotations
                        GenerateModelAnnotations("modelBuilder", model, builder);

                        // Generate sequences
                        var sequences = model.GetSequences().ToList();
                        if (sequences.Any())
                        {
                            builder.AppendLine();
                            foreach (var sequence in sequences)
                            {
                                GenerateSequence("modelBuilder", sequence, builder);
                            }
                        }

                        builder.AppendLine();
                        builder.AppendLine("// Entity snapshots");

                        // Call each entity snapshot
                        var entityTypes = model.GetEntityTypes()
                            .Where(et => !et.IsOwned()) // Owned entities handled within parent
                            .ToList();

                        foreach (var entityType in entityTypes)
                        {
                            var entityName = GetEntitySnapshotName(entityType);
                            builder.AppendLine($"Snapshots.{entityName}.BuildModel(modelBuilder);");
                        }
                    }

                    builder.AppendLine("}");
                }

                builder.AppendLine("}");
            }

            builder.AppendLine("}");

            return builder.ToString();
        }

        /// <summary>
        /// Generates the file header with using statements.
        /// </summary>
        private void GenerateFileHeader(IndentedStringBuilder builder)
        {
            builder.AppendLine("using System;");
            builder.AppendLine("using Microsoft.EntityFrameworkCore;");
            builder.AppendLine("using Microsoft.EntityFrameworkCore.Infrastructure;");
            builder.AppendLine("using Microsoft.EntityFrameworkCore.Metadata;");
            builder.AppendLine("using Microsoft.EntityFrameworkCore.Migrations;");
            builder.AppendLine("using Microsoft.EntityFrameworkCore.Storage.ValueConversion;");
        }

        /// <summary>
        /// Generates model-level annotations by calling base class methods.
        /// </summary>
        private void GenerateModelAnnotations(
            string modelBuilderName,
            IModel model,
            IndentedStringBuilder builder)
        {
            // Call base class protected method for annotations
            var annotations = Dependencies.AnnotationCodeGenerator
                .FilterIgnoredAnnotations(model.GetAnnotations())
                .ToDictionary(a => a.Name, a => a);

            // Add product version
            if (model.GetProductVersion() is { } productVersion)
            {
                annotations[CoreAnnotationNames.ProductVersion] =
                    new Annotation(CoreAnnotationNames.ProductVersion, productVersion);
            }

            // Use base class method to generate annotations
            GenerateAnnotations(
                modelBuilderName,
                model,
                builder,
                annotations,
                inChainedCall: false,
                leadingNewline: false);
        }

        /// <summary>
        /// Gets a safe snapshot name for an entity.
        /// </summary>
        private static string GetEntitySnapshotName(IEntityType entityType)
        {
            var name = entityType.ClrType?.Name ?? entityType.Name;
            
            // Sanitize name for file system
            name = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            
            // Handle very long names
            if (name.Length > 200)
                name = name.Substring(0, 200);

            return $"{name}Snapshot";
        }
    }

    // ========================================================================
    // Split Migrations Generator
    // ========================================================================

    /// <summary>
    /// Extends CSharpMigrationsGenerator to support split snapshot generation.
    /// </summary>
    public class SplitCSharpMigrationsGenerator : CSharpMigrationsGenerator
    {
        private readonly SplitCSharpSnapshotGenerator _splitSnapshotGenerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitCSharpMigrationsGenerator"/> class.
        /// </summary>
        /// <param name="dependencies">The dependencies.</param>
        /// <param name="csharpDependencies">The C#-specific dependencies.</param>
        /// <param name="splitSnapshotGenerator">The split snapshot generator.</param>
        public SplitCSharpMigrationsGenerator(
            MigrationsCodeGeneratorDependencies dependencies,
            CSharpMigrationsGeneratorDependencies csharpDependencies,
            SplitCSharpSnapshotGenerator splitSnapshotGenerator)
            : base(dependencies, csharpDependencies)
        {
            _splitSnapshotGenerator = splitSnapshotGenerator 
                ?? throw new ArgumentNullException(nameof(splitSnapshotGenerator));
        }

        /// <summary>
        /// Generates split snapshots for the model.
        /// </summary>
        /// <param name="modelSnapshotNamespace">The namespace for snapshots.</param>
        /// <param name="contextType">The DbContext type.</param>
        /// <param name="modelSnapshotName">The name of the model snapshot class.</param>
        /// <param name="model">The model to generate snapshots for.</param>
        /// <returns>Collection of (fileName, code) tuples for all snapshot files.</returns>
        public virtual IEnumerable<(string fileName, string code)> GenerateSplitSnapshots(
            string? modelSnapshotNamespace,
            Type contextType,
            string modelSnapshotName,
            IModel model)
        {
            if (contextType == null)
                throw new ArgumentNullException(nameof(contextType));
            if (modelSnapshotName == null)
                throw new ArgumentNullException(nameof(modelSnapshotName));
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var snapshots = new List<(string, string)>();
            var ns = modelSnapshotNamespace ?? string.Empty;

            // Generate entity snapshots
            var entityTypes = model.GetEntityTypes()
                .Where(et => !et.IsOwned())
                .ToList();

            foreach (var entityType in entityTypes)
            {
                var entityName = GetEntitySnapshotName(entityType);
                var fileName = $"{entityName}{FileExtension}";
                
                var code = _splitSnapshotGenerator.GenerateEntitySnapshot(
                    ns,
                    contextType,
                    entityName,
                    entityType);

                snapshots.Add((fileName, code));
            }

            // Generate orchestrator snapshot (must be first for return value)
            var orchestratorCode = _splitSnapshotGenerator.GenerateOrchestratorSnapshot(
                ns,
                contextType,
                modelSnapshotName,
                model);

            // Insert orchestrator at beginning
            snapshots.Insert(0, ($"{modelSnapshotName}{FileExtension}", orchestratorCode));

            return snapshots;
        }

        private static string GetEntitySnapshotName(IEntityType entityType)
        {
            var name = entityType.ClrType?.Name ?? entityType.Name;
            name = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            if (name.Length > 200)
                name = name.Substring(0, 200);
            return $"{name}Snapshot";
        }
    }

    // ========================================================================
    // Split Snapshot Scaffolder
    // ========================================================================

    /// <summary>
    /// Extends MigrationsScaffolder to save split snapshot files.
    /// </summary>
    public class SplitSnapshotMigrationsScaffolder : MigrationsScaffolder
    {
        private readonly SplitCSharpMigrationsGenerator _splitGenerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitSnapshotMigrationsScaffolder"/> class.
        /// </summary>
        /// <param name="dependencies">The dependencies.</param>
        /// <param name="splitGenerator">The split migrations generator.</param>
        public SplitSnapshotMigrationsScaffolder(
            MigrationsScaffolderDependencies dependencies,
            SplitCSharpMigrationsGenerator splitGenerator)
            : base(dependencies)
        {
            _splitGenerator = splitGenerator 
                ?? throw new ArgumentNullException(nameof(splitGenerator));
        }

        /// <summary>
        /// Saves the scaffolded migration, using split snapshots if configured.
        /// </summary>
        /// <param name="projectDir">The project directory.</param>
        /// <param name="migration">The scaffolded migration.</param>
        /// <param name="outputDir">The output directory.</param>
        /// <param name="dryRun">Whether this is a dry run.</param>
        /// <returns>The migration files.</returns>
        public override MigrationFiles Save(
            string projectDir,
            ScaffoldedMigration migration,
            string? outputDir,
            bool dryRun = false)
        {
            if (projectDir == null)
                throw new ArgumentNullException(nameof(projectDir));
            if (migration == null)
                throw new ArgumentNullException(nameof(migration));

            // Check if split mode is enabled
            if (!ShouldUseSplitSnapshots())
            {
                // Use default behavior
                Dependencies.OperationReporter.WriteInformation(
                    "Using standard model snapshot (split mode disabled)");
                return base.Save(projectDir, migration, outputDir, dryRun);
            }

            Dependencies.OperationReporter.WriteInformation(
                "Using split model snapshots");

            // Save migration files normally (reuse base implementation)
            var migrationDirectory = outputDir ?? 
                GetDirectory(projectDir, null, migration.SnapshotSubnamespace);

            var migrationFile = Path.Combine(
                migrationDirectory,
                migration.MigrationId + migration.FileExtension);
            
            var migrationMetadataFile = Path.Combine(
                migrationDirectory,
                migration.MigrationId + ".Designer" + migration.FileExtension);

            if (!dryRun)
            {
                Dependencies.OperationReporter.WriteVerbose($"Writing migration to {migrationFile}");
                Directory.CreateDirectory(migrationDirectory);
                File.WriteAllText(migrationFile, migration.MigrationCode, Encoding.UTF8);
                
                Dependencies.OperationReporter.WriteVerbose($"Writing metadata to {migrationMetadataFile}");
                File.WriteAllText(migrationMetadataFile, migration.MetadataCode, Encoding.UTF8);
            }

            // Generate and save split snapshots
            var snapshotFiles = SaveSplitSnapshots(
                projectDir,
                migration,
                migrationDirectory,
                dryRun);

            return new MigrationFiles
            {
                MigrationFile = migrationFile,
                MetadataFile = migrationMetadataFile,
                SnapshotFile = snapshotFiles.First() // Orchestrator file
            };
        }

        /// <summary>
        /// Saves the split snapshot files.
        /// </summary>
        private List<string> SaveSplitSnapshots(
            string projectDir,
            ScaffoldedMigration migration,
            string migrationDirectory,
            bool dryRun)
        {
            var savedFiles = new List<string>();
            var model = Dependencies.Model;
            var contextType = Dependencies.CurrentContext.Context.GetType();

            // Create snapshots subdirectory
            var snapshotsDir = Path.Combine(migrationDirectory, "Snapshots");

            // Generate all split snapshots
            var splitSnapshots = _splitGenerator.GenerateSplitSnapshots(
                migration.SnapshotNamespace,
                contextType,
                migration.SnapshotName,
                model);

            var isFirst = true;
            foreach (var (fileName, code) in splitSnapshots)
            {
                string filePath;

                if (isFirst)
                {
                    // Orchestrator goes in migration directory
                    filePath = Path.Combine(migrationDirectory, fileName);
                    isFirst = false;
                    savedFiles.Add(filePath); // Add first for return value
                }
                else
                {
                    // Entity snapshots go in Snapshots subdirectory
                    filePath = Path.Combine(snapshotsDir, fileName);
                }

                if (!dryRun)
                {
                    var dir = Path.GetDirectoryName(filePath)!;
                    Directory.CreateDirectory(dir);
                    
                    Dependencies.OperationReporter.WriteVerbose($"Writing snapshot to {filePath}");
                    File.WriteAllText(filePath, code, Encoding.UTF8);
                }

                if (!isFirst) // Don't add orchestrator twice
                    savedFiles.Add(filePath);
            }

            Dependencies.OperationReporter.WriteInformation(
                $"Generated {savedFiles.Count} snapshot files ({savedFiles.Count - 1} entities + 1 orchestrator)");

            return savedFiles;
        }

        /// <summary>
        /// Checks if split snapshots should be used.
        /// </summary>
        private bool ShouldUseSplitSnapshots()
        {
            try
            {
                var options = Dependencies.CurrentContext.Context
                    .GetService<IDbContextOptions>();

                var extension = options?.FindExtension<SplitSnapshotsOptionsExtension>();
                return extension?.UseSplitSnapshots ?? false;
            }
            catch
            {
                // If we can't read the option, default to disabled
                return false;
            }
        }
    }

    // ========================================================================
    // Design-Time Services Registration
    // ========================================================================

    /// <summary>
    /// Registers split snapshot services for design-time operations.
    /// </summary>
    public class SplitSnapshotDesignTimeServices : IDesignTimeServices
    {
        /// <summary>
        /// Configures design-time services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureDesignTimeServices(IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Register our custom implementations
            // These replace the default EF Core services via DI
            services.AddSingleton<SplitCSharpSnapshotGenerator>();
            services.AddSingleton<ICSharpMigrationsGenerator, SplitCSharpMigrationsGenerator>();
            services.AddSingleton<IMigrationsScaffolder, SplitSnapshotMigrationsScaffolder>();
        }
    }
}

// ============================================================================
// Usage Example (add to your DbContext project)
// ============================================================================

/*
// 1. Register the design-time services
[assembly: DesignTimeServicesReference(
    "EntityFrameworkCore.SplitSnapshots.SplitSnapshotDesignTimeServices, EntityFrameworkCore.SplitSnapshots")]

namespace YourProject.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Product> Products { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder
                    .UseSqlServer("YourConnectionString")
                    .UseSplitSnapshots(); // Enable split snapshots
            }
        }
    }
}
*/

// ============================================================================
// Generated File Structure Example
// ============================================================================

/*
After running "dotnet ef migrations add InitialCreate", you'll get:

Migrations/
├── 20240216120000_InitialCreate.cs           # Migration file (unchanged)
├── 20240216120000_InitialCreate.Designer.cs  # Designer file (unchanged)
├── ApplicationDbContextModelSnapshot.cs      # Orchestrator snapshot
└── Snapshots/
    ├── UserSnapshot.cs                       # User entity snapshot
    ├── OrderSnapshot.cs                      # Order entity snapshot
    └── ProductSnapshot.cs                    # Product entity snapshot

The orchestrator snapshot looks like:

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

Each entity snapshot is independent:

    internal partial class UserSnapshot
    {
        public static void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity("YourProject.Models.User", b =>
            {
                b.Property<int>("Id");
                b.Property<string>("Name");
                b.HasKey("Id");
                b.ToTable("Users");
            });
        }
    }
*/
