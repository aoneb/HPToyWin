using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;

namespace HPToy.Core.Ble;

internal static class BleAsync
{
    public static async Task WithTimeout(Task task, TimeSpan timeout, string operationName)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException($"{operationName} 超时 ({timeout.TotalSeconds:0}s)");

        await task;
    }

    public static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout, string operationName)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException($"{operationName} 超时 ({timeout.TotalSeconds:0}s)");

        return await task;
    }

    public static async Task<T> WithTimeout<T>(IAsyncOperation<T> operation, TimeSpan timeout, string operationName)
    {
        return await WithTimeout(operation.AsTask(), timeout, operationName);
    }
}
