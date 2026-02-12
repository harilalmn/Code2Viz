using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Code2Viz.Documentation;

namespace Code2Viz
{
    public partial class HelpWindow : Window
    {
        private DocGenerator _generator;
        private List<Type> _allTypes;
        private List<SearchableItem> _searchIndex;

        public HelpWindow()
        {
            InitializeComponent();
            _generator = new DocGenerator();
            _allTypes = _generator.GetDocumentableTypes();

            BuildSearchIndex();
            PopulateTree(_allTypes);

            // Show welcome page by default
            ShowWelcomePage();
        }

        private void BuildSearchIndex()
        {
            _searchIndex = new List<SearchableItem>();

            foreach (var type in _allTypes)
            {
                // Add the type itself
                _searchIndex.Add(new SearchableItem
                {
                    Name = type.Name,
                    FullName = type.Name,
                    ItemType = "Class",
                    DeclaringType = type
                });

                // Add properties
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (var prop in props)
                {
                    _searchIndex.Add(new SearchableItem
                    {
                        Name = prop.Name,
                        FullName = $"{type.Name}.{prop.Name}",
                        ItemType = "Property",
                        DeclaringType = type,
                        Signature = prop.PropertyType.Name
                    });
                }

                // Add methods
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object));
                foreach (var method in methods)
                {
                    var paramStr = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                    _searchIndex.Add(new SearchableItem
                    {
                        Name = method.Name,
                        FullName = $"{type.Name}.{method.Name}",
                        ItemType = "Method",
                        DeclaringType = type,
                        Signature = $"({paramStr}) → {method.ReturnType.Name}"
                    });
                }
            }
        }

        private void ShowWelcomePage()
        {
            DocViewer.Document = _generator.GenerateWelcomePage();
            DocViewerFSharp.Document = _generator.GenerateWelcomePage();
        }

        private void PopulateTree(List<Type> types)
        {
            DocTree.Items.Clear();

            // Group by Namespace
            var groups = types.GroupBy(t => t.Namespace).OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var nsItem = new TreeViewItem { Header = group.Key, IsExpanded = true };
                foreach (var type in group)
                {
                    var typeItem = new TreeViewItem { Header = type.Name, Tag = type };
                    nsItem.Items.Add(typeItem);
                }
                DocTree.Items.Add(nsItem);
            }
        }

        private void PopulateTreeWithSearchResults(List<SearchableItem> results)
        {
            DocTree.Items.Clear();

            // Group by declaring type
            var groups = results.GroupBy(r => r.DeclaringType).OrderBy(g => g.Key.Name);

            foreach (var group in groups)
            {
                var typeItem = new TreeViewItem
                {
                    Header = group.Key.Name,
                    Tag = group.Key,
                    IsExpanded = true
                };

                foreach (var item in group.OrderBy(i => i.ItemType).ThenBy(i => i.Name))
                {
                    if (item.ItemType == "Class")
                    {
                        // Don't add duplicate class entry under itself
                        continue;
                    }

                    var icon = item.ItemType == "Property" ? "◆" : "●";
                    var memberItem = new TreeViewItem
                    {
                        Header = $"{icon} {item.Name}  {item.Signature}",
                        Tag = group.Key,
                        Foreground = item.ItemType == "Property"
                            ? System.Windows.Media.Brushes.DarkCyan
                            : System.Windows.Media.Brushes.DarkBlue
                    };
                    typeItem.Items.Add(memberItem);
                }

                DocTree.Items.Add(typeItem);
            }
        }

        private void DocTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is Type type)
            {
                DocViewer.Document = _generator.GenerateDocForType(type);
                DocViewerFSharp.Document = _generator.GenerateFSharpDocForType(type);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allTypes == null || _searchIndex == null) return;

            var query = SearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                PopulateTree(_allTypes);
            }
            else
            {
                // Search across all items (types, properties, methods)
                var results = _searchIndex
                    .Where(item => item.Name.ToLower().Contains(query) ||
                                   item.FullName.ToLower().Contains(query))
                    .ToList();

                if (results.Any())
                {
                    PopulateTreeWithSearchResults(results);
                }
                else
                {
                    DocTree.Items.Clear();
                    DocTree.Items.Add(new TreeViewItem { Header = "No results found", IsEnabled = false });
                }
            }
        }

        private class SearchableItem
        {
            public string Name { get; set; }
            public string FullName { get; set; }
            public string ItemType { get; set; } // "Class", "Property", "Method"
            public Type DeclaringType { get; set; }
            public string Signature { get; set; }
        }

        private void PrintBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DocViewer.Document != null)
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // FlowDocument needs to be adjusted for printing sometimes, usually works okay directly
                    // Or we clone it to avoid UI thread issues if needed.
                    // For simplicitly, printing the viewer's document.
                    
                    // We need to clone the document essentially or detach it, but IDocumentPaginatorSource works.
                    IDocumentPaginatorSource idp = DocViewer.Document;
                    printDialog.PrintDocument(idp.DocumentPaginator, "Viz2D Documentation");
                }
            }
        }
    }
}
