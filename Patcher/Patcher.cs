using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.IO;

namespace ILCodePatcher.Patch
{
    public static class Patcher
    {
        private static string patcherILTxtName = "ILPatcher";
        private static string patcherLogBefore = $"{patcherILTxtName}_Before.txt";
        private static string patcherLogNow = $"{patcherILTxtName}_Now.txt";
        private static string patcherLogFind = $"{patcherILTxtName}_Find.txt";
        private static bool patcherLogInit;

        private enum patcherLogType
        {
            BEFORE,
            NOW,
            FIND
        }

        private static void patcherLog(patcherLogType type, string txt)
        {
            if (!Util.Config.PatcherILDebug)
                return;

            if (!patcherLogInit)
            {
                if (File.Exists(patcherLogBefore))
                    File.Delete(patcherLogBefore);

                if (File.Exists(patcherLogNow))
                    File.Delete(patcherLogNow);

                if (File.Exists(patcherLogFind))
                    File.Delete(patcherLogFind);

                patcherLogInit = true;
            }

            try
            {
                switch (type)
                {
                    case patcherLogType.BEFORE:
                        using (var fs = new FileStream(patcherLogBefore, FileMode.Append))
                        using (var s = new StreamWriter(fs))
                            s.WriteLine(txt);

                        break;
                    case patcherLogType.NOW:
                        using (var fs = new FileStream(patcherLogNow, FileMode.Append))
                        using (var s = new StreamWriter(fs))
                            s.WriteLine(txt);

                        break;
                    case patcherLogType.FIND:
                        using (var fs = new FileStream(patcherLogFind, FileMode.Append))
                        using (var s = new StreamWriter(fs))
                            s.WriteLine(txt);

                        break;
                }
            }
            catch (Exception e)
            {
                if (Util.Config.Debug)
                    Util.Error($"Patcher log failed!", e);
                else
                    Util.Error($"Patcher log failed!");
            }
        }

        private static void patcherCheck(int index, CodeInstruction code, List<CodeInstruction> codes, PatchTask[] tasks, List<PatchInfo> needPatchs, Type classType)
        {
            var name = code.TryGetOperand();

            patcherLog(patcherLogType.BEFORE, $"[{index}] {classType.Name} IL: ({code.opcode}) \"{code.operand}\"");

            foreach (PatchTask task in tasks)
            {
                if (!task.active() || task.isDone)
                    continue;

                if (task.findOpcode != OpCodes.Nop && task.findOpcode != code.opcode)
                    continue;

                if (task.findOperand != null && name != task.findOperand)
                    continue;

                patcherLog(patcherLogType.FIND, $"[{index}] {classType.Name} IL Checking: ({code.opcode}) \"{name}\"");

                if (task.filter != null && !task.filter(codes, index))
                    continue;

                patcherLog(patcherLogType.FIND, $"[{index}] {classType.Name} IL Find!! ({code.opcode}) \"{name}\"");

                var info = new PatchInfo
                {
                    index = index + task.start,
                    task = task
                };

                task.done();

                if (task is PatchReplace rep)
                {
                    patcherLog(patcherLogType.FIND, $"[{index}] {classType.Name} Start patch index: {index + rep.start}, Skip range: {rep.range - 1}, Replace opcode to: {rep.repOpcode}, Number of tasks remaining: {task.remaining}");
                }
                else if (task is PatchMethod method)
                {
                    patcherLog(patcherLogType.FIND, $"[{index}] {classType.Name} Start patch index: {index + method.start}, Injection method: {classType.Name}.{method.methodName}, Number of tasks remaining: {task.remaining}");
                }

                needPatchs.Add(info);

                break;
            }
        }

        const string INSTANCE_PARAM = "__instance";
        const string INSTANCE_FIELD_PREFIX = "___";

        private static IEnumerable<CodeInstruction> patcherNew(IEnumerable<CodeInstruction> instructions, PatchTask[] tasks, Type classType, HarmonyMethod harmonyMethod)
        {
            var codes = new List<CodeInstruction>(instructions);
            var needPatchs = new List<PatchInfo>();
            var index = 0;
            var skipRange = 0;
            var needSkipRange = 0;
            var checkRange = Math.Min(20, codes.Count);
            var patchCount = 0;
            var className = classType.Name;

            if (checkRange == codes.Count)
                checkRange = Math.Min(8, codes.Count);

            if (checkRange == codes.Count)
                checkRange = Math.Min(4, codes.Count);

            for (var i = 0; i < checkRange; i++)
            {
                patcherCheck(i, codes[i], codes, tasks, needPatchs, classType);
            }

            foreach (CodeInstruction code in instructions)
            {
                var nextIndex = Math.Min(index + checkRange, codes.Count - 1);

                patcherCheck(nextIndex, codes[nextIndex], codes, tasks, needPatchs, classType);

                foreach (PatchInfo info in needPatchs)
                {
                    if (info.isDone || info.index != index)
                        continue;

                    if (info.task is PatchReplace rep)
                    {
                        code.opcode = rep.repOpcode;

                        if (rep.repOperand is not PatchNoNeedReplace)
                            code.operand = rep.repOperand;

                        needSkipRange = rep.range - 1;
                    }
                    else if (info.task is PatchMethod method)
                    {
                        var patch = AccessTools.Method(classType, method.methodName);

                        if (patch == null)
                        {
                            throw new ArgumentException($"Cannot not find method for type {classType} and name {method.methodName}");
                        }

                        if (!patch.IsStatic)
                        {
                            throw new ArgumentException("Harmony method must be static", "patch");
                        }

                        var patchParameters = patch.GetParameters();
                        var patchParametersCount = 0;
                        var originalClass = harmonyMethod.declaringType;
                        var originalMethodName = harmonyMethod.methodName;
                        var original = AccessTools.Method(originalClass, originalMethodName);
                        var originalParameters = original.GetParameters();
                        var originalParameterNames = originalParameters.Select(p => p.Name).ToArray();

                        foreach (var patchParam in patchParameters)
                        {
                            if (patchParam.Name == INSTANCE_PARAM)
                            {
                                if (original.IsStatic)
                                {
                                    yield return new CodeInstruction(OpCodes.Ldnull);
                                }
                                else
                                {
                                    var paramType = patchParam.ParameterType;
                                    var parameterIsRef = paramType.IsByRef;
                                    var parameterIsObject = paramType == typeof(object) || paramType == typeof(object).MakeByRefType();

                                    if (!AccessTools.IsStruct(originalClass))
                                    {
                                        if (parameterIsRef)
                                        {
                                            yield return new CodeInstruction(OpCodes.Ldarga, 0);
                                            patcherLog(patcherLogType.NOW, $"[{index}] {className} IL: ({OpCodes.Ldarga}) \"{0}\"");
                                        }
                                        else
                                        {
                                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                                            patcherLog(patcherLogType.NOW, $"[{index}] {className} IL: ({OpCodes.Ldarg_0}) \"\"");
                                        }
                                    }
                                }

                                patchParametersCount++;
                                continue;
                            }

                            if (patchParam.Name.StartsWith(INSTANCE_FIELD_PREFIX, StringComparison.Ordinal))
                            {
                                var fieldName = patchParam.Name.Substring(INSTANCE_FIELD_PREFIX.Length);
                                FieldInfo fieldInfo;
                                if (fieldName.All(char.IsDigit))
                                {
                                    // field access by index only works for declared fields
                                    fieldInfo = AccessTools.DeclaredField(originalClass, int.Parse(fieldName));
                                    if (fieldInfo is null)
                                        throw new ArgumentException($"No field found at given index in class {originalClass?.AssemblyQualifiedName ?? "null"}", fieldName);
                                }
                                else
                                {
                                    fieldInfo = AccessTools.Field(originalClass, fieldName);
                                    if (fieldInfo is null)
                                        throw new ArgumentException($"No such field defined in class {originalClass?.AssemblyQualifiedName ?? "null"}", fieldName);
                                }

                                if (fieldInfo.IsStatic)
                                {
                                    var newOpcode = patchParam.ParameterType.IsByRef ? OpCodes.Ldsflda : OpCodes.Ldsfld;
                                    yield return new CodeInstruction(newOpcode, fieldInfo);
                                    patcherLog(patcherLogType.NOW, $"[{index}] {className} IL: ({newOpcode}) \"{fieldInfo}\"");
                                }
                                else
                                {
                                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                                    patcherLog(patcherLogType.NOW, $"[{index}] {className} IL: ({OpCodes.Ldarg_0}) \"\"");

                                    var newOpcode = patchParam.ParameterType.IsByRef ? OpCodes.Ldsflda : OpCodes.Ldsfld;
                                    yield return new CodeInstruction(newOpcode, fieldInfo);
                                    patcherLog(patcherLogType.NOW, $"[{index}] {className} IL: ({newOpcode}) \"{fieldInfo}\"");
                                }

                                patchParametersCount++;
                                continue;
                            }
                        }

                        yield return new CodeInstruction(OpCodes.Callvirt, patch);
                        patcherLog(patcherLogType.NOW, $"[{index}] {className} IL: ({OpCodes.Callvirt}) \"{patch}\"");

                        if (patchParametersCount > 1)
                        {
                            yield return new CodeInstruction(OpCodes.Pop);
                            patcherLog(patcherLogType.NOW, $"[{index}] {className} IL: ({OpCodes.Pop}) \"\"");
                        }
                    }

                    info.Done();
                    patchCount++;
                }

                if (skipRange < 1)
                {
                    patcherLog(patcherLogType.NOW, $"[{index}] {className} IL: ({code.opcode}) \"{code.operand}\"");

                    if (needSkipRange > 0)
                        skipRange = needSkipRange;

                    yield return code;
                }
                else
                {
                    skipRange--;
                    needSkipRange = 0;
                }

                index++;
            }

            Util.Msg($"{className} had {patchCount} successful patches.");
        }

        public static IEnumerable<CodeInstruction> Do(IEnumerable<CodeInstruction> instructions, PatchTask[] tasks)
        {
            var methodInfo = new StackTrace().GetFrame(1).GetMethod();
            var classType = methodInfo.ReflectedType;
            var className = classType.Name;
            var att = classType.GetCustomAttribute<HarmonyPatch>();
            var count = 0;

            Util.Msg($"{className} start patching... Total Tasks: {tasks.Where((v) =>
            {
                if (v.count > 1)
                    count = count + (v.count - 1);

                return v.active();
            }).Count() + count}");

            if (att == null)
            {
                Util.Error($"{className} patch failed!\nIs this really a harmony method?");

                return instructions;
            }

            try
            {
                return patcherNew(instructions, tasks, classType, att.info);
            }
            catch (Exception e)
            {
                if (Util.Config.Debug)
                    Util.Error($"{className} patch failed!", e);
                else
                    Util.Error($"{className} patch failed!");

                return instructions;
            }
        }

        // Too slow, the this method will slow down the game start speed.
        //private static IEnumerable<CodeInstruction> patcherOld(IEnumerable<CodeInstruction> instructions, PatchTask[] tasks, string debugName)
        //{
        //    var codes = new List<CodeInstruction>(instructions);

        //    foreach (PatchTask task in tasks)
        //    {
        //        for (var r = 0; r < task.count; r++)
        //        {
        //            int index = -1;

        //            for (var i = 0; i < codes.Count; i++)
        //            {
        //                DBG($"{debugName} IL:  {i} {codes[i].opcode} \"{codes[i].operand}\"");

        //                if (codes[i].operand == null)
        //                    continue;

        //                var name = codes[i].TryGetOperand();

        //                if (name != task.findOperand)
        //                    continue;

        //                DBG($"{name} check \"{i}\"");

        //                if (task.filter != null && !task.filter(codes, i))
        //                    continue;

        //                DBG($"Find!! {name} \"{i}\"");

        //                index = i;
        //                break;
        //            }

        //            if (index != -1)
        //            {
        //                var removeRange = task.range;

        //                index += task.start;

        //                codes[index].opcode = task.repOpcode;
        //                codes.RemoveRange(index + 1, removeRange - 1);
        //            }
        //        }
        //    }

        //    //if (debugName != null) {
        //    //    for (var i = 0; i < codes.Count; i++)
        //    //    {
        //    //        Msg($"{debugName} IL Now:  {i} {codes[i].opcode} \"{codes[i].operand}\"");
        //    //    }
        //    //}

        //    return codes.AsEnumerable();
        //}
    }
}
