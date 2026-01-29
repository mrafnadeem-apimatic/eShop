using System.Reflection;

namespace SdkInspector;

public static class ReflectionHelpers
{
    public static void DumpPayPalServerSdkInfo(Assembly assembly)
    {
        Console.WriteLine($"Assembly Full Name: {assembly.FullName}");
        Console.WriteLine();

        Type[] types;
        try
        {
            types = assembly
                .GetTypes()
                .OrderBy(t => t.Namespace)
                .ThenBy(t => t.Name)
                .ToArray();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types
                .Where(t => t is not null)
                .Select(t => t!)
                .OrderBy(t => t.Namespace ?? string.Empty)
                .ThenBy(t => t.Name)
                .ToArray();

            Console.WriteLine("ReflectionTypeLoadException encountered; some types may be missing.");
            if (ex.LoaderExceptions is not null)
            {
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    if (loaderException is null)
                    {
                        continue;
                    }

                    Console.WriteLine($"  Loader exception: {loaderException.Message}");
                }
            }

            Console.WriteLine();
        }

        static string FormatType(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }

            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            if (tickIndex >= 0)
            {
                name = name[..tickIndex];
            }

            var genericArgs = string.Join(
                ", ",
                type.GetGenericArguments().Select(FormatType));

            return $"{name}<{genericArgs}>";
        }

        // Additionally, try to dump the shared ApiResponse<T> type used by OrdersController.CaptureOrderAsync.
        try
        {
            var ordersControllerType = assembly.GetType("PaypalServerSdk.Standard.Controllers.OrdersController");
            if (ordersControllerType is not null)
            {
                var captureAsync = ordersControllerType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "CaptureOrderAsync");

                if (captureAsync is not null && captureAsync.ReturnType.IsGenericType)
                {
                    var taskInnerType = captureAsync.ReturnType.GetGenericArguments().FirstOrDefault();
                    if (taskInnerType is not null && taskInnerType.IsGenericType)
                    {
                        var apiResponseGeneric = taskInnerType.GetGenericTypeDefinition();

                        Console.WriteLine($"Type: {apiResponseGeneric.FullName}");

                        var props = apiResponseGeneric
                            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .OrderBy(p => p.Name);

                        foreach (var prop in props)
                        {
                            Console.WriteLine($"  Property: {FormatType(prop.PropertyType)} {prop.Name}");
                        }

                        Console.WriteLine();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while inspecting ApiResponse<T>: {ex.Message}");
            Console.WriteLine();
        }

        foreach (var type in types)
        {
            if (type?.FullName is null)
            {
                continue;
            }

            // Focus on the main client, controllers, and selected input models to keep output small.
            var fullName = type.FullName;
            if (!fullName.Contains("PaypalServerSdk.Standard.PaypalServerSdkClient", StringComparison.Ordinal) &&
                !fullName.Contains(".Controllers.", StringComparison.Ordinal) &&
                !fullName.Contains("CaptureOrderInput", StringComparison.Ordinal) &&
                !fullName.Contains("GetOrderInput", StringComparison.Ordinal) &&
                !fullName.EndsWith(".Models.Order", StringComparison.Ordinal) &&
                !fullName.EndsWith(".Models.OrderStatus", StringComparison.Ordinal) &&
                !fullName.EndsWith(".Utilities.ApiResponse`1", StringComparison.Ordinal))
            {
                continue;
            }

            Console.WriteLine($"Type: {fullName}");

            if (type.IsEnum)
            {
                Console.WriteLine($"  Enum values: {string.Join(", ", Enum.GetNames(type))}");
            }
            else
            {
                var methods = type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .OrderBy(m => m.Name)
                    .ToList();

                foreach (var method in methods)
                {
                    var parameters = string.Join(
                        ", ",
                        method.GetParameters()
                            .Select(p => $"{FormatType(p.ParameterType)} {p.Name}"));

                    Console.WriteLine($"  Method: {FormatType(method.ReturnType)} {method.Name}({parameters})");
                }
            }

            Console.WriteLine();
        }
    }
}

