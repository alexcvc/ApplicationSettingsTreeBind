# DevExpress TreeList — Binding Settings (struct + nested classes) in **bound** mode

This README demonstrates how to visualize and edit a settings object graph where a top-level `struct` contains nested `class` objects, **bound** to a DevExpress **TreeList** (WinForms) using a **self-referencing flat list** (`Id`/`ParentId`).

**Pattern**
1. Project the object graph into a flat, self-referencing list (`TreeNodeViewModel`) that TreeList can bind to easily.
2. For editable leaves, store `TargetObject` + `TargetProperty` so edits in TreeList write back to the original objects.
3. Support complex objects, **collections** (`List/Array/IEnumerable`) and **dictionaries** (`IDictionary`).

> ⚠️ You need DevExpress WinForms installed (TreeList) to build/run. See **Build & Run** below.

---

## Contents
- [Domain (settings)](#domain-settings)
- [Bindable node view-model](#bindable-node-view-model)
- [SettingsProjector](#settingsprojector)
- [WinForms wiring](#winforms-wiring)
- [Build & Run](#build--run)
- [Notes](#notes)

---

## DataModel (settings)

```csharp
    public readonly struct DataModel
    {
        public NetworkSettings Network { get; init; }
        public UiSettings Ui { get; init; }
        public LoggingSettings Logging { get; init; }
    }

    public class NetworkSettings
    {
        private string RemoteIp = "127.0.0.1";
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 502;

        public bool UseSsl { get; set; } = false;
        public int Timeout { get; set; } = 1000;
        public int MaxConnections { get; set; } = 100;

        public void ShowMemory()
        {
            GC.Collect();
            long memory = GC.GetTotalMemory(true);
            Console.WriteLine($"Memory used: {memory} bytes");
        }
    }

    public class UiSettings
    {
        public bool DarkMode { get; set; } = true;
        public double Scale { get; set; } = 1.0;
    }

    public class LoggingSettings
    {
        public string Level { get; set; } = "Info";
        public string FilePath { get; set; } = "app.log";
        public List<string> Tags { get; set; } = new() { "core", "startup" };
        public Dictionary<string, string> Extras { get; set; } = new()
        {
            ["RetentionDays"] = "7",
            ["Rotate"] = "true"
        };
    }
```

---

## Bindable node view-model

```csharp
    // Represents a view model for a tree node, designed to be used as a data record in a TreeList control.
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
```

---

## DataToViewProjector

```csharp
    /// Provides functionality to project a hierarchical settings object graph into a flat, self-referencing list
    /// suitable for use in tree-like UI components. This class supports complex objects, collections 
    public static class DataToViewProjector
    {
        private static readonly HashSet<Type> _leafSet = new(new[]
        {
            typeof(string), typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
            typeof(DateTime), typeof(TimeSpan), typeof(Guid), typeof(Uri)
        });

        public static BindingList<TreeNodeViewModel> ToFlatNodes(DataModel root)
        {
            var nodes = new List<TreeNodeViewModel>();
            int nextId = 1;

            // root group node
            int rootId = nextId++;
            nodes.Add(new TreeNodeViewModel
            {
                Id = rootId,
                ParentId = null,
                Name = "Settings",
                Type = "Object",
                Value = null,
                TargetObject = null,
                TargetProperty = null
            });

            // reflect top-level properties of the struct
            foreach (var p in typeof(DataModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var obj = p.GetValue(root);
                AddAnyRecursive(nodes, ref nextId, rootId, p.Name, obj, owner: null, ownerProp: null);
            }

            return new BindingList<TreeNodeViewModel>(nodes);
        }

        /// Recursively processes an object and adds its representation to a list of <see cref="TreeNodeViewModel"/> nodes.
        private static void AddAnyRecursive(List<TreeNodeViewModel> nodes, ref int nextId, int parentId,
                                            string name, object obj, object owner, PropertyInfo ownerProp)
        {
            if (obj == null)
            {
                // show null as a leaf; keep owner/ownerProp so it can be replaced later
                nodes?.Add(new TreeNodeViewModel
                {
                    Id = nextId++,
                    ParentId = parentId,
                    Name = name,
                    Type = "null",
                    Value = "<null>",
                    TargetObject = owner,
                    TargetProperty = ownerProp
                });
                return;
            }

            var t = obj.GetType();

            // unwrap Nullable<T>
            if (IsNullable(t, out var underlying))
            {
                t = underlying;
            }

            // leaf node?
            if (IsLeafType(t))
            {
                nodes?.Add(new TreeNodeViewModel
                {
                    Id = nextId++,
                    ParentId = parentId,
                    Name = name,
                    Type = t.Name,
                    Value = Convert.ToString(obj),
                    TargetObject = owner,
                    TargetProperty = ownerProp
                });
                return;
            }

            // collection?
            if (IsEnumerableButNotString(t))
            {
                AddEnumerable(nodes, ref nextId, parentId, name, obj);
                return;
            }

            // dictionary?
            if (IsDictionary(t))
            {
                AddDictionary(nodes, ref nextId, parentId, name, obj);
                return;
            }

            // complex object -> group + recurse into public instance properties
            var thisId = nextId++;
            nodes?.Add(new TreeNodeViewModel
            {
                Id = thisId,
                ParentId = parentId,
                Name = name,
                Type = "Object",
                Value = null,
                TargetObject = obj,
                TargetProperty = null
            });

            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(pi => pi.CanRead))
            {
                var child = p.GetValue(obj);
                AddAnyRecursive(nodes, ref nextId, thisId, p.Name, child, owner: obj, ownerProp: p);
            }
        }

        // IEnumerable (List/Array/...)
        private static void AddEnumerable(List<TreeNodeViewModel> nodes, ref int nextId, int parentId, string name, object enumerableObj)
        {
            var t = enumerableObj?.GetType();

            // collection group
            var colId = nextId++;
            nodes?.Add(new TreeNodeViewModel
            {
                Id = colId,
                ParentId = parentId,
                Name = name,
                Type = $"Collection<{GetElementTypeName(t)}>",
                Value = null,
                TargetObject = enumerableObj,
                TargetProperty = null
            });

            var index = 0;
            foreach (var (item, itemType) in IterateEnumerable(enumerableObj))
            {
                string itemName = $"[{index}]";
                // elements have no direct owner/ownerProp for write-back; handle edit/insert/remove via context menu patterns
                AddAnyRecursive(nodes, ref nextId, colId, itemName, item, owner: null, ownerProp: null);
                index++;
            }

            if (index == 0)
            {
                nodes?.Add(new TreeNodeViewModel
                {
                    Id = nextId++,
                    ParentId = colId,
                    Name = "(empty)",
                    Type = "Info",
                    Value = "",
                    TargetObject = null,
                    TargetProperty = null
                });
            }
        }

        /// Adds a dictionary object to the specified list of tree node view models, representing its structure
        private static void AddDictionary(List<TreeNodeViewModel> nodes, ref int nextId, int parentId, string name, object dictObj)
        {
            var t = dictObj.GetType();
            var (keyType, valueType) = GetDictTypes(t);

            int dictId = nextId++;
            nodes.Add(new TreeNodeViewModel
            {
                Id = dictId,
                ParentId = parentId,
                Name = name,
                Type = $"Dictionary<{keyType.Name},{valueType.Name}>",
                Value = null,
                TargetObject = dictObj,
                TargetProperty = null
            });

            var dictEnum = (IDictionary)dictObj;
            if (dictEnum.Count == 0)
            {
                nodes.Add(new TreeNodeViewModel
                {
                    Id = nextId++,
                    ParentId = dictId,
                    Name = "(empty)",
                    Type = "Info",
                    Value = "",
                    TargetObject = null,
                    TargetProperty = null
                });
                return;
            }

            foreach (DictionaryEntry de in dictEnum)
            {
                string entryName = $"Key={de.Key}";
                int entryId = nextId++;
                nodes.Add(new TreeNodeViewModel
                {
                    Id = entryId,
                    ParentId = dictId,
                    Name = entryName,
                    Type = "Entry",
                    Value = null,
                    TargetObject = null,
                    TargetProperty = null
                });

                // Key (read-only leaf)
                nodes.Add(new TreeNodeViewModel
                {
                    Id = nextId++,
                    ParentId = entryId,
                    Name = "Key",
                    Type = keyType.Name,
                    Value = Convert.ToString(de.Key),
                    TargetObject = null,
                    TargetProperty = null
                });

                // Value (could be leaf/complex/collection) -> recurse
                AddAnyRecursive(nodes, ref nextId, entryId, "Value", de.Value, owner: null, ownerProp: null);
            }
        }

        // helpers

        /// Determines whether the specified <see cref="Type"/> is considered a leaf type.
        private static bool IsLeafType(Type t)
        {
            if (t.IsEnum) return true;
            if (_leafSet != null && _leafSet.Contains(t)) return true;
            return Type.GetTypeCode(t) != TypeCode.Object; // numeric primitives etc.
        }

        /// Determines whether the specified <see cref="Type"/> is a nullable type and, if so, 
        /// retrieves the underlying type of the nullable type.
        private static bool IsNullable(Type t, out Type underlying)
        {
            underlying = Nullable.GetUnderlyingType(t);
            return underlying != null;
        }

        /// Determines whether the specified <see cref="Type"/> is an enumerable type 
        private static bool IsEnumerableButNotString(Type t)
        {
            if (t == typeof(string)) return false;
            if (IsDictionary(t)) return false;
            return typeof(IEnumerable).IsAssignableFrom(t);
        }

        /// Determines whether the specified <see cref="Type"/> represents a dictionary.
        private static bool IsDictionary(Type t)
        {
            if (typeof(IDictionary).IsAssignableFrom(t)) return true;
            return t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        /// Determines the key and value types of a dictionary type.
        private static (Type keyType, Type valueType) GetDictTypes(Type t)
        {
            var ide = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                ? t
                : t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
            if (ide != null)
            {
                var args = ide.GetGenericArguments();
                return (args[0], args[1]);
            }
            return (typeof(object), typeof(object)); // non-generic IDictionary
        }

        private static string GetElementTypeName(Type enumerableType)
        {
            if (enumerableType is { IsArray: true })
                return enumerableType.GetElementType()?.Name ?? "Object";
            if (enumerableType != null)
            {
                var ienum = enumerableType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                return ienum?.GetGenericArguments()[0].Name ?? "Object";
            }
            return "Object";
        }

        private static IEnumerable<(object Item, Type ItemType)> IterateEnumerable(object enumerableObj)
        {
            if (enumerableObj is not IEnumerable enumerable)
                throw new InvalidCastException($"Object must implement {nameof(IEnumerable)}.");
            foreach (var item in enumerable)
                yield return (item, item?.GetType() ?? typeof(object));
        }

    }

```

---

## WinForms wiring

```csharp
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
```

---

## Build & Run

1. Install **DevExpress WinForms** (TreeList) and ensure DevExpress assemblies are available to your project (via DevExpress NuGet feed or local assemblies).
2. Open `src/DevExpressTreeListDemo/DevExpressTreeListDemo.sln` in **Visual Studio 2022+** (or Rider).
3. Restore packages (if using DevExpress NuGet).  
4. Build and run.

The project targets `net8.0-windows` and uses `UseWindowsForms=true`.

---

## Notes

- Set `KeyFieldName`/`ParentFieldName` **before** `DataSource`.
- Ensure `Id` is unique.
- Use `BeforeExpand` to implement lazy-loading for very large graphs.
- Assign appropriate `RepositoryItem`s dynamically in `ShowingEditor` (e.g., check edit for `Boolean`, combo for `Enum`, date edit for `DateTime`, button-edit for browsing paths).
