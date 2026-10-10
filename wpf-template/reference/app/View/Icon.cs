using System.Windows;
using System.Windows.Media;

namespace WpfNotesSample.View;

// ボタンやアイコン表示にアイコンの形状を指定する。描画は View/Styles/ のテンプレートが担当する。
public static class Icon
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.RegisterAttached(
        "Data", typeof(Geometry), typeof(Icon), new FrameworkPropertyMetadata(null));

    public static Geometry? GetData(DependencyObject element) => (Geometry?)element.GetValue(DataProperty);
    public static void SetData(DependencyObject element, Geometry? value) => element.SetValue(DataProperty, value);
}
