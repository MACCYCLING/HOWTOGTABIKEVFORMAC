using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class PatchScreenshot {
    static void Guard(ModuleDefinition module, MethodDefinition method) {
        if (!method.HasBody || method.ReturnType.FullName != "System.Void")
            throw new InvalidOperationException("Expected a void method with IL.");
        if (method.Body.ExceptionHandlers.Any(h => h.CatchType != null &&
            h.CatchType.FullName == "System.ComponentModel.Win32Exception"))
            throw new InvalidOperationException("Screenshot guard is already present.");
        var body = method.Body;
        var il = body.GetILProcessor();
        var first = body.Instructions[0];
        var returns = body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
        if (returns.Length != 1 || returns[0] != body.Instructions.Last())
            throw new InvalidOperationException("Unexpected method return structure.");
        var done = il.Create(OpCodes.Ret);
        // Exit the new outer try while retaining all existing disposal finally blocks.
        returns[0].OpCode = OpCodes.Leave;
        returns[0].Operand = done;
        var handler = il.Create(OpCodes.Pop);
        il.Append(handler);
        il.Append(il.Create(OpCodes.Leave, done));
        il.Append(done);
        body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch) {
            CatchType = module.ImportReference(typeof(System.ComponentModel.Win32Exception)),
            TryStart = first, TryEnd = handler,
            HandlerStart = handler, HandlerEnd = done
        });
    }

    public static int Main(string[] args) {
        if (args.Length != 4) throw new ArgumentException("input output type method");
        using (var module = ModuleDefinition.ReadModule(args[0])) {
            if (module.Assembly.Name.HasPublicKey)
                throw new InvalidOperationException("Refusing to invalidate a signed assembly.");
            var type = module.Types.Single(t => t.FullName == args[2]);
            var method = type.Methods.Single(m => m.Name == args[3]);
            if (args[2] == "GTBikeV.Activity") {
                var signature = String.Join(",", method.Parameters.Select(p => p.ParameterType.FullName));
                if (signature != "System.Int32,System.Int32,System.String" ||
                    !method.Body.Instructions.Any(i => i.Operand is MethodReference &&
                      ((MethodReference)i.Operand).FullName.Contains("Graphics::CopyFromScreen")))
                    throw new InvalidOperationException("Unexpected GTBikeV screenshot method.");
            }
            int oldHandlers = method.Body.ExceptionHandlers.Count;
            Guard(module, method);
            module.Write(args[1]);
            Console.WriteLine("Guarded " + method.FullName + "; existing handlers preserved: " + oldHandlers);
        }
        return 0;
    }
}
