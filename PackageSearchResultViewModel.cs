using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Code2Viz;

public class PackageSearchResultViewModel : INotifyPropertyChanged
{
    private readonly IPackageSearchMetadata _metadata;
    private NuGetVersion _selectedVersion;
    private bool _isLoadingVersions;

    public PackageSearchResultViewModel(IPackageSearchMetadata metadata)
    {
        _metadata = metadata;
        _selectedVersion = metadata.Identity.Version;
        Versions = new ObservableCollection<NuGetVersion> { _selectedVersion };
    }

    public string Id => _metadata.Identity.Id;
    public string Description => _metadata.Description;
    public IPackageSearchMetadata Metadata => _metadata;

    public ObservableCollection<NuGetVersion> Versions { get; }

    public NuGetVersion SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (_selectedVersion != value)
            {
                _selectedVersion = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsLoadingVersions
    {
        get => _isLoadingVersions;
        set
        {
            if (_isLoadingVersions != value)
            {
                _isLoadingVersions = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
