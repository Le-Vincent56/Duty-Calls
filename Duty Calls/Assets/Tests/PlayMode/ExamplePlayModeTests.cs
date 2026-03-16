#nullable enable

using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DutyCalls.Tests.PlayMode
{
    public sealed class ExamplePlayModeTests
    {
        [UnityTest]
        public IEnumerator Sanity()
        {
            yield return null;
            Assert.Pass();
        }
    }
}
