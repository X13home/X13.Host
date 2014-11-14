using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace X13 {
  class Workspace : ViewModelBase {
    #region static
    static Workspace() {
      _this = new Workspace();
    }

    static Workspace _this;
    public static Workspace This {
      get { return _this; }
    }
    #endregion static

    #region instance

    #region instance variables

    private ObservableCollection<ItemViewModel> _files;
    private ReadOnlyObservableCollection<ItemViewModel> _readonyFiles;

    #endregion instance variables

    private Workspace() {
      _files = new ObservableCollection<ItemViewModel>();
      _readonyFiles = null;
      _files.Add(ItemViewModel.root);
    }


    public ReadOnlyObservableCollection<ItemViewModel> Files {
      get {
        if(_readonyFiles == null)
          _readonyFiles = new ReadOnlyObservableCollection<ItemViewModel>(_files);

        return _readonyFiles;
      }
    }
    public void AddFile(ItemViewModel i) {
      _files.Add(i);
      ActiveDocument=i;
    }
    private ItemViewModel _activeDocument = null;
    public ItemViewModel ActiveDocument {
      get { return _activeDocument; }
      set {
        if(_activeDocument != value) {
          _activeDocument = value;
          RaisePropertyChanged("ActiveDocument");
        }
      }
    }

    #endregion instance

    public ViewModelBase Open(string p) {
      if(p==null || p.Length<3) {
        return null;
      }
      var fileViewModel = _files.FirstOrDefault(fm => fm.contentId == p);
      if(fileViewModel != null) {
        this.ActiveDocument = fileViewModel; // File is already open so shiw it

        return fileViewModel;
      }

      fileViewModel = _files.FirstOrDefault(fm => fm.contentId == p);
      if(fileViewModel != null)
        return fileViewModel;
      if(p.StartsWith("LO:")) {
        var r=ItemViewModel.root.Get(p.Substring(3));
        r.view=Projection.LO;
        _files.Add(r);
        return r;
      } else if(p.StartsWith("IN:")) {
        var r=ItemViewModel.root.Get(p.Substring(3));
        r.view=Projection.IN;
        _files.Add(r);
        return r;
      }
      return null;
    }
  }
}
