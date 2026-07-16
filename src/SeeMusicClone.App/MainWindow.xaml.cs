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

        // The keyboard's "active notes" highlight isn't a simple one-way binding
        // (it's derived from Notes + CurrentTime), so recompute it each tick.
        _viewModel.TimeAdvanced += (_, _) =>
            Keyboard.UpdateActiveNotes(_viewModel.Notes, _viewModel.CurrentTime);

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Notes))
                Keyboard.UpdateActiveNotes(_viewModel.Notes, _viewModel.CurrentTime);
        };

        Closed += (_, _) => _viewModel.Dispose();
    }
}
