using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;

namespace DotnetPackaging.InstallerStub;

/// <summary>
/// Minimal reactive property implementation to project observable values into bindable state.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class ReactiveProperty<T> : ReactiveObject, IDisposable
{
    private readonly BehaviorSubject<T> subject;
    private readonly CompositeDisposable disposables = new();
    private T value;

    public ReactiveProperty(IObservable<T> source, T initialValue)
    {
        value = initialValue;
        subject = new BehaviorSubject<T>(initialValue);
        disposables.Add(subject);

        var subscription = source
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(OnNext);
        disposables.Add(subscription);
    }

    public T Value
    {
        get => value;
        private set => this.RaiseAndSetIfChanged(ref this.value, value);
    }

    public IObservable<T> Changes => subject.AsObservable();

    public void OnNext(T newValue)
    {
        subject.OnNext(newValue);
        Value = newValue;
    }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
