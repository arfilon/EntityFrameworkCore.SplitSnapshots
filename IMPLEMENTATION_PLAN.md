# EF Core Split Model Snapshot - Implementation Plan

## Project Overview

**Goal:** Create an extension for Entity Framework Core that splits the ModelSnapshot file into multiple entity-level files to reduce merge conflicts in team environments.

**Approach:** Extend EF Core's migration scaffolding through inheritance and dependency injection, requiring zero modifications to EF Core itself.

## Architecture

### Design Principles

1. **Maximum Reuse**: Inherit from EF Core classes and call protected methods - no code duplication
2. **Future-Proof**: Changes in EF Core automatically propagate to our implementation
3. **Non-Breaking**: Existing projects work unchanged; split snapshots are opt-in via configuration
4. **Clean Extension**: Uses DI replacement pattern - no forking, no reflection hacks

### Component Architecture

```
MigrationsOperations (EF Core)
    ↓ calls
IMigrationsScaffolder.Save()
    ↓ resolved to
SplitSnapshotMigrationsScaffolder : MigrationsScaffolder
    ↓ uses
SplitCSharpMigrationsGenerator : CSharpMigrationsGenerator
    ↓ uses
SplitCSharpSnapshotGenerator : CSharpSnapshotGenerator
    ↓ calls protected methods
CSharpSnapshotGenerator.GenerateEntityType() etc.
```

## File Structure

```
EntityFrameworkCore.SplitSnapshots/
├── src/
│   └── EntityFrameworkCore.SplitSnapshots/
│       ├── Design/
│       │   ├── SplitCSharpSnapshotGenerator.cs
│       │   ├── SplitCSharpMigrationsGenerator.cs
│       │   ├── SplitSnapshotMigrationsScaffolder.cs
│       │   └── SplitSnapshotDesignTimeServices.cs
│       ├── Extensions/
│       │   └── SplitSnapshotsOptionsExtension.cs
│       └── EntityFrameworkCore.SplitSnapshots.csproj
├── test/
│   └── EntityFrameworkCore.SplitSnapshots.Tests/
│       ├── IntegrationTests/
│       │   ├── BasicScenarioTests.cs
│       │   ├── MergeConflictTests.cs
│       │   └── MultipleEntityTests.cs
│       ├── TestUtilities/
│       │   ├── TestDbContext.cs
│       │   └── TestHelpers.cs
│       └── EntityFrameworkCore.SplitSnapshots.Tests.csproj
├── samples/
│   └── SampleApp/
│       ├── Models/
│       ├── Data/
│       ├── Migrations/
│       └── Program.cs
├── README.md
├── CONTRIBUTING.md
├── LICENSE
└── EntityFrameworkCore.SplitSnapshots.sln
```

## Implementation Steps

### Phase 1: Core Implementation (Week 1)

#### Step 1.1: Project Setup
- [ ] Create solution structure
- [ ] Add NuGet package references:
  - `Microsoft.EntityFrameworkCore.Design` (match target EF version)
  - `Microsoft.EntityFrameworkCore.Relational`
- [ ] Configure project for multi-targeting (.NET 6.0, .NET 8.0)
- [ ] Set up CI/CD pipeline

#### Step 1.2: Implement SplitCSharpSnapshotGenerator
- [ ] Inherit from `CSharpSnapshotGenerator`
- [ ] Implement `GenerateEntitySnapshot()` method
  - Reuse `GenerateEntityType()` protected method
  - Reuse `GenerateFileHeader()` logic
  - Generate proper namespace and class structure
- [ ] Implement `GenerateOrchestratorSnapshot()` method
  - Call `GenerateAnnotations()` for model-level annotations
  - Call `GenerateSequence()` for sequences
  - Generate calls to entity snapshots
- [ ] Add comprehensive XML documentation

**Key Implementation Details:**
```csharp
// Must call base class protected methods:
- GenerateEntityType(string builderName, IEntityType entityType, IndentedStringBuilder builder)
- GenerateAnnotations(string builderName, IAnnotatable annotatable, IndentedStringBuilder builder, ...)
- GenerateSequence(string builderName, ISequence sequence, IndentedStringBuilder builder)
```

#### Step 1.3: Implement SplitCSharpMigrationsGenerator
- [ ] Inherit from `CSharpMigrationsGenerator`
- [ ] Inject `SplitCSharpSnapshotGenerator` via constructor
- [ ] Implement `GenerateSplitSnapshots()` method
- [ ] Return collection of (fileName, code) tuples
- [ ] Ensure proper ordering (orchestrator first)

#### Step 1.4: Implement SplitSnapshotMigrationsScaffolder
- [ ] Inherit from `MigrationsScaffolder`
- [ ] Override `Save()` method
- [ ] Check configuration for split mode
- [ ] If disabled: call `base.Save()` (100% backward compatible)
- [ ] If enabled:
  - Reuse base logic for migration files
  - Generate split snapshots
  - Write to `Snapshots/` subdirectory
  - Write orchestrator to migrations root
- [ ] Reuse `GetDirectory()` protected helper

#### Step 1.5: Implement Configuration Extension
- [ ] Create `SplitSnapshotsOptionsExtension : IDbContextOptionsExtension`
- [ ] Add `UseSplitSnapshots()` extension method on `DbContextOptionsBuilder`
- [ ] Store configuration in extension
- [ ] Read configuration in scaffolder

#### Step 1.6: Implement Design-Time Services
- [ ] Create `SplitSnapshotDesignTimeServices : IDesignTimeServices`
- [ ] Register all custom services in DI container
- [ ] Ensure proper service lifetime (Singleton)

### Phase 2: Testing (Week 1-2)

#### Step 2.1: Unit Tests
- [ ] Test entity snapshot generation
- [ ] Test orchestrator snapshot generation
- [ ] Test file path generation
- [ ] Test configuration reading
- [ ] Mock EF Core dependencies

#### Step 2.2: Integration Tests
- [ ] Create test DbContext with multiple entities
- [ ] Test migration generation with split snapshots enabled
- [ ] Verify file structure (orchestrator + entity files)
- [ ] Test that generated code compiles
- [ ] Test that migrations can be applied
- [ ] Test rollback scenarios

#### Step 2.3: Merge Conflict Simulation Tests
- [ ] Simulate parallel development scenarios
- [ ] Branch A: Add entity User
- [ ] Branch B: Add entity Order
- [ ] Merge: Verify no conflicts in entity snapshots
- [ ] Verify orchestrator merge is simple (just two new lines)

#### Step 2.4: Backward Compatibility Tests
- [ ] Test existing projects with split mode disabled
- [ ] Verify 100% compatibility with standard EF Core
- [ ] Test migration from standard to split mode
- [ ] Test migration from split to standard mode

### Phase 3: Documentation & Packaging (Week 2)

#### Step 3.1: Code Documentation
- [ ] XML documentation on all public APIs
- [ ] Code comments on complex logic
- [ ] Architecture decision records (ADRs)

#### Step 3.2: User Documentation
- [ ] README.md with quick start
- [ ] Installation guide
- [ ] Configuration options
- [ ] Migration guide (standard → split)
- [ ] Troubleshooting guide
- [ ] FAQ

#### Step 3.3: Sample Application
- [ ] Create working sample with multiple entities
- [ ] Demonstrate merge conflict scenarios
- [ ] Include both modes (standard vs split)
- [ ] Add to samples/ directory

#### Step 3.4: NuGet Package
- [ ] Configure package metadata
- [ ] Create .nuspec file
- [ ] Set up versioning (semantic versioning)
- [ ] Create package icon
- [ ] Configure source link for debugging

### Phase 4: Community & PR (Week 2-3)

#### Step 4.1: NuGet Publication
- [ ] Publish v0.1.0-beta to NuGet
- [ ] Get community feedback
- [ ] Fix issues
- [ ] Publish v1.0.0

#### Step 4.2: EF Core PR Preparation
- [ ] Fork dotnet/efcore
- [ ] Create feature branch
- [ ] Adapt code for EF Core codebase standards
- [ ] Write comprehensive tests in EF Core test suite
- [ ] Update EF Core documentation

#### Step 4.3: Submit PR
- [ ] Reference existing issues (#2268, #9976, #23911)
- [ ] Provide clear problem statement
- [ ] Explain implementation approach
- [ ] Include benchmark results
- [ ] Respond to code review

## Technical Considerations

### Edge Cases to Handle

1. **Entities without CLR types** (table splitting, owned entities)
   - Use entity.Name fallback when ClrType is null
   - Generate safe file names

2. **Very long entity names**
   - Truncate file names if needed
   - Ensure uniqueness

3. **Special characters in entity names**
   - Sanitize file names
   - Use safe naming conventions

4. **Circular dependencies in entity relationships**
   - Navigation properties handled by orchestrator
   - Entity snapshots only define structure

5. **Model-level annotations**
   - Must stay in orchestrator
   - Sequences, default schemas, etc.

### Performance Considerations

1. **File I/O overhead**
   - Multiple small files vs one large file
   - Minimal impact (migrations are design-time only)
   - Network file systems might be slower

2. **Compilation time**
   - More files to compile
   - Typically negligible for modern compilers

3. **Git performance**
   - More files = more inodes
   - Better for merge conflicts
   - Trade-off is worth it

### Compatibility Matrix

| EF Core Version | .NET Version | Support Status |
|-----------------|--------------|----------------|
| 6.0.x           | .NET 6.0     | ✅ Full Support |
| 7.0.x           | .NET 7.0     | ✅ Full Support |
| 8.0.x           | .NET 8.0     | ✅ Full Support |
| 9.0.x           | .NET 9.0     | 🔄 Planned      |

### Breaking Changes

**None.** This is a pure extension that:
- Does not modify EF Core behavior when disabled
- Uses standard DI replacement
- Respects all EF Core conventions
- Maintains file format compatibility

## Testing Strategy

### Test Pyramid

```
           E2E Tests (5%)
        ┌──────────────┐
        │ Full workflow│
        └──────────────┘
              ▲
    Integration Tests (25%)
   ┌────────────────────────┐
   │ Multiple entities      │
   │ Real DbContext        │
   │ File generation       │
   └────────────────────────┘
              ▲
      Unit Tests (70%)
┌──────────────────────────────┐
│ Individual methods           │
│ Mocked dependencies         │
│ Edge cases                  │
└──────────────────────────────┘
```

### Test Coverage Goals

- Line coverage: >80%
- Branch coverage: >75%
- Critical paths: 100%

### CI/CD Pipeline

```yaml
on: [push, pull_request]

jobs:
  build:
    - Restore dependencies
    - Build solution
    - Run unit tests
    - Run integration tests
    - Generate coverage report
    
  pack:
    - Create NuGet package
    - Run package validation
    
  publish:
    - Publish to NuGet (on release tags)
```

## Success Criteria

### Functional Requirements
- ✅ Splits ModelSnapshot into entity-level files
- ✅ Generates working orchestrator snapshot
- ✅ Reduces merge conflicts in parallel development
- ✅ 100% backward compatible
- ✅ Works with all EF Core providers

### Non-Functional Requirements
- ✅ Zero performance impact on runtime
- ✅ Minimal performance impact on design-time
- ✅ Easy to configure (one line of code)
- ✅ Clear error messages
- ✅ Comprehensive documentation

### Quality Gates
- ✅ All tests pass
- ✅ Code coverage >80%
- ✅ No breaking changes
- ✅ Documentation complete
- ✅ Sample app works
- ✅ Package published to NuGet

## Risk Mitigation

### Risk 1: EF Core Breaking Changes
**Probability:** Medium  
**Impact:** High  
**Mitigation:**
- Multi-target package for different EF versions
- Monitor EF Core release notes
- Automated tests against EF Core nightlies
- Quick response to breaking changes

### Risk 2: Community Adoption
**Probability:** Low  
**Impact:** Medium  
**Mitigation:**
- Clear documentation
- Working samples
- Blog posts and tutorials
- Community engagement

### Risk 3: EF Team Rejects PR
**Probability:** Medium  
**Impact:** Low  
**Mitigation:**
- Publish as community NuGet regardless
- Still provides value to users
- Continue maintenance independently

### Risk 4: Performance Issues
**Probability:** Low  
**Impact:** Medium  
**Mitigation:**
- Benchmark before/after
- Optimize file I/O
- Provide opt-out mechanism

## Timeline

**Total Duration:** 2-3 weeks for initial release

| Week | Phase | Deliverables |
|------|-------|--------------|
| 1 | Core Implementation | Working code, basic tests |
| 1-2 | Testing | Comprehensive test suite |
| 2 | Documentation | README, samples, docs |
| 2-3 | Community | NuGet package, PR to EF Core |

## Next Steps

1. **Immediate (Day 1):**
   - Set up project structure
   - Create repository
   - Set up CI/CD

2. **Short-term (Week 1):**
   - Implement core functionality
   - Write basic tests
   - Create sample application

3. **Medium-term (Week 2):**
   - Complete test coverage
   - Write documentation
   - Publish beta to NuGet

4. **Long-term (Week 3+):**
   - Gather community feedback
   - Submit PR to EF Core
   - Maintain package

## Resources

### Required Skills
- C# and .NET development
- Entity Framework Core internals
- Design patterns (inheritance, DI)
- Git and version control
- Unit testing

### Tools Needed
- Visual Studio 2022 or Rider
- .NET 6/8 SDK
- Git
- NuGet account for publishing

### Reference Materials
- EF Core source code: https://github.com/dotnet/efcore
- Related issues: #2268, #9976, #23911
- EF Core docs: https://docs.microsoft.com/ef/core/

## Appendix: Code Conventions

### Naming Conventions
- Classes: PascalCase
- Methods: PascalCase
- Parameters: camelCase
- Private fields: _camelCase
- Constants: PascalCase

### File Organization
- One class per file
- File name matches class name
- Related classes in same directory
- Tests mirror source structure

### Code Style
- Follow EF Core coding conventions
- Use C# 12 features where appropriate
- Null-forgiving operators only when safe
- Prefer explicit types over var for clarity
