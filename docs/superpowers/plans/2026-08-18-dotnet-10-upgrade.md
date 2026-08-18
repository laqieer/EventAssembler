# .NET 10 Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade EventAssembler and its complete submodule-backed project graph to .NET 10, push the verified result to `main`, and publish `v2026.08.18` with all expected CI assets.

**Architecture:** Perform one coordinated project-graph retarget so the MAUI and Blazor applications consume the already-upgraded submodule projects without an intermediate mixed-framework commit. Update GitHub automation and developer documentation in a separate reviewable commit, then use the existing tag-triggered workflow as the release publisher.

**Tech Stack:** .NET SDK 10.0.x, .NET MAUI, Blazor WebAssembly, MSBuild, git submodules, GitHub Actions, GitHub CLI, PowerShell 7.

## Global Constraints

- Target `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, and `net10.0-windows10.0.19041.0`.
- Use ASP.NET Core WebAssembly package version `10.0.11`.
- Pin CI to `actions/setup-dotnet@v4` with `dotnet-version: 10.0.x`.
- Preserve application identifiers, platform minimum versions, assets, signing configuration, user-facing links, and release ZIP naming.
- Use submodule commits `b9ff48e67a816050f14e388e74a14643f1b0ac08`, `0b8b228bf3c7b5b0514f28bb62c12b8f2ed1b5f6`, and `006dcef0a1410f71441014942ff2565440e68c14`.
- Do not retain a .NET 6 fallback or create a `global.json`.
- Every commit must include `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
- Do not create the release tag until local validation and both main-branch workflows succeed.

## File Structure

- `Core/ColorzCore`: gitlink to the .NET 10 ColorzCore head.
- `Core/Event-Assembler`: gitlink to the .NET 10 Event Assembler Core head.
- `Library/EAStandardLibrary`: gitlink to the current default-branch data-library head.
- `EventAssembler/EventAssembler/EventAssembler.csproj`: .NET MAUI target framework declarations.
- `EventAssembler/EventAssemblerWeb/EventAssemblerWeb.csproj`: Blazor target framework and explicit ASP.NET Core package versions.
- `.github/workflows/ci.yml`: cross-platform web artifact and tag-release build.
- `.github/workflows/main.yml`: GitHub Pages build and deployment.
- `README.md`: development SDK and recursive submodule checkout requirements.

---

### Task 1: Upgrade the complete project graph

**Files:**
- Modify: `Core/ColorzCore`
- Modify: `Core/Event-Assembler`
- Modify: `Library/EAStandardLibrary`
- Modify: `EventAssembler/EventAssembler/EventAssembler.csproj:4-5`
- Modify: `EventAssembler/EventAssemblerWeb/EventAssemblerWeb.csproj:4,10-11`

**Interfaces:**
- Consumes: the three exact upstream git SHAs in Global Constraints.
- Produces: a single .NET 10 project-reference graph that later workflow tasks can restore and publish.

- [ ] **Step 1: Run the project-graph assertion and verify the old state fails**

Run from the repository root:

```powershell
$expected = @{
  "Core\ColorzCore" = "b9ff48e67a816050f14e388e74a14643f1b0ac08"
  "Core\Event-Assembler" = "0b8b228bf3c7b5b0514f28bb62c12b8f2ed1b5f6"
  "Library\EAStandardLibrary" = "006dcef0a1410f71441014942ff2565440e68c14"
}

foreach ($entry in $expected.GetEnumerator()) {
  $treePath = $entry.Key.Replace("\", "/")
  $actual = ((git ls-tree HEAD -- $treePath) -split "\s+")[2]
  if ($actual -ne $entry.Value) {
    throw "$treePath is $actual instead of $($entry.Value)"
  }
}
```

Expected: exit code 1; the first message reports an old gitlink SHA.

- [ ] **Step 2: Initialize and advance each submodule to the approved commit**

Run:

```powershell
git submodule update --init --recursive
git -C "Core\ColorzCore" fetch origin master
git -C "Core\ColorzCore" checkout --detach b9ff48e67a816050f14e388e74a14643f1b0ac08
git -C "Core\Event-Assembler" fetch origin master
git -C "Core\Event-Assembler" checkout --detach 0b8b228bf3c7b5b0514f28bb62c12b8f2ed1b5f6
git -C "Library\EAStandardLibrary" fetch origin experimental
git -C "Library\EAStandardLibrary" checkout --detach 006dcef0a1410f71441014942ff2565440e68c14
```

Expected: each checkout reports detached HEAD at the requested commit; no submodule has uncommitted files.

- [ ] **Step 3: Retarget the MAUI project**

Replace the two target-framework properties in
`EventAssembler\EventAssembler\EventAssembler.csproj` with:

```xml
		<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
		<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
```

Do not change any platform minimum version or signing property.

- [ ] **Step 4: Retarget the Blazor project and packages**

Set the framework and package references in
`EventAssembler\EventAssemblerWeb\EventAssemblerWeb.csproj` to:

```xml
    <TargetFramework>net10.0</TargetFramework>
```

```xml
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.11" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.11" PrivateAssets="all" />
```

- [ ] **Step 5: Re-run the project-graph assertion**

Run:

```powershell
$expected = @{
  "Core\ColorzCore" = "b9ff48e67a816050f14e388e74a14643f1b0ac08"
  "Core\Event-Assembler" = "0b8b228bf3c7b5b0514f28bb62c12b8f2ed1b5f6"
  "Library\EAStandardLibrary" = "006dcef0a1410f71441014942ff2565440e68c14"
}

foreach ($entry in $expected.GetEnumerator()) {
  $actual = (git -C $entry.Key rev-parse HEAD).Trim()
  if ($actual -ne $entry.Value) {
    throw "$($entry.Key) is $actual instead of $($entry.Value)"
  }
}

$maui = Get-Content "EventAssembler\EventAssembler\EventAssembler.csproj" -Raw
$web = Get-Content "EventAssembler\EventAssemblerWeb\EventAssemblerWeb.csproj" -Raw
if ($maui -notmatch "net10\.0-android" -or $maui -notmatch "net10\.0-windows10\.0\.19041\.0") {
  throw "MAUI .NET 10 targets are missing"
}
if ($web -notmatch "<TargetFramework>net10\.0</TargetFramework>" -or
    ([regex]::Matches($web, 'Version="10\.0\.11"')).Count -ne 2) {
  throw "Blazor .NET 10 framework or package versions are missing"
}
```

Expected: exit code 0 with no output.

- [ ] **Step 6: Restore and compile the release-producing web graph**

Run:

```powershell
dotnet restore "EventAssembler\EventAssemblerWeb\EventAssemblerWeb.csproj"
dotnet publish "EventAssembler\EventAssemblerWeb\EventAssemblerWeb.csproj" -c Release -o "artifacts\package\web" --no-restore
```

Expected: both commands exit 0; `artifacts\package\web\wwwroot\index.html` and
`artifacts\package\web\wwwroot\_framework` exist.

- [ ] **Step 7: Commit the project graph**

Run:

```powershell
git add -- "Core\ColorzCore" "Core\Event-Assembler" "Library\EAStandardLibrary" "EventAssembler\EventAssembler\EventAssembler.csproj" "EventAssembler\EventAssemblerWeb\EventAssemblerWeb.csproj"
git commit -m "Upgrade project graph to .NET 10" `
  -m "Retarget the MAUI and Blazor applications, align ASP.NET Core packages, and advance all submodules to their upgraded heads." `
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

Expected: one commit containing exactly three gitlink changes and two project files.

### Task 2: Upgrade automation and build documentation

**Files:**
- Modify: `.github/workflows/ci.yml:27-29`
- Modify: `.github/workflows/main.yml:2-3,16-22`
- Modify: `README.md`

**Interfaces:**
- Consumes: the .NET 10 project graph from Task 1.
- Produces: pinned .NET 10 GitHub builds and documented local prerequisites.

- [ ] **Step 1: Run the legacy-framework assertion and verify it fails**

Run:

```powershell
$matches = @(git grep -n -E 'net6\.0|6\.0\.x|Version="6\.0\.' -- ".github" "EventAssembler" "README.md")
if ($matches.Count -gt 0) {
  $matches
  throw "Legacy .NET 6 settings remain"
}
```

Expected: exit code 1 with matches from both workflows and the original project files if Task 1 has not removed all project matches.

- [ ] **Step 2: Pin CI to the .NET 10 SDK**

In `.github\workflows\ci.yml`, keep `actions/setup-dotnet@v4` and change its
input to:

```yaml
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
```

- [ ] **Step 3: Upgrade the GitHub Pages workflow**

Change the publish directory and checkout action in
`.github\workflows\main.yml`, and add SDK setup before publishing:

```yaml
env:
  PUBLISH_DIR: EventAssembler/EventAssemblerWeb/bin/Release/net10.0/publish/wwwroot
```

```yaml
    - uses: actions/checkout@v4
      with:
        submodules: 'recursive'

    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 10.0.x

    - name: Publish app
      run: dotnet publish -c Release
```

Preserve the base-href rewrite, `gh-pages` target branch, and token wiring.

- [ ] **Step 4: Document the development prerequisites**

Append this section to `README.md` after the existing guide links:

````markdown

## Development

Building from source requires the .NET 10 SDK and the workloads for the target
platform. Clone submodules recursively, or initialize them after cloning:

```powershell
git submodule update --init --recursive
```
````

- [ ] **Step 5: Re-run the legacy-framework assertion**

Run:

```powershell
$matches = @(git grep -n -E 'net6\.0|6\.0\.x|Version="6\.0\.' -- ".github" "EventAssembler" "README.md")
if ($matches.Count -gt 0) {
  $matches
  throw "Legacy .NET 6 settings remain"
}
```

Expected: exit code 0 with no output.

- [ ] **Step 6: Reproduce the workflow publish and package contract**

Run:

```powershell
dotnet publish "EventAssembler\EventAssemblerWeb\EventAssemblerWeb.csproj" -c Release -o "artifacts\package\web"
Copy-Item "README.md" "artifacts\package\"
Compress-Archive -Path "artifacts\package\*" -DestinationPath "artifacts\EventAssembler-Web-Windows.zip" -Force

if (-not (Test-Path "artifacts\package\web\wwwroot\index.html")) {
  throw "Published index.html is missing"
}
if (-not (Test-Path "artifacts\EventAssembler-Web-Windows.zip")) {
  throw "Packaged workflow artifact is missing"
}
```

Expected: exit code 0 and a non-empty `artifacts\EventAssembler-Web-Windows.zip`.

- [ ] **Step 7: Commit automation and documentation**

Run:

```powershell
git add -- ".github\workflows\ci.yml" ".github\workflows\main.yml" "README.md"
git commit -m "Build and deploy with .NET 10" `
  -m "Pin GitHub workflows to the .NET 10 SDK, publish the net10.0 web output, and document local build prerequisites." `
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

Expected: one commit containing only the two workflows and `README.md`.

### Task 3: Validate supported local targets and repository state

**Files:**
- Verify: `EventAssembler/EventAssembler.sln`
- Verify: all files modified in Tasks 1 and 2

**Interfaces:**
- Consumes: Tasks 1 and 2.
- Produces: evidence that the exact commit intended for `main` builds and has no stale framework settings or accidental files.

- [ ] **Step 1: Restore the solution**

Run:

```powershell
dotnet restore "EventAssembler\EventAssembler.sln"
```

Expected: exit code 0 with all projects restored for .NET 10.

- [ ] **Step 2: Publish the web release**

Run:

```powershell
dotnet publish "EventAssembler\EventAssemblerWeb\EventAssemblerWeb.csproj" -c Release -o "artifacts\validation\web" --no-restore
```

Expected: exit code 0; the output contains `wwwroot\index.html` and a non-empty
`wwwroot\_framework` directory.

- [ ] **Step 3: Build the Windows MAUI target**

Run:

```powershell
dotnet build "EventAssembler\EventAssembler\EventAssembler.csproj" -c Debug -f net10.0-windows10.0.19041.0 --no-restore
```

Expected: exit code 0 with no compilation errors.

- [ ] **Step 4: Build the Android MAUI target**

Run:

```powershell
dotnet build "EventAssembler\EventAssembler\EventAssembler.csproj" -c Debug -f net10.0-android --no-restore
```

Expected: exit code 0 with no compilation errors.

- [ ] **Step 5: Verify target frameworks, gitlinks, and artifact shape**

Run:

```powershell
$legacy = @(git grep -n -E 'net6\.0|6\.0\.x|Version="6\.0\.' -- ".github" "EventAssembler" "README.md")
if ($legacy.Count -gt 0) {
  $legacy
  throw "Legacy .NET 6 settings remain"
}

$expected = @{
  "Core\ColorzCore" = "b9ff48e67a816050f14e388e74a14643f1b0ac08"
  "Core\Event-Assembler" = "0b8b228bf3c7b5b0514f28bb62c12b8f2ed1b5f6"
  "Library\EAStandardLibrary" = "006dcef0a1410f71441014942ff2565440e68c14"
}
foreach ($entry in $expected.GetEnumerator()) {
  $actual = (git -C $entry.Key rev-parse HEAD).Trim()
  if ($actual -ne $entry.Value) {
    throw "$($entry.Key) is $actual instead of $($entry.Value)"
  }
}

if (-not (Test-Path "artifacts\validation\web\wwwroot\index.html")) {
  throw "Validated web index.html is missing"
}
if ((Get-ChildItem "artifacts\validation\web\wwwroot\_framework" -File).Count -eq 0) {
  throw "Validated web framework payload is empty"
}
```

Expected: exit code 0 with no output.

- [ ] **Step 6: Inspect the final branch**

Run:

```powershell
git diff --check
git status --short
git --no-pager log -5 --decorate --oneline
```

Expected: `git diff --check` exits 0; status has no tracked or untracked files;
the latest commits are the project-graph and automation commits after the
design and plan checkpoints.

### Task 4: Integrate and push `main`

**Files:**
- No file changes.

**Interfaces:**
- Consumes: the verified implementation branch from Task 3.
- Produces: `origin/main` at the verified .NET 10 commit.

- [ ] **Step 1: Fast-forward local `main` to the implementation branch**

From the original repository worktree, run:

```powershell
git fetch origin main
git checkout main
git merge --ff-only upgrade-dotnet-10
```

Expected: `main` advances without a merge commit. If execution occurred
directly on `main`, verify `git branch --show-current` prints `main` and skip
only the merge command.

- [ ] **Step 2: Verify the push preconditions**

Run:

```powershell
git status --short --branch
git rev-list --left-right --count origin/main...main
git tag --list v2026.08.18
git ls-remote --tags origin refs/tags/v2026.08.18
```

Expected: clean status; the rev-list result is `0` behind and at least `1`
ahead; both tag commands return no tag.

- [ ] **Step 3: Push `main` and verify the remote SHA**

Run:

```powershell
git push origin main
$local = (git rev-parse HEAD).Trim()
$remote = (git ls-remote origin refs/heads/main).Split("`t")[0]
if ($local -ne $remote) {
  throw "origin/main is $remote instead of $local"
}
```

Expected: exit code 0; local and remote SHAs match.

- [ ] **Step 4: Wait for main-branch CI and Pages**

Run:

```powershell
$sha = (git rev-parse HEAD).Trim()
$deadline = (Get-Date).AddMinutes(30)
do {
  $runs = gh run list --repo laqieer/EventAssembler --commit $sha --limit 20 `
    --json databaseId,workflowName,status,conclusion | ConvertFrom-Json
  $required = @($runs | Where-Object { $_.workflowName -in @("CI", "DeployToGitHubPages") })
  if ($required.Count -eq 2 -and @($required | Where-Object status -eq "completed").Count -eq 2) {
    break
  }
  if ((Get-Date) -ge $deadline) {
    throw "Timed out waiting for main workflows"
  }
  Start-Sleep -Seconds 20
} while ($true)

$failed = @($required | Where-Object conclusion -ne "success")
if ($failed.Count -gt 0) {
  $failed | Format-Table
  throw "A required main workflow failed"
}
$required | Format-Table workflowName,status,conclusion,databaseId
```

Expected: both required workflows are completed with `success`.

### Task 5: Publish and verify `v2026.08.18`

**Files:**
- No repository file changes.
- Temporary verification files: `C:\Users\zhiwenzhu\.copilot\session-state\a5362513-04c6-47f4-b695-46be082b3b5d\files\v2026.08.18`

**Interfaces:**
- Consumes: successful main-branch workflows from Task 4.
- Produces: a published GitHub release whose tag and assets are verified.

- [ ] **Step 1: Create and push the release tag**

Run:

```powershell
$sha = (git rev-parse HEAD).Trim()
git tag -a v2026.08.18 -m "v2026.08.18"
git push origin v2026.08.18
$remoteTag = (git ls-remote origin 'refs/tags/v2026.08.18^{}').Split("`t")[0]
if ($remoteTag -ne $sha) {
  throw "Release tag resolves to $remoteTag instead of $sha"
}
```

Expected: the annotated tag is pushed and its peeled SHA equals `main`.

- [ ] **Step 2: Wait for tag CI to publish assets**

Run:

```powershell
$sha = (git rev-parse HEAD).Trim()
$deadline = (Get-Date).AddMinutes(30)
do {
  $runs = gh run list --repo laqieer/EventAssembler --commit $sha --limit 20 `
    --json databaseId,workflowName,headBranch,event,status,conclusion | ConvertFrom-Json
  $tagRun = $runs |
    Where-Object { $_.workflowName -eq "CI" -and $_.headBranch -eq "v2026.08.18" } |
    Select-Object -First 1
  if ($null -ne $tagRun -and $tagRun.status -eq "completed") {
    break
  }
  if ((Get-Date) -ge $deadline) {
    throw "Timed out waiting for tag CI"
  }
  Start-Sleep -Seconds 20
} while ($true)

if ($tagRun.conclusion -ne "success") {
  gh run view $tagRun.databaseId --repo laqieer/EventAssembler --log-failed
  throw "Tag CI failed"
}
```

Expected: the `v2026.08.18` CI run completes with `success`.

- [ ] **Step 3: Verify release metadata and asset names**

Run:

```powershell
$release = gh release view v2026.08.18 --repo laqieer/EventAssembler `
  --json tagName,name,isDraft,isPrerelease,publishedAt,url,assets | ConvertFrom-Json

if ($release.isDraft -or $release.isPrerelease) {
  throw "Release is not a final published release"
}
$expectedAssets = @(
  "EventAssembler-Web-Linux.zip",
  "EventAssembler-Web-Windows.zip",
  "EventAssembler-Web-macOS.zip"
)
$actualAssets = @($release.assets.name)
$missing = @($expectedAssets | Where-Object { $_ -notin $actualAssets })
if ($missing.Count -gt 0) {
  throw "Missing release assets: $($missing -join ', ')"
}
if (@($release.assets | Where-Object size -le 0).Count -gt 0) {
  throw "One or more release assets are empty"
}
$release | Select-Object tagName,name,publishedAt,url
```

Expected: final release metadata and all three non-empty ZIP assets.

- [ ] **Step 4: Download and inspect every release archive**

Run:

```powershell
$verifyDir = "C:\Users\zhiwenzhu\.copilot\session-state\a5362513-04c6-47f4-b695-46be082b3b5d\files\v2026.08.18"
New-Item -ItemType Directory -Path $verifyDir -Force | Out-Null
gh release download v2026.08.18 --repo laqieer/EventAssembler --pattern "*.zip" --dir $verifyDir --clobber

foreach ($zip in Get-ChildItem $verifyDir -Filter "*.zip") {
  $destination = Join-Path $verifyDir $zip.BaseName
  Expand-Archive -Path $zip.FullName -DestinationPath $destination -Force
  if (-not (Test-Path (Join-Path $destination "web\wwwroot\index.html"))) {
    throw "$($zip.Name) lacks web\wwwroot\index.html"
  }
  if ((Get-ChildItem (Join-Path $destination "web\wwwroot\_framework") -File).Count -eq 0) {
    throw "$($zip.Name) has an empty Blazor framework payload"
  }
  if (-not (Test-Path (Join-Path $destination "README.md"))) {
    throw "$($zip.Name) lacks README.md"
  }
}
```

Expected: three archives are downloaded and each contains the web entry point,
Blazor framework payload, and README.

- [ ] **Step 5: Verify final repository and GitHub state**

Run:

```powershell
$sha = (git rev-parse HEAD).Trim()
$remote = (git ls-remote origin refs/heads/main).Split("`t")[0]
$tag = (git ls-remote origin 'refs/tags/v2026.08.18^{}').Split("`t")[0]
if ($sha -ne $remote -or $sha -ne $tag) {
  throw "Local main, origin/main, and v2026.08.18 do not resolve to one commit"
}
git status --short --branch
gh release view v2026.08.18 --repo laqieer/EventAssembler --json url,tagName,publishedAt
```

Expected: clean `main`, matching SHAs, and the published release URL.
