// Supplemental checks using the existing NUnit assertions outside Unity.
// This does not replace the Unity Test Runner report.
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

internal static class RegressionRunner
{
    private static int Main(string[] args)
    {
        int passed = 0, failed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Name.EndsWith("Tests")))
        {
            if (args.Length > 0 && !args.Contains(type.Name)) continue;
            foreach (var method in type.GetMethods())
            {
                var cases = method.GetCustomAttributes<TestCaseAttribute>().Select(c => c.Arguments).ToList();
                if (method.IsDefined(typeof(TestAttribute))) cases.Add(Array.Empty<object>());
                foreach (var values in cases)
                {
                    try
                    {
                        method.Invoke(Activator.CreateInstance(type), values);
                        passed++;
                    }
                    catch (Exception e)
                    {
                        failed++;
                        Console.WriteLine($"FAIL {type.Name}.{method.Name}({string.Join(",", values)}): {(e.InnerException ?? e).Message}");
                    }
                }
            }
        }
        Console.WriteLine($"Supplemental NUnit checks: passed={passed}, failed={failed}");
        return failed == 0 ? 0 : 1;
    }
}
