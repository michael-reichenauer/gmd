using System.Text.RegularExpressions;

namespace gmdTest.Utils;

// R/R<T> and Try are how every fallible operation in gmd reports failure; exceptions are for bugs.
// These pin the contract the whole codebase is written against.
[TestClass]
public class ResultTest
{
    [TestMethod]
    public void TestOkValue()
    {
        Assert.IsTrue(Try(out var value, out var e, Divide(10, 2)), $"{e}");
        Assert.AreEqual(5, value);
    }

    [TestMethod]
    public void TestErrorValue()
    {
        Assert.IsFalse(Try(out var value, out var e, Divide(10, 0)));
        Assert.AreEqual(0, value, "The value is default when there is an error");
        Assert.AreEqual("Cannot divide by zero", e.ErrorMessage);
    }

    // The overload used where the error itself does not matter
    [TestMethod]
    public void TestTryIgnoringTheError()
    {
        Assert.IsTrue(Try(out var value, Divide(10, 2)));
        Assert.AreEqual(5, value);
        Assert.IsFalse(Try(out var _, Divide(10, 0)));
    }

    // R without a value, i.e. an operation that either succeeds or fails
    [TestMethod]
    public void TestResultWithoutValue()
    {
        Assert.IsTrue(Try(out var e, Validate("dev")), $"{e}");
        Assert.IsFalse(Try(out var e2, Validate("")));
        Assert.AreEqual("Empty name", e2.ErrorMessage);

        Assert.IsTrue(Try(Validate("dev")));
        Assert.IsFalse(Try(Validate("")));
    }

    [TestMethod]
    public void TestOk()
    {
        bool isOk = R.Ok;

        Assert.IsTrue(Try(R.Ok));
        Assert.IsTrue(isOk);
        Assert.AreEqual("OK", R.Ok.ToString());
    }

    // The idiom used everywhere: an error from a called function is returned as is, so the
    // original message survives all the way up
    [TestMethod]
    public void TestErrorPropagates()
    {
        Assert.IsFalse(Try(out var _, out var e, Outer(0)));
        Assert.AreEqual("Cannot divide by zero", e.ErrorMessage);
    }

    // ... and wrapping adds a message without losing the inner one
    [TestMethod]
    public void TestWrappedErrorKeepsTheInnerMessages()
    {
        Assert.IsFalse(Try(out var _, out var e, OuterWrapping(0)));

        Assert.AreEqual("Failed to calculate", e.ErrorMessage);
        Assert.AreEqual("Failed to calculate,\nCannot divide by zero", e.AllErrorMessages());
        Assert.AreEqual("Error: Failed to calculate", e.ToString());
    }

    // R.Error captures where it was created, since the exception it wraps is never thrown and so
    // has no stack of its own
    [TestMethod]
    public void TestErrorCapturesTheCallerFileAndLine()
    {
        var error = R.Error("failed");

        var stack = error.GetResultException().StackTrace ?? "";
        StringAssert.Matches(
            stack,
            new Regex($@"ResultTest\.cs\(\d+\){nameof(TestErrorCapturesTheCallerFileAndLine)}"),
            $"Caller info missing from '{stack}'"
        );
    }

    // Reading the value without having checked for an error is a bug in the caller, so it fails
    // fast rather than returning a default value that would then travel on
    [TestMethod]
    public void TestGetResultValueFailsFastWhenTheErrorWasNeverChecked()
    {
        R<int> result = 5;

        var e = Assert.ThrowsException<InvalidOperationException>(() =>
        {
            result.GetResultValue();
        });
        StringAssert.Contains(e.Message, "IsError or IsOk was never checked");
    }

    [TestMethod]
    public void TestGetResultValueFailsFastOnAnError()
    {
        R<int> result = R.Error("the failure");

        Assert.IsTrue(result.IsResultError, "Checked, so this is not the never-checked fail fast");
        var e = Assert.ThrowsException<InvalidOperationException>(() =>
        {
            result.GetResultValue();
        });
        StringAssert.Contains(e.Message, "the failure");
    }

    // A fail fast is reported through Asserter, which the running program logs and shows
    [TestMethod]
    public void TestFailFastRaisesTheAsserterEvent()
    {
        var raised = 0;
        void OnAssert(object? s, AsserterEventArgs e) => raised++;

        Asserter.AssertOccurred += OnAssert;
        try
        {
            R<int> result = 5;
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                result.GetResultValue();
            });
        }
        finally
        {
            Asserter.AssertOccurred -= OnAssert;
        }

        Assert.AreEqual(1, raised);
    }

    // Or is the way to read a value without checking, since it checks on the caller's behalf
    [TestMethod]
    public void TestOr()
    {
        Assert.AreEqual(5, Divide(10, 2).Or(42));
        Assert.AreEqual(42, Divide(10, 0).Or(42));
    }

    // Returning a value or an error is just 'return value' / 'return R.Error(...)', which is
    // these conversions
    [TestMethod]
    public void TestImplicitConversions()
    {
        R<int> fromValue = 5;
        Assert.IsTrue(Try(out var value, fromValue));
        Assert.AreEqual(5, value);

        R<int> fromError = R.Error("an error");
        Assert.IsFalse(Try(out var _, fromError));

        R<int> fromException = new InvalidOperationException("an exception");
        Assert.IsFalse(Try(out var _, out var e, fromException));
        Assert.AreEqual("an exception", e.ErrorMessage);

        R rFromException = new InvalidOperationException("an exception");
        Assert.IsFalse(Try(out var _, rFromException));
    }

    // R and R<T> convert to bool, so a result can be used directly in a condition
    [TestMethod]
    public void TestBoolConversion()
    {
        bool okValue = Divide(10, 2);
        bool errorValue = Divide(10, 0);
        bool ok = Validate("dev");
        bool error = Validate("");

        Assert.IsTrue(okValue);
        Assert.IsFalse(errorValue);
        Assert.IsTrue(ok);
        Assert.IsFalse(error);
    }

    // A null value is not an error, it is a bug in the function that returned it
    [TestMethod]
    public void TestNullValueFailsFast()
    {
        string? nothing = null;

        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            _ = (R<string>)nothing!;
        });
    }

    [TestMethod]
    public void TestErrorOfANonErrorFailsFast()
    {
        var e = Assert.ThrowsException<InvalidOperationException>(() =>
        {
            R.Error(R.Ok);
        });
        StringAssert.Contains(e.Message, "Was no error error");
    }

    // Try also wraps a throwing API into an R, which is how the file and process calls are used
    [TestMethod]
    public void TestTryWrapsAThrowingFunc()
    {
        Assert.IsTrue(Try(out var value, out var e, () => int.Parse("42")), $"{e}");
        Assert.AreEqual(42, value);

        Assert.IsFalse(Try(out var _, out var e2, () => int.Parse("not a number")));
        StringAssert.Contains(e2.ErrorMessage, "not in a correct format");
    }

    [TestMethod]
    public void TestTryWrapsAThrowingAction()
    {
        var didRun = false;
        Assert.IsTrue(Try(out var e, () => didRun = true), $"{e}");
        Assert.IsTrue(didRun);

        Assert.IsFalse(Try(out var e2, () => throw new InvalidOperationException("boom")));
        Assert.AreEqual("boom", e2.ErrorMessage);
    }

    [TestMethod]
    public void TestToString()
    {
        Assert.AreEqual("5", Divide(10, 2).ToString(), "An ok result is its value");
        Assert.AreEqual("Error: Cannot divide by zero", Divide(10, 0).ToString());
        Assert.AreEqual("OK", Validate("dev").ToString());

        StringAssert.StartsWith(Divide(10, 0).ToString(true), "Error: Cannot divide by zero\n");
    }

    static R<int> Divide(int a, int b)
    {
        if (b == 0)
            return R.Error("Cannot divide by zero");
        return a / b;
    }

    static R Validate(string name)
    {
        if (name == "")
            return R.Error("Empty name");
        return R.Ok;
    }

    static R<int> Outer(int b)
    {
        if (!Try(out var value, out var e, Divide(10, b)))
            return e;
        return value;
    }

    static R<int> OuterWrapping(int b)
    {
        if (!Try(out var value, out var e, Divide(10, b)))
            return R.Error("Failed to calculate", e);
        return value;
    }
}
