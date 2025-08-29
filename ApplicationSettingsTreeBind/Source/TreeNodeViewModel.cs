using System;
using System.Reflection;

namespace ApplicationSettingsTreeBind.Source
{
    /// <summary>
    /// Represents a view model for a tree node, designed to be used as a data record in a TreeList control.
    /// </summary>
    public class TreeNodeViewModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the tree node.
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Gets or sets the identifier of the parent node in the tree structure.
        /// </summary>
        public int? ParentId { get; set; }

        // columns to display
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }

        // write-back target when edited
        public object TargetObject { get; set; }
        public PropertyInfo TargetProperty { get; set; }

        public bool IsLeaf => TargetProperty != null;
    }
}
