using System.IO;
using System.Threading;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.Frameworks;
using NuGet.Configuration;
using Code2Viz.Console;

namespace Code2Viz.Execution;

public class NuGetHelper : IDisposable
{
    private readonly string _localRepo;
    private readonly SourceRepository _repository;
    private readonly SourceCacheContext _cacheContext;
    private readonly ILogger _logger;
    private readonly NuGetFramework _targetFramework;

    public NuGetHelper(string localRepoPath)
    {
        _localRepo = localRepoPath;
        var providers = Repository.Provider.GetCoreV3();
        
        var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
        _repository = new SourceRepository(packageSource, providers);
        
        _cacheContext = new SourceCacheContext();
        _logger = NullLogger.Instance;
        _targetFramework = NuGetFramework.Parse("net9.0-windows");
    }

    public async Task<List<string>> RestorePackageAsync(string packageId, string versionStr)
    {
        var references = new List<string>();
        
        if (!NuGetVersion.TryParse(versionStr, out var version))
        {
            throw new ArgumentException($"Invalid version: {versionStr}");
        }

        var identity = new PackageIdentity(packageId, version);
        await DownloadAndExtractAsync(identity, references, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        
        if (references.Count == 0)
        {
            ConsoleOutput.Instance.WriteLine("NuGet", 0, $"Warning: No DLLs found for {packageId} compatible with {_targetFramework}.");
        }

        return references;
    }

    public async Task<IEnumerable<IPackageSearchMetadata>> SearchPackagesAsync(string searchTerm, int skip = 0, int take = 20)
    {
        var resource = await _repository.GetResourceAsync<PackageSearchResource>();
        var searchFilter = new SearchFilter(includePrerelease: false);

        var results = await resource.SearchAsync(
            searchTerm,
            searchFilter,
            skip: skip,
            take: take,
            _logger,
            CancellationToken.None);

        return results;
    }

    public async Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(string packageId)
    {
        var resource = await _repository.GetResourceAsync<FindPackageByIdResource>();
        var versions = await resource.GetAllVersionsAsync(
            packageId,
            _cacheContext,
            _logger,
            CancellationToken.None);

        return versions;
    }

    private async Task DownloadAndExtractAsync(PackageIdentity identity, List<string> references, HashSet<string> visited)
    {
        if (visited.Contains(identity.Id)) return;
        visited.Add(identity.Id);

        var installPath = Path.Combine(_localRepo, $"{identity.Id}.{identity.Version}");
        var nuspecPath = Path.Combine(installPath, $"{identity.Id}.nuspec");
        
        // 1. Download if not exists
        // 1. Download if not exists
        if (!Directory.Exists(installPath) || !File.Exists(nuspecPath))
        {
            var resource = await _repository.GetResourceAsync<FindPackageByIdResource>();
            var fileName = Path.Combine(_localRepo, $"{identity.Id}.{identity.Version}.nupkg");

            if (!Directory.Exists(_localRepo)) Directory.CreateDirectory(_localRepo);

            // Clean up potentially stale partial download
            if (File.Exists(fileName)) File.Delete(fileName);

            // Scope downloader to ensure it releases the stream
            {
                using var downloader = await resource.GetPackageDownloaderAsync(identity, _cacheContext, _logger, CancellationToken.None);

                // Extract directly
                if (Directory.Exists(installPath)) Directory.Delete(installPath, true);
                Directory.CreateDirectory(installPath);

                await downloader.CopyNupkgFileToAsync(fileName, CancellationToken.None);
            }

            // Scope reader to ensure it is disposed before deleting the file
            {
                using var packageReader = new PackageArchiveReader(fileName);
                foreach (var file in packageReader.GetFiles())
                {
                    var target = Path.Combine(installPath, file);
                    var dir = Path.GetDirectoryName(target);
                    if (!Directory.Exists(dir) && dir != null) Directory.CreateDirectory(dir);
                    packageReader.ExtractFile(file, target, _logger);
                }
            }
            
            // Cleanup nupkg
            File.Delete(fileName);
        }

        // 2. Read dependencies and recurse
        using var reader = new PackageFolderReader(installPath);
        
        // Get references for this package
        // Get references for this package
        var libItems = reader.GetLibItems().ToList();
        var reducer = new FrameworkReducer();
        
        // Try to find best match in Lib
        var nearest = reducer.GetNearest(_targetFramework, libItems.Select(x => x.TargetFramework));
        
        if (nearest == null)
        {
            // Fallback 1: Try Ref
            var refItems = reader.GetItems("ref").ToList();
            if (refItems.Any())
            {
                var refFrameworks = refItems.Select(x => x.TargetFramework);
                nearest = reducer.GetNearest(_targetFramework, refFrameworks);

                if (nearest != null)
                {
                    AddReferencesFromGroup(refItems.First(x => x.TargetFramework.Equals(nearest)), installPath, references);
                }
                else
                {
                    // Fallback 2: Loose match - just pick the last one (usually highest version)
                    // This is "unsafe" but necessary if strict matching fails (e.g. net8.0-windows7.0 on net9.0)
                    var bestRef = refItems.OrderByDescending(x => x.TargetFramework.Version).FirstOrDefault();
                    if (bestRef != null)
                    {
                        ConsoleOutput.Instance.WriteLine("NuGet", 0, $"Warning: Using fallback framework '{bestRef.TargetFramework}' for {identity.Id}");
                        AddReferencesFromGroup(bestRef, installPath, references);
                    }
                }
            }
            else if (libItems.Any())
            {
                // Fallback 3: Loose match in Lib
                var bestLib = libItems.OrderByDescending(x => x.TargetFramework.Version).FirstOrDefault();
                if (bestLib != null)
                {
                    ConsoleOutput.Instance.WriteLine("NuGet", 0, $"Warning: Using fallback framework '{bestLib.TargetFramework}' for {identity.Id}");
                    AddReferencesFromGroup(bestLib, installPath, references);
                }
            }
        }
        else
        {
            AddReferencesFromGroup(libItems.First(x => x.TargetFramework.Equals(nearest)), installPath, references);
        }

        // Handle Dependencies
        var depGroups = reader.GetPackageDependencies();
        var nearestDep = reducer.GetNearest(_targetFramework, depGroups.Select(x => x.TargetFramework));
        
        if (nearestDep != null)
        {
            var depGroup = depGroups.First(x => x.TargetFramework.Equals(nearestDep));
            foreach (var pkgDep in depGroup.Packages)
            {
                // Find latest version if range? 
                // For simplicity, we just resolve the explicit version range to a specific version?
                // Actually, resolving versions is hard. We will just take the MinVersion.
                
                await DownloadAndExtractAsync(new PackageIdentity(pkgDep.Id, pkgDep.VersionRange.MinVersion), references, visited);
            }
        }
    }

    private void AddReferencesFromGroup(FrameworkSpecificGroup group, string installPath, List<string> references)
    {
        foreach (var item in group.Items)
        {
            if (item.EndsWith(".dll"))
            {
                var fullPath = Path.Combine(installPath, item);
                if (!references.Contains(fullPath))
                {
                    references.Add(fullPath);
                }
            }
        }
    }

    public void Dispose()
    {
        _cacheContext.Dispose();
    }
}
