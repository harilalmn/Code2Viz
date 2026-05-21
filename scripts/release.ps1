<#
.SYNOPSIS
    Manual release driver for Code2Viz.

.DESCRIPTION
    Bumps the version (major/minor/patch) in Directory.Build.props and
    installer.iss, builds Release configs of Code2Viz + Animator, builds
    the Inno Setup installer if ISCC.exe is available, tags the commit,
    pushes main + tag, and creates a GitHub release with the installer
    attached (uses `gh` CLI if installed; otherwise prints the manual
    upload URL).

    Run /update_docs FIRST so the docs commit goes out before the bump
    commit — the release should ship with current documentation.

.PARAMETER Bump
    Which segment of semver to bump. One of: major, minor, patch.

.PARAMETER Notes
    Optional release notes body. Defaults to the auto-generated
    "v<new> — changes since v<previous>" with git log oneline.

.EXAMPLE
    .\scripts\release.ps1 -Bump patch
    .\scripts\release.ps1 -Bump minor -Notes "First public beta"
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("major", "minor", "patch")]
    [string]$Bump,

    [string]$Notes = ""
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
$prevTag = "v$current"

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

# 6. Build Release configurations.
Write-Host "Building Code2Viz (Release)..." -ForegroundColor Cyan
dotnet build Code2Viz.csproj -c Release -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Code2Viz build failed." }

Write-Host "Building Animator (Release)..." -ForegroundColor Cyan
dotnet build Animator/Animator.csproj -c Release -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Animator build failed." }

# 7. Build Inno Setup installer.
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$installerPath = "installer\output\Code2Viz-$new-Setup.exe"
if (Test-Path $iscc) {
    Write-Host "Building installer..." -ForegroundColor Cyan
    & $iscc installer.iss | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup build failed." }
    if (-not (Test-Path $installerPath)) {
        Write-Warning "Expected $installerPath but it wasn't produced."
    }
} else {
    Write-Warning "ISCC.exe not found at $iscc; skipping installer build."
    $installerPath = $null
}

# 8. Tag and push.
git tag $newTag
git push origin main
git push origin $newTag
Write-Host "Pushed $newTag to origin." -ForegroundColor Green

# 9. Auto-generate notes if none supplied.
if ([string]::IsNullOrEmpty($Notes)) {
    $previousExists = git rev-parse -q --verify "refs/tags/$prevTag" 2>$null
    if ($LASTEXITCODE -eq 0) {
        $log = git log "$prevTag..HEAD" --pretty=format:"- %s" 2>$null
        $Notes = "Changes since ${prevTag}:`n`n$log"
    } else {
        $Notes = "Release $newTag"
    }
}

# 10. Create GitHub release.
$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($gh) {
    $ghArgs = @("release", "create", $newTag, "--title", $newTag, "--notes", $Notes)
    if ($installerPath -and (Test-Path $installerPath)) { $ghArgs += $installerPath }
    & $gh.Source @ghArgs
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GitHub release published." -ForegroundColor Green
    } else {
        Write-Warning "gh release create failed; finish manually at the URL below."
    }
} else {
    Write-Host ""
    Write-Host "gh CLI not installed. Finish the release manually:" -ForegroundColor Yellow
    $url = "https://github.com/harilalmn/Code2Viz/releases/new?tag=$newTag"
    Write-Host "  $url"
    if ($installerPath) { Write-Host "  Upload: $installerPath" }
}

Write-Host ""
Write-Host "Released $newTag" -ForegroundColor Green
