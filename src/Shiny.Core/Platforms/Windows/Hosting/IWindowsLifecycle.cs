namespace Shiny.Hosting;


public interface IWindowsLifecycle
{
    public interface IApplicationLifecycle
    {
        void OnWindowCreated(Microsoft.UI.Xaml.Window window);
    }
}
