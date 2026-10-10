using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfNotesSample.View;

// 一覧の項目の円に表示するため、文字列の先頭の 1 文字（サロゲートペアや結合文字を含む）を返す。
[ValueConversion(typeof(string), typeof(string))]
public sealed class InitialConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string { Length: > 0 } text ? StringInfo.GetNextTextElement(text) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
