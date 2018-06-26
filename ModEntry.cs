using System;
using System.IO;
using Colonisation.BlockNPCs;
using PhentrixGames.NewColonyAPI.Helpers;
using Pipliz.Mods.APIProvider.Jobs;

namespace Colonisation.Jobs
{
    [ModLoader.ModManager]
    public static class ModEntry
    {
        public static string ModFolder;
        public static string ModGameDataDirectory;
        public static string LocalizationFolder;
        public static Version ModVersion = new Version(0, 0, 0, 1);
        public const string ModName = "Colonisation";
        public const string Naming = "Colonisation.Jobs.";

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, Naming + "OnAssemblyLoaded")]
        public static void OnAssemblyLoaded(string path)
        {
            ModFolder = Path.GetDirectoryName(path).Replace("\\", "/");
            ModGameDataDirectory = Path.Combine(Path.GetDirectoryName(path), "gamedata/").Replace("\\", "/");

            LocalizationFolder = Path.Combine(ModGameDataDirectory, "localization/").Replace("\\", "/");
            Utilities.CreateLogs("Colonisation");
            PhentrixGames.NewColonyAPI.Managers.ConfigManager.RegisterConfig("Colonisation", "Magikaas/Colonisation");
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, Naming + "AfterStartup")]
        public static void AfterStartup()
        {
            string versionURL = "https://raw.githubusercontent.com/Magikaas/Colonisation/master/Colonisation.md";

            PhentrixGames.NewColonyAPI.Helpers.Utilities.WriteLog("Colonisation", "Modname: " + ModName + ". ModFolder: " + ModFolder + ". Config: " + ModFolder + "/config. VersionURL: " + versionURL);
            
            PhentrixGames.NewColonyAPI.Managers.ModManager.RegisterMod(ModName, ModFolder, ModVersion, ModFolder + "/config");
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, Naming + "RegisterJobs")]
        [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.jobs.resolvetypes")]
        public static void RegisterJobs()
        {
            Pipliz.Log.Write("Colonisation Test");
            BlockJobManagerTracker.Register<OreProcessorJob>(OreProcessorJob.JOB_STATION);
            BlockJobManagerTracker.Register<ScoutJob>(ScoutJob.JOB_STATION);
        }
    }
}