$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF"
& $csc /nologo /target:winexe /optimize+ /win32icon:"$PSScriptRoot\prism.ico" /out:"$PSScriptRoot\Prism.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:"$wpf\PresentationCore.dll" /r:"$wpf\WindowsBase.dll" /r:System.Xaml.dll "$PSScriptRoot\Prism.cs" "$PSScriptRoot\Engine.cs"
