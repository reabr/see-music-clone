using System.Windows;
using SeeMusicClone.App.ViewModels;

namespace SeeMusicClone.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        Closed += (_, _) => _viewModel.Dispose();
    }
}
