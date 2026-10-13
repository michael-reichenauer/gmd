using System.ComponentModel;
using System.IO.Pipes;

namespace gmdTest.Utils;

[TestClass]
public class FatalExceptionsExtensionsTest
{
    // What fails for reasons outside gmd: a program that is not there, a pipe that closed, a path
    // or an argument an API could not take, a call on something already disposed
    [TestMethod]
    public void TestAFailureOfWhatWasTriedIsNotFatal()
    {
        Exception[] failures =
        [
            new IOException("Pipe is broken"),
            new Win32Exception(2, "No such file or directory"),
            new TimeoutException(),
            new UnauthorizedAccessException(),
            new ArgumentException("Illegal characters in path"),
            new ArgumentOutOfRangeException("index"),
            new InvalidOperationException("Pipe hasn't been connected yet"),
            new ObjectDisposedException(nameof(NamedPipeServerStream)),
            new PlatformNotSupportedException(),
            new OperationCanceledException(),
        ];

        foreach (var e in failures)
            Assert.IsTrue(e.IsNotFatal(), e.GetType().Name);
    }

    // A bug in gmd or a broken runtime, including what derives from the types listed, e.g. a
    // DivideByZeroException; it used to take the exact type, so only ArithmeticException itself
    [TestMethod]
    public void TestABugIsFatal()
    {
        Exception[] bugs =
        [
            Asserter.Fail("Broken"),
            new ResultException("Result is not an error"),
            new NullReferenceException(),
            new InvalidCastException(),
            new IndexOutOfRangeException(),
            new DivideByZeroException(),
            new OverflowException(),
            new NotImplementedException(),
            new OutOfMemoryException(),
            new InsufficientExecutionStackException(),
            new TypeInitializationException("gmd.Some", null),
            new MissingMethodException(),
        ];

        foreach (var e in bugs)
            Assert.IsFalse(e.IsNotFatal(), e.GetType().Name);
    }
}
