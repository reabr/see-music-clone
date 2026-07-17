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

        // Active keys are derived from Notes + VisualTime, so refresh them whenever either changes.
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Notes) ||
                e.PropertyName == nameof(MainViewModel.VisualTime))
            {
                Keyboard.UpdateActiveNotes(_viewModel.Notes, _viewModel.VisualTime);
            }
        };

        Closed += (_, _) => _viewModel.Dispose();
    }
}
