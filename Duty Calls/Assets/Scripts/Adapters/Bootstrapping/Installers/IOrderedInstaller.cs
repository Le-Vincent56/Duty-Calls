#nullable enable
namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    /// <summary>
    /// Base installer contract with deterministic ordering.
    /// </summary>
    public interface IOrderedInstaller
    {
        int Order { get; }
    }
}