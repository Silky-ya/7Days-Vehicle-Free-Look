using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace VehicleFreeLook
{
    public sealed class ModApi : IModApi
    {
        public void InitMod(Mod mod)
        {
            new Harmony("local.vehiclefreelook.v1").PatchAll(Assembly.GetExecutingAssembly());
            Log.Out("[VehicleFreeLook] 1.0.0 loaded. Keyboard steering on mount; vehicle yaw follow disabled.");
        }
    }

    [HarmonyPatch(typeof(EntityVehicle), "GetCameraOffset")]
    public static class CameraPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var yaw = AccessTools.Field(typeof(vp_FPCamera), "yaw3P");
            var matches = new List<int>();
            for (int i = 4; i < code.Count; i++)
            {
                // Camera reference, dup, old yaw, vehicle delta, add, store yaw.
                // Only discard the vehicle delta. The mouse look path is untouched.
                if (code[i].opcode == OpCodes.Stfld && Equals(code[i].operand, yaw)
                    && code[i - 1].opcode == OpCodes.Add
                    && code[i - 3].opcode == OpCodes.Ldfld && Equals(code[i - 3].operand, yaw)
                    && code[i - 4].opcode == OpCodes.Dup
                    && code[i - 2].IsLdloc())
                    matches.Add(i - 1);
            }
            if (yaw == null || matches.Count != 1)
                throw new InvalidOperationException("VehicleFreeLook: unsupported camera code; expected exactly one vehicle yaw correction.");
            // add consumes oldYaw and delta, producing their sum; pop consumes only
            // delta, leaving oldYaw for the original store. Labels are retained.
            code[matches[0]].opcode = OpCodes.Pop;
            return code;
        }
    }

    [HarmonyPatch(typeof(EntityVehicle), "CameraInit")]
    public static class MountPatch
    {
        public static void Postfix(EntityVehicle __instance)
        {
            var player = __instance.GetAttachedPlayerLocal();
            if (player != null && __instance.AttachedMainEntity == player)
                EntityVehicle.isTurnTowardsLook = false;
        }
    }
}
