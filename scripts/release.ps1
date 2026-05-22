<#
.SYNOPSIS
    Release driver for Code2Viz. Bumps version, commits, tags, and pushes.

.DESCRIPTION
    Bumps the version (major/minor/patch) in Directory.Build.props and
    installer.iss, commits the bump on main, tags it, and pushes the tag
    to origin. The `.github/workflows/release.yml` workflow takes over
    from there: it builds Code2Viz + Animator (Release), runs Inno Setup,
    creates the GitHub release, and attaches the installer.

    Run /update_docs FIRST so the docs commit goes out before the bump
    commit — the release should ship with current documentation.

.PARAMETER Bump
    Which segment of semver to bump. One of: major, minor, patch.

.PARAMETER LocalBuild
    Also build Release configs and the installer locally before pushing
    (useful for offline smoke-testing). The GitHub Actions workflow still
    builds and publishes the canonical artifacts on tag push.

.EXAMPLE
    .\scripts\release.ps1 -Bump patch
    .\scripts\release.ps1 -Bump minor -LocalBuild
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("major", "minor", "patch")]
    [string]$Bump,

    [switch]$LocalBuild
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

# Guard: clean working tree, on main, in sync with origin.
$status = git status --porcelain
if ($status) {
    Write-Error "Working tree not clean. Commit or stash first."
}
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne "main") {
    Write-Error "Not on main (currently $branch). Switch first."
}
git fetch origin --quiet
$behind = (git rev-list HEAD..origin/main --count).Trim()
if ($behind -ne "0") {
    Write-Error "Local main is behind origin/main by $behind commit(s). Pull first."
}

# 1. Read current version from Directory.Build.props.
$propsPath = "Directory.Build.props"
[xml]$props = Get-Content $propsPath
$current = [Version]($props.Project.PropertyGroup.Version)

# 2. Compute new version.
$new = switch ($Bump) {
    "major" { "$($current.Major + 1).0.0" }
    "minor" { "$($current.Major).$($current.Minor + 1).0" }
    "patch" { "$($current.Major).$($current.Minor).$($current.Build + 1)" }
}
$newTag = "v$new"
Write-Host "Bumping $current -> $new" -ForegroundColor Cyan

# 3. Update Directory.Build.props.
$props.Project.PropertyGroup.Version = $new
$props.Project.PropertyGroup.AssemblyVersion = "$new.0"
$props.Project.PropertyGroup.FileVersion = "$new.0"
$props.Save((Resolve-Path $propsPath).Path)

# 4. Sync installer.iss MyAppVersion.
$iss = Get-Content installer.iss -Raw
$iss = $iss -replace '(?m)^(#define MyAppVersion\s+").*?(")', "`${1}$new`${2}"
Set-Content installer.iss -Value $iss -NoNewline

# 5. Commit the bump.
git add Directory.Build.props installer.iss
git commit -m "Release $newTag"

# 6. Optional local smoke build.
if ($LocalBuild) {
    Write-Host "Building Code2Viz (Release)..." -ForegroundColor Cyan
    dotnet build Code2Viz.csproj -c Release -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Code2Viz build failed." }

    Write-Host "Building Animator (Release)..." -ForegroundColor Cyan
    dotnet build Animator/Animator.csproj -c Release -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Animator build failed." }

    $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (Test-Path $iscc) {
        Write-Host "Building installer..." -ForegroundColor Cyan
        & $iscc installer.iss | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup build failed." }
    } else {
        Write-Warning "ISCC.exe not found at $iscc; skipping local installer build (CI will still build it)."
    }
}

# 7. Tag and push. The tag push triggers .github/workflows/release.yml,
#    which builds Release configs, runs Inno Setup, and publishes the
#    GitHub release with the installer attached.
git tag $newTag
git push origin main
git push origin $newTag
Write-Host "Pushed $newTag to origin." -ForegroundColor Green

Write-Host ""
Write-Host "Release workflow triggered. Watch progress at:" -ForegroundColor Cyan
Write-Host "  https://github.com/harilalmn/Code2Viz/actions/workflows/release.yml"
Write-Host ""
Write-Host "When green, the release will appear at:" -ForegroundColor Cyan
Write-Host "  https://github.com/harilalmn/Code2Viz/releases/tag/$newTag"
