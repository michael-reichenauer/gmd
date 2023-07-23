using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

interface IApplicationBarView
{
    View View { get; }
}

class ApplicationBarView : View, IApplicationBarView
{
    UILabel label;

    public View View => this;

    public ApplicationBarView()
    {
        X = 0;
        Y = 0;
        Height = 2;
        Width = Dim.Fill();
        label = new UILabel(0, 0, Common.Text.BrightMagenta("Gmd"));

        var border = new Label(0, 1, new string('─', 200)) { ColorScheme = ColorSchemes.Border };
        Add(label, border);
    }
}

// class FilterView : View, IFilterView
// {
//     readonly Dictionary<Key, OnKeyCallback> keys = new Dictionary<Key, OnKeyCallback>();
//     bool isFocus = false;
//     Label label = null!;
//     UITextField filterField = null!;
//     Label textEnd = null!;
//     Label border = null!;
//     string currentFilter = "";

//     public event Action? FilterChange;

//     public FilterView()
//     {
//         X = 0;
//         Y = 0;
//         Height = 0;
//         Width = Dim.Fill();
//     }

//     public string Filter => filterField.Text;

//     public View View => this;
//     public bool IsFocus
//     {
//         get => isFocus;
//         set
//         {
//             if (label == null)
//             {
//                 label = new Label(0, 0, "Search:") { ColorScheme = ColorSchemes.Label };
//                 filterField = new UITextField(8, 0, 40, "") { ColorScheme = ColorSchemes.TextField };
//                 //filterField.KeyUp += OnKeyUp;    // Update results and select commit on keys
//                 textEnd = new Label(48, 0, "│") { ColorScheme = ColorSchemes.Indicator };
//                 border = new Label(0, 1, new string('─', 200)) { ColorScheme = ColorSchemes.Border };
//             }

//             isFocus = value;
//             if (isFocus)
//             {
//                 Add(label, textEnd, border);
//                 label.PositionCursor();
//                 //Application.Driver.Cols = ;
//                 UI.ShowCursor();
//             }
//             else
//             {
//                 Log.Info("Remove");
//                 RemoveAll();
//             }
//             SetNeedsDisplay();
//         }
//     }

//     // User pressed key in filter field, select commit on enter or update results 
//     void OnKeyUp(View.KeyEventEventArgs e)
//     {
//         if (!IsFocus) return;
//         try
//         {
//             var key = e.KeyEvent.Key;
//             if (key == Key.Enter)
//             {
//                 Log.Info("Enter");
//                 // OnEnter(resultsView.CurrentIndex);
//                 return;
//             }

//             // Log.Info($"Key: {key}");
//             OnFilterChanged();

//         }
//         finally
//         {
//             e.Handled = true;
//         }
//     }

//     void OnFilterChanged()
//     {
//         if (Filter == currentFilter) return;
//         currentFilter = Filter;

//         FilterChange?.Invoke();
//     }

//     // User selected commit from list (or pressed enter on empty results to just close dlg)
//     void OnEnter(int index)
//     {
//         // if (filteredCommits.Count > 0)
//         // {   // User selected commit from list
//         //     this.selectedCommit = filteredCommits[index];
//         // }

//         // dlg.Close();
//     }




//     public void RegisterKeyHandler(Key key, OnKeyCallback callback)
//     {
//         keys[key] = callback;
//     }

//     public override bool ProcessHotKey(KeyEvent keyEvent)
//     {
//         Log.Info($"ProcessHotKey: {keyEvent.Key}");
//         if (!IsFocus) return false;
//         Log.Info($"Filter:IsFocus {IsFocus}");

//         if (keys.TryGetValue(keyEvent.Key, out var callback))
//         {
//             callback();
//             return true;
//         }

//         // // if keyEvent is a letter char, add it to the filter
//         // if (keyEvent.KeyValue >= 32 && keyEvent.KeyValue <= 126)
//         // {
//         //     var c = (char)keyEvent.KeyValue;
//         //     filterText += c;
//         //     filterField.SetFocus();
//         //     return true;
//         // }






//         // if (keyEvent.KeyValue )
//         // {
//         //     Log.Info("Enter");
//         //     // OnEnter(resultsView.CurrentIndex);
//         //     return true;
//         // }

//         return true;
//     }

// internal static ColorScheme Border => new ColorScheme()
// {
//     Normal = Color.BrightMagenta,
//     Focus = Color.White,
//     HotNormal = Color.White,
//     HotFocus = Color.White,
//     Disabled = Color.Dark,
// };

// }



