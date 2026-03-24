using CommunityToolkit.Mvvm.ComponentModel;

namespace Type4Me.ViewModels;

/// <summary>
/// About tab ViewModel.
/// </summary>
public partial class AboutViewModel : ObservableObject
{
    public string Version => System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "1.0.0";

    public string Platform => "Windows (.NET 8)";
    public string Engine => "Type4Me";

    public string DataPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Type4Me");
}
