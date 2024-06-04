using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI;

namespace DotnetPackaging.Gui.ViewModels;

public class ValidatableDialogViewModel<T> : IResult<Unit> where T : ReactiveValidationObject
{
    public T ViewModel { get; }
    private readonly TaskCompletionSource<Unit> tcs = new();
    public ValidatableDialogViewModel(T viewModel)
    {
        ViewModel = viewModel;
        Accept = ReactiveCommand.Create(() => Result, ViewModel.IsValid());
    }

    public ReactiveCommand<Unit, Task<Unit>> Accept { get; set; }

    public Task<Unit> Result => tcs.Task;
}