using System.Text.RegularExpressions;

namespace gmdTest.Utils;

// R and R<T> are how every fallible operation in gmd reports failure; exceptions are for bugs.
// These pin the contract the whole codebase is written against: a result is a union of its value
// (or Success) and an Error, and is matched on the case type.
[TestClass]
public class ResultTest
{
    [TestMethod]
    public void TestValueCase()
    {
        var result = Divide(10, 2);

        Assert.IsTrue(result is int);
        Assert.IsTrue(result is int value && value == 5);
        Assert.AreEqual(5, AssertOk(result));
    }

    [TestMethod]
    public void TestErrorCase()
    {
        var result = Divide(10, 0);

        Assert.IsTrue(result is Error);
        Assert.AreEqual("Cannot divide by zero", AssertError(result).Message);
    }

    // A switch over a result is exhaustive with its two cases; no discard arm is needed, and a
    // missing arm is a build error (CS8509)
    [TestMethod]
    public void TestSwitchIsExhaustive()
    {
        static string Describe(R<int> result) =>
            result switch
            {
                int value => $"value {value}",
                Error e => $"error {e.Message}",
            };

        Assert.AreEqual("value 5", Describe(Divide(10, 2)));
        Assert.AreEqual("error Cannot divide by zero", Describe(Divide(10, 0)));

        static string DescribeOutcome(R result) =>
            result switch
            {
                Success => "ok",
                Error e => e.Message,
            };

        Assert.AreEqual("ok", DescribeOutcome(Validate("dev")));
        Assert.AreEqual("Empty name", DescribeOutcome(Validate("")));
    }

    // The propagation idiom: the pattern binds the value, and when there is none the error is
    // returned as it is, so the original message survives all the way up
    [TestMethod]
    public void TestGuardBindsTheValueOrReturnsTheError()
    {
        Assert.AreEqual(5, AssertOk(Outer(2)));
        Assert.AreEqual("Cannot divide by zero", AssertError(Outer(0)).Message);
    }

    // R without a value, i.e. an operation that either succeeds or fails
    [TestMethod]
    public void TestResultWithoutValue()
    {
        Assert.IsTrue(Validate("dev") is Success);
        Assert.IsTrue(Validate("") is Error);
        Assert.AreEqual("Empty name", AssertError(Validate("")).Message);

        Assert.IsTrue(R.Ok is Success);
        Assert.AreEqual("OK", R.Ok.ToString());
    }

    // Wrapping adds a message without losing the inner one
    [TestMethod]
    public void TestWrappedErrorKeepsTheInnerMessages()
    {
        var e = AssertError(OuterWrapping(0));

        Assert.AreEqual("Failed to calculate", e.Message);
        Assert.AreEqual("Cannot divide by zero", e.Inner!.Message);
        Assert.AreEqual("Failed to calculate,\nCannot divide by zero", e.AllMessages());
        Assert.AreEqual("Error: Failed to calculate", e.ToString());
    }

    // An error wrapping an exception carries the exception's message chain as well
    [TestMethod]
    public void TestErrorFromAnException()
    {
        var inner = new IOException("disk full");

        var wrapping = new Error("Failed to save", new InvalidOperationException("write failed", inner));
        Assert.AreEqual("Failed to save", wrapping.Message);
        Assert.AreEqual("Failed to save,\nwrite failed,\ndisk full", wrapping.AllMessages());

        var fromException = new Error(new InvalidOperationException("boom", inner));
        Assert.AreEqual("boom", fromException.Message);
        Assert.AreEqual("boom,\ndisk full", fromException.AllMessages(), "The exception's own message is not repeated");
    }

    // An error records where it was created, since nothing is thrown and so there is no stack
    [TestMethod]
    public void TestErrorCapturesTheCallerFileAndLine()
    {
        var error = new Error("failed");

        StringAssert.Matches(
            error.Origin,
            new Regex($@"ResultTest\.cs\(\d+\) {nameof(TestErrorCapturesTheCallerFileAndLine)}"),
            $"Caller info missing from '{error.Origin}'"
        );
    }

    // Reading the error of a result that holds a value is a bug in the caller, so it fails fast,
    // which is reported through Asserter, which the running program logs and shows
    [TestMethod]
    public void TestErrorOfAValueFailsFast()
    {
        R<int> result = 5;
        var raised = 0;
        void OnAssert(object? s, AsserterEventArgs e) => raised++;

        Asserter.AssertOccurred += OnAssert;
        try
        {
            var e = Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                _ = result.Error;
            });
            StringAssert.Contains(e.Message, "Result is not an error");
        }
        finally
        {
            Asserter.AssertOccurred -= OnAssert;
        }

        Assert.AreEqual(1, raised);
    }

    // A null value is not an error, it is a bug in the function that returned it
    [TestMethod]
    public void TestNullValueFailsFast()
    {
        string? nothing = null;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            _ = (R<string>)nothing!;
        });
    }

    // Returning a value or an error is just 'return value' / 'return new Error(...)', which is
    // these conversions
    [TestMethod]
    public void TestImplicitConversions()
    {
        R<int> fromValue = 5;
        Assert.AreEqual(5, AssertOk(fromValue));

        R<int> fromError = new Error("an error");
        Assert.AreEqual("an error", AssertError(fromError).Message);

        R outcomeOfValue = Divide(10, 2);
        Assert.IsTrue(outcomeOfValue is Success, "Dropping the value keeps the outcome");

        R outcomeOfError = Divide(10, 0);
        Assert.AreEqual("Cannot divide by zero", AssertError(outcomeOfError).Message);
    }

    // A bool value is a value like any other: a result never converts to bool itself, so there is
    // no mistaking "is ok" for "is true"
    [TestMethod]
    public void TestBoolValue()
    {
        R<bool> isFalse = false;

        Assert.IsTrue(isFalse is bool value && !value);
        Assert.IsFalse(AssertOk(isFalse));
    }

    // Catch turns a throwing API into a result, which is how the file and process calls are used
    [TestMethod]
    public void TestCatchWrapsAThrowingFunc()
    {
        Assert.AreEqual(42, AssertOk(R.Catch(() => int.Parse("42"))));

        var e = AssertError(R.Catch(() => int.Parse("not a number")));
        StringAssert.Contains(e.Message, "not in a correct format");
        Assert.IsInstanceOfType<FormatException>(e.Exception);
        StringAssert.Contains(e.Origin, nameof(TestCatchWrapsAThrowingFunc), "The origin is the caller, not Catch");
    }

    [TestMethod]
    public void TestCatchWrapsAThrowingAction()
    {
        var didRun = false;
        AssertOk(
            R.Catch(() =>
            {
                didRun = true;
            })
        );
        Assert.IsTrue(didRun);

        var e = AssertError(R.Catch(() => throw new InvalidOperationException("boom")));
        Assert.AreEqual("boom", e.Message);
    }

    [TestMethod]
    public void TestToString()
    {
        Assert.AreEqual("5", Divide(10, 2).ToString(), "An ok result is its value");
        Assert.AreEqual("Error: Cannot divide by zero", Divide(10, 0).ToString());
        Assert.AreEqual("OK", Validate("dev").ToString());
        Assert.AreEqual("Error: Empty name", Validate("").ToString());
    }

    static R<int> Divide(int a, int b)
    {
        if (b == 0)
            return new Error("Cannot divide by zero");
        return a / b;
    }

    static R Validate(string name)
    {
        if (name == "")
            return new Error("Empty name");
        return R.Ok;
    }

    static R<int> Outer(int b)
    {
        var result = Divide(10, b);
        if (result is not int value)
            return result.Error;
        return value;
    }

    static R<int> OuterWrapping(int b)
    {
        var result = Divide(10, b);
        if (result is not int value)
            return new Error("Failed to calculate", result.Error);
        return value;
    }
}
