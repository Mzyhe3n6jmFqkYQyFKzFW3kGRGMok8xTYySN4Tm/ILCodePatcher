using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace ILCodePatcher.Patch
{
    public class PatchTask
    {
        private int allCount = 1;
        private int lastCount = 1;

        public int start = 0;

        public string findOperand = null;
        public OpCode findOpcode = OpCodes.Nop;
        public Func<List<CodeInstruction>, int, bool> filter = null;
        public Func<bool> active = () => true;

        public int count
        {
            get
            {
                return allCount;
            }
            set
            {
                allCount = value;
                lastCount = value;
            }
        }

        public bool isDone => lastCount < 1;
        public int remaining => lastCount;

        public void done()
        {
            lastCount--;
        }
    }

    public class PatchReplace : PatchTask
    {
        public int range = 0;

        public object repOperand = new PatchNoNeedReplace();
        public OpCode repOpcode = OpCodes.Nop;
    }

    public class PatchMethod : PatchTask
    {
        public string methodName = null;
    }

    public class PatchInfo
    {
        private bool _isDone = false;

        public PatchTask task;
        public int index = -1;
        public bool isDone => _isDone;

        public void Done()
        {
            _isDone = true;
        }
    }

    public class PatchNoNeedReplace
    {
        // What is this?
        // The class just using to let the patcher know need to replace operand or not
    }

    public static class CodeInstructionEX
    {
        public static string TryGetOperand(this CodeInstruction self)
        {
            if (self.operand == null)
                return "";

            return self.operand.ToString();
        }
    }

    internal static class PatchArgumentExtensions // Copy form HarmonyLib
    {
        static HarmonyArgument[] AllHarmonyArguments(object[] attributes)
        {
            return attributes.Select(attr =>
            {
                if (attr.GetType().Name != nameof(HarmonyArgument)) return null;
                return AccessTools.MakeDeepCopy<HarmonyArgument>(attr);
            })
            .Where(harg => harg is not null)
            .ToArray();
        }

        static HarmonyArgument GetArgumentAttribute(this ParameterInfo parameter)
        {
            var attributes = parameter.GetCustomAttributes(false);
            return AllHarmonyArguments(attributes).FirstOrDefault();
        }

        static HarmonyArgument[] GetArgumentAttributes(this MethodInfo method)
        {
            if (method is null || method is DynamicMethod)
                return default;

            var attributes = method.GetCustomAttributes(false);
            return AllHarmonyArguments(attributes);
        }

        static HarmonyArgument[] GetArgumentAttributes(this Type type)
        {
            var attributes = type.GetCustomAttributes(false);
            return AllHarmonyArguments(attributes);
        }

        static string GetOriginalArgumentName(this ParameterInfo parameter, string[] originalParameterNames)
        {
            var attribute = parameter.GetArgumentAttribute();

            if (attribute is null)
                return null;

            if (string.IsNullOrEmpty(attribute.OriginalName) is false)
                return attribute.OriginalName;

            if (attribute.Index >= 0 && attribute.Index < originalParameterNames.Length)
                return originalParameterNames[attribute.Index];

            return null;
        }

        static string GetOriginalArgumentName(HarmonyArgument[] attributes, string name, string[] originalParameterNames)
        {
            if ((attributes?.Length ?? 0) <= 0)
                return null;

            var attribute = attributes.SingleOrDefault(p => p.NewName == name);
            if (attribute is null)
                return null;

            if (string.IsNullOrEmpty(attribute.OriginalName) is false)
                return attribute.OriginalName;

            if (originalParameterNames is not null && attribute.Index >= 0 && attribute.Index < originalParameterNames.Length)
                return originalParameterNames[attribute.Index];

            return null;
        }

        static string GetOriginalArgumentName(this MethodInfo method, string[] originalParameterNames, string name)
        {
            string argumentName;

            argumentName = GetOriginalArgumentName(method?.GetArgumentAttributes(), name, originalParameterNames);
            if (argumentName is not null)
                return argumentName;

            argumentName = GetOriginalArgumentName(method?.DeclaringType?.GetArgumentAttributes(), name, originalParameterNames);
            if (argumentName is not null)
                return argumentName;

            return name;
        }

        internal static int GetArgumentIndex(this MethodInfo patch, string[] originalParameterNames, ParameterInfo patchParam)
        {
            if (patch is DynamicMethod)
                return Array.IndexOf(originalParameterNames, patchParam.Name);

            var originalName = patchParam.GetOriginalArgumentName(originalParameterNames);
            if (originalName is not null)
                return Array.IndexOf(originalParameterNames, originalName);

            originalName = patch.GetOriginalArgumentName(originalParameterNames, patchParam.Name);
            if (originalName is not null)
                return Array.IndexOf(originalParameterNames, originalName);

            return -1;
        }
    }
}
