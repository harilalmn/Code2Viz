using System;
using System.Collections.Generic;
using System.Linq;
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

        public HelpWindow()
        {
            InitializeComponent();
            _generator = new DocGenerator();
            _allTypes = _generator.GetDocumentableTypes();
            
            PopulateTree(_allTypes);
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
            var query = SearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                PopulateTree(_allTypes);
            }
            else
            {
                var filtered = _allTypes.Where(t => t.Name.ToLower().Contains(query)).ToList();
                PopulateTree(filtered);
            }
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
