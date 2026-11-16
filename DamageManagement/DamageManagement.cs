using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using DamageManagement.API;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace DamageManagement
{
    public class Config : BasePluginConfig
    {
        [JsonPropertyName("enable")]
        public bool enableCfg { get; set; } = true;
        [JsonPropertyName("enable_inflictors")] public string[] listWeapons { get; set; } = ["inferno", "hegrenade_projectile", "flashbang_projectile", 
            "smokegrenade_projectile", "decoy_projectile", "planted_c4"];
    }

    public partial class DamageManagement : BasePlugin, IPluginConfig<Config>
    {
        public override string ModuleName => "CS2-DamageManagement";

        public override string ModuleVersion => "1.1";
        public override string ModuleAuthor => "HoanNK";
        public Config Config { get; set; } = new();
        bool enabled { get; set; }
        static bool isSlayCommand { get; set; }
        //This contains all designer name of the weapon class that allow to be taken damage to the teammates
        //Designer name can be found here https://cs2.poggu.me/dumped-data/entity-list
        string[] enableDmgInflictors = [];

        //Shared API
        public DamageManagementAPI managementAPI { get; set; } = new();
        private static PluginCapability<IDamageManagementAPI> DamageManagemenCapability { get; } = new("damagemanagement:api");   

        public override void Load(bool hotReload)
        {
            Logger.LogInformation("Loading plugin");

            managementAPI = new DamageManagementAPI();
            Capabilities.RegisterPluginCapability(DamageManagemenCapability, () => managementAPI);

            RegisterListener<Listeners.OnPlayerTakeDamagePre>(OnPlayerTakeDamagePre);
        }

        public void OnConfigParsed(Config config)
        {
            enabled = config.enableCfg;
            enableDmgInflictors = config.listWeapons;
            Logger.LogInformation("Config has been parsed successfully");
            if(enableDmgInflictors.Length == 0)
            {
                enableDmgInflictors = ["inferno", "hegrenade_projectile", "flashbang_projectile", "smokegrenade_projectile", "decoy_projectile", "planted_c4"];
            }
        }

        public override void Unload(bool hotReload)
        {
            Logger.LogInformation("Unloading plugin");
            RemoveListener<Listeners.OnPlayerTakeDamagePre>(OnPlayerTakeDamagePre);
        }

        private HookResult OnPlayerTakeDamagePre(CCSPlayerPawn victim, CTakeDamageInfo damageInfo)
        {
            //Check handle for API first
            //it will allow consumer determine execute original method or not
            if (managementAPI.IsNeedCallOriginalMethod() || !enabled)
            {
                return HookResult.Continue;
            }

            try
            {
                if (victim == null || !victim.IsValid)
                    return HookResult.Continue;

                var attacker = damageInfo.Attacker.Value;
                if (attacker == null || !attacker.IsValid || attacker.Handle == victim.Handle)
                    return HookResult.Continue;

                // Only player-vs-player damage is relevant for friendly fire
                if (!"player".Equals(attacker.DesignerName))
                    return HookResult.Continue;

                var attackerPawn = attacker.As<CCSPlayerPawn>();
                if (attackerPawn.TeamNum != victim.TeamNum)
                    return HookResult.Continue;

                string inflictor = damageInfo.Inflictor.Value?.DesignerName ?? "";
                if (enableDmgInflictors.Contains(inflictor))
                {
                    isSlayCommand = false;
                    return HookResult.Continue;
                }
                return HookResult.Handled;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in OnPlayerTakeDamagePre");
            }
            return HookResult.Continue;
        }


        [ConsoleCommand("dmg_manage_enable", "Enable plugin")]
        [CommandHelper(minArgs: 1, usage: "[true/false]", whoCanExecute: CommandUsage.SERVER_ONLY)]
        [RequiresPermissions("@css/cvar")]
        public void OnEnableCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            string arg = commandInfo.GetArg(1);
            if ("true".Equals(arg) || "1".Equals(arg))
            {
                enabled = true;
                return;
            }
            if ("false".Equals(arg) || "0".Equals(arg))
            {
                enabled = false;
                return;
            }
            commandInfo.ReplyToCommand("true/false value only");
        }

    }
}
