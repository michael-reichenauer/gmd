namespace gmd.Utils;

public static class FatalExceptionsExtensions
{
    // Whether an exception can be taken for a failure of what was tried, rather than for a bug in
    // gmd or a broken runtime, which no catch should take for one: those go on up to the handlers
    // of ExceptionHandling, which stop gmd. For a catch around what fails for reasons outside gmd,
    // a process, a pipe, a download:
    //
    //   catch (Exception e) when (e.IsNotFatal())
    //
    // An ArgumentException, InvalidOperationException or NotSupportedException is not fatal, since
    // an API throws them for input it cannot take as well, which is a failure of the call, as
    // Result.Catch takes it too. The types are matched with what derives from them, e.g. a
    // DivideByZeroException is an ArithmeticException.
    public static bool IsNotFatal(this Exception e) =>
        e
            is not (
                AssertException
                or ResultException
                or NullReferenceException
                or InvalidCastException
                or IndexOutOfRangeException
                or ArrayTypeMismatchException
                or ArithmeticException
                or NotImplementedException
                or OutOfMemoryException
                or InsufficientExecutionStackException
                or StackOverflowException
                or AccessViolationException
                or BadImageFormatException
                or InvalidProgramException
                or TypeInitializationException
                or MemberAccessException
            );
}
