using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

// .NET Framework で init アクセサーをコンパイルするためのマーカー型。
[SuppressMessage("Major Code Smell", "S2094", Justification = "init のメタデータに必要なコンパイラ用マーカー型。")]
internal static class IsExternalInit
{
}
