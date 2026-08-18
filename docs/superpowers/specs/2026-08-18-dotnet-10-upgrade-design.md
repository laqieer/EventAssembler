# .NET 10 Upgrade Design

## Objective

Upgrade the EventAssembler superproject from .NET 6 to .NET 10, advance every
registered submodule to its already-upgraded default-branch head, and publish a
verified GitHub release from the resulting commit.

## Current State

- `EventAssembler/EventAssembler/EventAssembler.csproj` targets .NET 6 MAUI
  platforms.
- `EventAssembler/EventAssemblerWeb/EventAssemblerWeb.csproj` targets
  `net6.0` and references ASP.NET Core WebAssembly 6.0.9 packages.
- The GitHub Pages and CI workflows publish the web project with .NET 6 paths
  or SDK setup.
- The three submodules are pinned behind their default-branch heads:
  - `Core/ColorzCore`: `00e67ddf2fcc` -> `b9ff48e67a81`
  - `Core/Event-Assembler`: `c3f4ae4ad81d` -> `0b8b228bf3c7`
  - `Library/EAStandardLibrary`: `87296edce602` -> `006dcef0a141`
- The ColorzCore and Event-Assembler heads already target `net10.0`.

## Approaches Considered

### Direct coordinated upgrade (selected)

Retarget both application projects, align package and workflow SDK versions,
and update all submodule gitlinks in one release. This produces one coherent
.NET 10 dependency graph and removes the unsupported .NET 6 build path.

### Temporary .NET 6/.NET 10 multi-targeting

This would preserve an older runtime during migration, but MAUI multi-targeting
would materially increase project and CI complexity. It would also retain an
end-of-support surface without a stated compatibility requirement.

### Tooling-only upgrade

Updating the CI SDK and submodules while leaving the application target
frameworks unchanged would minimize edits, but it would not satisfy the
repository-wide .NET 10 objective and could leave incompatible project
references.

## Design

### Project graph

- Advance each gitlink to the current default-branch head listed above.
- Change the MAUI target frameworks to `net10.0-android`, `net10.0-ios`,
  `net10.0-maccatalyst`, and `net10.0-windows10.0.19041.0`.
- Change the Blazor WebAssembly project to `net10.0`.
- Align the two explicit ASP.NET Core WebAssembly package references to the
  installed .NET 10 runtime patch line (`10.0.11`).
- Preserve application identifiers, platform minimum versions, assets,
  signing configuration, and source behavior unless .NET 10 compilation
  exposes a required compatibility fix.

### Automation

- Configure both workflows to use `actions/setup-dotnet@v4` with `10.0.x`.
- Update the GitHub Pages publish path from `net6.0` to `net10.0`.
- Keep the existing three-operating-system artifact matrix and tag-triggered
  release job.
- Update the older Pages checkout action to the repository's existing
  `actions/checkout@v4` baseline.

### Documentation

Document .NET 10 as the development/build requirement without changing the
existing user-facing links or release artifact layout.

### Failure handling

Submodule initialization, restore, compilation, publishing, artifact
packaging, workflow execution, and release verification must fail visibly.
There will be no fallback to .NET 6, stale submodule commits, or
success-shaped release result when required assets are missing.

## Validation

1. Initialize and update submodules recursively, then verify each checked-out
   SHA matches the intended gitlink.
2. Restore the solution with the installed .NET 10 SDK and workloads.
3. Publish the Blazor project in Release configuration to the same layout used
   by CI.
4. Build the MAUI project for the locally supported Windows and Android .NET 10
   target frameworks; do not treat unavailable Apple host tooling as an
   application failure.
5. Confirm tracked project/workflow files no longer contain active `net6.0` or
   `6.0.x` settings.
6. Run `git diff --check` and inspect the complete diff and status before
   committing.

## Delivery and Release

1. Commit the verified upgrade on `main` with the required co-author trailer.
2. Push `main` and verify the remote branch points to the local commit.
3. Wait for the main-branch CI and GitHub Pages workflows to succeed.
4. Create and push the next date-based tag, `v2026.08.18`, pointing to that
   commit.
5. Wait for tag CI to publish the release, then verify:
   - the release is published and not a draft or prerelease;
   - its tag resolves to the pushed upgrade commit;
   - Linux, Windows, and macOS web ZIP assets are present;
   - each archive is non-empty and contains the expected published web output.

## Success Condition

The repository and all submodule pointers form a buildable .NET 10 graph, local
validation passes for supported targets, `origin/main` contains the upgrade,
and the published `v2026.08.18` release points to that commit with all expected
CI-generated assets.
