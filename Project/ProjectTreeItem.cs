using System.Collections.ObjectModel;
using System.IO;

namespace Code2Viz.Project;

public class ProjectTreeItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool IsReferencesNode { get; set; }  // True for the "References" virtual node
    public bool IsReferenceItem { get; set; }   // True for individual reference items
    public ObservableCollection<ProjectTreeItem> Children { get; set; } = new();
    
    // Icon Image Path
    public string Icon
    {
        get
        {
            if (IsReferencesNode) return "/img/folder.png";  // Could use a different icon
            if (IsReferenceItem) return "/img/file.png";     // Could use a DLL icon
            return IsDirectory ? "/img/folder.png" : "/img/file.png";
        }
    }

    public ProjectTreeItem()
    {
        // Add dummy item for lazy loading if needed,
        // but for now we'll load eagerly for simplicity
    }
}
