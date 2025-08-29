using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using ApplicationSettingsTreeBind.Source;

using DevExpress.XtraTreeList;

namespace ApplicationSettingsTreeBind
{
    public partial class MainForm : DevExpress.XtraEditors.XtraForm
    {
        private BindingList<TreeNodeViewModel> _treeViewModel;

        public MainForm()
        {
            InitializeComponent();


            treeListSettings.Dock = DockStyle.Fill;

            // 1) Build sample data
            var settings = new DataModel
            {
                Network = new NetworkSettings { Host = "192.168.0.10", Port = 2404 },
                Ui = new UiSettings { DarkMode = false, Scale = 1.25 },
                Logging = new LoggingSettings
                {
                    Level = "Debug",
                    FilePath = @"C:\logs\app.log",
                    Tags = ["core", "startup", "ui"],
                    Extras = new Dictionary<string, string> { ["RetentionDays"] = "14", ["Rotate"] = "true" }
                }
            };
            _treeViewModel = DataToViewProjector.ToFlatNodes(settings);

            // 2) Self-referencing bind
            treeListSettings.KeyFieldName = nameof(TreeNodeViewModel.Id);
            treeListSettings.ParentFieldName = nameof(TreeNodeViewModel.ParentId);
            treeListSettings.DataSource = _treeViewModel;

            // 3) Columns
            treeListSettings.PopulateColumns();
            treeListSettings.Columns[nameof(TreeNodeViewModel.TargetObject)].Visible = false;
            treeListSettings.Columns[nameof(TreeNodeViewModel.TargetProperty)].Visible = false;
            treeListSettings.Columns[nameof(TreeNodeViewModel.IsLeaf)].Visible = false;

            treeListSettings.OptionsBehavior.Editable = true;

            treeListSettings.Columns[nameof(TreeNodeViewModel.Name)].OptionsColumn.AllowEdit = false;
            treeListSettings.Columns[nameof(TreeNodeViewModel.Type)].OptionsColumn.AllowEdit = false;
            treeListSettings.Columns[nameof(TreeNodeViewModel.Value)].OptionsColumn.AllowEdit = true;

            treeListSettings.BestFitColumns();
            treeListSettings.ExpandAll();

            // 4) Write-back on edits
            treeListSettings.CellValueChanging += (s, e) =>
            {
                if (e.Column.FieldName != nameof(TreeNodeViewModel.Value)) return;
                var nodeVm = (TreeNodeViewModel)treeListSettings.GetDataRecordByNode(e.Node);
                if (nodeVm?.TargetProperty == null || nodeVm.TargetObject == null) return;

                try
                {
                    object converted = ConvertTo(e.Value, nodeVm.TargetProperty.PropertyType);
                    nodeVm.TargetProperty.SetValue(nodeVm.TargetObject, converted);
                    nodeVm.Value = Convert.ToString(converted);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Invalid value: {ex.Message}", "Validation", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };
        }

        private static object ConvertTo(object newValue, Type targetType)
        {
            if (newValue == null) return null;
            if (targetType == typeof(string)) return newValue.ToString();
            if (targetType.IsEnum) return Enum.Parse(targetType, newValue.ToString(), ignoreCase: true);
            if (targetType == typeof(bool)) return bool.Parse(newValue.ToString());
            if (targetType == typeof(int)) return int.Parse(newValue.ToString());
            if (targetType == typeof(double)) return double.Parse(newValue.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal)) return decimal.Parse(newValue.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(DateTime)) return DateTime.Parse(newValue.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(TimeSpan)) return TimeSpan.Parse(newValue.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(Uri)) return new Uri(newValue.ToString());
            return System.Convert.ChangeType(newValue, targetType);
        }

    }
}
