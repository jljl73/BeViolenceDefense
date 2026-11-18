using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

internal static class Runner
{
    static int Main(string[] args)
    {
        int pass = 0, fail = 0;
        foreach (Type type in typeof(Runner).Assembly.GetTypes().OrderBy(t => t.Name))
            foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var cases = m.GetCustomAttributes<TestCaseAttribute>().Select(c => c.Arguments).ToList();
                if (cases.Count == 0 && m.GetCustomAttribute<TestAttribute>() != null) cases.Add(new object[0]);
                foreach (object[] a in cases)
                {
                    string name = $"{type.Name}.{m.Name}({string.Join(",", a)})";
                    if (args.Length > 0 && !name.Contains(args[0])) continue;
                    try
                    {
                        var ps = m.GetParameters();
                        object[] conv = a.Select((v, i) => v == null ? null : Convert.ChangeType(v, ps[i].ParameterType)).ToArray();
                        m.Invoke(Activator.CreateInstance(type), conv); pass++;
                    }
                    catch (TargetInvocationException e)
                    {
                        fail++;
                        string msg = (e.InnerException?.Message ?? "").Trim().Replace("\n", " | ");
                        Console.WriteLine($"FAIL {name}: {e.InnerException?.GetType().Name} {msg.Substring(0, Math.Min(300, msg.Length))}");
                    }
                }
            }
        Console.WriteLine($"pass {pass} fail {fail}");
        return fail;
    }
}
