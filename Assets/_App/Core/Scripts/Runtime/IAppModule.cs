namespace Unity.XR.XREAL.App.Core
{
    /// <summary>
    /// Contract for top-level application modules (Drawing Viewer, Switch Recognition, etc.).
    /// </summary>
    public interface IAppModule
    {
        bool IsActive { get; }

        void EnterMode();

        void ExitMode();
    }
}
