using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class VerifyPatch {
    static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types) {
        foreach (var t in types) { yield return t; foreach (var n in Types(t.NestedTypes)) yield return n; }
    }
    static string Operand(object o, MethodDefinition m) {
        if (o == null) return "";
        if (o is Instruction) return "target:" + m.Body.Instructions.IndexOf((Instruction)o);
        if (o is Instruction[]) return String.Join(",", ((Instruction[])o).Select(i => Operand(i,m)));
        if (o is MemberReference) return ((MemberReference)o).FullName;
        return o.ToString();
    }
    static string IL(MethodDefinition m) {
        return String.Join("\n",m.Body.Instructions.Select(i => i.OpCode.Name + " " + Operand(i.Operand,m)));
    }
    public static int Main(string[] args) {
        using (var original = ModuleDefinition.ReadModule(args[0]))
        using (var patched = ModuleDefinition.ReadModule(args[1])) {
            if (original.Assembly.Name.FullName != patched.Assembly.Name.FullName || original.Mvid != patched.Mvid)
                throw new Exception("Assembly identity changed.");
            var oldMethods=Types(original.Types).SelectMany(t => t.Methods).ToDictionary(m => m.FullName);
            var newMethods=Types(patched.Types).SelectMany(t => t.Methods).ToDictionary(m => m.FullName);
            if (oldMethods.Count != newMethods.Count) throw new Exception("Method inventory changed.");
            int changed=0;
            foreach (var pair in oldMethods) {
                var a=pair.Value; var b=newMethods[pair.Key];
                if (a.HasBody != b.HasBody) throw new Exception("Method body presence changed.");
                if (!a.HasBody) continue;
                if (IL(a) != IL(b)) {
                    changed++;
                    if (a.DeclaringType.FullName != "GTBikeV.Activity" || a.Name != "ScreenShot")
                        throw new Exception("Unexpected method changed: " + pair.Key);
                    var ah=a.Body.ExceptionHandlers; var bh=b.Body.ExceptionHandlers;
                    if (bh.Count != ah.Count+1) throw new Exception("Incorrect handler count.");
                    if (ah.Count(h => h.HandlerType == ExceptionHandlerType.Finally) !=
                        bh.Count(h => h.HandlerType == ExceptionHandlerType.Finally))
                        throw new Exception("Disposal handler changed.");
                    if (bh.Last().CatchType.FullName != "System.ComponentModel.Win32Exception")
                        throw new Exception("Incorrect caught exception.");
                }
            }
            if (changed != 1) throw new Exception("Expected exactly one changed method.");
            Console.WriteLine("PASS: exactly Activity.ScreenShot changed; all other " + (oldMethods.Count-1) + " methods unchanged; assembly identity and disposal handlers preserved.");
        }
        return 0;
    }
}
