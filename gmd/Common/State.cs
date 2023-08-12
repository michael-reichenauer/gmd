// using gmd.Common.Private;

// namespace gmd.Common;


// interface IState
// {
//     State Get();
//     void Set(Action<State> set);
// }


// class StateImpl : IState
// {
//     static readonly string FilePath = Path.Join(Environment.GetFolderPath(
//         Environment.SpecialFolder.UserProfile), ".gmdstate");
//     private readonly IFileStore store;

//     internal StateImpl(IFileStore store) => this.store = store;

//     public State Get() => store.Get<State>(FilePath);

//     public void Set(Action<State> set) => store.Set(FilePath, set);
// }
