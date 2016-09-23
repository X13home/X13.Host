using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace X13.Agent3 {
  public class DataButton : Button {
	private Image _img;
	private TopicSrc _TonClick;
	private TopicSrc _TisEnabled;
	private TopicSrc _TValue;
	private bool _value;


	public DataButton()
	  : base() {
	  _img=new Image();
	  _img.Height=64;
	  _img.Width=64;
	  _img.Stretch=Stretch.Fill;
	  base.Background=null;
	  base.Content=_img;
	  base.PreviewMouseDown+=MouseDownFunc;
	  base.PreviewMouseUp+=MouseUpFunc;
	  //base.BorderThickness=new Thickness(0);
	}


	public static readonly DependencyProperty ImageFalseProperty = DependencyProperty.Register("ImageFalse", typeof(ImageSource), typeof(DataButton), new PropertyMetadata(DPCallback));
	public ImageSource ImageFalse { get { return (ImageSource)GetValue(ImageFalseProperty); } set { SetValue(ImageFalseProperty, value); } }


	public static readonly DependencyProperty ImageTrueProperty = DependencyProperty.Register("ImageTrue", typeof(ImageSource), typeof(DataButton), new PropertyMetadata(DPCallback));
	public ImageSource ImageTrue { get { return (ImageSource)GetValue(ImageTrueProperty); } set { SetValue(ImageTrueProperty, value); } }

	private static void DPCallback(DependencyObject dobj, DependencyPropertyChangedEventArgs args) {
	  DataButton tb=dobj as DataButton;
	  if(tb!=null) {
		tb.UpdateState();
	  }
	}


	public static readonly DependencyProperty PathOnClickProperty = DependencyProperty.Register("PathOnClick", typeof(String), typeof(DataButton), new PropertyMetadata(PathOnClickCallback));
	public String PathOnClick { get { return (String)GetValue(PathOnClickProperty); } set { SetValue(PathOnClickProperty, value); } }
	private static void PathOnClickCallback(DependencyObject dobj, DependencyPropertyChangedEventArgs args) {
	  DataButton tb=dobj as DataButton;
	  string path=args.NewValue as string;
	  if(tb!=null && path!=null) {
		tb._TonClick=new TopicSrc(path);
	  } else {
		tb._TonClick=null;
	  }
	}

	public static readonly DependencyProperty PathIsEnabledProperty = DependencyProperty.Register("PathIsEnabled", typeof(String), typeof(DataButton), new PropertyMetadata(IsEnabledCallback));
	public String PathIsEnabled { get { return (String)GetValue(PathIsEnabledProperty); } set { SetValue(PathIsEnabledProperty, value); } }
	private static void IsEnabledCallback(DependencyObject dobj, DependencyPropertyChangedEventArgs args) {
	  DataButton tb=dobj as DataButton;
	  string path=args.NewValue as string;
	  if(tb!=null && path!=null) {
		tb._TisEnabled=new TopicSrc(path);
		tb._TisEnabled.PropertyChanged+=tb._TisEnabled_PropertyChanged;
	  } else {
		if(tb._TisEnabled!=null) {
		  tb._TisEnabled.PropertyChanged-=tb._TisEnabled_PropertyChanged;
		  tb._TisEnabled=null;
		}
	  }
	}

	private void _TisEnabled_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
	  bool v=_TisEnabled==null || (_TisEnabled.value is bool && (bool)_TisEnabled.value==true);
	  base.Dispatcher.BeginInvoke(new Action(() => base.IsEnabled=v));
	}

	public static readonly DependencyProperty PathValueProperty = DependencyProperty.Register("PathValue", typeof(String), typeof(DataButton), new PropertyMetadata(ValueCallback));
	public String PathValue { get { return (String)GetValue(PathValueProperty); } set { SetValue(PathValueProperty, value); } }
	private static void ValueCallback(DependencyObject dobj, DependencyPropertyChangedEventArgs args) {
	  DataButton tb=dobj as DataButton;
	  string path=args.NewValue as string;
	  if(tb!=null && path!=null) {
		tb._TValue=new TopicSrc(path);
		tb._TValue.PropertyChanged+=tb._TValue_PropertyChanged;
	  } else {
		if(tb._TValue!=null) {
		  tb._TValue.PropertyChanged-=tb._TValue_PropertyChanged;
		  tb._TValue=null;
		}
	  }
	}

	private void MouseDownFunc(object sender, System.Windows.Input.MouseButtonEventArgs e) {
	  if(_TonClick!=null) {
		_TonClick.value=true;
	  }
	}
	private void MouseUpFunc(object sender, System.Windows.Input.MouseButtonEventArgs e) {
	  if(_TonClick!=null) {
		_TonClick.value=false;
	  }
	}

	private void _TValue_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
	  _value=_TValue!=null && _TValue.value is bool && (bool)_TValue.value==true;
	  base.Dispatcher.BeginInvoke(new Action(() => {
		UpdateState();
	  }));
	}

	private void UpdateState() {
	  _img.Source=(base.IsEnabled && _value)?ImageTrue:ImageFalse;
	}
  }
}
