using System.Windows.Controls;
using System.Windows.Input;
using XboxControllerStudio.ViewModels;
namespace XboxControllerStudio.Views;

public partial class ProfilesView : UserControl
{
    public ProfilesView()
    {
        InitializeComponent();
    }

    private ProfilesViewModel? Vm => DataContext as ProfilesViewModel;

    private void MappingsGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm is null)
            return;

        if (e.Key == Key.Escape)
        {
            Vm.CancelCapture();
            e.Handled = true;
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (Vm.TryCaptureInputFromVirtualKey(vk))
            e.Handled = true;
    }

    private void CaptureInputButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        MappingsGrid.Focus();
        Keyboard.Focus(MappingsGrid);
    }

    private void ProfilesView_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null)
            return;

        if (Vm.TryCaptureInputFromMouse(e.ChangedButton))
            e.Handled = true;
    }
}
