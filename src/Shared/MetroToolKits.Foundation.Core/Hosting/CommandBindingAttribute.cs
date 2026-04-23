using System.Reflection;

namespace MetroToolKits.Foundation.Core.Hosting;

/// <summary>
/// 声明命令类对外暴露的命令绑定。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CommandBindingAttribute : Attribute
{
    public CommandBindingAttribute(string commandName, string methodName = "Execute")
    {
        CommandName = commandName;
        MethodName = methodName;
    }

    public string CommandName { get; }

    public string MethodName { get; }
}

/// <summary>
/// 根据命令类上的绑定特性批量完成注册。
/// </summary>
public static class CommandRegistrationScanner
{
    public static void RegisterAttributedCommands(ICommandRegistry registry, Assembly assembly)
    {
        var commandTypes = assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Select(type => new
            {
                CommandType = type,
                Bindings = type.GetCustomAttributes<CommandBindingAttribute>(inherit: false).ToArray()
            })
            .Where(static item => item.Bindings.Length > 0);

        foreach (var command in commandTypes)
        {
            foreach (var binding in command.Bindings)
            {
                registry.RegisterCommand(command.CommandType, binding.CommandName, binding.MethodName);
            }
        }
    }
}
