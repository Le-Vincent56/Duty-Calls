#nullable enable
using System;

namespace DutyCalls.Adapters.DI
{
    /// <summary>
    /// Marks a single method as the injection entry point for a component.
    /// The injector resolves parameters by type from the active scope and invokes the method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class InjectAttribute : Attribute { }
}